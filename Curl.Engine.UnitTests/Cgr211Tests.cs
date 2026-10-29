using Curl.Api.Options.Proxy;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins CGR-211 (BL-3740): <c>-v</c> writes <c>Uses proxy env variable http_proxy == 'http://127.0.0.1:20600'</c>
/// when the proxy comes from the environment, as curl 8.22.0 does, and nothing when <c>-x</c> gave it.
/// </summary>
[TestClass]
public sealed class Cgr211Tests
{
    private static readonly CurlUrl Url = CurlUrl.Parse("http://ex.test:20600/x");

    [TestMethod]
    public void EnvironmentProxyInfoLine_ProxyFromHttpProxyVariable_NamesVariableAndValue()
    {
        ProxySelector selector = new(name => name == "http_proxy" ? "http://127.0.0.1:20600" : null);
        EasyProxyOptions options = new();
        TransferProxySelection.TrySelect(selector, options, false, null, Url, out ProxyEndpoint? proxy, out _);

        string? line = EngineComposition.EnvironmentProxyInfoLine(selector, options, Url.Scheme, proxy);

        Assert.AreEqual("Uses proxy env variable http_proxy == 'http://127.0.0.1:20600'", line);
    }

    [TestMethod]
    public void ReportEnvironmentProxy_ProxyFromEnvironment_ReportsTheLineOnlyWhenOne()
    {
        ProxySelector selector = new(name => name == "http_proxy" ? "http://127.0.0.1:20600" : null);
        ProxyEndpoint proxy = new(ProxyKind.Http, "127.0.0.1", 20600, null);
        List<string> lines = [];

        EngineComposition.ReportEnvironmentProxy(selector, new EasyProxyOptions(), "http", proxy, lines.Add);
        EngineComposition.ReportEnvironmentProxy(selector, new EasyProxyOptions(), "http", null, lines.Add);

        CollectionAssert.AreEqual(new[] { "Uses proxy env variable http_proxy == 'http://127.0.0.1:20600'" }, lines);
    }

    [TestMethod]
    public void EnvironmentProxyInfoLine_ProxyFromAllProxyVariable_NamesAllProxy()
    {
        ProxySelector selector = new(name => name == "ALL_PROXY" ? "http://p.test:1" : null);
        EasyProxyOptions options = new();
        TransferProxySelection.TrySelect(selector, options, false, null, Url, out ProxyEndpoint? proxy, out _);

        string? line = EngineComposition.EnvironmentProxyInfoLine(selector, options, Url.Scheme, proxy);

        Assert.AreEqual("Uses proxy env variable ALL_PROXY == 'http://p.test:1'", line);
    }

    [TestMethod]
    public void EnvironmentProxyInfoLine_ProxyOptionGiven_WritesNothing()
    {
        ProxySelector selector = new(_ => "http://env.test:1");
        EasyProxyOptions options = new() { Proxy = "http://opt.test:2" };
        TransferProxySelection.TrySelect(selector, options, false, null, Url, out ProxyEndpoint? proxy, out _);

        Assert.IsNull(EngineComposition.EnvironmentProxyInfoLine(selector, options, Url.Scheme, proxy));
    }

    [TestMethod]
    public void EnvironmentProxyInfoLine_PreProxyGiven_WritesNothing()
    {
        ProxySelector selector = new(_ => "http://env.test:1");
        EasyProxyOptions options = new() { PreProxy = "socks5://pre.test:3" };

        Assert.IsNull(EngineComposition.EnvironmentProxyInfoLine(selector, options, Url.Scheme, new ProxyEndpoint(ProxyKind.Http, "pre.test", 3, null)));
    }

    [TestMethod]
    public void EnvironmentProxyInfoLine_NoProxyChosen_WritesNothing()
    {
        ProxySelector selector = new(_ => "http://env.test:1");

        Assert.IsNull(EngineComposition.EnvironmentProxyInfoLine(selector, new EasyProxyOptions(), Url.Scheme, null));
    }

    [TestMethod]
    public void EnvironmentProxyInfoLine_ProxyChosenButNoVariable_WritesNothing()
    {
        ProxySelector selector = new(_ => null);

        Assert.IsNull(EngineComposition.EnvironmentProxyInfoLine(selector, new EasyProxyOptions(), Url.Scheme, new ProxyEndpoint(ProxyKind.Http, "x.test", 3, null)));
    }
}
