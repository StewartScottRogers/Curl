using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.QuicMigration;

/// <summary>A datagram channel with no socket: records what is sent, replays queued datagrams, and fails on demand.</summary>
internal sealed class FakeDatagramChannel(int localPort) : IDatagramChannel
{
    public static readonly IPEndPoint Server = new(IPAddress.Loopback, 443);

    private readonly Queue<byte[]> incoming = new();

    public List<byte[]> Sent { get; } = [];

    public Exception? SendFailure { get; set; }

    public Exception? ReceiveFailure { get; set; }

    public TaskCompletionSource? ReceiveGate { get; set; }

    public bool IsDisposed { get; private set; }

    public EndPoint ServerEndPoint => Server;

    public EndPoint? LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, localPort);

    public void Enqueue(params byte[] datagram) => incoming.Enqueue(datagram);

    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (SendFailure is not null)
        {
            throw SendFailure;
        }

        Sent.Add(datagram.ToArray());
        return ValueTask.CompletedTask;
    }

    public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (ReceiveGate is not null)
        {
            await ReceiveGate.Task;
        }

        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (ReceiveFailure is not null)
        {
            throw ReceiveFailure;
        }

        byte[] datagram = incoming.Dequeue();
        datagram.CopyTo(buffer);
        return new DatagramReceived(datagram.Length, Server);
    }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
