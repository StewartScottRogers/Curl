using System.Net;
using Curl.Extension.WireGuard.Configuration;
using Curl.Extension.WireGuard.Tunnel;

namespace Curl.Extension.WireGuard.UnitTests.Tunnel;

/// <summary>Checks cryptokey routing against whitepaper section 2.</summary>
[TestClass]
public sealed class WireGuardRouteTests
{
    [TestMethod]
    [DataRow("10.0.0.0", 24, "10.0.0.255", true)]
    [DataRow("10.0.0.0", 24, "10.0.1.0", false)]
    [DataRow("10.0.0.0", 9, "10.127.0.1", true)]
    [DataRow("10.0.0.0", 9, "10.128.0.1", false)]
    [DataRow("0.0.0.0", 0, "203.0.113.9", true)]
    [DataRow("fd00::", 64, "fd00::1", true)]
    [DataRow("fd00::", 64, "fd00:0:0:1::1", false)]
    [DataRow("10.0.0.0", 8, "fd00::1", false)]
    public void Contains_Address_MatchesTheFirstPrefixLengthBits(string prefix, int prefixLength, string address, bool expected) =>
        Assert.AreEqual(expected, WireGuardRoute.Contains(new WireGuardCidr(IPAddress.Parse(prefix), prefixLength), IPAddress.Parse(address)));

    [TestMethod]
    public void PeerFor_OverlappingAllowedIps_ChoosesTheMostSpecificPeerWithAnEndpoint()
    {
        WireGuardPeer everything = Peer("0.0.0.0/0", "192.0.2.1");
        WireGuardPeer office = Peer("10.1.0.0/16", "192.0.2.2");
        WireGuardPeer roaming = Peer("10.1.2.0/24", null);

        Assert.AreSame(office, WireGuardRoute.PeerFor([everything, office, roaming], IPAddress.Parse("10.1.2.3")));
        Assert.AreSame(office, WireGuardRoute.PeerFor([office, everything], IPAddress.Parse("10.1.2.3")));
        Assert.AreSame(everything, WireGuardRoute.PeerFor([office, everything], IPAddress.Parse("203.0.113.1")));
        Assert.IsNull(WireGuardRoute.PeerFor([office, roaming], IPAddress.Parse("203.0.113.1")));
    }

    [TestMethod]
    public void SourceFor_EachFamily_GivesTheFirstAddressOfThatFamily()
    {
        WireGuardCidr[] addresses = [new(IPAddress.Parse("10.0.0.2"), 32), new(IPAddress.Parse("fd00::2"), 128)];

        Assert.AreEqual(IPAddress.Parse("10.0.0.2"), WireGuardRoute.SourceFor(addresses, IPAddress.Parse("10.9.9.9")));
        Assert.AreEqual(IPAddress.Parse("fd00::2"), WireGuardRoute.SourceFor(addresses, IPAddress.Parse("2001:db8::1")));
        Assert.IsNull(WireGuardRoute.SourceFor(addresses[..1], IPAddress.Parse("2001:db8::1")));
    }

    private static WireGuardPeer Peer(string allowedIps, string? endpointHost)
    {
        string[] parts = allowedIps.Split('/');
        return new WireGuardPeer(new byte[32], null, endpointHost is null ? null : new WireGuardEndpoint(endpointHost, 51820), [new WireGuardCidr(IPAddress.Parse(parts[0]), int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture))], null);
    }
}
