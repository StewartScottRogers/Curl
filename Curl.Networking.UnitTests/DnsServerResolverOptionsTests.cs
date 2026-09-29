namespace Curl.Networking;

/// <summary>Pins when <see cref="DnsServerResolverOptions" /> sends lookups to the hand-built client (BL-694).</summary>
[TestClass]
public sealed class DnsServerResolverOptionsTests
{
    [TestMethod]
    [DataRow("192.0.2.1", null, null, null)]
    [DataRow(null, "eth0", null, null)]
    [DataRow(null, null, "10.1.2.3", null)]
    [DataRow(null, null, null, "::1")]
    public void IsAnyGiven_AnyOneOption_IsTrue(string? servers, string? interfaceName, string? ipv4Address, string? ipv6Address)
    {
        Assert.IsTrue(new DnsServerResolverOptions(servers, interfaceName, ipv4Address, ipv6Address).IsAnyGiven);
    }

    [TestMethod]
    public void IsAnyGiven_NoOption_IsFalse()
    {
        Assert.IsFalse(new DnsServerResolverOptions(null, null, null, null).IsAnyGiven);
    }
}
