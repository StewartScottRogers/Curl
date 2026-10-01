using System.Net;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The production <see cref="IUdpChannelOpener" />: opens a <see cref="UdpDatagramChannel" />
/// bound to the local address and port it is constructed with, or to any address on an ephemeral
/// port when it is not given one, and, through <see cref="OpenFrom" />, to the first free port of
/// a <c>--local-port</c> range.
/// </summary>
/// <param name="localAddress">The local address <see cref="Open" /> binds, or <see langword="null" /> for any address of the server's family.</param>
/// <param name="localPort">The local port <see cref="Open" /> binds, or <c>0</c> for an ephemeral one.</param>
public sealed class UdpChannelOpener(IPAddress? localAddress = null, int localPort = 0) : IUdpChannelOpener
{
    /// <inheritdoc />
    public IDatagramChannel Open(IPEndPoint serverEndPoint) => new UdpDatagramChannel(serverEndPoint, localAddress, localPort);

    /// <inheritdoc />
    public IDatagramChannel OpenFrom(IPEndPoint serverEndPoint, IPEndPoint localEndPoint, int localPortCount)
    {
        ArgumentNullException.ThrowIfNull(localEndPoint);

        return new UdpDatagramChannel(
            serverEndPoint,
            (socket, firstLocalEndPoint) => TcpDialer.BindLocalEnd(socket, (IPEndPoint)firstLocalEndPoint, localPortCount),
            localEndPoint.Address,
            localEndPoint.Port);
    }
}
