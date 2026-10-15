namespace Curl.Api.Options.Proxy;

/// <summary>
/// An easy handle's proxy options (ADR-0506, plan row <c>EasyProxyOptions</c>): the proxy
/// and pre-proxy, the hosts that bypass them, tunnelling and its <c>CONNECT</c> headers,
/// SOCKS5 authentication and the HAProxy protocol. Every default is libcurl 8.22.0's.
/// Proxy credentials are <c>EasyAuthenticationOptions</c>' and proxy TLS is
/// <c>EasyTlsOptions.Proxy</c>.
/// </summary>
public sealed record EasyProxyOptions
{
    /// <summary>
    /// <c>CURLOPT_PROXY</c>: the proxy, <c>[scheme://]host[:port]</c>. Default
    /// <see langword="null"/>: the proxy environment variables decide; an empty string
    /// disables every proxy, the environment's included.
    /// </summary>
    public string? Proxy { get; init; }

    /// <summary>
    /// <c>CURLOPT_PROXYPORT</c>: the proxy's port when <see cref="Proxy"/> gives none.
    /// Default 0: the proxy scheme's default port (1080, or 443 for HTTPS).
    /// </summary>
    public int ProxyPort { get; init; }

    /// <summary>
    /// <c>CURLOPT_PROXYTYPE</c>: the kind of proxy when <see cref="Proxy"/> has no scheme.
    /// Default <see cref="ProxyType.Http"/> (<c>CURLPROXY_HTTP</c>).
    /// </summary>
    public ProxyType ProxyType { get; init; } = ProxyType.Http;

    /// <summary>
    /// <c>CURLOPT_PRE_PROXY</c>: a SOCKS proxy connected to first, through which
    /// <see cref="Proxy"/> is reached. Default <see langword="null"/>: none.
    /// </summary>
    public string? PreProxy { get; init; }

    /// <summary>
    /// <c>CURLOPT_NOPROXY</c>: comma-separated hosts, domains and CIDR ranges that bypass the
    /// proxy; <c>*</c> matches every host. Default <see langword="null"/>: the
    /// <c>NO_PROXY</c> environment variable decides.
    /// </summary>
    public string? NoProxy { get; init; }

    /// <summary>
    /// <c>CURLOPT_HTTPPROXYTUNNEL</c>: whether an HTTP proxy is asked to tunnel with
    /// <c>CONNECT</c>. Default <see langword="false"/> (0).
    /// </summary>
    public bool HttpProxyTunnel { get; init; }

    /// <summary>
    /// <c>CURLOPT_SUPPRESS_CONNECT_HEADERS</c>: whether the proxy's <c>CONNECT</c> response
    /// headers are kept out of the header callback and the headers output. Default
    /// <see langword="false"/> (0).
    /// </summary>
    public bool SuppressConnectHeaders { get; init; }

    /// <summary>
    /// <c>CURLOPT_PROXYHEADER</c>: extra headers sent to the proxy only, while
    /// <see cref="HeaderOption"/> is <c>CURLHEADER_SEPARATE</c>. Default empty (libcurl's
    /// <see langword="null"/> list).
    /// </summary>
    public IReadOnlyList<string> ProxyHeaders { get; init; } = [];

    /// <summary>
    /// <c>CURLOPT_HEADEROPT</c>: whether <c>CURLOPT_HTTPHEADER</c>'s headers also go to the
    /// proxy. Default <c>CURLHEADER_SEPARATE</c>: they do not.
    /// </summary>
    public HeaderOption HeaderOption { get; init; } = HeaderOption.Separate;

    /// <summary>
    /// <c>CURLOPT_SOCKS5_AUTH</c>: the methods a SOCKS5 proxy may be authenticated with.
    /// Default <c>CURLAUTH_BASIC | CURLAUTH_GSSAPI</c>.
    /// </summary>
    public Socks5Authentication Socks5Authentication { get; init; } =
        Socks5Authentication.Basic | Socks5Authentication.GssApi;

    /// <summary>
    /// <c>CURLOPT_SOCKS5_GSSAPI_NEC</c>: whether the GSS-API protection-level negotiation is
    /// sent unprotected, as the NEC reference implementation does. Default
    /// <see langword="false"/> (0).
    /// </summary>
    public bool Socks5GssApiNec { get; init; }

    /// <summary>
    /// <c>CURLOPT_SOCKS5_GSSAPI_SERVICE</c> (deprecated in libcurl for
    /// <c>CURLOPT_PROXY_SERVICE_NAME</c>): the SOCKS5 proxy's GSS-API service name. Default
    /// <see langword="null"/>: libcurl uses <c>rcmd</c>.
    /// </summary>
    public string? Socks5GssApiService { get; init; }

    /// <summary>
    /// <c>CURLOPT_HAPROXYPROTOCOL</c>: whether an HAProxy PROXY protocol v1 header opens the
    /// connection. Default <see langword="false"/> (0).
    /// </summary>
    public bool HaProxyProtocol { get; init; }

    /// <summary>
    /// <c>CURLOPT_HAPROXY_CLIENT_IP</c>: the client address the HAProxy PROXY header names,
    /// which also sends the header. Default <see langword="null"/>: the connection's own
    /// local address.
    /// </summary>
    public string? HaProxyClientIp { get; init; }
}
