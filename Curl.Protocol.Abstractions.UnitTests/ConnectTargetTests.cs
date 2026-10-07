using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the host and port a <see cref="ConnectTarget" /> accepts, at both ends of the
/// port range.
/// </summary>
[TestClass]
public sealed class ConnectTargetTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_WithValidValues_RoundTripsEveryValue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("host", "example.com");
        diagnostics.Arrange("port", 443);
        diagnostics.Arrange("use tls", true);

        var target = new ConnectTarget("example.com", 443, true);

        diagnostics.Act("target", target);
        diagnostics.Assert("host", "example.com", target.Host);
        Assert.AreEqual("example.com", target.Host);
        Assert.AreEqual(443, target.Port);
        Assert.IsTrue(target.UseTls);
    }

    [TestMethod]
    public void Constructor_WithNullHost_ThrowsArgumentNullException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        string? host = null;
        diagnostics.Arrange("host", host);

        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => new ConnectTarget(host!, 80, false));

        diagnostics.Act("param name", exception.ParamName);
        diagnostics.Assert("param name", "Host", exception.ParamName);
        Assert.AreEqual("Host", exception.ParamName);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("\t")]
    public void Constructor_WithEmptyOrWhitespaceHost_ThrowsArgumentException(string host)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("host length", host.Length);
        diagnostics.Arrange("host code points", string.Join(' ', host.Select(character => ((int)character).ToString("x4"))));

        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => new ConnectTarget(host, 80, false));

        diagnostics.Act("param name", exception.ParamName);
        diagnostics.Assert("param name", "Host", exception.ParamName);
        Assert.AreEqual("Host", exception.ParamName);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(65536)]
    public void Constructor_WithPortOutsideRange_ThrowsArgumentOutOfRangeException(int port)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("port", port);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new ConnectTarget("example.com", port, false));

        diagnostics.Act("param name", exception.ParamName);
        diagnostics.Assert("param name", "Port", exception.ParamName);
        Assert.AreEqual("Port", exception.ParamName);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(65535)]
    public void Constructor_WithPortAtRangeBound_Accepts(int port)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("port", port);

        var target = new ConnectTarget("example.com", port, false);

        diagnostics.Act("port", target.Port);
        diagnostics.Assert("port", port, target.Port);
        Assert.AreEqual(port, target.Port);
    }

    [TestMethod]
    public void Equals_ForTwoTargetsBuiltTheSameWay_ReturnsTrue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("target", "example.com:21 without tls, built twice");

        var first = new ConnectTarget("example.com", 21, false);
        var second = new ConnectTarget("example.com", 21, false);

        diagnostics.Act("equal", first.Equals(second));
        diagnostics.Act("hash codes equal", first.GetHashCode() == second.GetHashCode());
        diagnostics.Assert("equal", true, first.Equals(second));
        Assert.AreEqual(first, second);
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
    }

    [TestMethod]
    public void With_ChangingUseTls_KeepsHostAndPort()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var plain = new ConnectTarget("example.com", 21, false);
        diagnostics.Arrange("plain target", plain);

        var secure = plain with { UseTls = true };

        diagnostics.Act("secure target", secure);
        diagnostics.Assert("port", 21, secure.Port);
        Assert.AreEqual("example.com", secure.Host);
        Assert.AreEqual(21, secure.Port);
        Assert.IsTrue(secure.UseTls);
    }

    [TestMethod]
    public void Proxy_WhenNotSet_IsNull()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("proxy", "not set");

        var target = new ConnectTarget("example.com", 443, true);

        diagnostics.Act("proxy", target.Proxy);
        diagnostics.Assert("proxy", null, target.Proxy);
        Assert.IsNull(target.Proxy);
    }

    [TestMethod]
    public void Proxy_WhenSetWithInitializer_RoundTrips()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var proxy = new ProxyEndpoint(ProxyKind.Socks5Hostname, "proxy.example", 1080, null);
        diagnostics.Arrange("proxy", proxy);

        var target = new ConnectTarget("example.com", 443, true) { Proxy = proxy };

        diagnostics.Act("proxy", target.Proxy);
        diagnostics.Assert("same proxy", true, ReferenceEquals(proxy, target.Proxy));
        Assert.AreSame(proxy, target.Proxy);
        Assert.AreEqual("example.com", target.Host);
        Assert.AreEqual(443, target.Port);
    }

    [TestMethod]
    public void Equals_ForTargetsDifferingOnlyInProxy_ReturnsFalse()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var direct = new ConnectTarget("example.com", 443, true);
        var proxied = direct with { Proxy = new ProxyEndpoint(ProxyKind.Http, "proxy.example", 3128, null) };
        diagnostics.Arrange("direct", direct);
        diagnostics.Arrange("proxied", proxied);

        bool equal = direct.Equals(proxied);

        diagnostics.Act("equal", equal);
        diagnostics.Assert("equal", false, equal);
        Assert.AreNotEqual(direct, proxied);
    }

    [TestMethod]
    public void PoolScheme_WhenNotSet_IsNull()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("pool scheme", "not set");

        var target = new ConnectTarget("example.com", 443, true);

        diagnostics.Act("pool scheme", target.PoolScheme);
        diagnostics.Assert("pool scheme", null, target.PoolScheme);
        Assert.IsNull(target.PoolScheme);
    }

    [TestMethod]
    public void PoolScheme_WhenSetWithInitializer_RoundTrips()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("pool scheme", "https");

        var target = new ConnectTarget("example.com", 443, true) { PoolScheme = "https" };

        diagnostics.Act("pool scheme", target.PoolScheme);
        diagnostics.Assert("pool scheme", "https", target.PoolScheme);
        Assert.AreEqual("https", target.PoolScheme);
    }

    [TestMethod]
    public void IsForwardProxy_WhenNotSet_IsFalse()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("is forward proxy", "not set");

        var target = new ConnectTarget("example.com", 80, false);

        diagnostics.Act("is forward proxy", target.IsForwardProxy);
        diagnostics.Assert("is forward proxy", false, target.IsForwardProxy);
        Assert.IsFalse(target.IsForwardProxy);
    }

    [TestMethod]
    public void IsForwardProxy_WhenSetWithInitializer_RoundTrips()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("is forward proxy", true);

        var target = new ConnectTarget("proxy.example", 3128, false) { IsForwardProxy = true };

        diagnostics.Act("is forward proxy", target.IsForwardProxy);
        diagnostics.Assert("is forward proxy", true, target.IsForwardProxy);
        Assert.IsTrue(target.IsForwardProxy);
    }

    [TestMethod]
    public void Events_WhenNotSet_IsNoTransferEvents()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("events", "not set");

        var target = new ConnectTarget("example.com", 443, true);

        diagnostics.Act("events type", target.Events.GetType().Name);
        diagnostics.Assert("events type", nameof(NoTransferEvents), target.Events.GetType().Name);
        Assert.AreSame(NoTransferEvents.Instance, target.Events);
    }

    [TestMethod]
    public void Events_WhenSetWithInitializer_RoundTrips()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var events = new StubTransferEvents();
        diagnostics.Arrange("events type", events.GetType().Name);

        var target = new ConnectTarget("example.com", 443, true) { Events = events };

        diagnostics.Act("events type", target.Events.GetType().Name);
        diagnostics.Assert("same events", true, ReferenceEquals(events, target.Events));
        Assert.AreSame(events, target.Events);
    }

    [TestMethod]
    public void ApplicationProtocols_ByDefault_IsNullForTheConnectorsOwnList()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("application protocols", "not set");

        var target = new ConnectTarget("example.com", 443, true);

        diagnostics.Act("application protocols", target.ApplicationProtocols);
        diagnostics.Assert("application protocols", null, target.ApplicationProtocols);
        Assert.IsNull(target.ApplicationProtocols);
    }

    [TestMethod]
    public void ApplicationProtocols_WhenSetWithInitializer_RoundTrips()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        IReadOnlyList<string> protocols = ["h2"];
        diagnostics.Arrange("application protocols", string.Join(',', protocols));

        var target = new ConnectTarget("example.com", 443, true) { ApplicationProtocols = protocols };

        diagnostics.Act("application protocol count", target.ApplicationProtocols!.Count);
        diagnostics.Assert("same list", true, ReferenceEquals(protocols, target.ApplicationProtocols));
        Assert.AreSame(protocols, target.ApplicationProtocols);
    }

    [TestMethod]
    public void With_AnyChange_CannotBypassHostAndPortChecks()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var target = new ConnectTarget("example.com", 443, true);
        diagnostics.Arrange("target", target);

        var copy = target with { UseTls = false, Proxy = null };

        diagnostics.Act("copy", copy);
        diagnostics.Assert("port", 443, copy.Port);

        // Host and Port have no init accessor, so the only way to a new value is the
        // positional constructor, which validates it.
        Assert.IsNull(typeof(ConnectTarget).GetProperty(nameof(ConnectTarget.Host))!.SetMethod);
        Assert.IsNull(typeof(ConnectTarget).GetProperty(nameof(ConnectTarget.Port))!.SetMethod);
        Assert.AreEqual("example.com", copy.Host);
        Assert.AreEqual(443, copy.Port);
    }
}
