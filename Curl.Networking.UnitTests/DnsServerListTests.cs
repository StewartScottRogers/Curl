using System.Net;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="DnsServerList" /> to the <c>--dns-servers</c> values curl 8.22.0's c-ares
/// 1.34.8 build accepted and refused when it resolved a name (measured, BL-643 and BL-694).
/// </summary>
[TestClass]
public sealed class DnsServerListTests
{
    [TestMethod]
    [DataRow("192.0.2.1", "192.0.2.1:53")]
    [DataRow("192.0.2.1:5353", "192.0.2.1:5353")]
    [DataRow("192.0.2.1:0", "192.0.2.1:53")]
    [DataRow("::1", "[::1]:53")]
    [DataRow("[::1]", "[::1]:53")]
    [DataRow("[2001:db8::1]:5353", "[2001:db8::1]:5353")]
    [DataRow(" 192.0.2.1", "192.0.2.1:53")]
    [DataRow("192.0.2.1,", "192.0.2.1:53")]
    public void TryParse_OneEntry_ReturnsItsEndPoint(string text, string expected)
    {
        Assert.IsTrue(DnsServerList.TryParse(text, out var servers));

        Assert.AreEqual(expected, servers.Single().ToString());
    }

    [TestMethod]
    public void TryParse_SeveralEntries_KeepsTheirOrderAndTrimsSpaces()
    {
        Assert.IsTrue(DnsServerList.TryParse("192.0.2.1:15380, [::1]:53 ,192.0.2.2", out var servers));

        CollectionAssert.AreEqual(
            new[] { "192.0.2.1:15380", "[::1]:53", "192.0.2.2:53" },
            servers.Select(server => server.ToString()).ToArray());
    }

    [TestMethod]
    [DataRow("bogus")]
    [DataRow("localhost:15380")]
    [DataRow("1.2.3.4:99999")]
    [DataRow("1.2.3.4;5.6.7.8")]
    [DataRow("1.2.3")]
    [DataRow("1.2.3.4:x")]
    [DataRow("1.2.3.4:+53")]
    [DataRow("::1:53:zz")]
    [DataRow("[::1")]
    [DataRow("[192.0.2.1]")]
    [DataRow("[::1]53")]
    [DataRow("2001:db8::1:")]
    [DataRow(",")]
    [DataRow("")]
    public void TryParse_AMalformedList_IsRefused(string text)
    {
        Assert.IsFalse(DnsServerList.TryParse(text, out var servers));

        Assert.IsNull(servers);
    }

    [TestMethod]
    public void TryParse_WithNull_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => DnsServerList.TryParse(null!, out _));
    }

    [TestMethod]
    public void TryParseAddress_AShortIPv4Form_IsRefusedAsInetPtonRefusesIt()
    {
        Assert.IsFalse(DnsServerList.TryParseAddress("127.1", System.Net.Sockets.AddressFamily.InterNetwork, out _));
        Assert.IsTrue(DnsServerList.TryParseAddress("127.0.0.1", System.Net.Sockets.AddressFamily.InterNetwork, out var address));
        Assert.AreEqual(IPAddress.Loopback, address);
    }
}
