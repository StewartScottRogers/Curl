using Curl.Api.Options.Proxy;

namespace Curl.Api;

/// <summary>
/// Proxy settings, mapped one to one onto <see cref="EasyProxyOptions"/> (BL-2502, ADR-0636). Each
/// member starts at libcurl 8.22.0's default, so <c>new CurlProxyOptions()</c> changes nothing. Set on
/// <see cref="CurlClientOptions.Proxy"/> for every request or on <see cref="CurlRequest.Proxy"/> for
/// one; a request's record replaces the client's whole. An invalid value throws as it is set. The
/// proxy's own TLS settings are <see cref="CurlTlsOptions.Proxy"/>.
/// </summary>
public sealed record CurlProxyOptions
{
    /// <summary><c>CURLOPT_PROXY</c>: the proxy, <c>[scheme://]host[:port]</c>. Default <see langword="null"/>: the proxy environment variables apply.</summary>
    public string? Proxy { get; init; }

    /// <summary><c>CURLOPT_PROXYPORT</c>: the proxy's port when <see cref="Proxy"/> gives none, 0 to 65535. Default 0: the scheme's port.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside 0 to 65535.</exception>
    public int ProxyPort
    {
        get;
        init => field = TransportOptionGuard.Port(value, nameof(ProxyPort));
    }

    /// <summary><c>CURLOPT_PROXYTYPE</c>: the proxy protocol when <see cref="Proxy"/> has no scheme. Default <see cref="ProxyType.Http"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a <see cref="Options.Proxy.ProxyType"/> value.</exception>
    public ProxyType ProxyType
    {
        get;
        init => field = TransportOptionGuard.Defined(value, nameof(ProxyType));
    } = ProxyType.Http;

    /// <summary><c>CURLOPT_PRE_PROXY</c>: a SOCKS proxy to reach <see cref="Proxy"/> through. Default <see langword="null"/>.</summary>
    public string? PreProxy { get; init; }

    /// <summary><c>CURLOPT_NOPROXY</c>: the comma-separated hosts reached without the proxy. Default <see langword="null"/>.</summary>
    public string? NoProxy { get; init; }

    /// <summary><c>CURLOPT_HTTPPROXYTUNNEL</c>: tunnel through an HTTP proxy with <c>CONNECT</c>. Default <see langword="false"/>.</summary>
    public bool HttpProxyTunnel { get; init; }

    /// <summary><c>CURLOPT_SUPPRESS_CONNECT_HEADERS</c>: keep the <c>CONNECT</c> response's headers from the header callback. Default <see langword="false"/>.</summary>
    public bool SuppressConnectHeaders { get; init; }

    /// <summary><c>CURLOPT_PROXYHEADER</c>: header lines sent to the proxy. Default none.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public IReadOnlyList<string> ProxyHeaders
    {
        get;
        init => field = TransportOptionGuard.NotNull(value, nameof(ProxyHeaders));
    } = [];

    /// <summary><c>CURLOPT_HEADEROPT</c>: whether request headers also go to the proxy. Default <see cref="HeaderOption.Separate"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a <see cref="Options.Proxy.HeaderOption"/> value.</exception>
    public HeaderOption HeaderOption
    {
        get;
        init => field = TransportOptionGuard.Defined(value, nameof(HeaderOption));
    } = HeaderOption.Separate;

    /// <summary><c>CURLOPT_SOCKS5_AUTH</c>: the SOCKS5 authentication methods allowed. Default basic and GSS-API.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value sets a bit libcurl does not define.</exception>
    public Socks5Authentication Socks5Authentication
    {
        get;
        init => field = TransportOptionGuard.Flags(value, nameof(Socks5Authentication));
    } = Socks5Authentication.Basic | Socks5Authentication.GssApi;

    /// <summary><c>CURLOPT_SOCKS5_GSSAPI_NEC</c>: send the GSS-API protection level unprotected. Default <see langword="false"/>.</summary>
    public bool Socks5GssApiNec { get; init; }

    /// <summary><c>CURLOPT_PROXY_SERVICE_NAME</c>: the SOCKS5 GSS-API service name. Default <see langword="null"/>: <c>rcmd</c>.</summary>
    public string? Socks5GssApiService { get; init; }

    /// <summary><c>CURLOPT_HAPROXYPROTOCOL</c>: send a HAProxy PROXY protocol v1 header. Default <see langword="false"/>.</summary>
    public bool HaProxyProtocol { get; init; }

    /// <summary><c>CURLOPT_HAPROXY_CLIENT_IP</c>: the client address the PROXY header gives. Default <see langword="null"/>: the real one.</summary>
    public string? HaProxyClientIp { get; init; }

    /// <summary>An <see cref="EasyProxyOptions"/> holding every member of this record.</summary>
    /// <returns>The seam's proxy options.</returns>
    internal EasyProxyOptions ToEasy() => new()
    {
        Proxy = Proxy,
        ProxyPort = ProxyPort,
        ProxyType = ProxyType,
        PreProxy = PreProxy,
        NoProxy = NoProxy,
        HttpProxyTunnel = HttpProxyTunnel,
        SuppressConnectHeaders = SuppressConnectHeaders,
        ProxyHeaders = ProxyHeaders,
        HeaderOption = HeaderOption,
        Socks5Authentication = Socks5Authentication,
        Socks5GssApiNec = Socks5GssApiNec,
        Socks5GssApiService = Socks5GssApiService,
        HaProxyProtocol = HaProxyProtocol,
        HaProxyClientIp = HaProxyClientIp,
    };
}
