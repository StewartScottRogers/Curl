using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Curl.Protocol.Abstractions;
using Microsoft.Win32.SafeHandles;

namespace Curl.Core.FileSystem;

/// <summary>
/// The real-disk <see cref="IFileSystem" />, <see cref="IDirectoryLister" /> and <see cref="IFileTimeSetter" />: opens local files with
/// <see cref="FileStream" /> and reports every failed open as a
/// <see cref="FileAccessStatus" />, never as an exception, and lists a directory's entry
/// names, reporting one it cannot list as <see langword="null" />.
/// </summary>
/// <remarks>
/// <para>
/// It lives in Core rather than in the <c>file</c> protocol library, per ADR-0002, so
/// <c>FileProtocolHandler</c> constructs no <see cref="FileStream" /> and its tests never
/// touch a disk.
/// </para>
/// <para>
/// A path reaches this class as curl forwards it, which may be one the operating system
/// rejects (<c>c|/Windows</c>, a literal <c>%</c>). Every exception
/// <see cref="FileOpenFailure.IsOpenFailure(Exception)" /> names is absorbed into
/// <see cref="FileOpenResult.Failed(FileAccessStatus, Exception?)" />, which carries it; the only exception either open
/// lets out is the <see cref="OperationCanceledException" /> of a cancelled token. An open
/// that fails because the path is a directory carries the directory's last-write time.
/// </para>
/// <para>
/// A read open is seekable for a regular file. A handle the operating system cannot seek -
/// a FIFO, a pipe behind <c>/dev/stdin</c>, or <c>NUL</c> on Windows - opens as a stream
/// that cannot seek, reporting a length of zero, just as curl's <c>fstat</c> of one does;
/// the handler answers an offset on such a source with exit 36 rather than seeking it.
/// <c>/dev/null</c> on Linux and macOS accepts <c>lseek</c>, so it opens seekable, still
/// with the length zero its <c>fstat</c> reports.
/// </para>
/// <para>
/// On Windows a device such as <c>NUL</c> has no last-write time the operating system
/// will report, and <see cref="File.GetLastWriteTimeUtc(Microsoft.Win32.SafeHandles.SafeFileHandle)" />
/// throws for it. The Windows C runtime's <c>fstat</c>, which curl 8.21.0 calls, succeeds
/// for the same handle with a modification time of zero, so there this class reports the
/// Unix epoch and <c>curl -sI file:///NUL</c> prints
/// <c>Last-Modified: Thu, 01 Jan 1970 00:00:00 GMT</c> as upstream does. Elsewhere an
/// unreadable timestamp stays <see langword="null" />.
/// </para>
/// <para>
/// A write open on a POSIX system creates a missing file with the mode it is given, which
/// the operating system then narrows by the process umask, just as curl's
/// <c>open(2)</c> with <c>--create-file-mode</c> does. Windows has no such mode, so there
/// it is ignored and the option has no effect.
/// </para>
/// <para>
/// Every file opens with a synchronous handle (<see cref="FileOptions.None" />), as curl's
/// <c>fopen</c> does. On Windows an asynchronous handle makes each of a download's 16 KiB
/// writes an overlapped write completed through the thread pool, which made a 50 MiB
/// <c>-o</c> download take 1.8 times curl's wall time (BL-1675, AF-0048).
/// </para>
/// <para>
/// Off Windows a read open of a path in <see cref="RawBytePath" />'s form, holding a byte
/// that is not valid UTF-8, opens the file whose name is exactly those bytes, as curl's
/// <c>open()</c> does: .NET's file APIs would encode the byte as EF BF BD, so the name goes to
/// the runtime's own <c>open</c> shim instead and a <see cref="FileStream" /> wraps the
/// descriptor (CGR-114, BL-3899, ADR-0836). Its failure is the status of the <c>errno</c>
/// (<see cref="StatusForErrno(int)" />), so a name that does not exist is still
/// <see cref="FileAccessStatus.NotFound" />. Every other path, and every path on Windows,
/// opens as above.
/// </para>
/// </remarks>
public sealed partial class PhysicalFileSystem : IFileSystem, IDirectoryLister, IFileTimeSetter
{
    private const int BufferSize = 4096;

    /// <summary>The runtime's <c>O_RDONLY</c> (its PAL value, the same on Linux and macOS).</summary>
    private const int ReadOnly = 0x0000;

    /// <summary>The runtime's <c>O_CLOEXEC</c>.</summary>
    private const int CloseOnExec = 0x0010;

    /// <summary>The status of each <c>errno</c> a read <c>open</c> names specially; any other is an I/O error.</summary>
    private static readonly Dictionary<int, FileAccessStatus> StatusesByErrno = new()
    {
        [1] = FileAccessStatus.AccessDenied,
        [2] = FileAccessStatus.NotFound,
        [13] = FileAccessStatus.AccessDenied,
        [20] = FileAccessStatus.NotADirectory,
        [21] = FileAccessStatus.IsDirectory,
    };

    private readonly Func<byte[], RawBytePathOpen>? openRawBytePathForRead;

    [UnsupportedOSPlatformGuard("windows")]
    private readonly bool setsUnixCreateMode;

    private readonly bool reportsUnreadableTimestampAsEpoch;

    private readonly bool reportsFileInPathAsNotADirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhysicalFileSystem" /> class that sets
    /// the create mode of a written file everywhere except Windows, reports the Unix
    /// epoch for an unreadable timestamp only on Windows, and reports a path whose parent
    /// is a file as <see cref="FileAccessStatus.NotADirectory" /> everywhere except Windows,
    /// and opens a <see cref="RawBytePath" /> for reading under its raw bytes everywhere except Windows.
    /// </summary>
    public PhysicalFileSystem()
        : this(
            setsUnixCreateMode: !OperatingSystem.IsWindows(),
            reportsUnreadableTimestampAsEpoch: OperatingSystem.IsWindows(),
            reportsFileInPathAsNotADirectory: !OperatingSystem.IsWindows(),
            openRawBytePathForRead: RawBytePathReadOpenerFor(OperatingSystem.IsWindows()))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PhysicalFileSystem" /> class with the
    /// platform decisions made by the caller, so a test on Windows can reach the POSIX paths.
    /// </summary>
    /// <param name="setsUnixCreateMode">
    /// <see langword="true" /> to pass the create mode to the operating system; only
    /// meaningful off Windows, where <see cref="FileStreamOptions.UnixCreateMode" /> throws
    /// <see cref="PlatformNotSupportedException" />.
    /// </param>
    /// <param name="reportsUnreadableTimestampAsEpoch">
    /// <see langword="true" /> to report the Unix epoch, as the Windows C runtime's
    /// <c>fstat</c> does, for a handle whose last-write time cannot be read;
    /// <see langword="false" /> to report <see langword="null" />.
    /// </param>
    /// <param name="reportsFileInPathAsNotADirectory">
    /// <see langword="true" /> to report an open that fails as not found, when a file stands
    /// where the path needs a directory, as <see cref="FileAccessStatus.NotADirectory" />:
    /// curl's <c>open(2)</c> gets <c>ENOTDIR</c> there, which .NET off Windows raises as a
    /// <see cref="DirectoryNotFoundException" />. <see langword="false" /> keeps
    /// <see cref="FileAccessStatus.NotFound" />, as Windows reports it.
    /// </param>
    /// <param name="openRawBytePathForRead">
    /// Opens a <see cref="RawBytePath" />'s NUL-terminated bytes for reading, as <c>open()</c>
    /// does, for a read path holding a raw byte; <see langword="null" /> opens every path through
    /// <see cref="FileStream" />, as Windows does.
    /// </param>
    internal PhysicalFileSystem(
        bool setsUnixCreateMode,
        bool reportsUnreadableTimestampAsEpoch = false,
        bool reportsFileInPathAsNotADirectory = false,
        Func<byte[], RawBytePathOpen>? openRawBytePathForRead = null)
    {
        this.setsUnixCreateMode = setsUnixCreateMode;
        this.reportsUnreadableTimestampAsEpoch = reportsUnreadableTimestampAsEpoch;
        this.reportsFileInPathAsNotADirectory = reportsFileInPathAsNotADirectory;
        this.openRawBytePathForRead = openRawBytePathForRead;
    }

    /// <inheritdoc />
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was cancelled before the open.
    /// </exception>
    public ValueTask<FileOpenResult> OpenForReadAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (openRawBytePathForRead is not null && RawBytePath.HoldsRawByte(path))
        {
            return ValueTask.FromResult(OpenUnderRawBytes(openRawBytePathForRead, path));
        }

        // ReadWrite and Delete sharing: curl opens with a plain open(2), which locks
        // nothing, so a file another process is writing is still readable.
        return ValueTask.FromResult(
            Open(path, OptionsFor(FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)));
    }

    /// <inheritdoc />
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was cancelled before the open.
    /// </exception>
    public ValueTask<FileOpenResult> OpenForWriteAsync(
        string path,
        FileWriteMode mode,
        UnixFileMode createMode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        FileMode fileMode = FileModeFor(mode);
        FileStreamOptions options = setsUnixCreateMode
            ? WriteOptionsWithCreateMode(fileMode, createMode)
            : OptionsFor(fileMode, FileAccess.Write, FileShare.Read);

        return ValueTask.FromResult(Open(path, options));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Lists with <see cref="Directory.EnumerateFileSystemEntries(string)" />, unsorted, so
    /// the names come in the operating system's order as curl's <c>readdir</c> loop gives
    /// them. Every exception <see cref="FileOpenFailure.IsOpenFailure(Exception)" /> names,
    /// such as the <see cref="DirectoryNotFoundException" /> of a missing directory or the
    /// <see cref="IOException" /> of a regular file, is reported as <see langword="null" />.
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was cancelled before the listing.
    /// </exception>
    public ValueTask<IReadOnlyList<string>?> ListEntryNamesAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return ValueTask.FromResult<IReadOnlyList<string>?>(
                Directory.EnumerateFileSystemEntries(path).Select(entry => Path.GetFileName(entry)).ToList());
        }
        catch (Exception exception) when (FileOpenFailure.IsOpenFailure(exception))
        {
            return ValueTask.FromResult<IReadOnlyList<string>?>(null);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Every exception <see cref="FileOpenFailure.IsOpenFailure(Exception)" /> names, such as
    /// the <see cref="FileNotFoundException" /> of a missing file, is reported as
    /// <see langword="false" /> and <see cref="FileTimeFailedStep.Open" />, with the error code
    /// <see cref="FileOpenFailure.Win32ErrorCodeOf(Exception)" /> reads from it. Every time is
    /// set by <see cref="NativeFileTimeSetter" /> with the operating system's own call, which
    /// opens the file and stamps it in two steps on Windows, so a refused time is
    /// <see cref="FileTimeFailedStep.SetTime" /> with <c>SetFileTime</c>'s error code (BL-1453);
    /// off Windows <paramref name="errorCode" /> is <c>utimes</c>'s <c>errno</c>, which curl's
    /// POSIX build prints with <c>strerror</c> (BL-1433).
    /// </remarks>
    public bool TrySetLastWriteUnixSeconds(string path, long unixSeconds, out int errorCode, out FileTimeFailedStep failedStep)
    {
        bool set;
        try
        {
            set = NativeFileTimeSetter.TrySetLastWriteUnixSeconds(path, unixSeconds, out errorCode);
        }
        catch (Exception exception) when (FileOpenFailure.IsOpenFailure(exception))
        {
            errorCode = FileOpenFailure.Win32ErrorCodeOf(exception);
            failedStep = FileTimeFailedStep.Open;
            return false;
        }

        failedStep = set ? FileTimeFailedStep.None : FileTimeFailedStep.SetTime;
        return set;
    }

    /// <summary>
    /// The <see cref="FileMode" /> a write open asks for.
    /// </summary>
    /// <param name="mode">How existing content is treated.</param>
    /// <returns>
    /// <see cref="FileMode.Append" />, <see cref="FileMode.CreateNew" /> - one exclusive
    /// create, as curl's <c>O_EXCL</c> - or <see cref="FileMode.Create" /> for a truncating open.
    /// </returns>
    private static FileMode FileModeFor(FileWriteMode mode) => mode switch
    {
        FileWriteMode.Append => FileMode.Append,
        FileWriteMode.CreateNew => FileMode.CreateNew,
        _ => FileMode.Create,
    };

    /// <summary>
    /// The options for a write open that also sets the mode a created file receives.
    /// </summary>
    /// <param name="fileMode">How to open or create the file.</param>
    /// <param name="createMode">The mode a newly created file receives, before the umask.</param>
    /// <returns>Options for a synchronous <see cref="FileStream" />.</returns>
    [UnsupportedOSPlatform("windows")]
    private static FileStreamOptions WriteOptionsWithCreateMode(FileMode fileMode, UnixFileMode createMode) =>
        new()
        {
            Mode = fileMode,
            Access = FileAccess.Write,
            Share = FileShare.Read,
            BufferSize = BufferSize,
            Options = FileOptions.None,
            UnixCreateMode = createMode,
        };

    /// <summary>
    /// The options every open shares, with the mode, access and sharing that differ.
    /// </summary>
    /// <param name="fileMode">How to open or create the file.</param>
    /// <param name="access">Read or write.</param>
    /// <param name="share">What other processes may do meanwhile.</param>
    /// <returns>Options for a synchronous <see cref="FileStream" />.</returns>
    private static FileStreamOptions OptionsFor(FileMode fileMode, FileAccess access, FileShare share) =>
        new()
        {
            Mode = fileMode,
            Access = access,
            Share = share,
            BufferSize = BufferSize,
            Options = FileOptions.None,
        };

    /// <summary>
    /// Opens <paramref name="path" /> and reads the length and timestamp of the handle
    /// that was opened, not of the path.
    /// </summary>
    /// <param name="path">The operating-system path.</param>
    /// <param name="options">How to open it.</param>
    /// <returns>The opened handle, or the reason it could not be opened.</returns>
    private FileOpenResult Open(string path, FileStreamOptions options)
    {
        FileStream stream;

        try
        {
            stream = new FileStream(path, options);
        }
        catch (Exception exception) when (FileOpenFailure.IsOpenFailure(exception))
        {
            return FailedOpenOf(path, exception);
        }

        return FileOpenResult.Opened(stream, LengthOf(stream), LastWriteTimeUtcOf(stream));
    }

    /// <summary>
    /// The result of an open that threw; for a directory it carries the directory's
    /// last-write time, as curl's <c>fstat</c> of a directory records its modification
    /// time, so a listing can write <c>Last-Modified</c> and apply <c>-z</c>.
    /// </summary>
    /// <param name="path">The operating-system path that failed to open.</param>
    /// <param name="exception">The exception the open threw.</param>
    /// <returns>The failed result.</returns>
    private FileOpenResult FailedOpenOf(string path, Exception exception)
    {
        FileOpenResult failed = FileOpenResult.Failed(StatusOfFailedOpen(path, exception), exception);

        return failed.Status == FileAccessStatus.IsDirectory
            ? failed with { LastWriteTimeUtc = new DateTimeOffset(Directory.GetLastWriteTimeUtc(path), TimeSpan.Zero) }
            : failed;
    }

    /// <summary>
    /// The <see cref="FileAccessStatus" /> of an open that threw: the one
    /// <see cref="FileOpenFailure.StatusFor(Exception, bool)" /> chooses, except that a not
    /// found path with a file among its parents is
    /// <see cref="FileAccessStatus.NotADirectory" /> when this instance reports it so.
    /// </summary>
    /// <param name="path">The operating-system path that failed to open.</param>
    /// <param name="exception">The exception the open threw.</param>
    /// <returns>The status to report.</returns>
    private FileAccessStatus StatusOfFailedOpen(string path, Exception exception)
    {
        FileAccessStatus status = FileOpenFailure.StatusFor(exception, Directory.Exists(path));

        return status == FileAccessStatus.NotFound && reportsFileInPathAsNotADirectory && AParentIsAFile(path)
            ? FileAccessStatus.NotADirectory
            : status;
    }

    /// <summary>
    /// Tells whether any parent of <paramref name="path" />, nearest first, is an existing
    /// file rather than a directory.
    /// </summary>
    /// <param name="path">The operating-system path.</param>
    /// <returns><see langword="true" /> when a parent is a file.</returns>
    private static bool AParentIsAFile(string path)
    {
        for (string? parent = Path.GetDirectoryName(path); !string.IsNullOrEmpty(parent); parent = Path.GetDirectoryName(parent))
        {
            if (File.Exists(parent))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The <see cref="FileAccessStatus" /> of a failed read <c>open</c>'s <c>errno</c>, whose
    /// values these are on both Linux and macOS.
    /// </summary>
    /// <param name="errno">The error number.</param>
    /// <returns>The status.</returns>
    internal static FileAccessStatus StatusForErrno(int errno) => StatusesByErrno.GetValueOrDefault(errno, FileAccessStatus.IoError);

    /// <summary>
    /// The raw-byte read opener a platform uses: none on Windows, whose file APIs take UTF-16
    /// names and never see a raw byte, and the runtime's <c>open</c> shim elsewhere.
    /// </summary>
    /// <param name="isWindows">Whether this is Windows.</param>
    /// <returns>The opener, or <see langword="null" /> on Windows.</returns>
    internal static Func<byte[], RawBytePathOpen>? RawBytePathReadOpenerFor(bool isWindows) =>
        isWindows ? null : OpenUnderRawBytesForRead;

    /// <summary>
    /// Opens <paramref name="path" /> under its raw bytes with <paramref name="openRaw" />, and
    /// reads the length and timestamp of the descriptor it gives, as <see cref="Open" /> does.
    /// </summary>
    /// <param name="openRaw">Opens NUL-terminated raw bytes for reading.</param>
    /// <param name="path">A <see cref="RawBytePath" /> holding a raw byte.</param>
    /// <returns>The opened handle, or the reason it could not be opened.</returns>
    private FileOpenResult OpenUnderRawBytes(Func<byte[], RawBytePathOpen> openRaw, string path)
    {
        RawBytePathOpen open = openRaw([.. RawBytePath.ToBytes(path), 0]);
        if (open.Handle is null)
        {
            return FileOpenResult.Failed(StatusForErrno(open.Errno));
        }

        FileStream stream;
        try
        {
            stream = new FileStream(open.Handle, FileAccess.Read, BufferSize);
        }
        catch (Exception exception) when (FileOpenFailure.IsOpenFailure(exception))
        {
            open.Handle.Dispose();
            return FileOpenResult.Failed(FileOpenFailure.StatusFor(exception, pathIsDirectory: false), exception);
        }

        return FileOpenResult.Opened(stream, LengthOf(stream), LastWriteTimeUtcOf(stream));
    }

    /// <summary>Opens the NUL-terminated name <paramref name="path" /> for reading with the runtime's <c>open</c>.</summary>
    /// <param name="path">The name's raw bytes and a NUL.</param>
    /// <returns>The descriptor, or the <c>errno</c> of the failed open.</returns>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083, ADR-0836: a thin adapter over an open only Linux and macOS take raw bytes in.")]
    private static RawBytePathOpen OpenUnderRawBytesForRead(byte[] path)
    {
        nint descriptor;
        int errno;
        unsafe
        {
            fixed (byte* name = path)
            {
                descriptor = SystemNativeOpen(name, ReadOnly | CloseOnExec, 0);
                errno = Marshal.GetLastSystemError();
            }
        }

        return descriptor < 0
            ? new RawBytePathOpen(null, errno)
            : new RawBytePathOpen(new SafeFileHandle(descriptor, ownsHandle: true), 0);
    }

    /// <summary>
    /// The runtime's non-variadic <c>open</c> shim, which every .NET build on Linux and macOS ships
    /// and Native AOT links in; libc's own <c>open</c> is variadic, which a P/Invoke cannot call
    /// correctly on Apple silicon. Its arguments are blittable, so no marshalling stub is generated;
    /// the caller reads <c>errno</c> straight after, as a <c>SetLastError</c> stub would.
    /// </summary>
    [LibraryImport("libSystem.Native", EntryPoint = "SystemNative_Open")]
    private static unsafe partial nint SystemNativeOpen(byte* path, int flags, int mode);

    /// <summary>
    /// What a raw-byte read <c>open</c> gave: the descriptor's handle, or <see langword="null" />
    /// and the <c>errno</c> it failed with.
    /// </summary>
    /// <param name="Handle">The opened descriptor, owned by the receiver.</param>
    /// <param name="Errno">The failed open's error number; zero when it opened.</param>
    internal sealed record RawBytePathOpen(SafeFileHandle? Handle, int Errno);

    /// <summary>
    /// The length of an opened handle, or zero for one that cannot seek and so has none.
    /// </summary>
    /// <param name="stream">The opened handle.</param>
    /// <returns>Its length in bytes.</returns>
    private static long LengthOf(FileStream stream) => stream.CanSeek ? stream.Length : 0;

    /// <summary>
    /// The last-write timestamp of an opened handle; when the operating system reports none
    /// for it, as for a Windows device, the Unix epoch or <see langword="null" /> as this
    /// instance was constructed to report.
    /// </summary>
    /// <param name="stream">The opened handle.</param>
    /// <returns>The timestamp in Coordinated Universal Time, or <see langword="null" />.</returns>
    private DateTimeOffset? LastWriteTimeUtcOf(FileStream stream)
    {
        try
        {
            return new DateTimeOffset(File.GetLastWriteTimeUtc(stream.SafeFileHandle), TimeSpan.Zero);
        }
        catch (Exception exception) when (FileOpenFailure.IsOpenFailure(exception))
        {
            return reportsUnreadableTimestampAsEpoch ? DateTimeOffset.UnixEpoch : null;
        }
    }
}
