using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the host and port a <see cref="ProxyEndpoint" /> accepts, with the same checks and
/// exceptions as <see cref="ConnectTarget" /> (ADR-0014).
/// </summary>
[TestClass]
public sealed class ProxyEndpointTests
{
    [TestMethod]
    public void Constructor_WithValidValues_RoundTripsEveryValue()
    {
        var credential = new NetworkCredential("user", "secret");

        var proxy = new ProxyEndpoint(ProxyKind.Http, "proxy.example", 3128, credential);

        Assert.AreEqual(ProxyKind.Http, proxy.Kind);
        Assert.AreEqual("proxy.example", proxy.Host);
        Assert.AreEqual(3128, proxy.Port);
        Assert.AreSame(credential, proxy.Credential);
    }

    [TestMethod]
    public void Constructor_WithNullHost_ThrowsArgumentNullException()
    {
        string? host = null;

        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => new ProxyEndpoint(ProxyKind.Http, host!, 80, null));

        Assert.AreEqual("Host", exception.ParamName);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("\t")]
    public void Constructor_WithEmptyOrWhitespaceHost_ThrowsArgumentException(string host)
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => new ProxyEndpoint(ProxyKind.Http, host, 80, null));

        Assert.AreEqual("Host", exception.ParamName);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(65536)]
    public void Constructor_WithPortOutsideRange_ThrowsArgumentOutOfRangeException(int port)
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new ProxyEndpoint(ProxyKind.Socks5, "proxy.example", port, null));

        Assert.AreEqual("Port", exception.ParamName);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(65535)]
    public void Constructor_WithPortAtRangeBound_Accepts(int port)
    {
        var proxy = new ProxyEndpoint(ProxyKind.Socks4, "proxy.example", port, null);

        Assert.AreEqual(port, proxy.Port);
    }

    [TestMethod]
    public void Equals_ForTwoEndpointsBuiltTheSameWay_ReturnsTrue()
    {
        var first = new ProxyEndpoint(ProxyKind.Https, "proxy.example", 443, null);
        var second = new ProxyEndpoint(ProxyKind.Https, "proxy.example", 443, null);

        Assert.AreEqual(first, second);
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
    }

    [TestMethod]
    public void With_ChangingKind_KeepsHostAndPort()
    {
        var http = new ProxyEndpoint(ProxyKind.Http, "proxy.example", 8080, null);

        var http10 = http with { Kind = ProxyKind.Http10 };

        Assert.AreEqual(ProxyKind.Http10, http10.Kind);
        Assert.AreEqual("proxy.example", http10.Host);
        Assert.AreEqual(8080, http10.Port);
        Assert.AreNotEqual(http, http10);
    }

    [TestMethod]
    public void With_AddingCredential_KeepsKindHostAndPort()
    {
        var anonymous = new ProxyEndpoint(ProxyKind.Socks5, "proxy.example", 1080, null);
        var credential = new NetworkCredential("user", "secret");

        var authenticated = anonymous with { Credential = credential };

        Assert.AreSame(credential, authenticated.Credential);
        Assert.AreEqual(ProxyKind.Socks5, authenticated.Kind);
        Assert.AreEqual("proxy.example", authenticated.Host);
        Assert.AreEqual(1080, authenticated.Port);
    }

    [TestMethod]
    public void With_AnyChange_CannotBypassHostAndPortChecks()
    {
        var proxy = new ProxyEndpoint(ProxyKind.Http, "proxy.example", 3128, null);

        var copy = proxy with { Kind = ProxyKind.Https, Credential = null };

        // Host and Port have no init accessor, so the only way to a new value is the
        // positional constructor, which validates it.
        Assert.IsNull(typeof(ProxyEndpoint).GetProperty(nameof(ProxyEndpoint.Host))!.SetMethod);
        Assert.IsNull(typeof(ProxyEndpoint).GetProperty(nameof(ProxyEndpoint.Port))!.SetMethod);
        Assert.AreEqual("proxy.example", copy.Host);
        Assert.AreEqual(3128, copy.Port);
    }
}
