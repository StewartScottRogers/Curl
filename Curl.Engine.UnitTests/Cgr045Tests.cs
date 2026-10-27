using System.Net;
using System.Net.Sockets;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// CGR-045: curl 8.22.0 given a <c>--doh-url</c> it cannot use (<c>"a b"</c>, <c>ftp://...</c>) still
/// connects to an IP literal and to <c>localhost</c>, which need no look-up, and fails only a name
/// with exit 6 (measured: <c>curl -sS --doh-url "a b" http://127.0.0.1:20002/</c> is exit 7, refused).
/// </summary>
[TestClass]
public sealed class Cgr045Tests
{
    [TestMethod]
    [DataRow("a b")]
    [DataRow("ftp://127.0.0.1:20002/")]
    public async Task CreateDohResolver_WithAnUnusableUrl_AnswersAnIpv4LiteralWithItself(string dohUrl)
    {
        IDnsResolver resolver = EngineComposition.CreateDohResolver(dohUrl, new ScriptedConnector([]), AddressFamily.Unspecified);

        var addresses = await resolver.ResolveAsync("127.0.0.1", CancellationToken.None);

        Assert.AreEqual(IPAddress.Loopback, addresses.Single());
    }

    [TestMethod]
    [DataRow("::1")]
    [DataRow("[::1]")]
    public async Task UnusableDohUrlResolver_AnswersAnIpv6LiteralWithItself(string host)
    {
        var addresses = await new UnusableDohUrlResolver().ResolveAsync(host, CancellationToken.None);

        Assert.AreEqual(IPAddress.IPv6Loopback, addresses.Single());
    }

    [TestMethod]
    [DataRow("localhost")]
    [DataRow("LocalHost")]
    [DataRow("a.localhost")]
    public async Task UnusableDohUrlResolver_AnswersLocalhostWithTheLoopbackPair(string host)
    {
        var addresses = await new UnusableDohUrlResolver().ResolveAsync(host, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { IPAddress.IPv6Loopback, IPAddress.Loopback }, addresses.ToArray());
    }

    [TestMethod]
    public async Task UnusableDohUrlResolver_AnswersANameWithNoAddress()
    {
        var addresses = await new UnusableDohUrlResolver().ResolveAsync("example.invalid", CancellationToken.None);

        Assert.IsEmpty(addresses);
    }
}
