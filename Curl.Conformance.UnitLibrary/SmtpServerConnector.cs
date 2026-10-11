using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// An <see cref="IConnector"/> on which <see cref="SmtpPort"/>, upstream's <c>%SMTPPORT</c>, reaches
/// the SMTP side of upstream's <c>tests/ftpserver.pl</c> (at <c>curl-8_21_0</c>) for one case, and
/// which hands every other connection to the server it wraps. Each connection to
/// <see cref="SmtpPort"/> gets its own <see cref="SmtpResponder"/> answering from the case's
/// <c>&lt;servercmd&gt;</c> and <c>&lt;reply&gt;</c> parts, each with the line endings its
/// <c>crlf</c> attribute forces, as <c>prepro</c> forces them before ftpserver.pl reads the part.
/// No socket is opened.
/// </summary>
/// <param name="testCase">The expanded case whose <c>&lt;servercmd&gt;</c> and <c>&lt;reply&gt;</c> parts the server answers from.</param>
/// <param name="backend">The server every connection not to <see cref="SmtpPort"/> reaches.</param>
public sealed class SmtpServerConnector(UpstreamTestCase testCase, IConnector backend) : IConnector
{
    /// <summary>The port of the SMTP server, <c>%SMTPPORT</c>.</summary>
    public const int SmtpPort = 8995;

    private readonly List<SmtpResponder> responders = [];

    private LineProtocolServerConnector? server;

    /// <summary>
    /// Gets every command line the server has answered, across every connection, each with its CRLF:
    /// what ftpserver.pl logs for <c>&lt;verify&gt;&lt;protocol&gt;</c>, without a <c>DATA</c> message's lines.
    /// </summary>
    public ReadOnlyMemory<byte> ProtocolLog =>
        Encoding.Latin1.GetBytes(string.Concat(responders.SelectMany(responder => responder.ReceivedCommandLines)));

    /// <summary>
    /// Gets the last <c>DATA</c> message any connection received, as ftpserver.pl stores it for
    /// <c>&lt;verify&gt;&lt;upload&gt;</c>, or nothing when none was sent.
    /// </summary>
    public ReadOnlyMemory<byte> UploadedMessage =>
        responders.Select(responder => responder.UploadedMessage).LastOrDefault(message => message.Length > 0);

    /// <summary>Opens an SMTP connection when <paramref name="target"/>'s port is <see cref="SmtpPort"/>, otherwise a connection to the wrapped server.</summary>
    /// <param name="target">The host and port to connect to.</param>
    /// <param name="cancellationToken">Passed to the server the connection reaches.</param>
    /// <returns>The connected result.</returns>
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        target.Port == SmtpPort
            ? Server().ConnectAsync(target, cancellationToken)
            : backend.ConnectAsync(target, cancellationToken);

    private LineProtocolServerConnector Server() => server ??= new(CreateResponder);

    private SmtpResponder CreateResponder()
    {
        SmtpResponder responder = new(
            LineProtocolServerCommands.Read((testCase.Find("reply", "servercmd")?.Content ?? ReadOnlyMemory<byte>.Empty).Span),
            testCase.Sections.Where(part => part.Section == "reply").ToDictionary(part => part.Name, UpstreamTestPartBodies.Served, StringComparer.Ordinal));
        responders.Add(responder);
        return responder;
    }
}
