using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Proxy;
using Libcurl.Options;

namespace Libcurl.UnitTests.Options.Proxy;

[TestClass]
public sealed class EasyProxyOptionSettersTests
{
    [TestMethod]
    public void SetOption_Proxy_SetsTheProxyAndNullClearsIt()
    {
        var options = SetOk(new EasyOptions(), CurlOption.Proxy, "http://proxy:3128");

        Assert.AreEqual("http://proxy:3128", options.Proxy.Proxy);
        Assert.IsNull(SetOk(options, CurlOption.Proxy, null).Proxy.Proxy);
    }

    [TestMethod]
    public void SetOption_Proxyport_TakesZeroTo65535AndRefusesTheRest()
    {
        Assert.AreEqual(65535, SetOk(new EasyOptions(), CurlOption.Proxyport, 65535L).Proxy.ProxyPort);
        Assert.AreEqual(0, SetOk(new EasyOptions(), CurlOption.Proxyport, 0).Proxy.ProxyPort);
        AssertRefused(CurlOption.Proxyport, -1L, CurlCode.BadFunctionArgument);
        AssertRefused(CurlOption.Proxyport, 65536L, CurlCode.BadFunctionArgument);
    }

    [TestMethod]
    public void SetOption_Proxytype_TakesHttpToSocks5HostnameRefusesHttps3AsNotBuiltInAndTheRestAsBad()
    {
        Assert.AreEqual(ProxyType.Socks5Hostname, SetOk(new EasyOptions(), CurlOption.Proxytype, ProxyType.Socks5Hostname).Proxy.ProxyType);
        Assert.AreEqual(ProxyType.Http, SetOk(new EasyOptions(), CurlOption.Proxytype, 0L).Proxy.ProxyType);
        AssertRefused(CurlOption.Proxytype, -1L, CurlCode.BadFunctionArgument);
        AssertRefused(CurlOption.Proxytype, 8L, CurlCode.NotBuiltIn);
        AssertRefused(CurlOption.Proxytype, 9L, CurlCode.BadFunctionArgument);
    }

    [TestMethod]
    public void SetOption_PreProxy_SetsThePreProxy()
    {
        Assert.AreEqual("socks5://pre:1080", SetOk(new EasyOptions(), CurlOption.PreProxy, "socks5://pre:1080").Proxy.PreProxy);
    }

    [TestMethod]
    public void SetOption_Noproxy_SetsTheBypassList()
    {
        Assert.AreEqual("localhost,10.0.0.0/8", SetOk(new EasyOptions(), CurlOption.Noproxy, "localhost,10.0.0.0/8").Proxy.NoProxy);
    }

    [TestMethod]
    public void SetOption_Httpproxytunnel_IsOnForNonZeroAndOffForZero()
    {
        AssertSwitch(CurlOption.Httpproxytunnel, p => p.HttpProxyTunnel);
    }

    [TestMethod]
    public void SetOption_SuppressConnectHeaders_IsOnForNonZeroAndOffForZero()
    {
        AssertSwitch(CurlOption.SuppressConnectHeaders, p => p.SuppressConnectHeaders);
    }

    [TestMethod]
    public void SetOption_Proxyheader_KeepsACopyOfTheListAndNullEmptiesIt()
    {
        var headers = new List<string> { "X-Proxy: 1" };
        var options = SetOk(new EasyOptions(), CurlOption.Proxyheader, headers);
        headers.Add("X-Later: 2");

        CollectionAssert.AreEqual(new[] { "X-Proxy: 1" }, options.Proxy.ProxyHeaders.ToArray());
        Assert.AreEqual(0, SetOk(options, CurlOption.Proxyheader, null).Proxy.ProxyHeaders.Count);
    }

    [TestMethod]
    public void SetOption_Headeropt_IsSeparateWhenTheLowestBitIsSetAndUnifiedOtherwise()
    {
        Assert.AreEqual(HeaderOption.Unified, SetOk(new EasyOptions(), CurlOption.Headeropt, 0L).Proxy.HeaderOption);
        Assert.AreEqual(HeaderOption.Unified, SetOk(new EasyOptions(), CurlOption.Headeropt, 2L).Proxy.HeaderOption);
        var unified = SetOk(new EasyOptions(), CurlOption.Headeropt, HeaderOption.Unified);
        Assert.AreEqual(HeaderOption.Separate, SetOk(unified, CurlOption.Headeropt, 3).Proxy.HeaderOption);
    }

    [TestMethod]
    public void SetOption_Socks5Auth_TakesBasicAndGssApiAndAnyOtherBitIsNotBuiltIn()
    {
        Assert.AreEqual(Socks5Authentication.Basic, SetOk(new EasyOptions(), CurlOption.Socks5Auth, 1L).Proxy.Socks5Authentication);
        Assert.AreEqual(Socks5Authentication.None, SetOk(new EasyOptions(), CurlOption.Socks5Auth, 0L).Proxy.Socks5Authentication);
        Assert.AreEqual(Socks5Authentication.Basic | Socks5Authentication.GssApi, SetOk(new EasyOptions(), CurlOption.Socks5Auth, 5).Proxy.Socks5Authentication);
        AssertRefused(CurlOption.Socks5Auth, 2L, CurlCode.NotBuiltIn);
        AssertRefused(CurlOption.Socks5Auth, -1L, CurlCode.NotBuiltIn);
    }

    [TestMethod]
    public void SetOption_Socks5GssapiNec_IsOnForNonZeroAndOffForZero()
    {
        AssertSwitch(CurlOption.Socks5GssapiNec, p => p.Socks5GssApiNec);
    }

    [TestMethod]
    public void SetOption_Socks5GssapiService_SetsTheServiceName()
    {
        Assert.AreEqual("rcmd", SetOk(new EasyOptions(), CurlOption.Socks5GssapiService, "rcmd").Proxy.Socks5GssApiService);
    }

    [TestMethod]
    public void SetOption_Haproxyprotocol_IsOnForNonZeroAndOffForZero()
    {
        AssertSwitch(CurlOption.Haproxyprotocol, p => p.HaProxyProtocol);
    }

    [TestMethod]
    public void SetOption_HaproxyClientIp_SetsTheAddressAndTurnsTheProtocolOnEvenForNull()
    {
        var options = SetOk(new EasyOptions(), CurlOption.HaproxyClientIp, "192.0.2.7");

        Assert.AreEqual("192.0.2.7", options.Proxy.HaProxyClientIp);
        Assert.IsTrue(options.Proxy.HaProxyProtocol);
        var cleared = SetOk(new EasyOptions(), CurlOption.HaproxyClientIp, null);
        Assert.IsNull(cleared.Proxy.HaProxyClientIp);
        Assert.IsTrue(cleared.Proxy.HaProxyProtocol);
    }

    [TestMethod]
    public void SetOption_EveryProxyOption_IsInTheOptionsTableAndNoLongerNotBuiltIn()
    {
        (CurlOption Option, object? Value)[] all =
        [
            (CurlOption.Proxy, null), (CurlOption.Proxyport, 0L), (CurlOption.Proxytype, 0L), (CurlOption.PreProxy, null),
            (CurlOption.Noproxy, null), (CurlOption.Httpproxytunnel, 0L), (CurlOption.SuppressConnectHeaders, 0L),
            (CurlOption.Proxyheader, null), (CurlOption.Headeropt, 0L), (CurlOption.Socks5Auth, 0L),
            (CurlOption.Socks5GssapiNec, 0L), (CurlOption.Socks5GssapiService, null), (CurlOption.Haproxyprotocol, 0L),
            (CurlOption.HaproxyClientIp, null),
        ];

        foreach (var (option, value) in all)
        {
            Assert.IsNotNull(CurlOptions.ById(option), option.ToString());
            SetOk(new EasyOptions(), option, value);
        }
    }

    private static EasyOptions SetOk(EasyOptions options, CurlOption option, object? value)
    {
        Assert.AreEqual(CurlCode.Ok, EasyOptionDispatch.Default.SetOption(ref options, option, value), $"{option} {value}");
        return options;
    }

    private static void AssertRefused(CurlOption option, object? value, CurlCode expected)
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(expected, EasyOptionDispatch.Default.SetOption(ref options, option, value), $"{option} {value}");
        Assert.AreSame(before, options);
    }

    private static void AssertSwitch(CurlOption option, Func<EasyProxyOptions, bool> read)
    {
        Assert.IsTrue(read(SetOk(new EasyOptions(), option, 2L).Proxy), $"{option} 2");
        Assert.IsFalse(read(SetOk(new EasyOptions(), option, 0).Proxy), $"{option} 0");
    }
}
