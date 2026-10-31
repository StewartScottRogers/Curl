namespace Curl.Extension.EncryptedDns.DnsOverQuic;

/// <summary>
/// The DNS-over-QUIC server a <see cref="DnsOverQuicResolver" /> asks.
/// </summary>
/// <param name="Host">The server's host name or address, which the datagram connector resolves and dials.</param>
/// <param name="Port">The server's UDP port: 853 by default, the port RFC 9250 section 4.1.1 assigns.</param>
/// <param name="TlsServerName">
/// The name sent in the TLS server name indication and the certificate is checked against, or
/// <see langword="null" /> to use <paramref name="Host" />.
/// </param>
public sealed record DnsOverQuicServer(string Host, int Port = DnsOverQuicServer.DefaultPort, string? TlsServerName = null)
{
    /// <summary>The port RFC 9250 section 4.1.1 assigns to DNS over QUIC.</summary>
    public const int DefaultPort = 853;

    /// <summary>Gets the name the server's certificate is checked against.</summary>
    public string AuthenticationName => TlsServerName ?? Host;
}
