using Curl.Networking;

namespace Curl.Console;

/// <summary>
/// The production transports one run builds, and the pieces they were built from, kept
/// so the composition can be checked without opening a socket.
/// </summary>
/// <param name="DnsResolver">The one resolver both connectors share.</param>
/// <param name="TimeProvider">The clock both connectors share.</param>
/// <param name="TcpDialer">Opens the plaintext TCP connections <see cref="TcpConnector" /> dials.</param>
/// <param name="TlsClientOptions">The settings <see cref="TlsProvider" /> applies to every handshake.</param>
/// <param name="TlsProvider">Upgrades a TCP connection to TLS with <see cref="System.Net.Security.SslStream" />.</param>
/// <param name="TcpConnector">Connects the TCP protocols, with TLS when the target asks for it.</param>
/// <param name="UdpDatagramConnector">Opens the UDP channels the datagram protocols use.</param>
internal sealed record CurlTransports(
    SystemDnsResolver DnsResolver,
    TimeProvider TimeProvider,
    TcpDialer TcpDialer,
    TlsClientOptions TlsClientOptions,
    SslStreamTlsProvider TlsProvider,
    TcpConnector TcpConnector,
    UdpDatagramConnector UdpDatagramConnector);
