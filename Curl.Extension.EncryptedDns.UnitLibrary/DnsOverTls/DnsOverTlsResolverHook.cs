using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Resolution;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.EncryptedDns.DnsOverTls;

/// <summary>
/// What <c>--dns-over-tls</c> supplies in place of curl's resolver (ADR-1064): a <see cref="DnsOverTlsResolver" />
/// that dials the server over TCP, resolving the server's own name with curl's resolver, and checks its
/// certificate against the transfer's <c>--cacert</c>/<c>--capath</c> (or not at all under <c>-k</c>; without the revocation check under <c>--ssl-no-revoke</c>), else the system's trust store, with <see cref="SslStreamTlsProvider" />.
/// </summary>
/// <param name="server">The server the option named.</param>
/// <param name="tlsOptions">The transfer's trust settings (<c>--cacert</c>, <c>--capath</c>, <c>-k</c>, <c>--ssl-no-revoke</c>) the certificate is verified with; <see langword="null" /> for the system store.</param>
internal sealed class DnsOverTlsResolverHook(DnsOverTlsServer server, TlsClientOptions? tlsOptions = null) : IResolverHook
{
    /// <inheritdoc />
    public ExtensionOption Option => EncryptedDnsExtension.DnsOverTlsOption;

    /// <summary>Gets the TLS options the server's certificate is verified with.</summary>
    public TlsClientOptions TlsOptions { get; } = tlsOptions ?? new TlsClientOptions();

    /// <summary>Gets the server the option named.</summary>
    public DnsOverTlsServer Server => server;

    /// <inheritdoc />
    public IDnsResolver ResolverOver(IDnsResolver curlResolver)
    {
        ArgumentNullException.ThrowIfNull(curlResolver);
        var tlsProvider = new SslStreamTlsProvider(TlsOptions, TimeProvider.System);
        return new DnsOverTlsResolver(new TcpConnector(curlResolver, new TcpDialer(), tlsProvider, TimeProvider.System), tlsProvider, server);
    }
}
