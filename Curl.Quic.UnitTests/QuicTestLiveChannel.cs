using System.Net;
using System.Threading.Channels;
using Curl.Protocol.Abstractions;

namespace Curl.Quic;

/// <summary>
/// A datagram channel wired to a <see cref="QuicTestServer" /> that a connection's loop and a
/// test can use at once: every use of the server is under one lock, and datagrams to receive
/// queue in a <see cref="Channel{T}" />. A test can make the server stop answering, send it
/// frames to deliver, queue a datagram from another endpoint, or make the next receive fail.
/// The channel owns the server and disposes it.
/// </summary>
internal sealed class QuicTestLiveChannel(QuicTestServer server) : IDatagramChannel
{
    public static readonly EndPoint ClientAddress = new IPEndPoint(IPAddress.Loopback, 50123);

    private readonly Channel<(byte[]? Datagram, EndPoint From, Exception? Error)> inbound = Channel.CreateUnbounded<(byte[]?, EndPoint, Exception?)>();

    private readonly Lock gate = new();

    private bool silent;

    public EndPoint ServerEndPoint => QuicTestChannel.ServerAddress;

    public EndPoint? LocalEndPoint => ClientAddress;

    public bool IsDisposed { get; private set; }

    /// <summary>From now on the server still takes what the client sends but its answers are lost, as on a path that drops everything coming back.</summary>
    public void Silence()
    {
        lock (gate)
        {
            silent = true;
        }
    }

    /// <summary>Has the server send <paramref name="frames" /> in one 1-RTT packet.</summary>
    public void FromServer(params QuicFrame[] frames)
    {
        byte[] datagram;
        lock (gate)
        {
            datagram = server.Protect(QuicPacketType.OneRtt, frames);
        }

        inbound.Writer.TryWrite((datagram, ServerEndPoint, null));
    }

    /// <summary>Queues a 1-RTT packet the server protected, as if it came from an endpoint other than the server.</summary>
    public void FromElsewhere(params QuicFrame[] frames)
    {
        byte[] datagram;
        lock (gate)
        {
            datagram = server.Protect(QuicPacketType.OneRtt, frames);
        }

        inbound.Writer.TryWrite((datagram, ClientAddress, null));
    }

    /// <summary>Makes the next receive throw <paramref name="error" />.</summary>
    public void FailNextReceive(Exception error) => inbound.Writer.TryWrite((null, ServerEndPoint, error));

    /// <summary>Returns the frames of type <typeparamref name="T" /> the server has taken from the client in 1-RTT packets.</summary>
    public List<T> Sent<T>()
        where T : QuicFrame
    {
        lock (gate)
        {
            return QuicStreamTest.Sent<T>(server);
        }
    }

    /// <summary>Waits, a millisecond at a time for up to ten seconds, until <paramref name="condition" /> holds.</summary>
    public static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 10000 && !condition(); attempt++)
        {
            await Task.Delay(1);
        }

        Assert.IsTrue(condition());
    }

    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
    {
        IReadOnlyList<byte[]> answers = [];
        lock (gate)
        {
            if (!IsDisposed)
            {
                answers = server.Receive(datagram.ToArray());
            }

            if (silent)
            {
                answers = [];
            }
        }

        foreach (byte[] answer in answers)
        {
            inbound.Writer.TryWrite((answer, ServerEndPoint, null));
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        (byte[]? datagram, EndPoint from, Exception? error) = await inbound.Reader.ReadAsync(cancellationToken);
        if (error is not null)
        {
            throw error;
        }

        datagram!.CopyTo(buffer);
        return new DatagramReceived(datagram!.Length, from);
    }

    /// <summary>Disposes the server too, which the channel owns; what it took from the client stays readable.</summary>
    public ValueTask DisposeAsync()
    {
        lock (gate)
        {
            IsDisposed = true;
            server.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
