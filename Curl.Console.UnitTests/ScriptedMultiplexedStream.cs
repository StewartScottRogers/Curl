using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// An in-memory QUIC stream: reads replay <paramref name="incoming" /> and then end the stream,
/// and writes are recorded.
/// </summary>
/// <param name="streamId">The QUIC stream ID.</param>
/// <param name="incoming">The bytes the server sends on the stream.</param>
internal sealed class ScriptedMultiplexedStream(long streamId, byte[] incoming) : IMultiplexedStream
{
    private readonly TaskCompletionSource firstWrite = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int position;

    public long StreamId { get; } = streamId;

    /// <summary>Gets every byte the client wrote, in order.</summary>
    public MemoryStream Written { get; } = new();

    /// <summary>Gets the task every read waits for before it returns the server's bytes; complete by default.</summary>
    public Task ReadsAfter { get; init; } = Task.CompletedTask;

    /// <summary>Gets a task that completes once the client has written to the stream.</summary>
    public Task FirstWrite => firstWrite.Task;

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        await ReadsAfter.ConfigureAwait(false);
        int count = Math.Min(buffer.Length, incoming.Length - position);
        incoming.AsSpan(position, count).CopyTo(buffer.Span);
        position += count;
        return count;
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, bool endStream, CancellationToken cancellationToken)
    {
        Written.Write(buffer.Span);
        firstWrite.TrySetResult();
        return ValueTask.CompletedTask;
    }

    public void Abort(long applicationErrorCode)
    {
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
