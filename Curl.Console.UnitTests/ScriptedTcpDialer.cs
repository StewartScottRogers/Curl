using System.Net;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// A dialer whose every connection is one of <paramref name="server" />'s scripted
/// connections, so a real <see cref="TcpConnector" /> can run over scripted bytes: each
/// dialed address is recorded in <see cref="ScriptedConnector.Targets" /> and every byte
/// written, the CONNECT request included, in <see cref="ScriptedConnector.Written" />.
/// </summary>
internal sealed class ScriptedTcpDialer(ScriptedConnector server) : ITcpDialer
{
    public async ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
    {
        ConnectResult connected = await server
            .ConnectAsync(new ConnectTarget(endPoint.Address.ToString(), endPoint.Port, false), cancellationToken);

        IConnection connection = connected.Connection
            ?? throw new InvalidOperationException($"The scripted connect failed: {connected.ErrorMessage}");

        return new DialedTcpConnection(connection, new IPEndPoint(IPAddress.Loopback, 50000));
    }
}
