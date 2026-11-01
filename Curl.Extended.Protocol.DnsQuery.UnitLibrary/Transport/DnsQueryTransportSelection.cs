using Curl.Extended.Protocol.DnsQuery.Client;
using Curl.Extended.Protocol.DnsQuery.Transport.Https;
using Curl.Extended.Protocol.DnsQuery.Transport.Quic;
using Curl.Extended.Protocol.DnsQuery.Transport.Tcp;
using Curl.Extended.Protocol.DnsQuery.Transport.Tls;
using Curl.Extended.Protocol.DnsQuery.Transport.Udp;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.DnsQuery.Transport;

/// <summary>
/// The transports <c>--dns-transport</c> names (ADR-1140), and the transport each name gives. Each transport adds its
/// name to <see cref="Names" /> and its case to <see cref="TransportOf" />, and nothing else here.
/// </summary>
internal static class DnsQueryTransportSelection
{
    /// <summary>UDP (RFC 1035 section 4.2.1), retried over TCP when the answer is truncated; the default.</summary>
    public const string Udp = "udp";

    /// <summary>TCP with a two-octet length prefix (RFC 1035 section 4.2.2).</summary>
    public const string Tcp = "tcp";

    /// <summary>DNS over TLS (RFC 7858), port 853 by default.</summary>
    public const string Tls = "tls";

    /// <summary>DNS over QUIC (RFC 9250), port 853 by default.</summary>
    public const string Quic = "quic";

    /// <summary>DNS over HTTPS (RFC 8484), a POST to /dns-query on port 443 by default.</summary>
    public const string Https = "https";

    /// <summary>Gets every transport's name, in the order <c>--dns-transport</c>'s help lists them.</summary>
    public static IReadOnlyList<string> Names { get; } = [Udp, Tcp, Tls, Quic, Https];

    /// <summary>Gives the transport a name names, over curl's connection services.</summary>
    /// <param name="name">The name, in either case.</param>
    /// <param name="services">Curl's connection services.</param>
    /// <returns>The transport.</returns>
    /// <exception cref="DnsQueryException">No transport has that name (exit 2).</exception>
    public static IDnsQueryTransport TransportOf(string name, ProtocolHandlerServices services) =>
        name.ToLowerInvariant() switch
        {
            Udp => new DnsUdpTransport(services.DatagramConnector, new DnsTcpTransport(services.Connector)),
            Tcp => new DnsTcpTransport(services.Connector),
            Tls => new DnsTlsTransport(services.Connector, services.TlsProvider),
            Quic => new DnsQuicTransport(services.DatagramConnector, services.TlsProvider),
            Https => new DnsHttpsTransport(services.Connector, services.TlsProvider),
            _ => throw new DnsQueryException(CurlExitCode.FailedInit, $"--dns-transport {name} names no transport; give one of {string.Join(", ", Names)}"),
        };
}
