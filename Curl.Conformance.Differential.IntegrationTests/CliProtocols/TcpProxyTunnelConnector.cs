using System.Net.Sockets;
using Curl.Conformance.Differential.Protocols.Proxy;

namespace Curl.Conformance.Differential.IntegrationTests.CliProtocols;

/// <summary>
/// Opens a proxy tunnel's outbound connection over TCP, as proxy_server.py's <c>socket.create_connection</c>
/// did; a target that refuses or cannot be resolved gives <see langword="null"/>.
/// </summary>
internal sealed class TcpProxyTunnelConnector : IProxyTunnelConnector
{
    /// <inheritdoc />
    public async Task<Stream?> ConnectAsync(string host, int port, CancellationToken cancellationToken)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync(host, port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch (SocketException)
        {
            socket.Dispose();
            return null;
        }
    }
}
