using System.Net;
using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="DnsSourceBinding" />: which of <c>--dns-ipv4-addr</c>, <c>--dns-ipv6-addr</c>
/// and <c>--dns-interface</c> binds a DNS query's socket, and which values are refused (BL-694).
/// </summary>
[TestClass]
public sealed class DnsSourceBindingTests
{
    private static readonly IPAddress InterfaceFour = IPAddress.Parse("172.26.99.197");
    private static readonly IPAddress InterfaceSix = IPAddress.Parse("fe80::1");

    [TestMethod]
    public void TryParse_NoOption_IsTheNoneBinding()
    {
        Assert.IsTrue(DnsSourceBinding.TryParse(null, null, null, out var binding));

        Assert.AreEqual(DnsSourceBinding.None, binding);
    }

    [TestMethod]
    public void TryParse_EveryOption_KeepsEach()
    {
        Assert.IsTrue(DnsSourceBinding.TryParse("eth0", "10.1.2.3", "2a04:4e42::561", out var binding));

        Assert.AreEqual(new DnsSourceBinding("eth0", IPAddress.Parse("10.1.2.3"), IPAddress.Parse("2a04:4e42::561")), binding);
    }

    [TestMethod]
    [DataRow("bogus", null)]
    [DataRow("::1", null)]
    [DataRow(null, "bogus")]
    [DataRow(null, "1.2.3.4")]
    public void TryParse_AnAddressOfTheWrongFamilyOrNone_IsRefused(string? ipv4Address, string? ipv6Address)
    {
        Assert.IsFalse(DnsSourceBinding.TryParse(null, ipv4Address, ipv6Address, out var binding));

        Assert.IsNull(binding);
    }

    [TestMethod]
    [DataRow(AddressFamily.InterNetwork, "10.1.2.3")]
    [DataRow(AddressFamily.InterNetworkV6, "fd00::3")]
    public void LocalAddressFor_AGivenAddress_WinsOverTheInterface(AddressFamily family, string expected)
    {
        var binding = new DnsSourceBinding("eth0", IPAddress.Parse("10.1.2.3"), IPAddress.Parse("fd00::3"));

        Assert.AreEqual(IPAddress.Parse(expected), binding.LocalAddressFor(family, FindEth0));
    }

    [TestMethod]
    [DataRow(AddressFamily.InterNetwork, "172.26.99.197")]
    [DataRow(AddressFamily.InterNetworkV6, "fe80::1")]
    public void LocalAddressFor_AnInterface_IsItsFirstAddressOfTheFamily(AddressFamily family, string expected)
    {
        var binding = new DnsSourceBinding("eth0", null, null);

        Assert.AreEqual(IPAddress.Parse(expected), binding.LocalAddressFor(family, FindEth0));
    }

    [TestMethod]
    [DataRow(AddressFamily.InterNetwork, "0.0.0.0")]
    [DataRow(AddressFamily.InterNetworkV6, "::")]
    public void LocalAddressFor_NothingToBind_IsAnyAddress(AddressFamily family, string expected)
    {
        Assert.AreEqual(IPAddress.Parse(expected), DnsSourceBinding.None.LocalAddressFor(family, FindEth0));
        Assert.AreEqual(IPAddress.Parse(expected), new DnsSourceBinding("nosuchif0", null, null).LocalAddressFor(family, FindEth0));
    }

    [TestMethod]
    public void LocalAddressFor_AnInterfaceWithAGlobalIPv6Address_PrefersItToTheLinkLocalOne()
    {
        var binding = new DnsSourceBinding("eth0", null, null);

        var local = binding.LocalAddressFor(AddressFamily.InterNetworkV6, _ => [InterfaceSix, IPAddress.Parse("2001:db8::5")]);

        Assert.AreEqual(IPAddress.Parse("2001:db8::5"), local);
    }

    [TestMethod]
    public void LocalAddressFor_AnInterfaceWithoutTheFamily_IsAnyAddress()
    {
        var binding = new DnsSourceBinding("lo6", null, null);

        Assert.AreEqual(IPAddress.Any, binding.LocalAddressFor(AddressFamily.InterNetwork, _ => [IPAddress.IPv6Loopback]));
    }

    [TestMethod]
    public void LocalAddressFor_WithNullLookup_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => DnsSourceBinding.None.LocalAddressFor(AddressFamily.InterNetwork, null!));
    }

    private static IReadOnlyList<IPAddress>? FindEth0(string name) =>
        name == "eth0" ? [InterfaceSix, InterfaceFour] : null;
}
