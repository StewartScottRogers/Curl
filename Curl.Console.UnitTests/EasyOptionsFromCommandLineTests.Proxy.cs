using Curl.Api.Options.Proxy;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="EasyOptionsFromCommandLine.ProxyOf" />: each proxy command-line option given sets its
/// <see cref="EasyProxyOptions" /> member, and a command line without it keeps libcurl's default (ADR-0515).
/// </summary>
public sealed partial class EasyOptionsFromCommandLineTests
{
    private static readonly EasyProxyOptions LibcurlProxyDefaults = new();

    public static IEnumerable<object?[]> GivenProxyOptions =>
    [
        GivenProxy("-x", o => o.Proxy, "proxy.example:3128", "-x", "proxy.example:3128"),
        GivenProxy("-x empty", o => o.Proxy, "", "-x", ""),
        GivenProxy("-x type", o => o.ProxyType, ProxyType.Http, "-x", "proxy.example:3128"),
        GivenProxy("-x scheme keeps the option's type", o => o.ProxyType, ProxyType.Http, "-x", "socks5://proxy.example"),
        GivenProxy("--proxy1.0", o => o.ProxyType, ProxyType.Http10, "--proxy1.0", "proxy.example:3128"),
        GivenProxy("--proxy1.0 address", o => o.Proxy, "proxy.example:3128", "--proxy1.0", "proxy.example:3128"),
        GivenProxy("--socks4", o => o.ProxyType, ProxyType.Socks4, "--socks4", "proxy.example:1080"),
        GivenProxy("--socks4a", o => o.ProxyType, ProxyType.Socks4A, "--socks4a", "proxy.example:1080"),
        GivenProxy("--socks5", o => o.ProxyType, ProxyType.Socks5, "--socks5", "proxy.example:1080"),
        GivenProxy("--socks5-hostname", o => o.ProxyType, ProxyType.Socks5Hostname, "--socks5-hostname", "proxy.example:1080"),
        GivenProxy("--socks5 address", o => o.Proxy, "proxy.example:1080", "--socks5", "proxy.example:1080"),
        GivenProxy("last proxy option wins", o => o.ProxyType, ProxyType.Http, "--socks5", "a:1080", "-x", "b:3128"),
        GivenProxy("--proxy-http2", o => o.ProxyType, ProxyType.Https2, "--proxy-http2", "-x", "https://proxy.example"),
        GivenProxy("--proxy-http2 leaves SOCKS", o => o.ProxyType, ProxyType.Socks5, "--proxy-http2", "--socks5", "proxy.example:1080"),
        GivenProxy("--proxy-http2 without a proxy", o => o.ProxyType, ProxyType.Http, "--proxy-http2"),
        GivenProxy("--preproxy", o => o.PreProxy, "socks5://pre.example", "--preproxy", "socks5://pre.example"),
        GivenProxy("--noproxy", o => o.NoProxy, "localhost,.example", "--noproxy", "localhost,.example"),
        GivenProxy("--noproxy empty", o => o.NoProxy, "", "--noproxy", ""),
        GivenProxy("-p", o => o.HttpProxyTunnel, true, "-p"),
        GivenProxy("--suppress-connect-headers", o => o.SuppressConnectHeaders, true, "--suppress-connect-headers"),
        GivenProxy("--proxy-header", o => string.Join('|', o.ProxyHeaders), "X-A: 1|X-B: 2", "--proxy-header", "X-A: 1", "--proxy-header", "X-B: 2"),
        GivenProxy("--proxy-header leaves the header option", o => o.HeaderOption, HeaderOption.Separate, "--proxy-header", "X-A: 1"),
        GivenProxy("--socks5-basic", o => o.Socks5Authentication, Socks5Authentication.Basic, "--socks5-basic"),
        GivenProxy("--socks5-gssapi", o => o.Socks5Authentication, Socks5Authentication.GssApi, "--socks5-gssapi"),
        GivenProxy("--socks5-basic --socks5-gssapi", o => o.Socks5Authentication, Socks5Authentication.Basic | Socks5Authentication.GssApi, "--socks5-basic", "--socks5-gssapi"),
        GivenProxy("--no-socks5-basic", o => o.Socks5Authentication, LibcurlProxyDefaults.Socks5Authentication, "--socks5-basic", "--no-socks5-basic"),
        GivenProxy("--socks5-gssapi-nec", o => o.Socks5GssApiNec, true, "--socks5-gssapi-nec"),
        GivenProxy("--socks5-gssapi-service", o => o.Socks5GssApiService, "rcmd/host", "--socks5-gssapi-service", "rcmd/host"),
        GivenProxy("--haproxy-protocol", o => o.HaProxyProtocol, true, "--haproxy-protocol"),
        GivenProxy("--haproxy-clientip", o => o.HaProxyClientIp, "192.0.2.7", "--haproxy-clientip", "192.0.2.7"),
    ];

    public static IEnumerable<object?[]> AbsentProxyOptions =>
    [
        AbsentProxy("Proxy", o => o.Proxy),
        AbsentProxy("ProxyPort", o => o.ProxyPort),
        AbsentProxy("ProxyType", o => o.ProxyType),
        AbsentProxy("PreProxy", o => o.PreProxy),
        AbsentProxy("NoProxy", o => o.NoProxy),
        AbsentProxy("HttpProxyTunnel", o => o.HttpProxyTunnel),
        AbsentProxy("SuppressConnectHeaders", o => o.SuppressConnectHeaders),
        AbsentProxy("ProxyHeaders", o => o.ProxyHeaders.Count),
        AbsentProxy("HeaderOption", o => o.HeaderOption),
        AbsentProxy("Socks5Authentication", o => o.Socks5Authentication),
        AbsentProxy("Socks5GssApiNec", o => o.Socks5GssApiNec),
        AbsentProxy("Socks5GssApiService", o => o.Socks5GssApiService),
        AbsentProxy("HaProxyProtocol", o => o.HaProxyProtocol),
        AbsentProxy("HaProxyClientIp", o => o.HaProxyClientIp),
    ];

    [TestMethod]
    [DynamicData(nameof(GivenProxyOptions))]
    public void ProxyOf_OptionGiven_SetsItsMember(string option, Func<EasyProxyOptions, object?> read, object? expected, string[] arguments)
    {
        EasyProxyOptions proxy = EasyOptionsFromCommandLine.ProxyOf(Parse(arguments));

        Assert.AreEqual(expected, read(proxy), option);
    }

    [TestMethod]
    [DynamicData(nameof(AbsentProxyOptions))]
    public void ProxyOf_OptionAbsent_KeepsLibcurlDefault(string member, Func<EasyProxyOptions, object?> read)
    {
        EasyProxyOptions proxy = EasyOptionsFromCommandLine.ProxyOf(Parse());

        Assert.AreEqual(read(LibcurlProxyDefaults), read(proxy), member);
    }

    [TestMethod]
    public void ProxyOf_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => EasyOptionsFromCommandLine.ProxyOf(null!));
    }

    private static object?[] GivenProxy(string option, Func<EasyProxyOptions, object?> read, object? expected, params string[] arguments) =>
        [option, read, expected, arguments];

    private static object?[] AbsentProxy(string member, Func<EasyProxyOptions, object?> read) => [member, read];
}
