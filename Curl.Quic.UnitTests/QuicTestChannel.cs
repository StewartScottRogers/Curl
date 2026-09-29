using System.Diagnostics;
using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Quic;

/// <summary>
/// A datagram channel wired to a <see cref="QuicTestServer" />: each datagram sent is handed
/// to the server at once and its answers queue up to be received. With no server nothing
/// answers, as a silent UDP peer. A receive with nothing queued waits for cancellation.
/// </summary>
internal sealed class QuicTestChannel(QuicTestServer? server) : IDatagramChannel
{
    public static readonly EndPoint ServerAddress = new IPEndPoint(IPAddress.Loopback, 4433);

    private readonly Queue<(byte[] Datagram, EndPoint From)> inbound = new();

    public EndPoint ServerEndPoint => ServerAddress;

    /// <summary>Gets every datagram the client sent, in order.</summary>
    public List<byte[]> Sent { get; } = [];

    /// <summary>Queues a datagram for the client to receive, as if <paramref name="from" /> sent it.</summary>
    public void Enqueue(byte[] datagram, EndPoint from) => inbound.Enqueue((datagram, from));

    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
    {
        Assert.AreEqual(ServerAddress, destination);
        Sent.Add(datagram.ToArray());
        foreach (byte[] answer in server?.Receive(datagram.ToArray()) ?? [])
        {
            inbound.Enqueue((answer, ServerAddress));
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (inbound.TryDequeue(out (byte[] Datagram, EndPoint From) next))
        {
            next.Datagram.CopyTo(buffer);
            return new DatagramReceived(next.Datagram.Length, next.From);
        }

        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new UnreachableException();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
