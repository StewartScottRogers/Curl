using System.Net;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Dials every TCP connection the production <see cref="TcpConnector" /> opens into an upstream case's
/// in-memory server, so the case runs the connector's CONNECT tunnel, HAProxy line and name checks
/// without a socket (BL-1794).
/// </summary>
/// <param name="server">The case's in-memory server; every end point reaches it.</param>
internal sealed class InMemoryServerTcpDialer(IConnector server) : ITcpDialer
{
    public async ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
    {
        ConnectResult connected = await server
            .ConnectAsync(new ConnectTarget(endPoint.Address.ToString(), endPoint.Port, false), cancellationToken);

        IConnection connection = connected.Connection
            ?? throw new IOException($"The in-memory server refused the connection: {connected.ErrorMessage}");

        return new DialedTcpConnection(connection, connection.LocalEndPoint as IPEndPoint ?? new IPEndPoint(IPAddress.Loopback, 0));
    }

    public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
        DialAsync(endPoint, cancellationToken);

    public async ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken)
    {
        ConnectResult connected = await server
            .ConnectAsync(new ConnectTarget(address.Path, 1, false), cancellationToken);

        return connected.Connection
            ?? throw new IOException($"The in-memory server refused the connection: {connected.ErrorMessage}");
    }
}
