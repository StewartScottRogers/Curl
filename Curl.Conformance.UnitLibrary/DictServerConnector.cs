using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// An <see cref="IConnector"/> on which <see cref="DictPort"/>, upstream's <c>%DICTPORT</c>, reaches a
/// stand-in for upstream's <c>tests/dictserver.py</c> (at <c>curl-8_22_0</c>), and which hands every
/// other connection to the server it wraps. No socket is opened.
/// </summary>
/// <remarks>
/// dictserver.py sends <see cref="Greeting"/> on connect, reads one request, answers it with
/// <see cref="NoMatchesReply"/> and closes. Here the first CRLF-terminated line the client writes is
/// that request; bytes after it are dropped. dictserver.py writes nothing to <c>server.input</c>,
/// so no byte the client writes joins <c>&lt;verify&gt;&lt;protocol&gt;</c>.
/// </remarks>
/// <param name="backend">The server every connection not to <see cref="DictPort"/> reaches.</param>
public sealed class DictServerConnector(IConnector backend) : IConnector
{
    /// <summary>The port of the DICT server, <c>%DICTPORT</c>: servers.pm picks a free port, and this one no other stand-in uses.</summary>
    public const int DictPort = 9015;

    /// <summary>The line dictserver.py sends as soon as a client connects.</summary>
    public const string Greeting = "220 dictserver <xnooptions> <msgid@msgid>\n";

    /// <summary>dictserver.py's answer to every request but its own <c>verifiedserver</c> check.</summary>
    public const string NoMatchesReply = "552 No matches\n";

    private LineProtocolServerConnector? server;

    /// <summary>Gets the <c>&lt;server&gt;</c> names whose cases reach this stand-in: <c>dict</c>.</summary>
    public static IReadOnlyList<string> EmulatedServers { get; } = ["dict"];

    /// <summary>Opens a DICT connection when <paramref name="target"/>'s port is <see cref="DictPort"/>, otherwise a connection to the wrapped server.</summary>
    /// <param name="target">The host and port to connect to.</param>
    /// <param name="cancellationToken">Passed to the server the connection reaches.</param>
    /// <returns>The connected result.</returns>
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        target.Port == DictPort
            ? Server().ConnectAsync(target, cancellationToken)
            : backend.ConnectAsync(target, cancellationToken);

    private LineProtocolServerConnector Server() => server ??= new(() => new DictResponder());

    private sealed class DictResponder : ILineProtocolResponder
    {
        public ReadOnlyMemory<byte> Greeting { get; } = Encoding.Latin1.GetBytes(DictServerConnector.Greeting);

        public LineProtocolReply Answer(string commandLine) => new(Encoding.Latin1.GetBytes(NoMatchesReply), true);
    }
}
