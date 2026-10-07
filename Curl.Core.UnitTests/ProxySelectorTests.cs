using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Core;

/// <summary>
/// Pins which proxy <see cref="ProxySelector" /> chooses, each case measured against curl
/// 8.21.0 on 2026-09-26 as
/// <c>env -u ... &lt;variables&gt; curl -sv --connect-timeout 1 --resolve '*:2222:127.0.0.1' [-x ...] [--noproxy ...] &lt;url&gt;</c>,
/// reading from <c>Trying</c> whether it went to the proxy on 1111 or 3333 or directly to
/// 2222. The environment is always a dictionary; no test reads the real one.
/// </summary>
[TestClass]
public sealed class ProxySelectorTests
{
    private const string Proxy1111 = "http://127.0.0.1:1111";
    private const string Proxy3333 = "http://127.0.0.1:3333";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("http://a.test:2222/", "http_proxy", 1111)]
    [DataRow("https://a.test:2222/", "https_proxy", 1111)]
    [DataRow("https://a.test:2222/", "HTTPS_PROXY", 1111)]
    [DataRow("ftp://a.test:2222/", "FTP_PROXY", 1111)]
    [DataRow("http://a.test:2222/", "all_proxy", 1111)]
    [DataRow("ftp://a.test:2222/", "ALL_PROXY", 1111)]
    [DataRow("dict://a.test:2222/", "all_proxy", 1111)]
    [DataRow("telnet://a.test:2222/", "all_proxy", 1111)]
    [DataRow("gopher://a.test:2222/", "all_proxy", 1111)]
    [DataRow("ws://a.test:2222/", "http_proxy", 1111)]
    [DataRow("wss://a.test:2222/", "https_proxy", 1111)]
    [DataRow("wss://a.test:2222/", "HTTPS_PROXY", 1111)]
    public void TrySelect_OneVariable_UsesIt(string url, string variable, int port)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", url);
        diagnostics.Arrange("variable", variable);

        ProxyEndpoint? proxy = Select(url, CaseSensitive((variable, Proxy1111)));

        diagnostics.Act("proxy", proxy);
        diagnostics.Assert("proxy port", port, proxy?.Port);
        Assert.AreEqual(port, proxy?.Port);
    }

    [TestMethod]
    public void TrySelect_UpperCaseHttpProxyOnACaseSensitiveEnvironment_IsIgnored()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("environment", "HTTP_PROXY=" + Proxy1111 + " (case sensitive)");

        ProxyEndpoint? proxy = Select("http://a.test:2222/", CaseSensitive(("HTTP_PROXY", Proxy1111)));

        diagnostics.Act("proxy", proxy?.ToString() ?? "(null)");
        diagnostics.Assert("proxy", "(null)", proxy?.ToString() ?? "(null)");
        Assert.IsNull(proxy);
    }

    [TestMethod]
    public void TrySelect_UpperCaseHttpProxyOnWindows_IsReadAsHttpProxy()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("environment", "HTTP_PROXY=" + Proxy1111 + " (case insensitive)");

        ProxyEndpoint? proxy = Select("http://a.test:2222/", CaseInsensitive(("HTTP_PROXY", Proxy1111)));

        diagnostics.Act("proxy", proxy);
        diagnostics.Assert("proxy port", 1111, proxy?.Port);
        Assert.AreEqual(1111, proxy?.Port);
    }

    [TestMethod]
    [DataRow("https://a.test:2222/", "https_proxy", "HTTPS_PROXY", 3333)]
    [DataRow("http://a.test:2222/", "http_proxy", "all_proxy", 1111)]
    [DataRow("http://a.test:2222/", "all_proxy", "ALL_PROXY", 1111)]
    [DataRow("ws://a.test:2222/", "ws_proxy", "http_proxy", 1111)]
    [DataRow("wss://a.test:2222/", "https_proxy", "HTTPS_PROXY", 1111)]
    public void TrySelect_TwoVariables_TheEarlierNameWins(string url, string first, string second, int port)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", url);
        diagnostics.Arrange("variables", first + ", " + second);
        string firstProxy = port == 1111 ? Proxy1111 : Proxy3333;
        string secondProxy = port == 1111 ? Proxy3333 : Proxy1111;

        ProxyEndpoint? proxy = Select(url, CaseSensitive((first, firstProxy), (second, secondProxy)));

        diagnostics.Act("proxy", proxy);
        diagnostics.Assert("proxy port", port, proxy?.Port);
        Assert.AreEqual(port, proxy?.Port);
    }

    [TestMethod]
    public void TrySelect_HttpsProxyForAnHttpUrl_IsNotUsed()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("environment", "https_proxy=" + Proxy1111);

        ProxyEndpoint? proxy = Select("http://a.test:2222/", CaseSensitive(("https_proxy", Proxy1111)));

        diagnostics.Act("proxy", proxy?.ToString() ?? "(null)");
        diagnostics.Assert("proxy", "(null)", proxy?.ToString() ?? "(null)");
        Assert.IsNull(proxy);
    }

    [TestMethod]
    public void TrySelect_EmptyVariable_CountsAsUnset()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("environment", "http_proxy=(empty), all_proxy=" + Proxy1111);

        ProxyEndpoint? proxy = Select("http://a.test:2222/", CaseSensitive(("http_proxy", ""), ("all_proxy", Proxy1111)));

        diagnostics.Act("proxy", proxy);
        diagnostics.Assert("proxy port", 1111, proxy?.Port);
        Assert.AreEqual(1111, proxy?.Port);
    }

    [TestMethod]
    public void TrySelect_FileUrl_NeverUsesAProxy()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", "file:///nonexist");
        diagnostics.Arrange("environment", "all_proxy=" + Proxy1111);
        diagnostics.Arrange("proxy option", Proxy1111);

        ProxyEndpoint? proxy = Select("file:///nonexist", CaseSensitive(("all_proxy", Proxy1111)), Proxy1111);

        diagnostics.Act("proxy", proxy?.ToString() ?? "(null)");
        diagnostics.Assert("proxy", "(null)", proxy?.ToString() ?? "(null)");
        Assert.IsNull(proxy);
    }

    [TestMethod]
    public void TrySelect_ProxyOption_WinsOverTheEnvironment()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("environment", "http_proxy=" + Proxy1111);
        diagnostics.Arrange("proxy option", Proxy3333);

        ProxyEndpoint? proxy = Select("http://a.test:2222/", CaseSensitive(("http_proxy", Proxy1111)), Proxy3333);

        diagnostics.Act("proxy", proxy);
        diagnostics.Assert("proxy port", 3333, proxy?.Port);
        Assert.AreEqual(3333, proxy?.Port);
    }

    [TestMethod]
    public void TrySelect_EmptyProxyOption_ConnectsDirectly()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("environment", "http_proxy=" + Proxy1111);
        diagnostics.Arrange("proxy option", "(empty)");

        ProxyEndpoint? proxy = Select("http://a.test:2222/", CaseSensitive(("http_proxy", Proxy1111)), "");

        diagnostics.Act("proxy", proxy?.ToString() ?? "(null)");
        diagnostics.Assert("proxy", "(null)", proxy?.ToString() ?? "(null)");
        Assert.IsNull(proxy);
    }

    [TestMethod]
    [DataRow("no_proxy")]
    [DataRow("NO_PROXY")]
    public void TrySelect_NoProxyVariableNamesTheHost_ConnectsDirectly(string variable)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("environment", "http_proxy=" + Proxy1111 + ", " + variable + "=a.test");

        ProxyEndpoint? proxy = Select("http://a.test:2222/", CaseSensitive(("http_proxy", Proxy1111), (variable, "a.test")));

        diagnostics.Act("proxy", proxy?.ToString() ?? "(null)");
        diagnostics.Assert("proxy", "(null)", proxy?.ToString() ?? "(null)");
        Assert.IsNull(proxy);
    }

    [TestMethod]
    public void TrySelect_LowerCaseNoProxy_WinsOverUpperCase()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("environment", "http_proxy=" + Proxy1111 + ", no_proxy=b.test, NO_PROXY=a.test");

        ProxyEndpoint? proxy = Select(
            "http://a.test:2222/",
            CaseSensitive(("http_proxy", Proxy1111), ("no_proxy", "b.test"), ("NO_PROXY", "a.test")));

        diagnostics.Act("proxy", proxy);
        diagnostics.Assert("proxy port", 1111, proxy?.Port);
        Assert.AreEqual(1111, proxy?.Port);
    }

    [TestMethod]
    [DataRow("a.test")]
    [DataRow("*")]
    public void TrySelect_NoProxyVariable_AppliesToTheProxyOptionToo(string noProxy)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("no_proxy", noProxy);
        diagnostics.Arrange("proxy option", Proxy1111);

        ProxyEndpoint? proxy = Select("http://a.test:2222/", CaseSensitive(("no_proxy", noProxy)), Proxy1111);

        diagnostics.Act("proxy", proxy?.ToString() ?? "(null)");
        diagnostics.Assert("proxy", "(null)", proxy?.ToString() ?? "(null)");
        Assert.IsNull(proxy);
    }

    [TestMethod]
    [DataRow("b.test")]
    [DataRow("")]
    public void TrySelect_NoProxyOption_ReplacesTheVariable(string noProxyOption)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("environment", "http_proxy=" + Proxy1111 + ", no_proxy=a.test");
        diagnostics.Arrange("no-proxy option", noProxyOption.Length == 0 ? "(empty)" : noProxyOption);

        ProxyEndpoint? proxy = Select(
            "http://a.test:2222/",
            CaseSensitive(("http_proxy", Proxy1111), ("no_proxy", "a.test")),
            noProxyOption: noProxyOption);

        diagnostics.Act("proxy", proxy);
        diagnostics.Assert("proxy port", 1111, proxy?.Port);
        Assert.AreEqual(1111, proxy?.Port);
    }

    [TestMethod]
    [DataRow("a.test", "http://A.TEST:2222/")]
    [DataRow("::1", "http://[::1]:2222/")]
    [DataRow("fe80::1", "http://[fe80::1%25eth0]:2222/")]
    public void TrySelect_NoProxyOptionNamesTheHost_ConnectsDirectly(string noProxyOption, string url)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", url);
        diagnostics.Arrange("no-proxy option", noProxyOption);

        ProxyEndpoint? proxy = Select(url, CaseSensitive(("http_proxy", Proxy1111)), Proxy1111, noProxyOption);

        diagnostics.Act("proxy", proxy?.ToString() ?? "(null)");
        diagnostics.Assert("proxy", "(null)", proxy?.ToString() ?? "(null)");
        Assert.IsNull(proxy);
    }

    [TestMethod]
    public void TrySelect_ProxyVariableWithoutAScheme_IsAnHttpProxy()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("environment", "http_proxy=127.0.0.1:1111");

        ProxyEndpoint? proxy = Select("http://a.test:2222/", CaseSensitive(("http_proxy", "127.0.0.1:1111")));

        var expected = new ProxyEndpoint(ProxyKind.Http, "127.0.0.1", 1111, null);
        diagnostics.Act("proxy", proxy);
        diagnostics.Assert("proxy", expected, proxy);
        Assert.AreEqual(expected, proxy);
    }

    [TestMethod]
    public void TrySelect_Socks5OptionWithoutAScheme_IsASocks5ProxyOnPort1080()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("proxy option", "127.0.0.1 (kind Socks5)");
        var selector = new ProxySelector(CaseSensitive());

        bool selected = selector.TrySelect(
            CurlUrl.Parse("http://a.test:2222/"), "127.0.0.1", ProxyKind.Socks5, null, out ProxyEndpoint? proxy, out _);

        var expected = new ProxyEndpoint(ProxyKind.Socks5, "127.0.0.1", 1080, null);
        diagnostics.Act("selected", selected);
        diagnostics.Act("proxy", proxy);
        diagnostics.Assert("selected", true, selected);
        Assert.IsTrue(selected);
        diagnostics.Assert("proxy", expected, proxy);
        Assert.AreEqual(expected, proxy);
    }

    [TestMethod]
    public void TrySelect_Socks4OptionWithAnHttpScheme_IsAnHttpProxy()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("proxy option", "http://127.0.0.1:1111 (kind Socks4)");
        var selector = new ProxySelector(CaseSensitive());

        bool selected = selector.TrySelect(
            CurlUrl.Parse("http://a.test:2222/"), "http://127.0.0.1:1111", ProxyKind.Socks4, null, out ProxyEndpoint? proxy, out _);

        var expected = new ProxyEndpoint(ProxyKind.Http, "127.0.0.1", 1111, null);
        diagnostics.Act("selected", selected);
        diagnostics.Act("proxy", proxy);
        diagnostics.Assert("selected", true, selected);
        Assert.IsTrue(selected);
        diagnostics.Assert("proxy", expected, proxy);
        Assert.AreEqual(expected, proxy);
    }

    [TestMethod]
    public void TrySelect_OptionKindWithoutAnOption_DoesNotApplyToTheEnvironment()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("environment", "http_proxy=127.0.0.1:1111");
        diagnostics.Arrange("proxy option", "(none, kind Socks5)");
        var selector = new ProxySelector(CaseSensitive(("http_proxy", "127.0.0.1:1111")));

        bool selected = selector.TrySelect(
            CurlUrl.Parse("http://a.test:2222/"), null, ProxyKind.Socks5, null, out ProxyEndpoint? proxy, out _);

        var expected = new ProxyEndpoint(ProxyKind.Http, "127.0.0.1", 1111, null);
        diagnostics.Act("selected", selected);
        diagnostics.Act("proxy", proxy);
        diagnostics.Assert("selected", true, selected);
        Assert.IsTrue(selected);
        diagnostics.Assert("proxy", expected, proxy);
        Assert.AreEqual(expected, proxy);
    }

    [TestMethod]
    public void TrySelect_SocksSchemeInTheEnvironment_IsASocks4Proxy()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("environment", "all_proxy=socks://127.0.0.1");

        ProxyEndpoint? proxy = Select("http://a.test:2222/", CaseSensitive(("all_proxy", "socks://127.0.0.1")));

        var expected = new ProxyEndpoint(ProxyKind.Socks4, "127.0.0.1", 1080, null);
        diagnostics.Act("proxy", proxy);
        diagnostics.Assert("proxy", expected, proxy);
        Assert.AreEqual(expected, proxy);
    }

    [TestMethod]
    public void TrySelect_UnusableProxyText_IsItsFailure()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("environment", "http_proxy=foo://127.0.0.1:1111");
        var selector = new ProxySelector(CaseSensitive(("http_proxy", "foo://127.0.0.1:1111")));

        bool selected = selector.TrySelect(CurlUrl.Parse("http://a.test:2222/"), null, null, out ProxyEndpoint? proxy, out TransferResult? failure);

        diagnostics.Act("selected", selected);
        diagnostics.Act("proxy", proxy?.ToString() ?? "(null)");
        diagnostics.Act("failure exit code", failure?.ExitCode);
        diagnostics.Assert("selected", false, selected);
        Assert.IsFalse(selected);
        Assert.IsNull(proxy);
        diagnostics.Assert("failure exit code", CurlExitCode.CouldntConnect, failure!.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, failure!.ExitCode);
    }

    [TestMethod]
    public void TrySelect_NoProxyAnywhere_ConnectsDirectlyWithoutFailure()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("environment", "(empty)");
        var selector = new ProxySelector(CaseSensitive());

        bool selected = selector.TrySelect(CurlUrl.Parse("http://a.test:2222/"), null, null, out ProxyEndpoint? proxy, out TransferResult? failure);

        diagnostics.Act("selected", selected);
        diagnostics.Act("proxy", proxy?.ToString() ?? "(null)");
        diagnostics.Act("failure", failure?.ToString() ?? "(null)");
        diagnostics.Assert("selected", true, selected);
        Assert.IsTrue(selected);
        Assert.IsNull(proxy);
        Assert.IsNull(failure);
    }

    [TestMethod]
    public void Constructor_NullReader_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("reader", "(null)");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => new ProxySelector(null!));

        diagnostics.Act("exception", exception.GetType().Name);
        diagnostics.Assert("exception", nameof(ArgumentNullException), exception.GetType().Name);
    }

    [TestMethod]
    public void TrySelect_NullUrl_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", "(null)");
        var selector = new ProxySelector(CaseSensitive());

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => selector.TrySelect(null!, null, null, out _, out _));

        diagnostics.Act("exception", exception.GetType().Name);
        diagnostics.Assert("exception", nameof(ArgumentNullException), exception.GetType().Name);
    }

    private static ProxyEndpoint? Select(
        string url,
        Func<string, string?> environment,
        string? proxyOption = null,
        string? noProxyOption = null)
    {
        var selector = new ProxySelector(environment);
        Assert.IsTrue(selector.TrySelect(CurlUrl.Parse(url), proxyOption, noProxyOption, out ProxyEndpoint? proxy, out _));
        return proxy;
    }

    private static Func<string, string?> CaseSensitive(params (string Name, string Value)[] variables) =>
        Reader(StringComparer.Ordinal, variables);

    private static Func<string, string?> CaseInsensitive(params (string Name, string Value)[] variables) =>
        Reader(StringComparer.OrdinalIgnoreCase, variables);

    private static Func<string, string?> Reader(StringComparer comparer, (string Name, string Value)[] variables)
    {
        var environment = new Dictionary<string, string>(comparer);
        foreach ((string name, string value) in variables)
        {
            environment[name] = value;
        }

        return name => environment.GetValueOrDefault(name);
    }
}
