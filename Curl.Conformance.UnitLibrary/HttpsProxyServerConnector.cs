using System.Net;
using System.Security.Cryptography.X509Certificates;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// An <see cref="IConnector"/> that stands in for upstream's <c>https-proxy</c> server, <c>stunnel</c>
/// in front of the proxy-mode <c>sws</c>, on <see cref="HttpsProxyPort"/> (<c>%HTTPSPROXYPORT</c>):
/// a connection to that port speaks TLS with the certificate it is given, and the decrypted bytes
/// reach <see cref="SwsHttpServerConnector.ProxyPort"/> of the connector it wraps, where the sws
/// emulation records them in its <see cref="SwsHttpServerConnector.ProxyReceivedBytes"/> for
/// <c>&lt;verify&gt;&lt;proxy&gt;</c> and serves a proxied request or a <c>CONNECT</c> as the
/// http-proxy does. The proxy connection is kept alive between requests as sws keeps it
/// (<see cref="SwsKeptAliveConnection"/>), so curl reuses it for its next URL (test1586).
/// Every other connection reaches the wrapped connector as it is. No socket is opened.
/// </summary>
/// <remarks>
/// Like stunnel as upstream's <c>servers.pm</c> starts it, the server offers no ALPN protocol and
/// asks for no client certificate.
/// </remarks>
/// <param name="certificate">The server certificate, with its private key; see <see cref="HttpsServerConnector.LoadCertificate"/>.</param>
/// <param name="backend">The connector whose proxy port the TLS connections reach, and which every other connection reaches.</param>
public sealed class HttpsProxyServerConnector(X509Certificate2 certificate, IConnector backend) : IConnector
{
    /// <summary>The port of the HTTPS proxy, <c>%HTTPSPROXYPORT</c>.</summary>
    public const int HttpsProxyPort = 9010;

    /// <summary>The <c>&lt;server&gt;</c> name whose cases reach this stand-in.</summary>
    public const string EmulatedServer = "https-proxy";

    private readonly TlsServerOptions options = new(certificate, [], false);

    /// <summary>Opens a TLS connection to the proxy port behind <see cref="HttpsProxyPort"/>; any other on the wrapped connector.</summary>
    /// <param name="target">The host and port to connect to.</param>
    /// <param name="cancellationToken">Passed to the server connected to.</param>
    /// <returns>The connected result.</returns>
    public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.Port != HttpsProxyPort)
        {
            return await backend.ConnectAsync(target, cancellationToken).ConfigureAwait(false);
        }

        ConnectResult connected = await backend.ConnectAsync(new ConnectTarget(target.Host, SwsHttpServerConnector.ProxyPort, target.UseTls), cancellationToken).ConfigureAwait(false);
        SwsKeptAliveConnection proxy = new((SwsHttpServerConnection)connected.Connection!);
        return ConnectResult.Connected(new TlsRelayConnection(proxy, options, new IPEndPoint(IPAddress.Loopback, HttpsProxyPort)));
    }
}
