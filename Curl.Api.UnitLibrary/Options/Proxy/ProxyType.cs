namespace Curl.Api.Options.Proxy;

/// <summary>
/// The kind of proxy <c>CURLOPT_PROXY</c> names: the <c>CURLPROXY_*</c> values of
/// <c>CURLOPT_PROXYTYPE</c>. Each member's value is libcurl's. A scheme prefix in the proxy
/// string overrides it.
/// </summary>
public enum ProxyType
{
    /// <summary><c>CURLPROXY_HTTP</c> (0): an HTTP/1.1 proxy; libcurl's default.</summary>
    Http = 0,

    /// <summary><c>CURLPROXY_HTTP_1_0</c> (1): an HTTP/1.0 proxy.</summary>
    Http10 = 1,

    /// <summary><c>CURLPROXY_HTTPS</c> (2): an HTTPS proxy spoken to over HTTP/1.1.</summary>
    Https = 2,

    /// <summary><c>CURLPROXY_HTTPS2</c> (3): an HTTPS proxy that may be spoken to over HTTP/2.</summary>
    Https2 = 3,

    /// <summary><c>CURLPROXY_SOCKS4</c> (4): a SOCKS4 proxy; libcurl resolves the host name.</summary>
    Socks4 = 4,

    /// <summary><c>CURLPROXY_SOCKS5</c> (5): a SOCKS5 proxy; libcurl resolves the host name.</summary>
    Socks5 = 5,

    /// <summary><c>CURLPROXY_SOCKS4A</c> (6): a SOCKS4a proxy; the proxy resolves the host name.</summary>
    Socks4A = 6,

    /// <summary><c>CURLPROXY_SOCKS5_HOSTNAME</c> (7): a SOCKS5 proxy; the proxy resolves the host name.</summary>
    Socks5Hostname = 7,
}
