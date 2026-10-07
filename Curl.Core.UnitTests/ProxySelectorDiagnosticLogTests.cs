using Curl.Core.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void TrySelect_ProxyChosen_LogsInfoWithSchemeHostAndPortAndNoPassword()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("proxy text", "http://user:secret@proxy.test:3128");
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);

        Select(NoEnvironment, "http://a.test/", "http://user:secret@proxy.test:3128", null, log);

        diagnostics.Act("log lines", string.Join(" | ", log.Lines.Select(line => $"{line.Level} {line.Message}")));
        diagnostics.Assert("line count", 1, log.Lines.Count);
        Assert.AreEqual(1, log.Lines.Count);
        diagnostics.Assert("first line", $"Info {DiagnosticLogComponents.Proxy} using proxy http://proxy.test:3128 for http://a.test", $"{log.Lines[0].Level} {log.Lines[0].Component} {log.Lines[0].Message}");
        Assert.AreEqual((DiagnosticLogLevel.Info, DiagnosticLogComponents.Proxy, "using proxy http://proxy.test:3128 for http://a.test"), log.Lines[0]);
        diagnostics.Assert("message contains secret", false, log.Lines[0].Message.Contains("secret", StringComparison.Ordinal));
        Assert.IsFalse(log.Lines[0].Message.Contains("secret", StringComparison.Ordinal));
        diagnostics.Assert("message contains user", false, log.Lines[0].Message.Contains("user", StringComparison.Ordinal));
        Assert.IsFalse(log.Lines[0].Message.Contains("user", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("socks5h://proxy.test", "socks5h://proxy.test:1080")]
    [DataRow("socks4a://proxy.test:9", "socks4a://proxy.test:9")]
    [DataRow("https://[::1]:8443", "https://[::1]:8443")]
    public void TrySelect_ProxyChosen_NamesItsKindAsAScheme(string proxyText, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("proxy text", proxyText);
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Info);

        Select(NoEnvironment, "http://a.test/", proxyText, null, log);

        string message = log.At(DiagnosticLogLevel.Info).Single();
        diagnostics.Act("info message", message);
        diagnostics.Assert("info message", $"using proxy {expected} for http://a.test", message);
        Assert.AreEqual($"using proxy {expected} for http://a.test", message);
    }

    [TestMethod]
    public void TrySelect_ProxyFromTheEnvironment_LogsIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("environment", "https_proxy=proxy.test:8080");
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Info);
        ProxySelector selector = new(name => name == "https_proxy" ? "proxy.test:8080" : null);

        Select(selector, "https://a.test/", null, null, log);

        string message = log.At(DiagnosticLogLevel.Info).Single();
        diagnostics.Act("info message", message);
        diagnostics.Assert("info message", "using proxy http://proxy.test:8080 for https://a.test", message);
        Assert.AreEqual("using proxy http://proxy.test:8080 for https://a.test", message);
    }

    [TestMethod]
    [DataRow("other.test, .a.test", ".a.test")]
    [DataRow("*", "*")]
    public void TrySelect_NoProxyMatch_LogsVerboseNamingTheEntry(string noProxy, string entry)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("no_proxy", noProxy);
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);

        Select(NoEnvironment, "http://www.a.test/", "http://proxy.test:3128", noProxy, log);

        diagnostics.Act("log lines", string.Join(" | ", log.Lines.Select(line => $"{line.Level} {line.Message}")));
        diagnostics.Assert("line count", 1, log.Lines.Count);
        Assert.AreEqual(1, log.Lines.Count);
        diagnostics.Assert("first line", $"Verbose {DiagnosticLogComponents.Proxy} www.a.test matches no-proxy entry '{entry}'; connecting directly", $"{log.Lines[0].Level} {log.Lines[0].Component} {log.Lines[0].Message}");
        Assert.AreEqual(
            (DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Proxy, $"www.a.test matches no-proxy entry '{entry}'; connecting directly"),
            log.Lines[0]);
    }

    [TestMethod]
    public void TrySelect_NoProxyText_LogsVerboseThatItConnectsDirectly()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("proxy text", "(none)");
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);

        Select(NoEnvironment, "http://a.test/", null, null, log);

        string message = log.At(DiagnosticLogLevel.Verbose).Single();
        diagnostics.Act("verbose message", message);
        diagnostics.Assert("verbose message", "no proxy set for http://a.test; connecting directly", message);
        Assert.AreEqual("no proxy set for http://a.test; connecting directly", message);
    }

    [TestMethod]
    public void TrySelect_FileUrl_LogsVerboseThatItNeverUsesAProxy()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", "file:///dir/x");
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);

        Select(NoEnvironment, "file:///dir/x", "http://proxy.test:3128", null, log);

        string message = log.At(DiagnosticLogLevel.Verbose).Single();
        diagnostics.Act("verbose message", message);
        diagnostics.Assert("verbose message", "file URL never uses a proxy", message);
        Assert.AreEqual("file URL never uses a proxy", message);
    }

    [TestMethod]
    public void TrySelect_UnusableProxyText_LogsNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("proxy text", "bogus://proxy.test");
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);

        bool selected = NoEnvironment.TrySelect(
            CurlUrl.Parse("http://a.test/"), "bogus://proxy.test", ProxyKind.Http, null, out _, out _, log);

        diagnostics.Act("selected", selected);
        diagnostics.Act("log lines", string.Join(" | ", log.Lines.Select(line => $"{line.Level} {line.Message}")));
        diagnostics.Assert("selected", false, selected);
        Assert.IsFalse(selected);
        diagnostics.Assert("line count", 0, log.Lines.Count);
        Assert.AreEqual(0, log.Lines.Count);
    }

    [TestMethod]
    [DataRow("http://proxy.test:3128", null)]
    [DataRow("http://proxy.test:3128", "a.test")]
    [DataRow(null, null)]
    public void TrySelect_BelowTheLinesLevel_WritesNothing(string? proxyText, string? noProxy)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("proxy text", proxyText ?? "(null)");
        diagnostics.Arrange("no_proxy", noProxy ?? "(null)");
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Warning);

        Select(NoEnvironment, "http://a.test/", proxyText, noProxy, log);

        diagnostics.Act("log lines", string.Join(" | ", log.Lines.Select(line => $"{line.Level} {line.Message}")));
        diagnostics.Assert("line count", 0, log.Lines.Count);
        Assert.AreEqual(0, log.Lines.Count);
    }

    [TestMethod]
    public void TrySelect_WithoutALog_StillSelects()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("proxy text", "http://proxy.test:3128");

        bool selected = NoEnvironment.TrySelect(
            CurlUrl.Parse("http://a.test/"), "http://proxy.test:3128", ProxyKind.Http, null, out ProxyEndpoint? proxy, out _);

        diagnostics.Act("selected", selected);
        diagnostics.Act("proxy", proxy);
        diagnostics.Assert("selected", true, selected);
        Assert.IsTrue(selected);
        diagnostics.Assert("proxy host", "proxy.test", proxy?.Host);
        Assert.AreEqual("proxy.test", proxy?.Host);
    }

    private static void Select(ProxySelector selector, string url, string? proxyText, string? noProxy, RecordingDiagnosticLog log) =>
        Assert.IsTrue(selector.TrySelect(CurlUrl.Parse(url), proxyText, ProxyKind.Http, noProxy, out _, out _, log));
}
