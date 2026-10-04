using System.Runtime.Versioning;
using Curl.Protocol.Abstractions;

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
/// </remarks>
public sealed class PhysicalFileSystem : IFileSystem, IDirectoryLister, IFileTimeSetter
{
    private const int BufferSize = 4096;

    /// <summary>The Unix seconds of 0001-01-01T00:00:00Z, the earliest <see cref="DateTime" />.</summary>
    private const long MinimumDateTimeUnixSeconds = -62135596800;

    /// <summary>The Unix seconds of 9999-12-31T23:59:59Z, the latest whole second of <see cref="DateTime" />.</summary>
    private const long MaximumDateTimeUnixSeconds = 253402300799;

    [UnsupportedOSPlatformGuard("windows")]
    private readonly bool setsUnixCreateMode;

    private readonly bool reportsUnreadableTimestampAsEpoch;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhysicalFileSystem" /> class that sets
    /// the create mode of a written file everywhere except Windows, and reports the Unix
    /// epoch for an unreadable timestamp only on Windows.
    /// </summary>
    public PhysicalFileSystem()
        : this(
            setsUnixCreateMode: !OperatingSystem.IsWindows(),
            reportsUnreadableTimestampAsEpoch: OperatingSystem.IsWindows())
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
    internal PhysicalFileSystem(bool setsUnixCreateMode, bool reportsUnreadableTimestampAsEpoch = false)
    {
        this.setsUnixCreateMode = setsUnixCreateMode;
        this.reportsUnreadableTimestampAsEpoch = reportsUnreadableTimestampAsEpoch;
    }

    /// <inheritdoc />
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was cancelled before the open.
    /// </exception>
    public ValueTask<FileOpenResult> OpenForReadAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

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
    /// <see langword="false" />, with the error code
    /// <see cref="FileOpenFailure.Win32ErrorCodeOf(Exception)" /> reads from it. A time
    /// <see cref="DateTime" /> cannot hold, past year 9999 or before year 1, is set by
    /// <see cref="NativeFileTimeSetter" /> with the operating system's own call.
    /// </remarks>
    public bool TrySetLastWriteUnixSeconds(string path, long unixSeconds, out int errorCode)
    {
        try
        {
            if (unixSeconds is < MinimumDateTimeUnixSeconds or > MaximumDateTimeUnixSeconds)
            {
                return NativeFileTimeSetter.TrySetLastWriteUnixSeconds(path, unixSeconds, out errorCode);
            }

            File.SetLastWriteTimeUtc(path, DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime);
        }
        catch (Exception exception) when (FileOpenFailure.IsOpenFailure(exception))
        {
            errorCode = FileOpenFailure.Win32ErrorCodeOf(exception);
            return false;
        }

        errorCode = 0;
        return true;
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
    /// <returns>Options for an asynchronous <see cref="FileStream" />.</returns>
    [UnsupportedOSPlatform("windows")]
    private static FileStreamOptions WriteOptionsWithCreateMode(FileMode fileMode, UnixFileMode createMode) =>
        new()
        {
            Mode = fileMode,
            Access = FileAccess.Write,
            Share = FileShare.Read,
            BufferSize = BufferSize,
            Options = FileOptions.Asynchronous,
            UnixCreateMode = createMode,
        };

    /// <summary>
    /// The options every open shares, with the mode, access and sharing that differ.
    /// </summary>
    /// <param name="fileMode">How to open or create the file.</param>
    /// <param name="access">Read or write.</param>
    /// <param name="share">What other processes may do meanwhile.</param>
    /// <returns>Options for an asynchronous <see cref="FileStream" />.</returns>
    private static FileStreamOptions OptionsFor(FileMode fileMode, FileAccess access, FileShare share) =>
        new()
        {
            Mode = fileMode,
            Access = access,
            Share = share,
            BufferSize = BufferSize,
            Options = FileOptions.Asynchronous,
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
    private static FileOpenResult FailedOpenOf(string path, Exception exception)
    {
        bool isDirectory = Directory.Exists(path);
        FileOpenResult failed = FileOpenResult.Failed(FileOpenFailure.StatusFor(exception, isDirectory), exception);

        return failed.Status == FileAccessStatus.IsDirectory
            ? failed with { LastWriteTimeUtc = new DateTimeOffset(Directory.GetLastWriteTimeUtc(path), TimeSpan.Zero) }
            : failed;
    }

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
