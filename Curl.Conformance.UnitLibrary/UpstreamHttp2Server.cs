using System.Security.Cryptography.X509Certificates;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// What the case runner needs of the stand-in for upstream's <c>http/2</c> server, nghttpx serving
/// h2c and HTTP/2 over TLS in front of <c>sws</c>, which lives in <c>Curl.Conformance.Http2Server.UnitLibrary</c>
/// because it reuses the HTTP/2 frame codec and HPACK this library does not reference (ADR-0491): the
/// caller builds it from there and hands it to <see cref="UpstreamCaseRunner"/> (BL-2230), as it does
/// <see cref="UpstreamSshServer"/>.
/// </summary>
/// <param name="CreateServer">
/// Makes a fresh h2c server for one case in front of the given backend, the case's sws emulation, to
/// which it relays each HTTP/2 stream as HTTP/1.1; connections to <see cref="Http2Port"/> reach it.
/// </param>
/// <param name="CreateTlsServer">
/// Makes a fresh HTTP/2-over-TLS server for one case in front of the given backend, presenting the
/// given certificate with ALPN <c>h2</c>; connections to <see cref="Http2TlsPort"/> reach it (BL-2245).
/// <see langword="null"/> leaves that port to the other servers.
/// </param>
public sealed record UpstreamHttp2Server(Func<IConnector, IConnector> CreateServer, Func<IConnector, X509Certificate2, IConnector>? CreateTlsServer = null)
{
    /// <summary>The value of <c>%HTTP2PORT</c>.</summary>
    public const int Http2Port = 9009;

    /// <summary>The value of <c>%HTTP2TLSPORT</c>.</summary>
    public const int Http2TlsPort = 9018;

    /// <summary>
    /// Puts a fresh server on <see cref="Http2Port"/>, and, given a certificate and
    /// <see cref="CreateTlsServer"/>, a fresh TLS one on <see cref="Http2TlsPort"/>, each relaying to
    /// <paramref name="backend"/>, in front of the case's other servers.
    /// </summary>
    /// <param name="otherPorts">Where every connection to another port goes.</param>
    /// <param name="backend">The HTTP/1.1 server the stand-in relays to, the case's sws emulation.</param>
    /// <param name="certificate">The certificate the TLS server presents; <see langword="null"/> puts none on <see cref="Http2TlsPort"/>.</param>
    /// <returns>A connector routing by port.</returns>
    public IConnector InFrontOf(IConnector otherPorts, IConnector backend, X509Certificate2? certificate = null)
    {
        IConnector tlsServer = certificate is not null && CreateTlsServer is not null ? CreateTlsServer(backend, certificate) : otherPorts;
        return new Http2PortConnector(CreateServer(backend), tlsServer, otherPorts);
    }

    private sealed class Http2PortConnector(IConnector http2Server, IConnector http2TlsServer, IConnector otherPorts) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            (target.Port switch
            {
                Http2Port => http2Server,
                Http2TlsPort => http2TlsServer,
                _ => otherPorts,
            }).ConnectAsync(target, cancellationToken);
    }
}
