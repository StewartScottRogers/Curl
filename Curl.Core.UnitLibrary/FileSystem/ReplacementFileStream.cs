namespace Curl.Core.FileSystem;

/// <summary>
/// The write-only stream of a <see cref="Curl.Protocol.Abstractions.FileWriteMode.Replace" />
/// open: it writes a temporary file and, when it closes, renames that file over the one it
/// replaces. After a write or flush that failed it deletes the temporary file instead, as
/// libcurl does, so no partial file takes the old one's place.
/// </summary>
/// <param name="temporary">The open temporary file.</param>
/// <param name="temporaryPath">Its path.</param>
/// <param name="path">The path it is renamed to.</param>
internal sealed class ReplacementFileStream(Stream temporary, string temporaryPath, string path) : Stream
{
    private bool hasFailedToWrite;

    private bool isClosed;

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => !isClosed;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        try
        {
            temporary.Write(buffer, offset, count);
        }
        catch
        {
            hasFailedToWrite = true;
            throw;
        }
    }

    /// <inheritdoc />
    public override void Flush()
    {
        try
        {
            temporary.Flush();
        }
        catch
        {
            hasFailedToWrite = true;
            throw;
        }
    }

    /// <summary>
    /// Closes the temporary file and renames it over the replaced one, once.
    /// </summary>
    /// <param name="disposing">Whether the caller is closing the stream.</param>
    /// <exception cref="IOException">The temporary file could not be closed or renamed; it is deleted.</exception>
    protected override void Dispose(bool disposing)
    {
        // No finalizer calls this, so disposing is always true.
        if (!isClosed)
        {
            isClosed = true;
            CloseAndReplace();
        }

        base.Dispose(disposing);
    }

    private void CloseAndReplace()
    {
        try
        {
            temporary.Dispose();
            if (!hasFailedToWrite)
            {
                File.Move(temporaryPath, path, overwrite: true);
            }
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new IOException(exception.Message, exception);
        }
        finally
        {
            // Nothing is there once the rename has happened.
            File.Delete(temporaryPath);
        }
    }
}
