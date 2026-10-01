using System.Collections.Concurrent;
using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Quic;

/// <summary>
/// A datagram channel wired to a <see cref="QuicTestServer" /> that a connection's loop and a
/// test can use at once: every use of the server is under one lock, and datagrams to receive
/// queue in a <see cref="ConcurrentQueue{T}" /> counted by a <see cref="SemaphoreSlim" />, so a
/// receive the loop cancels never takes a datagram with it. A test can make the server stop
/// answering, send it frames to deliver, queue a datagram from another endpoint, make the next
/// receive fail, or wait, datagram by datagram, until the server has taken what it expects from
/// the client. The channel owns the server and disposes it.
/// </summary>
/// <remarks>
/// Not a <c>System.Threading.Channels</c> channel: on .NET 10.0.12 an unbounded channel loses an
/// item written just after a waiting <c>ReadAsync</c> is cancelled (about 1 in 27,000 such races
/// under load), and the connection's loop cancels its receive on every wake, so a test whose
/// server packet was lost waited forever (BL-1067).
/// </remarks>
internal sealed class QuicTestLiveChannel(QuicTestServer server) : IDatagramChannel
{
    public static readonly EndPoint ClientAddress = new IPEndPoint(IPAddress.Loopback, 50123);

    private readonly ConcurrentQueue<(byte[]? Datagram, EndPoint From, Exception? Error)> inbound = new();

    private readonly SemaphoreSlim inboundCount = new(0);

    /// <summary>How long a test waits for the connection's loop to act before it fails rather than hangs; never how long anything is meant to take.</summary>
    public static readonly TimeSpan HangGuard = TimeSpan.FromMinutes(1);

    private readonly Lock gate = new();

    private TaskCompletionSource datagramTaken = new(TaskCreationOptions.RunContinuationsAsynchronously);

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

        Enqueue((datagram, ServerEndPoint, null));
    }

    /// <summary>Queues a 1-RTT packet the server protected, as if it came from an endpoint other than the server.</summary>
    public void FromElsewhere(params QuicFrame[] frames)
    {
        byte[] datagram;
        lock (gate)
        {
            datagram = server.Protect(QuicPacketType.OneRtt, frames);
        }

        Enqueue((datagram, ClientAddress, null));
    }

    /// <summary>Makes the next receive throw <paramref name="error" />.</summary>
    public void FailNextReceive(Exception error) => Enqueue((null, ServerEndPoint, error));

    /// <summary>Returns the frames of type <typeparamref name="T" /> the server has taken from the client in 1-RTT packets.</summary>
    public List<T> Sent<T>()
        where T : QuicFrame
    {
        lock (gate)
        {
            return QuicStreamTest.Sent<T>(server);
        }
    }

    /// <summary>
    /// Waits until <paramref name="condition" />, a question about what the server has taken from
    /// the client, holds: checks it now and again each time the client sends a datagram, so it never
    /// polls the clock. Fails when the client sends nothing more for <see cref="HangGuard" />.
    /// </summary>
    public async Task WaitUntilSentAsync(Func<bool> condition)
    {
        while (true)
        {
            Task nextDatagram;
            lock (gate)
            {
                nextDatagram = datagramTaken.Task;
            }

            if (condition())
            {
                return;
            }

            await nextDatagram.WaitAsync(HangGuard);
        }
    }

    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
    {
        IReadOnlyList<byte[]> answers = [];
        TaskCompletionSource taken;
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

            taken = datagramTaken;
            datagramTaken = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        foreach (byte[] answer in answers)
        {
            Enqueue((answer, ServerEndPoint, null));
        }

        taken.SetResult();
        return ValueTask.CompletedTask;
    }

    public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        // A wait that is cancelled takes no count, so the datagram stays queued for the next receive.
        await inboundCount.WaitAsync(cancellationToken);
        inbound.TryDequeue(out (byte[]? Datagram, EndPoint From, Exception? Error) next);
        (byte[]? datagram, EndPoint from, Exception? error) = next;
        if (error is not null)
        {
            throw error;
        }

        datagram!.CopyTo(buffer);
        return new DatagramReceived(datagram!.Length, from);
    }

    // Queues a datagram, or a receive failure, for the client, then counts it.
    private void Enqueue((byte[]? Datagram, EndPoint From, Exception? Error) received)
    {
        inbound.Enqueue(received);
        inboundCount.Release();
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
