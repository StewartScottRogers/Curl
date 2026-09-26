namespace Curl.Protocol.Abstractions;

/// <summary>
/// The kind of proxy a <see cref="ProxyEndpoint" /> is, from the scheme on
/// <c>-x</c>/<c>--proxy</c> or from the option that named it (ADR-0014).
/// </summary>
public enum ProxyKind
{
    /// <summary>
    /// An HTTP/1.1 proxy: <c>http://</c> or no scheme on <c>-x</c>.
    /// </summary>
    Http = 0,

    /// <summary>
    /// An HTTP/1.0 proxy, per <c>--proxy1.0</c>.
    /// </summary>
    Http10,

    /// <summary>
    /// An HTTP proxy reached over TLS: <c>https://</c> on <c>-x</c>.
    /// </summary>
    Https,

    /// <summary>
    /// A SOCKS4 proxy: <c>socks4://</c> or <c>--socks4</c>.
    /// </summary>
    Socks4,

    /// <summary>
    /// A SOCKS4a proxy: <c>socks4a://</c> or <c>--socks4a</c>.
    /// </summary>
    Socks4a,

    /// <summary>
    /// A SOCKS5 proxy with local name resolution: <c>socks5://</c> or <c>--socks5</c>.
    /// </summary>
    Socks5,

    /// <summary>
    /// A SOCKS5 proxy that resolves the name itself: <c>socks5h://</c> or
    /// <c>--socks5-hostname</c>.
    /// </summary>
    Socks5Hostname,
}
