using System.Collections.Concurrent;
using System.Net;
using System.Threading.Channels;
using Curl.Extension.Coap.Dtls.TestServer;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Coap.Dtls.Handshake;

/// <summary>
/// A datagram channel to a <see cref="DtlsTestServer" />: each datagram the client sends is answered by
/// the server at once, every answer passing through <see cref="RewriteServerDatagram" /> (which may change
/// or drop it) before the client can receive it.
/// </summary>
internal sealed class DtlsTestServerChannel(DtlsTestServer server) : IDatagramChannel
{
    private readonly Channel<byte[]> inbox = Channel.CreateUnbounded<byte[]>();

    private readonly object starvedGate = new();
    private TaskCompletionSource starved = NewStarved();

    /// <summary>Completes once the client waits to receive with no datagram left in the inbox.</summary>
    public async Task WaitUntilStarvedAsync()
    {
        while (true)
        {
            Task wait;
            lock (starvedGate)
            {
                wait = starved.Task;
            }

            await wait;
            lock (starvedGate)
            {
                if (ReferenceEquals(wait, starved.Task))
                {
                    return;
                }
            }
        }
    }

    /// <inheritdoc />
    public EndPoint ServerEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 5684);

    /// <summary>Gets or sets what each server datagram becomes; <see langword="null" /> drops it.</summary>
    public Func<byte[], byte[]?> RewriteServerDatagram { get; set; } = datagram => datagram;

    /// <summary>Gets every datagram the client sent, in order.</summary>
    public ConcurrentQueue<byte[]> Sent { get; } = new();

    /// <summary>Gets every datagram the client was handed, in order.</summary>
    public ConcurrentQueue<byte[]> Delivered { get; } = new();

    /// <summary>Hands <paramref name="datagram" /> to the client as if the server sent it.</summary>
    public void Deliver(byte[] datagram)
    {
        lock (starvedGate)
        {
            if (starved.Task.IsCompleted)
            {
                starved = NewStarved();
            }
        }

        Delivered.Enqueue(datagram);
        inbox.Writer.TryWrite(datagram);
    }

    /// <inheritdoc />
    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
    {
        byte[] bytes = datagram.ToArray();
        Sent.Enqueue(bytes);
        foreach (byte[] answer in server.Receive(bytes))
        {
            if (RewriteServerDatagram(answer) is { } rewritten)
            {
                Deliver(rewritten);
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        lock (starvedGate)
        {
            if (inbox.Reader.Count == 0)
            {
                starved.TrySetResult();
            }
        }

        byte[] datagram = await inbox.Reader.ReadAsync(cancellationToken);
        datagram.CopyTo(buffer);
        return new DatagramReceived(datagram.Length, ServerEndPoint);
    }

    private static TaskCompletionSource NewStarved() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
