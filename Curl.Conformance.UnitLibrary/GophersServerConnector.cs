using System.Net;
using System.Security.Cryptography.X509Certificates;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// An <see cref="IConnector"/> that stands in for upstream's Gophers server, <c>sws --gopher</c>
/// behind <c>stunnel</c>: a connection to <see cref="GophersPort"/> (<c>%GOPHERSPORT</c>) speaks TLS
/// with the certificate it is given, and the decrypted selector reaches the plain Gopher server on
/// <see cref="GopherServerConnector.GopherPort"/> of the connector it wraps, which records it for
/// <c>&lt;verify&gt;&lt;protocol&gt;</c>. Every other connection reaches the wrapped connector as it is.
/// No socket is opened.
/// </summary>
/// <param name="certificate">The server certificate, with its private key; see <see cref="HttpsServerConnector.LoadCertificate"/>.</param>
/// <param name="backend">The connector whose plain Gopher server the TLS connections reach, and which every other connection reaches.</param>
public sealed class GophersServerConnector(X509Certificate2 certificate, IConnector backend) : IConnector
{
    /// <summary>The port of the Gophers server, <c>%GOPHERSPORT</c>: servers.pm picks a free port, and this one no other stand-in uses.</summary>
    public const int GophersPort = 9014;

    private readonly TlsServerOptions options = new(certificate, [], false);

    /// <summary>Gets the <c>&lt;server&gt;</c> names whose cases reach this stand-in: <c>gophers</c>.</summary>
    public static IReadOnlyList<string> EmulatedServers { get; } = ["gophers"];

    /// <summary>Opens a TLS connection to the plain Gopher server when <paramref name="target"/>'s port is <see cref="GophersPort"/>; any other on the wrapped connector.</summary>
    /// <param name="target">The host and port to connect to.</param>
    /// <param name="cancellationToken">Passed to the server connected to.</param>
    /// <returns>The connected result.</returns>
    public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.Port != GophersPort)
        {
            return await backend.ConnectAsync(target, cancellationToken).ConfigureAwait(false);
        }

        ConnectResult connected = await backend.ConnectAsync(new ConnectTarget(target.Host, GopherServerConnector.GopherPort, target.UseTls), cancellationToken).ConfigureAwait(false);
        return ConnectResult.Connected(new TlsRelayConnection(connected.Connection!, options, new IPEndPoint(IPAddress.Loopback, target.Port)));
    }
}
