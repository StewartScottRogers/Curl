using System.Net;
using System.Security.Cryptography.X509Certificates;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// An <see cref="IConnector"/> that stands in for upstream's FTPS server, <c>tests/ftpserver.pl</c>
/// behind <c>stunnel</c>: a connection to <see cref="FtpsPort"/> (<c>%FTPSPORT</c>) speaks TLS with
/// the certificate it is given at once (implicit FTPS), and the decrypted control channel reaches
/// the plain FTP server on <see cref="FtpServerConnector.FtpPort"/> of the connector it wraps, which
/// records it for <c>&lt;verify&gt;&lt;protocol&gt;</c>. Every other connection, data connections
/// included, reaches the wrapped connector as it is. No socket is opened.
/// </summary>
/// <remarks>
/// stunnel wraps the control port only, so data connections stay plain: ftpserver.pl answers
/// <c>PBSZ</c> and <c>PROT</c> with 500, and curl at <c>--ftp-ssl-control</c> carries on with clear
/// data. ftpserver.pl offers no <c>AUTH TLS</c>, so no upgrade of a plain control connection
/// (explicit FTPS) is emulated (ADR-0462).
/// </remarks>
/// <param name="certificate">The server certificate, with its private key; see <see cref="HttpsServerConnector.LoadCertificate"/>.</param>
/// <param name="backend">The connector whose plain FTP server the TLS connections reach, and which every other connection reaches.</param>
public sealed class FtpsServerConnector(X509Certificate2 certificate, IConnector backend) : IConnector
{
    /// <summary>The port of the FTPS server, <c>%FTPSPORT</c>.</summary>
    public const int FtpsPort = 9007;

    private readonly TlsServerOptions options = new(certificate, [], false);

    /// <summary>Gets the <c>&lt;server&gt;</c> names whose cases reach this stand-in: <c>ftps</c>.</summary>
    public static IReadOnlyList<string> EmulatedServers { get; } = ["ftps"];

    /// <summary>Opens a TLS connection to the plain FTP server when <paramref name="target"/>'s port is <see cref="FtpsPort"/>; any other on the wrapped connector.</summary>
    /// <param name="target">The host and port to connect to.</param>
    /// <param name="cancellationToken">Passed to the server connected to.</param>
    /// <returns>The connected result.</returns>
    public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.Port != FtpsPort)
        {
            return await backend.ConnectAsync(target, cancellationToken).ConfigureAwait(false);
        }

        ConnectResult connected = await backend.ConnectAsync(new ConnectTarget(target.Host, FtpServerConnector.FtpPort, target.UseTls), cancellationToken).ConfigureAwait(false);
        return ConnectResult.Connected(new TlsRelayConnection(connected.Connection!, options, new IPEndPoint(IPAddress.Loopback, target.Port)));
    }
}
