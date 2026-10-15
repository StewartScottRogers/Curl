using System.Security.Cryptography.X509Certificates;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Http2Server;

/// <summary>
/// Builds the stand-ins for upstream's <c>http/2</c> test server - nghttpx serving h2c, and HTTP/2
/// over TLS, in front of <c>sws</c> (ADR-0491) - so the case runner puts them on <c>%HTTP2PORT</c>
/// (BL-2230) and <c>%HTTP2TLSPORT</c> (BL-2245) in one line each.
/// </summary>
public static class UpstreamHttp2ServerFactory
{
    /// <summary>
    /// Creates an h2c server that relays each HTTP/2 stream to <paramref name="backend"/> as an
    /// HTTP/1.1 request on a new connection.
    /// </summary>
    /// <param name="backend">The HTTP/1.1 server, the case's sws emulation.</param>
    /// <returns>The server, as a connector whose every connection is served in memory.</returns>
    public static IConnector Create(IConnector backend)
    {
        ArgumentNullException.ThrowIfNull(backend);
        return new Http2ServerConnector(backend);
    }

    /// <summary>
    /// Creates an HTTP/2 server behind TLS, presenting <paramref name="certificate"/> and offering
    /// ALPN <c>h2</c>, that relays each stream to <paramref name="backend"/> as an HTTP/1.1 request
    /// with <c>X-Forwarded-Proto: https</c>.
    /// </summary>
    /// <param name="backend">The HTTP/1.1 server, the case's sws emulation.</param>
    /// <param name="certificate">The server certificate, with its private key, such as <c>%CERTDIR/certs/test-localhost.pem</c>.</param>
    /// <returns>The server, as a connector whose every connection is served in memory.</returns>
    public static IConnector CreateTls(IConnector backend, X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(certificate);
        return new Http2TlsServerConnector(backend, certificate);
    }
}
