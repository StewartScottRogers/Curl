using Curl.Testing;

namespace Curl.Networking;

/// <summary>Pins when <see cref="DnsServerResolverOptions" /> sends lookups to the hand-built client (BL-694).</summary>
[TestClass]
public sealed class DnsServerResolverOptionsTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("192.0.2.1", null, null, null)]
    [DataRow(null, "eth0", null, null)]
    [DataRow(null, null, "10.1.2.3", null)]
    [DataRow(null, null, null, "::1")]
    public void IsAnyGiven_AnyOneOption_IsTrue(string? servers, string? interfaceName, string? ipv4Address, string? ipv6Address)
    {
        Diagnostics.Arrange("servers", servers ?? "(none)");
        Diagnostics.Arrange("interface", interfaceName ?? "(none)");
        Diagnostics.Arrange("IPv4 address", ipv4Address ?? "(none)");
        Diagnostics.Arrange("IPv6 address", ipv6Address ?? "(none)");

        var isAnyGiven = new DnsServerResolverOptions(servers, interfaceName, ipv4Address, ipv6Address).IsAnyGiven;

        Diagnostics.Act("is any given", isAnyGiven);
        Diagnostics.Assert("is any given", true, isAnyGiven);
        Assert.IsTrue(new DnsServerResolverOptions(servers, interfaceName, ipv4Address, ipv6Address).IsAnyGiven);
    }

    [TestMethod]
    public void IsAnyGiven_NoOption_IsFalse()
    {
        Diagnostics.Arrange("options", "(none)");

        var isAnyGiven = new DnsServerResolverOptions(null, null, null, null).IsAnyGiven;

        Diagnostics.Act("is any given", isAnyGiven);
        Diagnostics.Assert("is any given", false, isAnyGiven);
        Assert.IsFalse(new DnsServerResolverOptions(null, null, null, null).IsAnyGiven);
    }
}
