using System.Threading.Channels;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.WebTransport;

/// <summary>
/// An <see cref="IMultiplexedStream" /> in memory: the test queues the bytes it receives, ends
/// or resets its receiving side, and reads back what was written to it, whether its end was
/// written, the code it was aborted with and whether it was disposed.
/// </summary>
/// <param name="streamId">The QUIC stream ID.</param>
internal sealed class InMemoryMultiplexedStream(long streamId) : IMultiplexedStream
{
    private readonly Channel<byte[]> inbound = Channel.CreateUnbounded<byte[]>();
    private ReadOnlyMemory<byte> pending;

    public long StreamId => streamId;

    public MemoryStream Written { get; } = new();

    public bool IsWriteEnded { get; private set; }

    public long? AbortedWith { get; private set; }

    public bool IsDisposed { get; private set; }

    /// <summary>Queues <paramref name="bytes" /> to be read.</summary>
    public InMemoryMultiplexedStream Receive(params byte[] bytes)
    {
        inbound.Writer.TryWrite(bytes);
        return this;
    }

    /// <summary>Ends the receiving side once the queued bytes are read.</summary>
    public void EndReceiving() => inbound.Writer.TryComplete();

    /// <summary>Makes the read after the queued bytes fail with <paramref name="error" />.</summary>
    public void FailReceiving(Exception error) => inbound.Writer.TryComplete(error);

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        while (pending.IsEmpty)
        {
            if (inbound.Reader.TryRead(out byte[]? ready))
            {
                pending = ready;
                continue;
            }

            if (!await inbound.Reader.WaitToReadAsync(cancellationToken))
            {
                return 0;
            }
        }

        int count = Math.Min(buffer.Length, pending.Length);
        pending[..count].CopyTo(buffer);
        pending = pending[count..];
        return count;
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, bool endStream, CancellationToken cancellationToken)
    {
        Written.Write(buffer.Span);
        IsWriteEnded |= endStream;
        return ValueTask.CompletedTask;
    }

    public void Abort(long applicationErrorCode) => AbortedWith = applicationErrorCode;

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
