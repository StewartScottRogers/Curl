namespace Curl.Original.Protocol.Http.Fakes;

/// <summary>
/// A read-only stream that reads as standard input on a pipe does: the first read gives every
/// byte written so far, and the read after it waits until the writing end is closed
/// (<see cref="CloseWritingEnd" />) and then gives 0, so the stream's bytes and its end arrive
/// in separate reads (upstream test60 run out of process, BL-2483).
/// </summary>
/// <param name="content">The bytes written into the pipe before the first read.</param>
public sealed class PipeLikeStream(byte[] content) : Stream
{
    private readonly TaskCompletionSource writingEndClosed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource endReadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private bool contentRead;

    /// <summary>
    /// Gets a task that completes when a read is waiting for the writing end to close.
    /// </summary>
    public Task EndReadStarted => endReadStarted.Task;

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// Closes the pipe's writing end, so the waiting read, and every read after it, gives 0.
    /// </summary>
    public void CloseWritingEnd() => writingEndClosed.TrySetResult();

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (!contentRead)
        {
            contentRead = true;
            content.CopyTo(buffer);
            return content.Length;
        }

        endReadStarted.TrySetResult();
        await writingEndClosed.Task.WaitAsync(cancellationToken);
        return 0;
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
