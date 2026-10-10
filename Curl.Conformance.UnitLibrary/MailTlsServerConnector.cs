using System.Net;
using System.Security.Cryptography.X509Certificates;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// An <see cref="IConnector"/> that stands in for upstream's implicit-TLS mail servers, the SMTP,
/// IMAP and POP3 sides of <c>tests/ftpserver.pl</c> behind <c>stunnel</c>: a connection to
/// <see cref="SmtpsPort"/> (<c>%SMTPSPORT</c>), <see cref="ImapsPort"/> (<c>%IMAPSPORT</c>) or
/// <see cref="Pop3sPort"/> (<c>%POP3SPORT</c>) speaks TLS with the certificate it is given, and
/// the decrypted bytes reach the plain server on <see cref="SmtpServerConnector.SmtpPort"/>,
/// <see cref="ImapServerConnector.ImapPort"/> or <see cref="Pop3ServerConnector.Pop3Port"/> of the
/// connector it wraps, which records them for <c>&lt;verify&gt;&lt;protocol&gt;</c>. Every other
/// connection reaches the wrapped connector as it is. No socket is opened.
/// </summary>
/// <remarks>
/// Like stunnel as upstream's <c>servers.pm</c> starts it, the server offers no ALPN protocol and
/// asks for no client certificate. ftpserver.pl offers no <c>STARTTLS</c>, so no upgrade of a
/// plain connection is emulated (ADR-0459).
/// </remarks>
/// <param name="certificate">The server certificate, with its private key; see <see cref="HttpsServerConnector.LoadCertificate"/>.</param>
/// <param name="backend">The connector whose plain mail servers the TLS connections reach, and which every other connection reaches.</param>
public sealed class MailTlsServerConnector(X509Certificate2 certificate, IConnector backend) : IConnector
{
    /// <summary>The port of the SMTPS server, <c>%SMTPSPORT</c>.</summary>
    public const int SmtpsPort = 9000;

    /// <summary>The port of the IMAPS server, <c>%IMAPSPORT</c>.</summary>
    public const int ImapsPort = 9001;

    /// <summary>The port of the POP3S server, <c>%POP3SPORT</c>.</summary>
    public const int Pop3sPort = 9002;

    private static readonly Dictionary<int, int> PlainPorts = new()
    {
        [SmtpsPort] = SmtpServerConnector.SmtpPort,
        [ImapsPort] = ImapServerConnector.ImapPort,
        [Pop3sPort] = Pop3ServerConnector.Pop3Port,
    };

    private readonly TlsServerOptions options = new(certificate, [], false);

    /// <summary>Gets the <c>&lt;server&gt;</c> names whose cases reach this stand-in: <c>smtps</c>, <c>imaps</c> and <c>pop3s</c>.</summary>
    public static IReadOnlyList<string> EmulatedServers { get; } = ["smtps", "imaps", "pop3s"];

    /// <summary>Opens a TLS connection to the plain mail server behind an implicit-TLS port; any other on the wrapped connector.</summary>
    /// <param name="target">The host and port to connect to.</param>
    /// <param name="cancellationToken">Passed to the server connected to.</param>
    /// <returns>The connected result.</returns>
    public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!PlainPorts.TryGetValue(target.Port, out int plainPort))
        {
            return await backend.ConnectAsync(target, cancellationToken).ConfigureAwait(false);
        }

        ConnectResult connected = await backend.ConnectAsync(new ConnectTarget(target.Host, plainPort, target.UseTls), cancellationToken).ConfigureAwait(false);
        return ConnectResult.Connected(new TlsRelayConnection(connected.Connection!, options, new IPEndPoint(IPAddress.Loopback, target.Port)));
    }
}
