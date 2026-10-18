using Curl.Api.Options;
using Curl.Api.Options.Network;
using Curl.Api.Options.Proxy;
using Curl.Api.Options.Tls;

namespace Curl.Api;

/// <summary>
/// Applies the transport option records, first the client's then a request's, to an easy handle's
/// <see cref="EasyOptions"/>. A record left <see langword="null"/> leaves its group as it was, so a
/// request's record wins over the client's and a request without one keeps the client's.
/// </summary>
internal static class CurlTransportOptions
{
    /// <summary><paramref name="options"/> with each record that is set applied to its group.</summary>
    /// <param name="options">The handle's options so far.</param>
    /// <param name="network">The network record, or <see langword="null"/> to keep the network group.</param>
    /// <param name="tls">The TLS record, or <see langword="null"/> to keep the TLS group.</param>
    /// <param name="proxy">The proxy record, or <see langword="null"/> to keep the proxy group.</param>
    /// <returns>The options with the records applied.</returns>
    public static EasyOptions Apply(EasyOptions options, CurlNetworkOptions? network, CurlTlsOptions? tls, CurlProxyOptions? proxy) =>
        options with
        {
            Network = ApplyNetwork(options.Network, network),
            Tls = ApplyTls(options.Tls, tls),
            Proxy = ApplyProxy(options.Proxy, proxy),
        };

    private static EasyNetworkOptions ApplyNetwork(EasyNetworkOptions current, CurlNetworkOptions? network) =>
        network is null ? current : network.ApplyTo(current);

    private static EasyTlsOptions ApplyTls(EasyTlsOptions current, CurlTlsOptions? tls) =>
        tls is null ? current : tls.ToEasy();

    private static EasyProxyOptions ApplyProxy(EasyProxyOptions current, CurlProxyOptions? proxy) =>
        proxy is null ? current : proxy.ToEasy();
}
