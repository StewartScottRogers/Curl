using System.Threading.Channels;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.WebSocketExtendedConnect.UnitTests.Http3;

/// <summary>A QUIC stream in memory: the test queues what the server sends and reads back what the client wrote.</summary>
internal sealed class InMemoryMultiplexedStream(long streamId) : IMultiplexedStream
{
    private readonly Channel<byte[]> inbound = Channel.CreateUnbounded<byte[]>();
    private ReadOnlyMemory<byte> pending;

    public long StreamId => streamId;

    public MemoryStream Written { get; } = new();

    public bool IsWriteEnded { get; private set; }

    public long? AbortedWith { get; private set; }

    public InMemoryMultiplexedStream Receive(params byte[] bytes)
    {
        inbound.Writer.TryWrite(bytes);
        return this;
    }

    public void EndReceiving() => inbound.Writer.TryComplete();

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

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
