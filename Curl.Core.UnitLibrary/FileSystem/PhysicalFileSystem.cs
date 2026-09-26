using Curl.Protocol.Abstractions;

namespace Curl.Core.FileSystem;

/// <summary>
/// The real-disk <see cref="IFileSystem" />: opens local files with
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
/// </remarks>
public sealed class PhysicalFileSystem : IFileSystem
{
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
            Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
    }

    /// <inheritdoc />
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was cancelled before the open.
    /// </exception>
    public ValueTask<FileOpenResult> OpenForWriteAsync(
        string path,
        FileWriteMode mode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        FileMode fileMode = mode == FileWriteMode.Append ? FileMode.Append : FileMode.Create;

        return ValueTask.FromResult(Open(path, fileMode, FileAccess.Write, FileShare.Read));
    }

    /// <summary>
    /// Opens <paramref name="path" /> and reads the length and timestamp of the handle
    /// that was opened, not of the path.
    /// </summary>
    /// <param name="path">The operating-system path.</param>
    /// <param name="fileMode">How to open or create the file.</param>
    /// <param name="access">Read or write.</param>
    /// <param name="share">What other processes may do meanwhile.</param>
    /// <returns>The opened handle, or the reason it could not be opened.</returns>
    private static FileOpenResult Open(string path, FileMode fileMode, FileAccess access, FileShare share)
    {
        FileStream stream;

        try
        {
            stream = new FileStream(path, fileMode, access, share, bufferSize: 4096, FileOptions.Asynchronous);
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
    /// The last-write timestamp of an opened handle, or <see langword="null" /> when the
    /// operating system reports none for it, as for a device.
    /// </summary>
    /// <param name="stream">The opened handle.</param>
    /// <returns>The timestamp in Coordinated Universal Time, or <see langword="null" />.</returns>
    private static DateTimeOffset? LastWriteTimeUtcOf(FileStream stream)
    {
        try
        {
            return new DateTimeOffset(File.GetLastWriteTimeUtc(stream.SafeFileHandle), TimeSpan.Zero);
        }
        catch (Exception exception) when (FileOpenFailure.IsOpenFailure(exception))
        {
            return null;
        }
    }
}
