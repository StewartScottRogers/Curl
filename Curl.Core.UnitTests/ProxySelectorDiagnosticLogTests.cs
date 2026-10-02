using Curl.Core.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>
/// Pins what <see cref="ProxySelector" /> writes to Curl's own diagnostic log, component
/// <c>proxy</c> (ADR-0222, BL-1072): the proxy chosen at <c>info</c>, by scheme, host and port only,
/// and why none was at <c>verbose</c>.
/// </summary>
[TestClass]
public sealed class ProxySelectorDiagnosticLogTests
{
    private static readonly ProxySelector NoEnvironment = new(_ => null);

    [TestMethod]
    public void TrySelect_ProxyChosen_LogsInfoWithSchemeHostAndPortAndNoPassword()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);

        Select(NoEnvironment, "http://a.test/", "http://user:secret@proxy.test:3128", null, log);

        Assert.AreEqual(1, log.Lines.Count);
        Assert.AreEqual((DiagnosticLogLevel.Info, DiagnosticLogComponents.Proxy, "using proxy http://proxy.test:3128 for http://a.test"), log.Lines[0]);
        Assert.IsFalse(log.Lines[0].Message.Contains("secret", StringComparison.Ordinal));
        Assert.IsFalse(log.Lines[0].Message.Contains("user", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("socks5h://proxy.test", "socks5h://proxy.test:1080")]
    [DataRow("socks4a://proxy.test:9", "socks4a://proxy.test:9")]
    [DataRow("https://[::1]:8443", "https://[::1]:8443")]
    public void TrySelect_ProxyChosen_NamesItsKindAsAScheme(string proxyText, string expected)
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Info);

        Select(NoEnvironment, "http://a.test/", proxyText, null, log);

        Assert.AreEqual($"using proxy {expected} for http://a.test", log.At(DiagnosticLogLevel.Info).Single());
    }

    [TestMethod]
    public void TrySelect_ProxyFromTheEnvironment_LogsIt()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Info);
        ProxySelector selector = new(name => name == "https_proxy" ? "proxy.test:8080" : null);

        Select(selector, "https://a.test/", null, null, log);

        Assert.AreEqual("using proxy http://proxy.test:8080 for https://a.test", log.At(DiagnosticLogLevel.Info).Single());
    }

    [TestMethod]
    [DataRow("other.test, .a.test", ".a.test")]
    [DataRow("*", "*")]
    public void TrySelect_NoProxyMatch_LogsVerboseNamingTheEntry(string noProxy, string entry)
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);

        Select(NoEnvironment, "http://www.a.test/", "http://proxy.test:3128", noProxy, log);

        Assert.AreEqual(1, log.Lines.Count);
        Assert.AreEqual(
            (DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Proxy, $"www.a.test matches no-proxy entry '{entry}'; connecting directly"),
            log.Lines[0]);
    }

    [TestMethod]
    public void TrySelect_NoProxyText_LogsVerboseThatItConnectsDirectly()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);

        Select(NoEnvironment, "http://a.test/", null, null, log);

        Assert.AreEqual("no proxy set for http://a.test; connecting directly", log.At(DiagnosticLogLevel.Verbose).Single());
    }

    [TestMethod]
    public void TrySelect_FileUrl_LogsVerboseThatItNeverUsesAProxy()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);

        Select(NoEnvironment, "file:///dir/x", "http://proxy.test:3128", null, log);

        Assert.AreEqual("file URL never uses a proxy", log.At(DiagnosticLogLevel.Verbose).Single());
    }

    [TestMethod]
    public void TrySelect_UnusableProxyText_LogsNothing()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);

        bool selected = NoEnvironment.TrySelect(
            CurlUrl.Parse("http://a.test/"), "bogus://proxy.test", ProxyKind.Http, null, out _, out _, log);

        Assert.IsFalse(selected);
        Assert.AreEqual(0, log.Lines.Count);
    }

    [TestMethod]
    [DataRow("http://proxy.test:3128", null)]
    [DataRow("http://proxy.test:3128", "a.test")]
    [DataRow(null, null)]
    public void TrySelect_BelowTheLinesLevel_WritesNothing(string? proxyText, string? noProxy)
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Warning);

        Select(NoEnvironment, "http://a.test/", proxyText, noProxy, log);

        Assert.AreEqual(0, log.Lines.Count);
    }

    [TestMethod]
    public void TrySelect_WithoutALog_StillSelects()
    {
        bool selected = NoEnvironment.TrySelect(
            CurlUrl.Parse("http://a.test/"), "http://proxy.test:3128", ProxyKind.Http, null, out ProxyEndpoint? proxy, out _);

        Assert.IsTrue(selected);
        Assert.AreEqual("proxy.test", proxy?.Host);
    }

    private static void Select(ProxySelector selector, string url, string? proxyText, string? noProxy, RecordingDiagnosticLog log) =>
        Assert.IsTrue(selector.TrySelect(CurlUrl.Parse(url), proxyText, ProxyKind.Http, noProxy, out _, out _, log));
}
