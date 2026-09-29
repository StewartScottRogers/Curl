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
    private int position;

    public long StreamId { get; } = streamId;

    /// <summary>Gets every byte the client wrote, in order.</summary>
    public MemoryStream Written { get; } = new();

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int count = Math.Min(buffer.Length, incoming.Length - position);
        incoming.AsSpan(position, count).CopyTo(buffer.Span);
        position += count;
        return ValueTask.FromResult(count);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, bool endStream, CancellationToken cancellationToken)
    {
        Written.Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    public void Abort(long applicationErrorCode)
    {
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
