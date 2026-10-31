namespace Curl.Extension.EncryptedDns.DnsOverTls;

/// <summary>
/// The DNS-over-TLS server a <see cref="DnsOverTlsResolver" /> asks.
/// </summary>
/// <param name="Host">The server's host name or address, which the connector resolves and dials.</param>
/// <param name="Port">The server's TCP port: 853 by default, the port RFC 7858 section 3.1 assigns.</param>
/// <param name="TlsServerName">
/// The name the server's certificate is checked against (RFC 8310's authentication domain name), or
/// <see langword="null" /> to check it against <paramref name="Host" />.
/// </param>
public sealed record DnsOverTlsServer(string Host, int Port = DnsOverTlsServer.DefaultPort, string? TlsServerName = null)
{
    /// <summary>The port RFC 7858 section 3.1 assigns to DNS over TLS.</summary>
    public const int DefaultPort = 853;

    /// <summary>Gets the name the server's certificate is checked against.</summary>
    public string AuthenticationName => TlsServerName ?? Host;
}
