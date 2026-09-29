using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// An in-memory <see cref="IMultiplexedStream" />: reads replay scripted bytes in chunks, once
/// <see cref="ReadsAfter" /> lets them, then end the stream (FIN), throw
/// <see cref="EndException" /> or, when it <see cref="StaysOpen" />, wait until cancelled;
/// writes are recorded, with whether
/// the client ended the stream and the code of any abort.
/// </summary>
/// <param name="streamId">The QUIC stream ID.</param>
/// <param name="incoming">The bytes the peer sends on the stream.</param>
/// <param name="chunkSize">The most bytes one read returns.</param>
public sealed class FakeMultiplexedStream(long streamId, byte[] incoming, int chunkSize = 65536) : IMultiplexedStream
{
    private readonly TaskCompletionSource drained = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int position;

    /// <inheritdoc />
    public long StreamId { get; } = streamId;

    /// <summary>
    /// Gets the exception reads throw once the scripted bytes are used up, or
    /// <see langword="null" /> for a clean end (FIN).
    /// </summary>
    public Exception? EndException { get; init; }

    /// <summary>
    /// Gets the exception every write throws, or <see langword="null" /> to accept writes.
    /// </summary>
    public Exception? WriteException { get; init; }

    /// <summary>Gets every byte the client wrote, in order.</summary>
    public MemoryStream Written { get; } = new();

    /// <summary>Gets a value indicating whether the client ended its side (FIN).</summary>
    public bool IsEndedByClient { get; private set; }

    /// <summary>Gets how many writes carried FIN.</summary>
    public int EndCount { get; private set; }

    /// <summary>Gets the error code the client aborted the stream with, or <see langword="null" />.</summary>
    public long? AbortCode { get; private set; }

    /// <summary>Gets a value indicating whether the stream was disposed.</summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Gets a task every read waits for before it reads, so a test decides when the bytes
    /// arrive, or <see langword="null" /> to read at once.
    /// </summary>
    public Task? ReadsAfter { get; init; }

    /// <summary>
    /// Gets a value indicating whether a read past the scripted bytes waits until it is
    /// cancelled, as an open stream the peer sends nothing more on does, instead of ending.
    /// </summary>
    public bool StaysOpen { get; init; }

    /// <summary>Gets a task that completes once a read has found every scripted byte read.</summary>
    public Task Drained => drained.Task;

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ReadsAfter is null ? ReadNow(buffer, cancellationToken) : ReadLaterAsync(buffer, cancellationToken);
    }

    private async ValueTask<int> ReadLaterAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        await ReadsAfter!;
        return await ReadNow(buffer, cancellationToken);
    }

    private ValueTask<int> ReadNow(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (position == incoming.Length)
        {
            drained.TrySetResult();
            if (StaysOpen)
            {
                return WaitUntilCancelledAsync(cancellationToken);
            }

            return EndException is null ? ValueTask.FromResult(0) : ValueTask.FromException<int>(EndException);
        }

        int count = Math.Min(Math.Min(chunkSize, buffer.Length), incoming.Length - position);
        incoming.AsSpan(position, count).CopyTo(buffer.Span);
        position += count;
        return ValueTask.FromResult(count);
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, bool endStream, CancellationToken cancellationToken)
    {
        if (WriteException is not null)
        {
            return ValueTask.FromException(WriteException);
        }

        Written.Write(buffer.Span);
        IsEndedByClient |= endStream;
        EndCount += endStream ? 1 : 0;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void Abort(long applicationErrorCode) => AbortCode = applicationErrorCode;

    private static async ValueTask<int> WaitUntilCancelledAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return 0;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
