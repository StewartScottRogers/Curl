using System.Diagnostics.CodeAnalysis;
using System.Net;
using Curl.Cli;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Chooses the proxy one transfer goes through from <c>-x</c> or a <c>--socks</c> option,
/// <c>--noproxy</c>, <c>-U</c> and the proxy environment variables, and refuses a proxy the
/// HTTP handler would have the connector tunnel through when the connector cannot yet.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ProxySelector" /> picks the proxy (ADR-0024); <c>-U</c>/<c>--proxy-user</c> then
/// replaces its credential, as curl 8.21.0 lets <c>-U</c> win over the proxy URL's user
/// information. The chosen proxy reaches <see cref="Curl.Protocol.Abstractions.HttpRequestOptions.ForwardProxy" />, where
/// the HTTP handler forwards a plain <c>http</c> request itself and hands every other route to
/// the connector's CONNECT tunnel (BL-183, BL-212).
/// </para>
/// <para>
/// The connector tunnels only through <see cref="ProxyKind.Http" /> and
/// <see cref="ProxyKind.Http10" /> proxies so far (SOCKS is BL-213, HTTPS proxies BL-266). An
/// <c>http</c> or <c>https</c> transfer that would, or under <c>-L</c> could, need any other tunnel ends with exit 4,
/// <c>Unsupported proxy '&lt;host&gt;:&lt;port&gt;', Curl cannot tunnel through a &lt;kind&gt;
/// proxy yet</c>, instead of reaching the connector (ADR-0053). Other schemes do not read the
/// proxy yet and connect directly.
/// </para>
/// </remarks>
internal static class TransferProxySelection
{
    /// <summary>
    /// Chooses the proxy for <paramref name="url" />.
    /// </summary>
    /// <param name="selector">Reads <c>-x</c>, <c>--noproxy</c> and the proxy environment variables.</param>
    /// <param name="options">The accepted command line.</param>
    /// <param name="url">The URL being transferred.</param>
    /// <param name="proxy">
    /// The proxy, with the <c>-U</c> credential when one was given; <see langword="null" /> for a
    /// direct connection or a failure.
    /// </param>
    /// <param name="failure">
    /// The selector's failure for proxy text curl cannot use, or the exit 4 failure for a tunnel
    /// the connector cannot open yet; <see langword="null" /> otherwise.
    /// </param>
    /// <returns><see langword="true" /> unless the transfer must end with <paramref name="failure" />.</returns>
    internal static bool TrySelect(
        ProxySelector selector,
        CommandLineOptions options,
        CurlUrl url,
        out ProxyEndpoint? proxy,
        [NotNullWhen(false)] out TransferResult? failure)
    {
        proxy = null;
        if (!selector.TrySelect(url, options.Proxy?.Address, KindWithoutSchemeOf(options.Proxy), options.NoProxy, out ProxyEndpoint? selected, out failure))
        {
            return false;
        }

        ProxyEndpoint? chosen = WithProxyUser(selected, options.ProxyCredentials);
        failure = TunnelNotBuiltYetFailure(chosen, url, options);
        if (failure is not null)
        {
            return false;
        }

        proxy = chosen;
        return true;
    }

    /// <summary>The kind proxy option text with no scheme names: the option's own, or HTTP when none was given.</summary>
    private static ProxyKind KindWithoutSchemeOf(CommandLineProxy? proxyOption) =>
        proxyOption?.KindWithoutScheme ?? ProxyKind.Http;

    /// <summary>Replaces the proxy's credential with the <c>-U</c> one, when both are present.</summary>
    private static ProxyEndpoint? WithProxyUser(ProxyEndpoint? proxy, NetworkCredential? proxyUser) =>
        proxy is not null && proxyUser is not null ? proxy with { Credential = proxyUser } : proxy;

    /// <summary>
    /// Returns the exit 4 failure when the HTTP handler would hand <paramref name="proxy" /> to
    /// the connector as a tunnel it cannot open yet; <see langword="null" /> otherwise.
    /// </summary>
    private static TransferResult? TunnelNotBuiltYetFailure(ProxyEndpoint? proxy, CurlUrl url, CommandLineOptions options) =>
        proxy is not null && NeedsTunnelNotBuiltYet(proxy, url, options)
            ? TransferResult.Failure(
                CurlExitCode.NotBuiltIn,
                $"Unsupported proxy '{proxy.Host}:{proxy.Port}', Curl cannot tunnel through a {proxy.Kind} proxy yet")
            : null;

    /// <summary>
    /// Tells whether an <c>http</c> or <c>https</c> transfer through <paramref name="proxy" />
    /// needs a tunnel the connector cannot open yet: through any proxy but an HTTP one, unless the
    /// handler forwards every request itself.
    /// </summary>
    private static bool NeedsTunnelNotBuiltYet(ProxyEndpoint proxy, CurlUrl url, CommandLineOptions options) =>
        IsHttpScheme(url.Scheme)
        && !IsHttpProxy(proxy.Kind)
        && !IsForwardedOverTls(proxy.Kind, url.Scheme, options);

    private static bool IsHttpScheme(string scheme) => scheme is "http" or "https";

    private static bool IsHttpProxy(ProxyKind kind) => kind is ProxyKind.Http or ProxyKind.Http10;

    /// <summary>
    /// Tells whether the HTTP handler forwards every request to an HTTPS proxy itself, over TLS:
    /// a plain <c>http</c> URL without <c>-p</c> or <c>-L</c>. Under <c>-L</c> a redirect hop to
    /// <c>https</c> keeps the proxy (BL-329) and would need the tunnel, so it is refused too.
    /// </summary>
    private static bool IsForwardedOverTls(ProxyKind kind, string scheme, CommandLineOptions options) =>
        kind == ProxyKind.Https && scheme == "http" && !options.ProxyTunnel && !options.FollowRedirects;
}
