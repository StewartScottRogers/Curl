using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Resolution;
using Curl.Extension.ObliviousDoh.Http;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.ObliviousDoh;

/// <summary>
/// The resolver hook <c>--odoh-proxy</c> and <c>--odoh-target</c> activate: it puts an
/// <see cref="ObliviousDohResolver" /> in place of curl's resolver, whose connections to the proxy and the target
/// resolve their own names with curl's resolver.
/// </summary>
/// <param name="endpoints">The proxy and the target.</param>
internal sealed class ObliviousDohResolverHook(ObliviousDohEndpoints endpoints) : IResolverHook
{
    /// <inheritdoc />
    public ExtensionOption Option => ObliviousDohExtension.ProxyOption;

    /// <summary>Gets the proxy and the target.</summary>
    public ObliviousDohEndpoints Endpoints => endpoints;

    /// <inheritdoc />
    public IDnsResolver ResolverOver(IDnsResolver curlResolver)
    {
        ArgumentNullException.ThrowIfNull(curlResolver);
        var tlsProvider = new SslStreamTlsProvider(new TlsClientOptions(), TimeProvider.System);
        var connector = new TcpConnector(curlResolver, new TcpDialer(), tlsProvider, TimeProvider.System);
        return new ObliviousDohResolver(new ObliviousDohHttpClient(connector), endpoints);
    }
}
