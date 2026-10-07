using System.Net;
using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the host and port a <see cref="ProxyEndpoint" /> accepts, with the same checks and
/// exceptions as <see cref="ConnectTarget" /> (ADR-0014).
/// </summary>
[TestClass]
public sealed class ProxyEndpointTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_WithValidValues_RoundTripsEveryValue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var credential = new NetworkCredential("user", "secret");
        diagnostics.Arrange("proxy", "Http proxy.example:3128 user " + credential.UserName);

        var proxy = new ProxyEndpoint(ProxyKind.Http, "proxy.example", 3128, credential);

        diagnostics.Act("kind", proxy.Kind);
        diagnostics.Act("host", proxy.Host);
        diagnostics.Act("port", proxy.Port);
        diagnostics.Assert("host", "proxy.example", proxy.Host);
        Assert.AreEqual(ProxyKind.Http, proxy.Kind);
        Assert.AreEqual("proxy.example", proxy.Host);
        Assert.AreEqual(3128, proxy.Port);
        Assert.AreSame(credential, proxy.Credential);
    }

    [TestMethod]
    public void Constructor_WithNullHost_ThrowsArgumentNullException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        string? host = null;
        diagnostics.Arrange("host", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => new ProxyEndpoint(ProxyKind.Http, host!, 80, null));

        diagnostics.Act("exception parameter", exception.ParamName);
        diagnostics.Assert("parameter name", "Host", exception.ParamName);
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
        diagnostics.Arrange("host", host.Replace("\t", "<tab>", StringComparison.Ordinal));

        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => new ProxyEndpoint(ProxyKind.Http, host, 80, null));

        diagnostics.Act("exception parameter", exception.ParamName);
        diagnostics.Assert("parameter name", "Host", exception.ParamName);
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
            () => new ProxyEndpoint(ProxyKind.Socks5, "proxy.example", port, null));

        diagnostics.Act("exception parameter", exception.ParamName);
        diagnostics.Assert("parameter name", "Port", exception.ParamName);
        Assert.AreEqual("Port", exception.ParamName);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(65535)]
    public void Constructor_WithPortAtRangeBound_Accepts(int port)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("port", port);

        var proxy = new ProxyEndpoint(ProxyKind.Socks4, "proxy.example", port, null);

        diagnostics.Act("port", proxy.Port);
        diagnostics.Assert("port", port, proxy.Port);
        Assert.AreEqual(port, proxy.Port);
    }

    [TestMethod]
    public void Equals_ForTwoEndpointsBuiltTheSameWay_ReturnsTrue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("endpoint", "Https proxy.example:443");
        var first = new ProxyEndpoint(ProxyKind.Https, "proxy.example", 443, null);
        var second = new ProxyEndpoint(ProxyKind.Https, "proxy.example", 443, null);

        diagnostics.Act("equal", first.Equals(second));
        diagnostics.Act("hash codes equal", first.GetHashCode() == second.GetHashCode());
        diagnostics.Assert("equal", true, first.Equals(second));
        Assert.AreEqual(first, second);
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
    }

    [TestMethod]
    public void With_ChangingKind_KeepsHostAndPort()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("endpoint", "Http proxy.example:8080");
        var http = new ProxyEndpoint(ProxyKind.Http, "proxy.example", 8080, null);

        var http10 = http with { Kind = ProxyKind.Http10 };

        diagnostics.Act("kind", http10.Kind);
        diagnostics.Act("host and port", $"{http10.Host}:{http10.Port}");
        diagnostics.Assert("kind", ProxyKind.Http10, http10.Kind);
        Assert.AreEqual(ProxyKind.Http10, http10.Kind);
        Assert.AreEqual("proxy.example", http10.Host);
        Assert.AreEqual(8080, http10.Port);
        Assert.AreNotEqual(http, http10);
    }

    [TestMethod]
    public void With_AddingCredential_KeepsKindHostAndPort()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("endpoint", "Socks5 proxy.example:1080");
        var anonymous = new ProxyEndpoint(ProxyKind.Socks5, "proxy.example", 1080, null);
        var credential = new NetworkCredential("user", "secret");

        var authenticated = anonymous with { Credential = credential };

        diagnostics.Act("kind", authenticated.Kind);
        diagnostics.Act("host and port", $"{authenticated.Host}:{authenticated.Port}");
        diagnostics.Act("credential user", authenticated.Credential?.UserName);
        diagnostics.Assert("kind", ProxyKind.Socks5, authenticated.Kind);
        Assert.AreSame(credential, authenticated.Credential);
        Assert.AreEqual(ProxyKind.Socks5, authenticated.Kind);
        Assert.AreEqual("proxy.example", authenticated.Host);
        Assert.AreEqual(1080, authenticated.Port);
    }

    [TestMethod]
    public void With_AnyChange_CannotBypassHostAndPortChecks()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("endpoint", "Http proxy.example:3128");
        var proxy = new ProxyEndpoint(ProxyKind.Http, "proxy.example", 3128, null);

        var copy = proxy with { Kind = ProxyKind.Https, Credential = null };

        var hostSetter = typeof(ProxyEndpoint).GetProperty(nameof(ProxyEndpoint.Host))!.SetMethod;
        var portSetter = typeof(ProxyEndpoint).GetProperty(nameof(ProxyEndpoint.Port))!.SetMethod;
        diagnostics.Act("host has setter", hostSetter is not null);
        diagnostics.Act("port has setter", portSetter is not null);
        diagnostics.Act("copy host and port", $"{copy.Host}:{copy.Port}");
        diagnostics.Assert("host has setter", false, hostSetter is not null);

        // Host and Port have no init accessor, so the only way to a new value is the
        // positional constructor, which validates it.
        Assert.IsNull(typeof(ProxyEndpoint).GetProperty(nameof(ProxyEndpoint.Host))!.SetMethod);
        Assert.IsNull(typeof(ProxyEndpoint).GetProperty(nameof(ProxyEndpoint.Port))!.SetMethod);
        Assert.AreEqual("proxy.example", copy.Host);
        Assert.AreEqual(3128, copy.Port);
    }
}
