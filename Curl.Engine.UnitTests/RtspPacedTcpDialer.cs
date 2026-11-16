using System.Net;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>A dialer whose every connection is one of <paramref name="server" />'s paced connections, so a real <see cref="TcpConnector" /> and pool run over it.</summary>
internal sealed class RtspPacedTcpDialer(RtspPacedConnector server) : ITcpDialer
{
    public async ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
    {
        ConnectResult connected = await server.ConnectAsync(new ConnectTarget(endPoint.Address.ToString(), endPoint.Port, false), cancellationToken);

        return new DialedTcpConnection(connected.Connection!, new IPEndPoint(IPAddress.Loopback, 50000));
    }

    public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
        DialAsync(endPoint, cancellationToken);

    public async ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken)
    {
        ConnectResult connected = await server.ConnectAsync(new ConnectTarget("unix:" + address.Path, 1, false), cancellationToken);

        return connected.Connection!;
    }
}
