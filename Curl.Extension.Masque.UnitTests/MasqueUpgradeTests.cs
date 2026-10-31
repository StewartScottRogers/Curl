using System.Text;
using Curl.Extension.Masque.Wire;

namespace Curl.Extension.Masque;

/// <summary>Checks the HTTP/1.1 upgrade of RFC 9298 sections 3.2 and 3.3 and RFC 9484 sections 4.5 and 4.6.</summary>
[TestClass]
public sealed class MasqueUpgradeTests
{
    [TestMethod]
    public void BuildRequest_ConnectUdp_IsTheRfc9298Request() =>
        Assert.AreEqual(
            "GET https://example.org/.well-known/masque/udp/192.0.2.6/443/ HTTP/1.1\r\nHost: example.org\r\nConnection: Upgrade\r\nUpgrade: connect-udp\r\nCapsule-Protocol: ?1\r\n\r\n",
            Encoding.ASCII.GetString(MasqueUpgrade.BuildRequest(new Uri("https://example.org/.well-known/masque/udp/192.0.2.6/443/"), MasqueTunnelKind.ConnectUdp)));

    [TestMethod]
    public void BuildRequest_ConnectIpOnAnotherPort_NamesThePortInHost() =>
        Assert.AreEqual(
            "GET https://proxy.example.org:4443/ip?t=%2A HTTP/1.1\r\nHost: proxy.example.org:4443\r\nConnection: Upgrade\r\nUpgrade: connect-ip\r\nCapsule-Protocol: ?1\r\n\r\n",
            Encoding.ASCII.GetString(MasqueUpgrade.BuildRequest(new Uri("https://proxy.example.org:4443/ip?t=%2A"), MasqueTunnelKind.ConnectIp)));

    [TestMethod]
    [DataRow("HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: connect-udp", false)]
    [DataRow("HTTP/1.1 101 Switching Protocols\r\nbad line\r\nupgrade:CONNECT-IP\r\nconnection: keep-alive, upgrade", true)]
    public void CheckReply_101WithTheUpgradeHeaders_Opens(string head, bool isConnectIp) =>
        MasqueUpgrade.CheckReply(head, isConnectIp ? MasqueTunnelKind.ConnectIp : MasqueTunnelKind.ConnectUdp);

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nUpgrade: connect-udp\r\nConnection: Upgrade", 200)]
    [DataRow("HTTP/1.1 407 Proxy Authentication Required", 407)]
    public void CheckReply_AnyOtherStatus_IsRefusedWithIt(string head, int status)
    {
        var exception = Assert.ThrowsExactly<MasqueException>(() => MasqueUpgrade.CheckReply(head, MasqueTunnelKind.ConnectUdp));
        Assert.AreEqual(status, exception.StatusCode);
        Assert.AreEqual($"MASQUE proxy refused the connect-udp upgrade with status {status}", exception.Message);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade")]
    [DataRow("HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: connect-ip")]
    [DataRow("HTTP/1.1 101 Switching Protocols\r\nUpgrade: connect-udp")]
    public void CheckReply_101WithoutTheUpgradeHeaders_IsRefused(string head)
    {
        var exception = Assert.ThrowsExactly<MasqueException>(() => MasqueUpgrade.CheckReply(head, MasqueTunnelKind.ConnectUdp));
        Assert.AreEqual(101, exception.StatusCode);
        StringAssert.Contains(exception.Message, "answered 101 without");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("HTTP/1.1")]
    [DataRow("HTTP/2 101")]
    [DataRow("HTTP/1.1 1x1 Odd")]
    [DataRow("HTTP/1.1 1010 Long")]
    public void CheckReply_MalformedStatusLine_IsRefused(string head)
    {
        var exception = Assert.ThrowsExactly<MasqueException>(() => MasqueUpgrade.CheckReply(head, MasqueTunnelKind.ConnectIp));
        Assert.IsNull(exception.StatusCode);
        StringAssert.Contains(exception.Message, "malformed status line");
    }
}
