using System.Globalization;
using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Proxy;

namespace Libcurl.Options;

/// <summary>
/// The setter rows of every <c>CURLOPT_*</c> <see cref="EasyProxyOptions"/> holds, each checking its
/// value as curl 8.22.0's <c>lib/setopt.c</c> does (ADR-0576).
/// </summary>
internal static class EasyProxyOptionSetters
{
    /// <summary>The largest port <c>CURLOPT_PROXYPORT</c> takes.</summary>
    private const long MostPort = 65535;

    /// <summary><c>CURLPROXY_HTTPS3</c>: an HTTPS proxy over HTTP/3, which a build without <c>USE_PROXY_HTTP3</c> refuses.</summary>
    private const long HttpsThreeProxyType = 8;

    /// <summary>Adds the proxy rows to <paramref name="setters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    internal static void AddTo(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        setters[CurlOption.Proxy] = new(SetProxy);
        setters[CurlOption.Proxyport] = new(SetProxyPort);
        setters[CurlOption.Proxytype] = new(SetProxyType);
        setters[CurlOption.PreProxy] = new(SetPreProxy);
        setters[CurlOption.Noproxy] = new(SetNoProxy);
        setters[CurlOption.Httpproxytunnel] = new(SetHttpProxyTunnel);
        setters[CurlOption.SuppressConnectHeaders] = new(SetSuppressConnectHeaders);
        setters[CurlOption.Proxyheader] = new(SetProxyHeaders);
        setters[CurlOption.Headeropt] = new(SetHeaderOption);
        setters[CurlOption.Socks5Auth] = new(SetSocks5Authentication);
        setters[CurlOption.Socks5GssapiNec] = new(SetSocks5GssApiNec);
        setters[CurlOption.Socks5GssapiService] = new(SetSocks5GssApiService);
        setters[CurlOption.Haproxyprotocol] = new(SetHaProxyProtocol);
        setters[CurlOption.HaproxyClientIp] = new(SetHaProxyClientIp);
    }

    private static CurlCode SetProxy(ref EasyOptions o, object? v) => Set(ref o, p => p with { Proxy = (string?)v });

    private static CurlCode SetPreProxy(ref EasyOptions o, object? v) => Set(ref o, p => p with { PreProxy = (string?)v });

    private static CurlCode SetNoProxy(ref EasyOptions o, object? v) => Set(ref o, p => p with { NoProxy = (string?)v });

    private static CurlCode SetHttpProxyTunnel(ref EasyOptions o, object? v) => Set(ref o, p => p with { HttpProxyTunnel = IsOn(v) });

    private static CurlCode SetSuppressConnectHeaders(ref EasyOptions o, object? v) => Set(ref o, p => p with { SuppressConnectHeaders = IsOn(v) });

    /// <summary><c>CURLOPT_PROXYHEADER</c>: a copy of the list is kept, so the caller may change its own afterwards; <see langword="null"/> empties it.</summary>
    private static CurlCode SetProxyHeaders(ref EasyOptions o, object? v) => Set(ref o, p => p with { ProxyHeaders = v is IEnumerable<string> lines ? [.. lines] : [] });

    /// <summary><c>CURLOPT_HEADEROPT</c>: <c>CURLHEADER_SEPARATE</c> when the value's lowest bit is set, else <c>CURLHEADER_UNIFIED</c>.</summary>
    private static CurlCode SetHeaderOption(ref EasyOptions o, object? v) =>
        Set(ref o, p => p with { HeaderOption = (HeaderOption)(NumberOf(v) & (long)HeaderOption.Separate) });

    private static CurlCode SetSocks5GssApiNec(ref EasyOptions o, object? v) => Set(ref o, p => p with { Socks5GssApiNec = IsOn(v) });

    private static CurlCode SetSocks5GssApiService(ref EasyOptions o, object? v) => Set(ref o, p => p with { Socks5GssApiService = (string?)v });

    private static CurlCode SetHaProxyProtocol(ref EasyOptions o, object? v) => Set(ref o, p => p with { HaProxyProtocol = IsOn(v) });

    /// <summary><c>CURLOPT_HAPROXY_CLIENT_IP</c>: sets the address and turns <c>CURLOPT_HAPROXYPROTOCOL</c> on, even for <see langword="null"/>.</summary>
    private static CurlCode SetHaProxyClientIp(ref EasyOptions o, object? v) => Set(ref o, p => p with { HaProxyClientIp = (string?)v, HaProxyProtocol = true });

    /// <summary><c>CURLOPT_PROXYPORT</c>: 0 to 65535; any other value is <see cref="CurlCode.BadFunctionArgument"/>.</summary>
    private static CurlCode SetProxyPort(ref EasyOptions options, object? value)
    {
        var port = NumberOf(value);
        return port is < 0 or > MostPort ? CurlCode.BadFunctionArgument : Set(ref options, p => p with { ProxyPort = (int)port });
    }

    /// <summary><c>CURLOPT_PROXYTYPE</c>: <c>CURLPROXY_HTTP</c> to <c>CURLPROXY_SOCKS5_HOSTNAME</c>; <c>CURLPROXY_HTTPS3</c> (8) is <see cref="CurlCode.NotBuiltIn"/>, Curl having no HTTP/3 proxy (ADR-0995), and any other value <see cref="CurlCode.BadFunctionArgument"/>.</summary>
    private static CurlCode SetProxyType(ref EasyOptions options, object? value)
    {
        var type = NumberOf(value);
        if (type == HttpsThreeProxyType)
        {
            return CurlCode.NotBuiltIn;
        }

        return type is < (long)ProxyType.Http or > (long)ProxyType.Socks5Hostname
            ? CurlCode.BadFunctionArgument
            : Set(ref options, p => p with { ProxyType = (ProxyType)type });
    }

    /// <summary><c>CURLOPT_SOCKS5_AUTH</c>: only <c>CURLAUTH_BASIC</c> and <c>CURLAUTH_GSSAPI</c>; any other bit is <see cref="CurlCode.NotBuiltIn"/>.</summary>
    private static CurlCode SetSocks5Authentication(ref EasyOptions options, object? value)
    {
        var methods = NumberOf(value);
        return (methods & ~(long)(Socks5Authentication.Basic | Socks5Authentication.GssApi)) != 0
            ? CurlCode.NotBuiltIn
            : Set(ref options, p => p with { Socks5Authentication = (Socks5Authentication)methods });
    }

    /// <summary>Replaces the proxy options with what <paramref name="change"/> returns for them; always <see cref="CurlCode.Ok"/>.</summary>
    private static CurlCode Set(ref EasyOptions options, Func<EasyProxyOptions, EasyProxyOptions> change)
    {
        options = options with { Proxy = change(options.Proxy) };
        return CurlCode.Ok;
    }

    /// <summary>Whether the option's integer value is not 0, as libcurl treats an on/off option.</summary>
    private static bool IsOn(object? value) => NumberOf(value) != 0;

    /// <summary>The option's integer value, whether it arrived as an <see cref="int"/>, a <see cref="long"/> or an enum.</summary>
    private static long NumberOf(object? value) => Convert.ToInt64(value, CultureInfo.InvariantCulture);
}
