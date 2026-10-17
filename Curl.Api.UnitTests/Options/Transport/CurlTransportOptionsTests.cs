using Curl.Api.Messages;
using Curl.Api.Options;
using Curl.Api.Options.Proxy;
using Curl.Api.Options.Tls;

namespace Curl.Api.UnitTests.Options.Transport;

[TestClass]
public sealed class CurlTransportOptionsTests
{
    private static readonly CurlClientOptions ClientOptions = new()
    {
        ConnectTimeout = TimeSpan.FromSeconds(3),
        Network = new CurlNetworkOptions { Port = 1000, MaxConnects = 1 },
        Tls = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { SslVersion = SslVersion.Tls12 } },
        Proxy = new CurlProxyOptions { Proxy = "http://client-proxy" },
    };

    private static async Task<EasyOptions> PerformedOptions(CurlRequest request)
    {
        ScriptedEasyTransfer easy = new();
        await using CurlClient client = new(new ScriptedCurlEngine(easy), ClientOptions, ownsEngine: true);
        await client.SendAsync(request, CancellationToken.None);
        return easy.PerformedOptions!;
    }

    [TestMethod]
    public async Task RequestWithoutRecords_KeepsTheClientRecords()
    {
        EasyOptions options = await PerformedOptions(new CurlRequest(HttpMethod.Get, "http://example/"));

        Assert.AreEqual(1000, options.Network.Port);
        Assert.AreEqual(1, options.Network.MaxConnects);
        Assert.AreEqual(TimeSpan.FromSeconds(3), options.Network.ConnectTimeout);
        Assert.AreEqual(SslVersion.Tls12, options.Tls.Origin.SslVersion);
        Assert.AreEqual("http://client-proxy", options.Proxy.Proxy);
        Assert.AreEqual("http://example/", options.Network.Url);
    }

    [TestMethod]
    public async Task RequestRecords_OverrideTheClientRecordsWhole()
    {
        CurlRequest request = new(HttpMethod.Get, "http://example/")
        {
            Network = new CurlNetworkOptions { Port = 2000, ConnectTimeout = TimeSpan.FromSeconds(7) },
            Tls = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { SslVersion = SslVersion.Tls13 } },
            Proxy = new CurlProxyOptions { Proxy = "socks5://request-proxy", ProxyType = ProxyType.Socks5 },
        };

        EasyOptions options = await PerformedOptions(request);

        Assert.AreEqual(2000, options.Network.Port);
        Assert.AreEqual(5, options.Network.MaxConnects);
        Assert.AreEqual(TimeSpan.FromSeconds(7), options.Network.ConnectTimeout);
        Assert.AreEqual(SslVersion.Tls13, options.Tls.Origin.SslVersion);
        Assert.AreEqual("socks5://request-proxy", options.Proxy.Proxy);
        Assert.AreEqual(ProxyType.Socks5, options.Proxy.ProxyType);
        Assert.AreEqual("http://example/", options.Network.Url);
    }

    [TestMethod]
    public async Task RequestNetworkWithoutConnectTimeout_KeepsTheClientConnectTimeout()
    {
        CurlRequest request = new(HttpMethod.Get, "http://example/") { Network = new CurlNetworkOptions() };

        EasyOptions options = await PerformedOptions(request);

        Assert.AreEqual(TimeSpan.FromSeconds(3), options.Network.ConnectTimeout);
        Assert.AreEqual(0, options.Network.Port);
    }

    [TestMethod]
    public void InvalidRequestValue_ThrowsBeforeAnyTransfer()
    {
        ScriptedEasyTransfer easy = new();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlRequest(HttpMethod.Get, "http://example/")
        {
            Network = new CurlNetworkOptions { ConnectTimeout = TimeSpan.FromSeconds(-1) },
        });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlRequest(HttpMethod.Get, "http://example/")
        {
            Tls = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { SslVersion = (SslVersion)99 } },
        });

        Assert.IsNull(easy.PerformedOptions);
    }

    [TestMethod]
    public void Apply_WithNoRecords_ReturnsTheOptionsUnchanged()
    {
        EasyOptions options = new();

        Assert.AreEqual(options, CurlTransportOptions.Apply(options, null, null, null));
    }
}
