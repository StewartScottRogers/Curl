using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// The production transports one run builds, and the pieces they were built from, kept
/// so the composition can be checked without opening a socket.
/// </summary>
/// <param name="DnsResolver">
/// The one resolver both connectors share: <see cref="DohDnsResolver" /> (or
/// <see cref="UnusableDohUrlResolver" />) when <c>--doh-url</c> is given (BL-642), otherwise
/// <see cref="DnsServerResolver" /> when a c-ares option is given (BL-694), otherwise <see cref="SystemDnsResolver" />.
/// </param>
/// <param name="TimeProvider">The clock both connectors share.</param>
/// <param name="TcpDialer">Opens the plaintext TCP connections <see cref="TcpConnector" /> dials.</param>
/// <param name="TlsClientOptions">The settings <see cref="TlsProvider" /> applies to every handshake.</param>
/// <param name="TlsProvider">
/// Upgrades a TCP connection to TLS with the client <see cref="TlsClientRouting" /> chooses for
/// <see cref="TlsClientOptions" />: <see cref="SslStreamTlsProvider" /> or <see cref="HandBuiltTlsProvider" />.
/// </param>
/// <param name="ProxyTlsClientOptions">The settings <see cref="ProxyTlsProvider" /> applies to the handshake to an HTTPS proxy.</param>
/// <param name="ProxyTlsProvider">Runs the handshake to an HTTPS proxy with the client <see cref="TlsClientRouting" /> chooses for <see cref="ProxyTlsClientOptions" />.</param>
/// <param name="ProxyTunnelOptions">
/// The <c>User-Agent</c> and credential encoding of the CONNECT request <see cref="TcpConnector" />
/// sends to tunnel through an HTTP proxy.
/// </param>
/// <param name="QuicDialer">
/// Opens the QUIC connections <see cref="TcpConnector" /> hands it for <c>--http3</c> and
/// <c>--http3-only</c>, judging the server's certificate with <see cref="TlsClientOptions" /> (ADR-0144).
/// </param>
/// <param name="TcpConnector">
/// Connects the TCP protocols, with TLS when the target asks for it, and resolves each QUIC
/// connection's host for <see cref="QuicDialer" />.
/// </param>
/// <param name="UdpDatagramConnector">Opens the UDP channels the datagram protocols use.</param>
/// <param name="PoolingConnector">
/// The option group's connection pool over <see cref="TcpConnector" />: every TCP handler connects
/// through it, so a later URL to the same pool key reuses an earlier URL's connection, and the
/// group's dispatch disposes it when the group ends, which closes a pool of its own and leaves the
/// run's shared <see cref="ConnectionCache" /> to the runner (ADR-0050, ADR-0285).
/// </param>
/// <param name="DiagnosticLog">The run's diagnostic log the authenticators were composed with (BL-923); <see langword="null" /> for none.</param>
/// <param name="TracesFtp">Whether the FTP handler writes the <c>--trace-config ftp</c> lines (<see cref="CurlComposition.TracesFtp" />, BL-1162).</param>
/// <param name="TracesSmtp">Whether the SMTP handler writes the <c>--trace-config smtp</c> lines (<see cref="CurlComposition.TracesSmtp" />, BL-1163).</param>
internal sealed record CurlTransports(
    IDnsResolver DnsResolver,
    TimeProvider TimeProvider,
    TcpDialer TcpDialer,
    TlsClientOptions TlsClientOptions,
    ITlsProviderWithWarnings TlsProvider,
    TlsClientOptions ProxyTlsClientOptions,
    ITlsProviderWithWarnings ProxyTlsProvider,
    HttpProxyTunnelOptions ProxyTunnelOptions,
    QuicDialer QuicDialer,
    TcpConnector TcpConnector,
    UdpDatagramConnector UdpDatagramConnector,
    PoolingConnector PoolingConnector,
    IDiagnosticLog? DiagnosticLog = null,
    bool TracesFtp = false,
    bool TracesSmtp = false);
