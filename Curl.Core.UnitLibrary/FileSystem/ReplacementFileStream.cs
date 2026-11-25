namespace Curl.Core.FileSystem;

/// <summary>
/// The write-only stream of a <see cref="Curl.Protocol.Abstractions.FileWriteMode.Replace" />
/// open: it writes a temporary file and, when it closes, renames that file over the one it
/// replaces. After a write or flush that failed it deletes the temporary file instead, as
/// libcurl does, so no partial file takes the old one's place. A rename the operating
/// system refuses is tried again, a millisecond apart, for the retry window by
/// <see cref="DisposeAsync" />, as <c>curlx_rename</c> retries on Windows;
/// <see cref="Stream.Dispose()" /> tries it once.
/// </summary>
/// <param name="temporary">The open temporary file.</param>
/// <param name="temporaryPath">Its path.</param>
/// <param name="path">The path it is renamed to.</param>
/// <param name="renameRetryWindow">How long <see cref="DisposeAsync" /> retries a refused rename.</param>
/// <param name="timeProvider">Times the retries; <see langword="null" /> is <see cref="TimeProvider.System" />.</param>
internal sealed class ReplacementFileStream(
    Stream temporary,
    string temporaryPath,
    string path,
    TimeSpan renameRetryWindow = default,
    TimeProvider? timeProvider = null) : Stream
{
    private static readonly TimeSpan RenameRetryPause = TimeSpan.FromMilliseconds(1);

    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

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
    /// Closes the temporary file and renames it over the replaced one, once, retrying a
    /// refused rename until the retry window has passed.
    /// </summary>
    /// <returns>A task that completes when the file is replaced.</returns>
    /// <exception cref="IOException">The temporary file could not be closed or renamed; it is deleted.</exception>
    public override async ValueTask DisposeAsync()
    {
        if (!isClosed)
        {
            isClosed = true;
            await CloseAndReplaceAsync().ConfigureAwait(false);
        }

        await base.DisposeAsync().ConfigureAwait(false);
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
            if (!hasFailedToWrite && TryRename() is { } failure)
            {
                throw failure;
            }
        }
        finally
        {
            // Nothing is there once the rename has happened.
            File.Delete(temporaryPath);
        }
    }

    private async Task CloseAndReplaceAsync()
    {
        try
        {
            await temporary.DisposeAsync().ConfigureAwait(false);
            if (!hasFailedToWrite)
            {
                await RenameWithinRetryWindowAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private async Task RenameWithinRetryWindowAsync()
    {
        long start = clock.GetTimestamp();
        IOException? failure = TryRename();
        while (failure is not null && clock.GetElapsedTime(start) < renameRetryWindow)
        {
            await Task.Delay(RenameRetryPause, clock).ConfigureAwait(false);
            failure = TryRename();
        }

        if (failure is not null)
        {
            throw failure;
        }
    }

    /// <summary>Renames the temporary file over the path.</summary>
    /// <returns><see langword="null" /> when renamed; otherwise why it was refused.</returns>
    private IOException? TryRename()
    {
        try
        {
            File.Move(temporaryPath, path, overwrite: true);
            return null;
        }
        catch (IOException exception)
        {
            return exception;
        }
        catch (UnauthorizedAccessException exception)
        {
            return new IOException(exception.Message, exception);
        }
    }
}
