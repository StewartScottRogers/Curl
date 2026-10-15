using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// An <see cref="IConnector"/> on which <see cref="TelnetPort"/>, upstream's <c>%TELNETPORT</c>, reaches a
/// stand-in for upstream's <c>tests/negtelnetserver.py</c> (at <c>curl-8_22_0</c>), and which hands every
/// other connection to the server it wraps. Each connection is a <see cref="TelnetServerConnection"/>.
/// No socket is opened.
/// </summary>
/// <remarks>
/// negtelnetserver.py writes nothing to <c>server.input</c>, so no byte the client writes joins
/// <c>&lt;verify&gt;&lt;protocol&gt;</c>.
/// </remarks>
/// <param name="backend">The server every connection not to <see cref="TelnetPort"/> reaches.</param>
public sealed class TelnetServerConnector(IConnector backend) : IConnector
{
    /// <summary>The port of the TELNET server, <c>%TELNETPORT</c>: servers.pm picks a free port, and this one no other stand-in uses.</summary>
    public const int TelnetPort = 9016;

    /// <summary>Gets the <c>&lt;server&gt;</c> names whose cases reach this stand-in: <c>telnet</c>.</summary>
    public static IReadOnlyList<string> EmulatedServers { get; } = ["telnet"];

    /// <summary>
    /// Opens an in-memory connection to the TELNET stand-in, its remote end point the loopback address at
    /// <see cref="TelnetPort"/>, when <paramref name="target"/>'s port is <see cref="TelnetPort"/>; otherwise a
    /// connection to the wrapped server.
    /// </summary>
    /// <param name="target">The host and port to connect to.</param>
    /// <param name="cancellationToken">Passed to the wrapped server.</param>
    /// <returns>The connection's result.</returns>
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        target.Port == TelnetPort
            ? ValueTask.FromResult(ConnectResult.Connected(new TelnetServerConnection
            {
                RemoteEndPoint = new IPEndPoint(LoopbackAddress.For(target.Host), target.Port),
            }))
            : backend.ConnectAsync(target, cancellationToken);
}
