using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// An <see cref="IConnector"/> on which <see cref="Pop3Port"/>, upstream's <c>%POP3PORT</c>, reaches
/// the POP3 side of upstream's <c>tests/ftpserver.pl</c> (at <c>curl-8_21_0</c>) for one case, and
/// which hands every other connection to the server it wraps. Each connection to
/// <see cref="Pop3Port"/> gets its own <see cref="Pop3Responder"/> answering from the case's
/// <c>&lt;servercmd&gt;</c> and <c>&lt;reply&gt;</c> parts, each with the line endings its
/// <c>crlf</c> attribute forces, as <c>prepro</c> forces them before ftpserver.pl reads the part.
/// No socket is opened.
/// </summary>
/// <param name="testCase">The expanded case whose <c>&lt;servercmd&gt;</c> and <c>&lt;reply&gt;</c> parts the server answers from.</param>
/// <param name="backend">The server every connection not to <see cref="Pop3Port"/> reaches.</param>
public sealed class Pop3ServerConnector(UpstreamTestCase testCase, IConnector backend) : IConnector
{
    /// <summary>The port of the POP3 server, <c>%POP3PORT</c>.</summary>
    public const int Pop3Port = 8999;

    private readonly List<Pop3Responder> responders = [];

    private LineProtocolServerConnector? server;

    /// <summary>
    /// Gets every command line the server has answered, across every connection, each with its CRLF:
    /// what ftpserver.pl logs for <c>&lt;verify&gt;&lt;protocol&gt;</c>.
    /// </summary>
    public ReadOnlyMemory<byte> ProtocolLog =>
        Encoding.Latin1.GetBytes(string.Concat(responders.SelectMany(responder => responder.ReceivedCommandLines)));

    /// <summary>Opens a POP3 connection when <paramref name="target"/>'s port is <see cref="Pop3Port"/>, otherwise a connection to the wrapped server.</summary>
    /// <param name="target">The host and port to connect to.</param>
    /// <param name="cancellationToken">Passed to the server the connection reaches.</param>
    /// <returns>The connected result.</returns>
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        target.Port == Pop3Port
            ? Server().ConnectAsync(target, cancellationToken)
            : backend.ConnectAsync(target, cancellationToken);

    private LineProtocolServerConnector Server() => server ??= new(CreateResponder);

    private Pop3Responder CreateResponder()
    {
        Pop3Responder responder = new(
            LineProtocolServerCommands.Read((testCase.Find("reply", "servercmd")?.Content ?? ReadOnlyMemory<byte>.Empty).Span),
            testCase.Sections.Where(part => part.Section == "reply").ToDictionary(part => part.Name, UpstreamTestPartBodies.Served, StringComparer.Ordinal));
        responders.Add(responder);
        return responder;
    }
}
