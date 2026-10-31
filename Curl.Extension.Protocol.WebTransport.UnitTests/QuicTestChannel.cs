using System.Net;
using System.Threading.Channels;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.WebTransport.Session;

/// <summary>A datagram channel wired to an in-memory <see cref="QuicTestServer" />: every datagram the client sends goes to the server, and its answers come back.</summary>
/// <param name="server">The server.</param>
internal sealed class QuicTestChannel(QuicTestServer server) : IDatagramChannel
{
    private readonly Channel<byte[]> inbound = Channel.CreateUnbounded<byte[]>();

    private readonly Lock gate = new();

    public EndPoint ServerEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 443);

    public bool IsDisposed { get; private set; }

    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            foreach (byte[] answer in server.Receive(datagram.ToArray()))
            {
                inbound.Writer.TryWrite(answer);
            }
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        byte[] datagram = await inbound.Reader.ReadAsync(cancellationToken);
        datagram.CopyTo(buffer);
        return new DatagramReceived(datagram.Length, ServerEndPoint);
    }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
