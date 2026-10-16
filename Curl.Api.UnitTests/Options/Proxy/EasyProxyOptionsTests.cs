namespace Curl.Api.Options.Proxy;

[TestClass]
public sealed class EasyProxyOptionsTests
{
    private static readonly EasyProxyOptions Defaults = new();

    [TestMethod]
    public void New_EasyProxyOptions_LeavesProxyStringsUnset()
    {
        Assert.IsNull(Defaults.Proxy);
        Assert.IsNull(Defaults.PreProxy);
        Assert.IsNull(Defaults.NoProxy);
        Assert.IsNull(Defaults.Socks5GssApiService);
        Assert.IsNull(Defaults.HaProxyClientIp);
    }

    [TestMethod]
    public void New_EasyProxyOptions_UsesAnHttpProxyOnTheSchemesDefaultPort()
    {
        Assert.AreEqual(ProxyType.Http, Defaults.ProxyType);
        Assert.AreEqual(0, Defaults.ProxyPort);
    }

    [TestMethod]
    public void New_EasyProxyOptions_LeavesEverySwitchOff()
    {
        Assert.IsFalse(Defaults.HttpProxyTunnel);
        Assert.IsFalse(Defaults.SuppressConnectHeaders);
        Assert.IsFalse(Defaults.Socks5GssApiNec);
        Assert.IsFalse(Defaults.HaProxyProtocol);
    }

    [TestMethod]
    public void New_EasyProxyOptions_SendsSeparateHeadersAndNoProxyHeaders()
    {
        Assert.AreEqual(HeaderOption.Separate, Defaults.HeaderOption);
        Assert.IsEmpty(Defaults.ProxyHeaders);
    }

    [TestMethod]
    public void New_EasyProxyOptions_OffersBasicAndGssApiToSocks5()
    {
        Assert.AreEqual(Socks5Authentication.Basic | Socks5Authentication.GssApi, Defaults.Socks5Authentication);
    }

    [TestMethod]
    public void ProxyType_Members_HoldLibcurlsCurlproxyValues()
    {
        Assert.AreEqual(0, (int)ProxyType.Http);
        Assert.AreEqual(1, (int)ProxyType.Http10);
        Assert.AreEqual(2, (int)ProxyType.Https);
        Assert.AreEqual(3, (int)ProxyType.Https2);
        Assert.AreEqual(4, (int)ProxyType.Socks4);
        Assert.AreEqual(5, (int)ProxyType.Socks5);
        Assert.AreEqual(6, (int)ProxyType.Socks4A);
        Assert.AreEqual(7, (int)ProxyType.Socks5Hostname);
    }

    [TestMethod]
    public void HeaderOption_Members_HoldLibcurlsCurlheaderValues()
    {
        Assert.AreEqual(0, (int)HeaderOption.Unified);
        Assert.AreEqual(1, (int)HeaderOption.Separate);
    }

    [TestMethod]
    public void Socks5Authentication_Members_HoldLibcurlsCurlauthBits()
    {
        Assert.AreEqual(0, (int)Socks5Authentication.None);
        Assert.AreEqual(1, (int)Socks5Authentication.Basic);
        Assert.AreEqual(4, (int)Socks5Authentication.GssApi);
    }

    [TestMethod]
    public void With_EachProperty_ChangesOnlyThatProperty()
    {
        AssertChangesOnly(Defaults with { Proxy = "http://proxy:3128" }, o => o with { Proxy = null });
        AssertChangesOnly(Defaults with { ProxyPort = 8080 }, o => o with { ProxyPort = 0 });
        AssertChangesOnly(Defaults with { ProxyType = ProxyType.Socks5Hostname }, o => o with { ProxyType = ProxyType.Http });
        AssertChangesOnly(Defaults with { PreProxy = "socks5://pre:1080" }, o => o with { PreProxy = null });
        AssertChangesOnly(Defaults with { NoProxy = "localhost" }, o => o with { NoProxy = null });
        AssertChangesOnly(Defaults with { HttpProxyTunnel = true }, o => o with { HttpProxyTunnel = false });
        AssertChangesOnly(Defaults with { SuppressConnectHeaders = true }, o => o with { SuppressConnectHeaders = false });
        AssertChangesOnly(Defaults with { ProxyHeaders = ["X-Proxy: 1"] }, o => o with { ProxyHeaders = Defaults.ProxyHeaders });
        AssertChangesOnly(Defaults with { HeaderOption = HeaderOption.Unified }, o => o with { HeaderOption = HeaderOption.Separate });
        AssertChangesOnly(Defaults with { Socks5Authentication = Socks5Authentication.None }, o => o with { Socks5Authentication = Socks5Authentication.Basic | Socks5Authentication.GssApi });
        AssertChangesOnly(Defaults with { Socks5GssApiNec = true }, o => o with { Socks5GssApiNec = false });
        AssertChangesOnly(Defaults with { Socks5GssApiService = "rcmd" }, o => o with { Socks5GssApiService = null });
        AssertChangesOnly(Defaults with { HaProxyProtocol = true }, o => o with { HaProxyProtocol = false });
        AssertChangesOnly(Defaults with { HaProxyClientIp = "192.0.2.1" }, o => o with { HaProxyClientIp = null });
    }

    private static void AssertChangesOnly(EasyProxyOptions changed, Func<EasyProxyOptions, EasyProxyOptions> restore)
    {
        Assert.AreNotEqual(Defaults, changed);
        Assert.AreEqual(Defaults, restore(changed));
    }
}
