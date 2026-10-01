using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The production <see cref="IUdpChannelOpener" />: opens a <see cref="UdpDatagramChannel" />
/// bound to the local address and port it is constructed with, or to any address on an ephemeral
/// port when it is not given one, through <see cref="OpenFrom" />, to the first free port of
/// a <c>--local-port</c> range, and through <see cref="OpenFromDeviceAsync" />, to an
/// <c>--interface</c> device first.
/// </summary>
public sealed class UdpChannelOpener : IUdpChannelOpener
{
    private readonly IPAddress? _localAddress;
    private readonly int _localPort;
    private readonly Func<Socket, string, bool> _tryBindToDevice;

    /// <summary>Creates the opener.</summary>
    /// <param name="localAddress">The local address <see cref="Open" /> binds, or <see langword="null" /> for any address of the server's family.</param>
    /// <param name="localPort">The local port <see cref="Open" /> binds, or <c>0</c> for an ephemeral one.</param>
    public UdpChannelOpener(IPAddress? localAddress = null, int localPort = 0)
        : this(TcpDialer.TryBindToDevice, localAddress, localPort)
    {
    }

    /// <summary>Creates the opener with the device bind it uses, so a test can make it succeed off Linux.</summary>
    /// <param name="tryBindToDevice">Binds a socket to the named device, answering whether it did.</param>
    /// <param name="localAddress">The local address <see cref="Open" /> binds, or <see langword="null" /> for any.</param>
    /// <param name="localPort">The local port <see cref="Open" /> binds, or <c>0</c> for an ephemeral one.</param>
    internal UdpChannelOpener(Func<Socket, string, bool> tryBindToDevice, IPAddress? localAddress = null, int localPort = 0)
    {
        _tryBindToDevice = tryBindToDevice;
        _localAddress = localAddress;
        _localPort = localPort;
    }

    /// <inheritdoc />
    public IDatagramChannel Open(IPEndPoint serverEndPoint) => new UdpDatagramChannel(serverEndPoint, _localAddress, _localPort);

    /// <inheritdoc />
    public IDatagramChannel OpenFrom(IPEndPoint serverEndPoint, IPEndPoint localEndPoint, int localPortCount)
    {
        ArgumentNullException.ThrowIfNull(localEndPoint);

        return new UdpDatagramChannel(
            serverEndPoint,
            (socket, firstLocalEndPoint) => TcpDialer.BindLocalEnd(socket, (IPEndPoint)firstLocalEndPoint, localPortCount, NoTransferEvents.Instance),
            localEndPoint.Address,
            localEndPoint.Port);
    }

    /// <inheritdoc />
    /// <remarks>
    /// A socket bound to its device alone is then bound to any address on an ephemeral port, as the
    /// kernel binds an unbound UDP socket on its first send, so it can receive before it sends. The
    /// <c>-v</c> bind lines are not written, as <see cref="OpenFrom" /> writes none: QUIC's were not measured.
    /// </remarks>
    public async ValueTask<IDatagramChannel> OpenFromDeviceAsync(
        IPEndPoint serverEndPoint,
        string deviceName,
        bool bindsAddressAfterDevice,
        Func<CancellationToken, ValueTask<IPEndPoint>> chooseLocalEndAsync,
        int localPortCount,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(serverEndPoint);
        ArgumentNullException.ThrowIfNull(deviceName);
        ArgumentNullException.ThrowIfNull(chooseLocalEndAsync);

        var socket = new Socket(serverEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        try
        {
            await TcpDialer.BindDeviceOrLocalEndAsync(
                deviceName,
                bindsAddressAfterDevice,
                name => _tryBindToDevice(socket, name),
                chooseLocalEndAsync,
                localEndPoint => TcpDialer.BindLocalEnd(socket, localEndPoint, localPortCount, NoTransferEvents.Instance),
                NoTransferEvents.Instance,
                cancellationToken).ConfigureAwait(false);

            if (!socket.IsBound)
            {
                socket.Bind(new IPEndPoint(UdpDatagramChannel.AnyAddressOf(serverEndPoint.AddressFamily), 0));
            }
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        return new UdpDatagramChannel(serverEndPoint, socket);
    }
}
