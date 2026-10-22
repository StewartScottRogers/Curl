namespace Curl.Core;

/// <summary>Pins <see cref="ConnectionPeer" /> to the rows of curl 8.22.0's <c>tests/unit/unit2413.c</c>.</summary>
[TestClass]
public sealed class ConnectionPeerTests
{
    [TestMethod]
    [DataRow("test.curl.se", "test.curl.se", false, null)]
    [DataRow("127.0.0.1", "127.0.0.1", false, null)]
    [DataRow("::1", "::1", true, null)]
    [DataRow("[::1]", "::1", true, null)]
    [DataRow("test.curl.se.", "test.curl.se.", false, null)]
    [DataRow("[::1%tada]", "::1", true, "tada")]
    [DataRow("::1%tada", "::1", true, "tada")]
    public void Create_UpstreamHostStrings_GiveUpstreamsHostnameIpv6FlagAndZone(string host, string hostname, bool isIpv6, string? zoneId)
    {
        ConnectionPeer peer = ConnectionPeer.Create("https", host, 1234);

        Assert.AreEqual(new ConnectionPeer("https", host, hostname, 1234, isIpv6, zoneId), peer);
    }

    [TestMethod]
    [DataRow("[test.curl.se]")]
    [DataRow("[::1")]
    [DataRow("::1]")]
    [DataRow("name%zone")]
    public void Create_HostThatIsNoIpv6Address_KeepsItsTextWholeWithNoZone(string host)
    {
        ConnectionPeer peer = ConnectionPeer.Create("http", host, 80);

        Assert.AreEqual(new ConnectionPeer("http", host, host, 80, false, null), peer);
    }
}
