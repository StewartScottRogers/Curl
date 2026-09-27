namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the host and port a <see cref="ConnectTarget" /> accepts, at both ends of the
/// port range.
/// </summary>
[TestClass]
public sealed class ConnectTargetTests
{
    [TestMethod]
    public void Constructor_WithValidValues_RoundTripsEveryValue()
    {
        var target = new ConnectTarget("example.com", 443, true);

        Assert.AreEqual("example.com", target.Host);
        Assert.AreEqual(443, target.Port);
        Assert.IsTrue(target.UseTls);
    }

    [TestMethod]
    public void Constructor_WithNullHost_ThrowsArgumentNullException()
    {
        string? host = null;

        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => new ConnectTarget(host!, 80, false));

        Assert.AreEqual("Host", exception.ParamName);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("\t")]
    public void Constructor_WithEmptyOrWhitespaceHost_ThrowsArgumentException(string host)
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => new ConnectTarget(host, 80, false));

        Assert.AreEqual("Host", exception.ParamName);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(65536)]
    public void Constructor_WithPortOutsideRange_ThrowsArgumentOutOfRangeException(int port)
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new ConnectTarget("example.com", port, false));

        Assert.AreEqual("Port", exception.ParamName);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(65535)]
    public void Constructor_WithPortAtRangeBound_Accepts(int port)
    {
        var target = new ConnectTarget("example.com", port, false);

        Assert.AreEqual(port, target.Port);
    }

    [TestMethod]
    public void Equals_ForTwoTargetsBuiltTheSameWay_ReturnsTrue()
    {
        var first = new ConnectTarget("example.com", 21, false);
        var second = new ConnectTarget("example.com", 21, false);

        Assert.AreEqual(first, second);
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
    }

    [TestMethod]
    public void With_ChangingUseTls_KeepsHostAndPort()
    {
        var plain = new ConnectTarget("example.com", 21, false);

        var secure = plain with { UseTls = true };

        Assert.AreEqual("example.com", secure.Host);
        Assert.AreEqual(21, secure.Port);
        Assert.IsTrue(secure.UseTls);
    }

    [TestMethod]
    public void Proxy_WhenNotSet_IsNull()
    {
        var target = new ConnectTarget("example.com", 443, true);

        Assert.IsNull(target.Proxy);
    }

    [TestMethod]
    public void Proxy_WhenSetWithInitializer_RoundTrips()
    {
        var proxy = new ProxyEndpoint(ProxyKind.Socks5Hostname, "proxy.example", 1080, null);

        var target = new ConnectTarget("example.com", 443, true) { Proxy = proxy };

        Assert.AreSame(proxy, target.Proxy);
        Assert.AreEqual("example.com", target.Host);
        Assert.AreEqual(443, target.Port);
    }

    [TestMethod]
    public void Equals_ForTargetsDifferingOnlyInProxy_ReturnsFalse()
    {
        var direct = new ConnectTarget("example.com", 443, true);
        var proxied = direct with { Proxy = new ProxyEndpoint(ProxyKind.Http, "proxy.example", 3128, null) };

        Assert.AreNotEqual(direct, proxied);
    }

    [TestMethod]
    public void PoolScheme_WhenNotSet_IsNull()
    {
        var target = new ConnectTarget("example.com", 443, true);

        Assert.IsNull(target.PoolScheme);
    }

    [TestMethod]
    public void PoolScheme_WhenSetWithInitializer_RoundTrips()
    {
        var target = new ConnectTarget("example.com", 443, true) { PoolScheme = "https" };

        Assert.AreEqual("https", target.PoolScheme);
    }

    [TestMethod]
    public void Events_WhenNotSet_IsNoTransferEvents()
    {
        var target = new ConnectTarget("example.com", 443, true);

        Assert.AreSame(NoTransferEvents.Instance, target.Events);
    }

    [TestMethod]
    public void Events_WhenSetWithInitializer_RoundTrips()
    {
        var events = new StubTransferEvents();

        var target = new ConnectTarget("example.com", 443, true) { Events = events };

        Assert.AreSame(events, target.Events);
    }

    [TestMethod]
    public void With_AnyChange_CannotBypassHostAndPortChecks()
    {
        var target = new ConnectTarget("example.com", 443, true);

        var copy = target with { UseTls = false, Proxy = null };

        // Host and Port have no init accessor, so the only way to a new value is the
        // positional constructor, which validates it.
        Assert.IsNull(typeof(ConnectTarget).GetProperty(nameof(ConnectTarget.Host))!.SetMethod);
        Assert.IsNull(typeof(ConnectTarget).GetProperty(nameof(ConnectTarget.Port))!.SetMethod);
        Assert.AreEqual("example.com", copy.Host);
        Assert.AreEqual(443, copy.Port);
    }
}
