using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Conformance.Http3Server;

/// <summary>
/// Builds the stand-in for upstream's <c>http/3</c> test server - nghttpx-h3 serving HTTP/3 over
/// QUIC in front of <c>sws</c> - so the case runner can put it on <c>%HTTP3PORT</c> in one line
/// (BL-2261).
/// </summary>
public static class UpstreamHttp3ServerFactory
{
    /// <summary>
    /// Creates an HTTP/3 server that relays each request stream to <paramref name="backend" /> as an
    /// HTTP/1.1 request on a new connection.
    /// </summary>
    /// <param name="backend">The HTTP/1.1 server, the case's sws emulation.</param>
    /// <param name="tlsSettings">
    /// The TLS handshake's certificate and key, such as <see cref="Http3ServerTlsSettings.FromRsaCertificate" />
    /// with upstream's <c>test-localhost.pem</c>.
    /// </param>
    /// <returns>The server, as a datagram connector whose every channel is served in memory.</returns>
    public static IDatagramConnector Create(IConnector backend, Http3ServerTlsSettings tlsSettings)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(tlsSettings);
        return new Http3ServerConnector(backend, tlsSettings, SystemTlsRandomSource.Instance);
    }
}
