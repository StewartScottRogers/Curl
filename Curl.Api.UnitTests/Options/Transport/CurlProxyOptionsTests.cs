using Curl.Api.Client;
using Curl.Api.Options.Proxy;

namespace Curl.Api.UnitTests.Options.Transport;

[TestClass]
public sealed class CurlProxyOptionsTests
{
    private static async Task<EasyProxyOptions> ReceivedByEngine(CurlProxyOptions proxy)
    {
        FakeCurlEngine engine = new();
        await using CurlClient client = new(engine, new CurlClientOptions { Proxy = proxy }, ownsEngine: true);
        client.CreateEasy();
        return engine.CreatedEasyHandles.Single().Options.Proxy;
    }

    [TestMethod]
    public async Task EveryMember_ReachesTheEngineUnderItsEasyName()
    {
        CurlProxyOptions proxy = new()
        {
            Proxy = "socks5://proxy.example",
            ProxyPort = 1081,
            ProxyType = ProxyType.Socks5Hostname,
            PreProxy = "socks4://pre.example",
            NoProxy = "localhost",
            HttpProxyTunnel = true,
            SuppressConnectHeaders = true,
            ProxyHeaders = ["X-Proxy: 1"],
            HeaderOption = HeaderOption.Unified,
            Socks5Authentication = Socks5Authentication.Basic,
            Socks5GssApiNec = true,
            Socks5GssApiService = "proxy-service",
            HaProxyProtocol = true,
            HaProxyClientIp = "192.0.2.1",
        };

        EasyProxyOptions easy = await ReceivedByEngine(proxy);

        Assert.AreEqual("socks5://proxy.example", easy.Proxy);
        Assert.AreEqual(1081, easy.ProxyPort);
        Assert.AreEqual(ProxyType.Socks5Hostname, easy.ProxyType);
        Assert.AreEqual("socks4://pre.example", easy.PreProxy);
        Assert.AreEqual("localhost", easy.NoProxy);
        Assert.IsTrue(easy.HttpProxyTunnel);
        Assert.IsTrue(easy.SuppressConnectHeaders);
        CollectionAssert.AreEqual(new[] { "X-Proxy: 1" }, easy.ProxyHeaders.ToArray());
        Assert.AreEqual(HeaderOption.Unified, easy.HeaderOption);
        Assert.AreEqual(Socks5Authentication.Basic, easy.Socks5Authentication);
        Assert.IsTrue(easy.Socks5GssApiNec);
        Assert.AreEqual("proxy-service", easy.Socks5GssApiService);
        Assert.IsTrue(easy.HaProxyProtocol);
        Assert.AreEqual("192.0.2.1", easy.HaProxyClientIp);
    }

    [TestMethod]
    public async Task DefaultRecord_LeavesTheEngineAtLibcurlDefaults()
    {
        EasyProxyOptions easy = await ReceivedByEngine(new CurlProxyOptions());

        Assert.AreEqual(new EasyProxyOptions(), easy);
    }

    [TestMethod]
    public void InvalidValues_ThrowAsTheyAreSet()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlProxyOptions { ProxyPort = -1 });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlProxyOptions { ProxyType = (ProxyType)8 });
        Assert.ThrowsExactly<ArgumentNullException>(() => new CurlProxyOptions { ProxyHeaders = null! });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlProxyOptions { HeaderOption = (HeaderOption)2 });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlProxyOptions { Socks5Authentication = (Socks5Authentication)2 });
    }
}
