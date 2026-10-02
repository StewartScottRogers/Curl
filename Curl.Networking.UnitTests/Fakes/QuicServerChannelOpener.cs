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

    /// <summary>
    /// Gets whether each channel also opens a real, unconnected UDP socket through
    /// <see cref="UdpChannelOpener" /> and reports that socket's local endpoint instead of
    /// <see cref="LocalEndPoint" />; the datagrams still go to the in-memory server, so nothing is sent.
    /// </summary>
    public bool ReportsARealUdpSocketsLocalEndPoint { get; init; }

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
        var realSocket = ReportsARealUdpSocketsLocalEndPoint ? new UdpChannelOpener().Open(serverEndPoint) : null;
        var channel = new Channel(this, serverEndPoint, ServerFor(serverEndPoint), realSocket);
        Opened.Add(channel);
        return channel;
    }

    /// <summary>Gets the local endpoint and port count each <see cref="OpenFrom" /> was asked for, in order.</summary>
    internal List<(IPEndPoint LocalEndPoint, int LocalPortCount)> BoundFrom { get; } = [];

    /// <inheritdoc />
    /// <remarks>Writes the <c>Local port: N</c> line <see cref="UdpChannelOpener" /> writes for the first port.</remarks>
    public IDatagramChannel OpenFrom(IPEndPoint serverEndPoint, IPEndPoint localEndPoint, int localPortCount, ITransferEvents events)
    {
        BoundFrom.Add((localEndPoint, localPortCount));
        var channel = Open(serverEndPoint);
        events.ReportInfo(LocalBindLines.LocalPort(localEndPoint.Port));
        return channel;
    }

    /// <summary>Gets whether each device bind <see cref="OpenFromDeviceAsync" /> asks for succeeds, as on Linux.</summary>
    public bool DeviceBinds { get; init; }

    /// <summary>Gets the device name and <c>ifhost!</c> choice each <see cref="OpenFromDeviceAsync" /> was asked for, in order.</summary>
    internal List<(string DeviceName, bool BindsAddressAfterDevice)> DeviceBoundTo { get; } = [];

    /// <inheritdoc />
    /// <remarks>Decides as <see cref="UdpChannelOpener" /> does, through <see cref="TcpDialer.BindDeviceOrLocalEndAsync" />, recording the local end it binds in <see cref="BoundFrom" />.</remarks>
    public async ValueTask<IDatagramChannel> OpenFromDeviceAsync(
        IPEndPoint serverEndPoint,
        string deviceName,
        bool bindsAddressAfterDevice,
        Func<CancellationToken, ValueTask<IPEndPoint>> chooseLocalEndAsync,
        int localPortCount,
        ITransferEvents events,
        CancellationToken cancellationToken)
    {
        DeviceBoundTo.Add((deviceName, bindsAddressAfterDevice));
        await TcpDialer.BindDeviceOrLocalEndAsync(
            deviceName,
            bindsAddressAfterDevice,
            _ => DeviceBinds,
            chooseLocalEndAsync,
            localEndPoint =>
            {
                BoundFrom.Add((localEndPoint, localPortCount));
                events.ReportInfo(LocalBindLines.LocalPort(localEndPoint.Port));
            },
            events,
            cancellationToken);
        return Open(serverEndPoint);
    }

    /// <summary>One channel: each datagram sent goes to the server at once, and its answers queue up to be received.</summary>
    internal sealed class Channel(QuicServerChannelOpener opener, IPEndPoint serverEndPoint, QuicTestServer? server, IDatagramChannel? realSocket) : IDatagramChannel
    {
        private readonly Queue<byte[]> _inbound = new();

        public EndPoint ServerEndPoint => serverEndPoint;

        public EndPoint? LocalEndPoint => realSocket is null ? opener.LocalEndPoint : realSocket.LocalEndPoint;

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

        public async ValueTask DisposeAsync()
        {
            IsDisposed = true;
            if (realSocket is not null)
            {
                await realSocket.DisposeAsync();
            }
        }
    }
}
