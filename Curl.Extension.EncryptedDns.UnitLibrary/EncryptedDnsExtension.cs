using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Resolution;
using Curl.Extension.EncryptedDns.DnsOverQuic;
using Curl.Extension.EncryptedDns.DnsOverTls;

namespace Curl.Extension.EncryptedDns;

/// <summary>
/// The encrypted DNS extension's options, description and resolver hook activators (ADR-1064):
/// <c>--dns-over-tls &lt;host[:port]&gt;</c> looks every transfer's host names up on a DNS-over-TLS server
/// (RFC 7858) and <c>--dns-over-quic &lt;host[:port]&gt;</c> on a DNS-over-QUIC server (RFC 9250), port 853 by default.
/// </summary>
public static class EncryptedDnsExtension
{
    /// <summary>Gets <c>--dns-over-tls &lt;host[:port]&gt;</c>.</summary>
    public static ExtensionOption DnsOverTlsOption { get; } = new("dns-over-tls", ExtensionOptionArgument.Value, "Look host names up on this DNS-over-TLS server, host[:port] (port 853 by default)");

    /// <summary>Gets <c>--dns-over-quic &lt;host[:port]&gt;</c>.</summary>
    public static ExtensionOption DnsOverQuicOption { get; } = new("dns-over-quic", ExtensionOptionArgument.Value, "Look host names up on this DNS-over-QUIC server, host[:port] (port 853 by default)");

    /// <summary>Gets what <c>curl --extensions</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "encrypted-dns",
        "Looks every transfer's host names up on a DNS-over-TLS (RFC 7858) or DNS-over-QUIC (RFC 9250) server.",
        [],
        [DnsOverTlsOption, DnsOverQuicOption],
        new Uri("https://www.rfc-editor.org/rfc/rfc7858"));

    /// <summary>Gives the DNS-over-TLS resolver hook for <paramref name="values" />.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook, or <see langword="null" /> without <c>--dns-over-tls</c>.</returns>
    /// <exception cref="ArgumentException">The <c>--dns-over-tls</c> value is not <c>host[:port]</c>.</exception>
    public static IResolverHook? ActivateDnsOverTls(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        string? value = values.ValueOf(DnsOverTlsOption);
        if (value is null)
        {
            return null;
        }

        (string host, int port) = EncryptedDnsServerAddress.Parse(value, DnsOverTlsOption, DnsOverTlsServer.DefaultPort);
        return new DnsOverTlsResolverHook(new DnsOverTlsServer(host, port));
    }

    /// <summary>Gives the DNS-over-QUIC resolver hook for <paramref name="values" />.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook, or <see langword="null" /> without <c>--dns-over-quic</c>.</returns>
    /// <exception cref="ArgumentException">The <c>--dns-over-quic</c> value is not <c>host[:port]</c>.</exception>
    public static IResolverHook? ActivateDnsOverQuic(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        string? value = values.ValueOf(DnsOverQuicOption);
        if (value is null)
        {
            return null;
        }

        (string host, int port) = EncryptedDnsServerAddress.Parse(value, DnsOverQuicOption, DnsOverQuicServer.DefaultPort);
        return new DnsOverQuicResolverHook(new DnsOverQuicServer(host, port));
    }
}
