using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Resolution;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.EncryptedDns.DnsOverTls;

/// <summary>
/// What <c>--dns-over-tls</c> supplies in place of curl's resolver (ADR-1064): a <see cref="DnsOverTlsResolver" />
/// that dials the server over TCP, resolving the server's own name with curl's resolver, and checks its
/// certificate against the system's trust store with <see cref="SslStreamTlsProvider" />.
/// </summary>
/// <param name="server">The server the option named.</param>
internal sealed class DnsOverTlsResolverHook(DnsOverTlsServer server) : IResolverHook
{
    /// <inheritdoc />
    public ExtensionOption Option => EncryptedDnsExtension.DnsOverTlsOption;

    /// <summary>Gets the server the option named.</summary>
    public DnsOverTlsServer Server => server;

    /// <inheritdoc />
    public IDnsResolver ResolverOver(IDnsResolver curlResolver)
    {
        ArgumentNullException.ThrowIfNull(curlResolver);
        var tlsProvider = new SslStreamTlsProvider(new TlsClientOptions(), TimeProvider.System);
        return new DnsOverTlsResolver(new TcpConnector(curlResolver, new TcpDialer(), tlsProvider, TimeProvider.System), tlsProvider, server);
    }
}
