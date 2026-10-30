using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Resolution;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.EncryptedDns.DnsOverQuic;

/// <summary>
/// What <c>--dns-over-quic</c> supplies in place of curl's resolver (ADR-1064): a <see cref="DnsOverQuicResolver" />
/// that opens a UDP channel to the server, resolving the server's own name with curl's resolver, and checks its
/// certificate against the system's trust store through <see cref="SslStreamTlsProvider" />.
/// </summary>
/// <param name="server">The server the option named.</param>
internal sealed class DnsOverQuicResolverHook(DnsOverQuicServer server) : IResolverHook
{
    /// <inheritdoc />
    public ExtensionOption Option => EncryptedDnsExtension.DnsOverQuicOption;

    /// <summary>Gets the server the option named.</summary>
    public DnsOverQuicServer Server => server;

    /// <inheritdoc />
    public IDnsResolver ResolverOver(IDnsResolver curlResolver)
    {
        ArgumentNullException.ThrowIfNull(curlResolver);
        var verifier = new TlsProviderCertificateVerifier(new SslStreamTlsProvider(new TlsClientOptions(), TimeProvider.System), server.AuthenticationName);
        return new DnsOverQuicResolver(new UdpDatagramConnector(curlResolver, TimeProvider.System), verifier, server, TimeProvider.System);
    }
}
