using System.Net;
using System.Threading.Channels;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Extended.Harness;

/// <summary>
/// curl's UDP channel to a reference server: each datagram sent is answered at once, and the answers queue for receiving.
/// A receive waiting when an answer queues completes before <see cref="SendAsync" /> returns, so a clock that fires
/// curl's timers one after another cannot fire the next before curl sees the answer to its last datagram (BL-4538).
/// </summary>
internal sealed class ReferenceDatagramChannel(IDatagramReferenceServer server, WireExchange exchange, EndPoint serverEndPoint) : IDatagramChannel
{
    private readonly Channel<byte[]> answers = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { AllowSynchronousContinuations = true });

    public EndPoint ServerEndPoint => serverEndPoint;

    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
    {
        exchange.RecordClient(datagram.Span);
        foreach (ReadOnlyMemory<byte> answer in server.Answer(datagram))
        {
            exchange.RecordServer(answer.Span);
            answers.Writer.TryWrite(answer.ToArray());
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        byte[] answer = await answers.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        int length = Math.Min(answer.Length, buffer.Length);
        answer.AsSpan(0, length).CopyTo(buffer.Span);
        return new DatagramReceived(length, serverEndPoint);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
