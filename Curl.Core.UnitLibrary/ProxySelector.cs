using System.Diagnostics.CodeAnalysis;
using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>
/// Chooses the proxy curl 8.21.0 would use for a URL from <c>-x</c>/<c>--proxy</c> or a SOCKS
/// option such as <c>--socks5</c>, <c>--noproxy</c> and the proxy environment variables, which it reads through an injected
/// function so no test touches the real environment.
/// </summary>
/// <remarks>
/// <para>
/// A <c>file://</c> URL never uses a proxy. Otherwise the exemption list is
/// <c>--noproxy</c> when given, even empty, else <c>no_proxy</c>, else <c>NO_PROXY</c>; a
/// host it exempts (<see cref="NoProxyMatcher" />) is reached directly, whichever proxy was
/// named. Then the proxy is <c>-x</c> when given, else <c>&lt;scheme&gt;_proxy</c>, then
/// <c>&lt;SCHEME&gt;_PROXY</c> except for <c>http</c>, whose upper-case name curl never
/// reads; then for <c>ws</c> <c>http_proxy</c>, for <c>wss</c> <c>https_proxy</c> and
/// <c>HTTPS_PROXY</c>; then <c>all_proxy</c>, then <c>ALL_PROXY</c>. Empty proxy text,
/// including <c>-x ""</c>, means a direct connection.
/// </para>
/// <para>
/// The lookups use exactly these names; how a name matches is the reader's business. On
/// Windows the process environment ignores case, so there <c>HTTP_PROXY</c> is read as
/// <c>http_proxy</c>, as the measured Schannel build does. An empty variable counts as
/// unset, as it does in that build. Measured against curl 8.21.0 on 2026-09-26 (ADR-0024).
/// </para>
/// </remarks>
/// <param name="readEnvironmentVariable">
/// Returns the value of the named environment variable, or <see langword="null" /> when it
/// is not set; production passes <see cref="Environment.GetEnvironmentVariable(string)" />.
/// </param>
public sealed class ProxySelector(Func<string, string?> readEnvironmentVariable)
{
    private readonly Func<string, string?> readEnvironmentVariable =
        readEnvironmentVariable ?? throw new ArgumentNullException(nameof(readEnvironmentVariable));

    /// <summary>Chooses the proxy for <paramref name="url" />, reading <c>-x</c> text with no scheme as HTTP.</summary>
    /// <param name="url">The URL being fetched.</param>
    /// <param name="proxyOption">The <c>-x</c>/<c>--proxy</c> text; <see langword="null" /> when not given.</param>
    /// <param name="noProxyOption">The <c>--noproxy</c> list; <see langword="null" /> when not given.</param>
    /// <param name="proxy">The proxy to use; <see langword="null" /> for a direct connection or a failure.</param>
    /// <param name="failure">
    /// The failure the transfer ends with when the chosen proxy text cannot be used
    /// (<see cref="ProxyUrlParser" />); <see langword="null" /> otherwise.
    /// </param>
    /// <returns><see langword="true" /> unless the chosen proxy text is unusable.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="url" /> is <see langword="null" />.</exception>
    public bool TrySelect(
        CurlUrl url,
        string? proxyOption,
        string? noProxyOption,
        out ProxyEndpoint? proxy,
        [NotNullWhen(false)] out TransferResult? failure) =>
        TrySelect(url, proxyOption, ProxyKind.Http, noProxyOption, out proxy, out failure);

    /// <summary>
    /// Chooses the proxy for <paramref name="url" />, reading proxy option text with no scheme
    /// as a proxy of <paramref name="proxyOptionKind" />.
    /// </summary>
    /// <param name="url">The URL being fetched.</param>
    /// <param name="proxyOption">
    /// The text of the proxy option that won - <c>-x</c>/<c>--proxy</c>, <c>--socks4</c>,
    /// <c>--socks4a</c>, <c>--socks5</c> or <c>--socks5-hostname</c>; <see langword="null" />
    /// when none was given.
    /// </param>
    /// <param name="proxyOptionKind">
    /// The kind that option names: <see cref="ProxyKind.Http" /> for <c>-x</c>, the SOCKS kind
    /// for a SOCKS option. A scheme in the text wins over it, and it never applies to proxy
    /// text read from the environment.
    /// </param>
    /// <param name="noProxyOption">The <c>--noproxy</c> list; <see langword="null" /> when not given.</param>
    /// <param name="proxy">The proxy to use; <see langword="null" /> for a direct connection or a failure.</param>
    /// <param name="failure">
    /// The failure the transfer ends with when the chosen proxy text cannot be used
    /// (<see cref="ProxyUrlParser" />); <see langword="null" /> otherwise.
    /// </param>
    /// <returns><see langword="true" /> unless the chosen proxy text is unusable.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="url" /> is <see langword="null" />.</exception>
    public bool TrySelect(
        CurlUrl url,
        string? proxyOption,
        ProxyKind proxyOptionKind,
        string? noProxyOption,
        out ProxyEndpoint? proxy,
        [NotNullWhen(false)] out TransferResult? failure)
    {
        ArgumentNullException.ThrowIfNull(url);

        proxy = null;
        failure = null;
        if (url.Scheme == "file"
            || NoProxyMatcher.Matches(HostName(url), noProxyOption ?? ReadNoProxy()))
        {
            return true;
        }

        (string? proxyText, ProxyKind kindWithoutScheme) = proxyOption is null
            ? (ReadProxyFor(url.Scheme), ProxyKind.Http)
            : (proxyOption, proxyOptionKind);
        if (string.IsNullOrEmpty(proxyText))
        {
            return true;
        }

        return ProxyUrlParser.TryParse(proxyText, kindWithoutScheme, out proxy, out failure);
    }

    /// <summary>
    /// Returns the URL's host as curl compares it with an exemption list: an IPv6 address
    /// loses its brackets (<see cref="CurlUrl.Host" /> never carries the zone).
    /// </summary>
    private static string HostName(CurlUrl url) => url.Host.StartsWith('[') ? url.Host[1..^1] : url.Host;

    private string? ReadNoProxy() => Read("no_proxy") ?? Read("NO_PROXY");

    private string? ReadProxyFor(string scheme)
    {
        string lowerName = scheme + "_proxy";
        string? proxyText = Read(lowerName);
        if (proxyText is null && scheme != "http")
        {
            proxyText = Read(lowerName.ToUpperInvariant());
        }

        return proxyText ?? ReadWebSocketFallback(scheme) ?? Read("all_proxy") ?? Read("ALL_PROXY");
    }

    /// <summary>
    /// Reads the HTTP proxy variable curl falls back to for a WebSocket URL: <c>http_proxy</c>
    /// for <c>ws</c>, <c>https_proxy</c> then <c>HTTPS_PROXY</c> for <c>wss</c>.
    /// </summary>
    private string? ReadWebSocketFallback(string scheme) => scheme switch
    {
        "ws" => Read("http_proxy"),
        "wss" => Read("https_proxy") ?? Read("HTTPS_PROXY"),
        _ => null,
    };

    private string? Read(string name)
    {
        string? value = readEnvironmentVariable(name);
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
