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

    /// <summary>Gets each local end point and port count a bound dial asked for (<c>--interface</c>, <c>--local-port</c>), in order.</summary>
    public List<(IPEndPoint LocalEndPoint, int LocalPortCount)> LocalBinds { get; } = [];

    /// <summary>Records the local end asked for, then connects as <see cref="DialAsync" /> does.</summary>
    public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken)
    {
        LocalBinds.Add((localEndPoint, localPortCount));
        return DialAsync(endPoint, cancellationToken);
    }

    /// <summary>
    /// Connects as <see cref="DialAsync" /> does, recording the socket as a target whose host is
    /// <c>unix:</c> (or <c>unix-abstract:</c>) and the path, with port 1, since a target needs one.
    /// </summary>
    public async ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken)
    {
        string scheme = address.IsAbstract ? "unix-abstract" : "unix";
        ConnectResult connected = await server
            .ConnectAsync(new ConnectTarget($"{scheme}:{address.Path}", 1, false), cancellationToken);

        return connected.Connection
            ?? throw new InvalidOperationException($"The scripted connect failed: {connected.ErrorMessage}");
    }
}
