using System.Diagnostics;
using System.Net;

using Curl.Protocol.Abstractions;

namespace Curl.Networking.Fakes;

/// <summary>
/// An <see cref="IUdpChannelOpener" /> whose channels are wired to in-memory QUIC servers:
/// <see cref="ServerFor" /> gives the server behind each endpoint (<see langword="null" /> for a
/// silent peer), <see cref="OpenOutcome" /> may throw instead of opening, and
/// <see cref="ReceiveFailure" /> makes every receive throw, as an ICMP port unreachable does.
/// </summary>
public sealed class QuicServerChannelOpener : IUdpChannelOpener
{
    /// <summary>Gets the local endpoint each channel reports, or <see langword="null" /> for none.</summary>
    public EndPoint? LocalEndPoint { get; init; } = new IPEndPoint(IPAddress.Loopback, 50123);

    /// <summary>Gets the server behind each endpoint; <see langword="null" /> for a peer that never answers.</summary>
    internal Func<IPEndPoint, QuicTestServer?> ServerFor { get; init; } = _ => null;

    /// <summary>Gets what runs before each channel opens, and may throw instead.</summary>
    public Action<IPEndPoint> OpenOutcome { get; init; } = _ => { };

    /// <summary>Gets an exception every receive throws, or <see langword="null" /> to receive normally.</summary>
    public Exception? ReceiveFailure { get; init; }

    /// <summary>Gets every channel opened, in order.</summary>
    internal List<Channel> Opened { get; } = [];

    /// <summary>Gets a semaphore released each time a channel starts waiting for a datagram, so a test moves the clock only once the client is idle.</summary>
    public SemaphoreSlim WaitingToReceive { get; } = new(0);

    /// <inheritdoc />
    public IDatagramChannel Open(IPEndPoint serverEndPoint)
    {
        OpenOutcome(serverEndPoint);
        var channel = new Channel(this, serverEndPoint, ServerFor(serverEndPoint));
        Opened.Add(channel);
        return channel;
    }

    /// <summary>Gets the local endpoint and port count each <see cref="OpenFrom" /> was asked for, in order.</summary>
    internal List<(IPEndPoint LocalEndPoint, int LocalPortCount)> BoundFrom { get; } = [];

    /// <inheritdoc />
    public IDatagramChannel OpenFrom(IPEndPoint serverEndPoint, IPEndPoint localEndPoint, int localPortCount)
    {
        BoundFrom.Add((localEndPoint, localPortCount));
        return Open(serverEndPoint);
    }

    /// <summary>One channel: each datagram sent goes to the server at once, and its answers queue up to be received.</summary>
    internal sealed class Channel(QuicServerChannelOpener opener, IPEndPoint serverEndPoint, QuicTestServer? server) : IDatagramChannel
    {
        private readonly Queue<byte[]> _inbound = new();

        public EndPoint ServerEndPoint => serverEndPoint;

        public EndPoint? LocalEndPoint => opener.LocalEndPoint;

        public QuicTestServer? Server => server;

        public bool IsDisposed { get; private set; }

        public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
        {
            lock (_inbound)
            {
                foreach (var answer in server?.Receive(datagram.ToArray()) ?? [])
                {
                    _inbound.Enqueue(answer);
                }
            }

            return ValueTask.CompletedTask;
        }

        public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (opener.ReceiveFailure is { } failure)
            {
                throw failure;
            }

            lock (_inbound)
            {
                if (_inbound.TryDequeue(out var next))
                {
                    next.CopyTo(buffer);
                    return new DatagramReceived(next.Length, serverEndPoint);
                }
            }

            opener.WaitingToReceive.Release();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new UnreachableException();
        }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
