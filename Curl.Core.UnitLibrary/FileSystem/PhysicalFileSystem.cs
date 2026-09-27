using System.Runtime.Versioning;
using Curl.Protocol.Abstractions;

namespace Curl.Core.FileSystem;

/// <summary>
/// The real-disk <see cref="IFileSystem" /> and <see cref="IFileTimeSetter" />: opens local files with
/// <see cref="FileStream" /> and reports every failed open as a
/// <see cref="FileAccessStatus" />, never as an exception.
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
/// <see cref="FileOpenResult.Failed(FileAccessStatus)" />; the only exception either open
/// lets out is the <see cref="OperationCanceledException" /> of a cancelled token.
/// </para>
/// <para>
/// A read open is seekable for a regular file. A character device or a FIFO - such as
/// <c>/dev/stdin</c> - opens as a stream that cannot seek, reporting a length of zero,
/// just as curl's <c>fstat</c> of one does; the handler answers an offset on such a
/// source with exit 36 rather than seeking it.
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
public sealed class PhysicalFileSystem : IFileSystem, IFileTimeSetter
{
    private const int BufferSize = 4096;

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

        FileMode fileMode = mode == FileWriteMode.Append ? FileMode.Append : FileMode.Create;
        FileStreamOptions options = setsUnixCreateMode
            ? WriteOptionsWithCreateMode(fileMode, createMode)
            : OptionsFor(fileMode, FileAccess.Write, FileShare.Read);

        return ValueTask.FromResult(Open(path, options));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Every exception <see cref="FileOpenFailure.IsOpenFailure(Exception)" /> names, such as
    /// the <see cref="FileNotFoundException" /> of a missing file, is reported as
    /// <see langword="false" />, with the error code
    /// <see cref="FileOpenFailure.Win32ErrorCodeOf(Exception)" /> reads from it.
    /// </remarks>
    public bool TrySetLastWriteTimeUtc(string path, DateTimeOffset lastWriteTimeUtc, out int errorCode)
    {
        try
        {
            File.SetLastWriteTimeUtc(path, lastWriteTimeUtc.UtcDateTime);
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
            return FileOpenResult.Failed(FileOpenFailure.StatusFor(exception, Directory.Exists(path)));
        }

        return FileOpenResult.Opened(stream, LengthOf(stream), LastWriteTimeUtcOf(stream));
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
