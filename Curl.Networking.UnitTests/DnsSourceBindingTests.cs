using System.Net;
using System.Net.Sockets;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void TryParse_NoOption_IsTheNoneBinding()
    {
        Diagnostics.Arrange("options", "(none)");

        var parsed = DnsSourceBinding.TryParse(null, null, null, out var binding);

        Diagnostics.Act("parsed", parsed);
        Diagnostics.Act("binding", binding);
        Diagnostics.Assert("parsed", true, parsed);
        Assert.IsTrue(parsed);

        Diagnostics.Assert("binding", DnsSourceBinding.None, binding);
        Assert.AreEqual(DnsSourceBinding.None, binding);
    }

    [TestMethod]
    public void TryParse_EveryOption_KeepsEach()
    {
        Diagnostics.Arrange("--dns-interface", "eth0");
        Diagnostics.Arrange("--dns-ipv4-addr", "10.1.2.3");
        Diagnostics.Arrange("--dns-ipv6-addr", "2a04:4e42::561");

        var parsed = DnsSourceBinding.TryParse("eth0", "10.1.2.3", "2a04:4e42::561", out var binding);

        Diagnostics.Act("parsed", parsed);
        Diagnostics.Act("binding", binding);
        Diagnostics.Assert("parsed", true, parsed);
        Assert.IsTrue(parsed);

        var expected = new DnsSourceBinding("eth0", IPAddress.Parse("10.1.2.3"), IPAddress.Parse("2a04:4e42::561"));
        Diagnostics.Assert("binding", expected, binding);
        Assert.AreEqual(new DnsSourceBinding("eth0", IPAddress.Parse("10.1.2.3"), IPAddress.Parse("2a04:4e42::561")), binding);
    }

    [TestMethod]
    [DataRow("bogus", null)]
    [DataRow("::1", null)]
    [DataRow(null, "bogus")]
    [DataRow(null, "1.2.3.4")]
    public void TryParse_AnAddressOfTheWrongFamilyOrNone_IsRefused(string? ipv4Address, string? ipv6Address)
    {
        Diagnostics.Arrange("--dns-ipv4-addr", ipv4Address ?? "(none)");
        Diagnostics.Arrange("--dns-ipv6-addr", ipv6Address ?? "(none)");

        var parsed = DnsSourceBinding.TryParse(null, ipv4Address, ipv6Address, out var binding);

        Diagnostics.Act("parsed", parsed);
        Diagnostics.Act("binding", binding?.ToString() ?? "(null)");
        Diagnostics.Assert("parsed", false, parsed);
        Assert.IsFalse(parsed);

        Diagnostics.Assert("binding is null", true, binding is null);
        Assert.IsNull(binding);
    }

    [TestMethod]
    [DataRow(AddressFamily.InterNetwork, "10.1.2.3")]
    [DataRow(AddressFamily.InterNetworkV6, "fd00::3")]
    public void LocalAddressFor_AGivenAddress_WinsOverTheInterface(AddressFamily family, string expected)
    {
        var binding = new DnsSourceBinding("eth0", IPAddress.Parse("10.1.2.3"), IPAddress.Parse("fd00::3"));
        Diagnostics.Arrange("binding", binding);
        Diagnostics.Arrange("family", family);

        var local = binding.LocalAddressFor(family, FindEth0);

        Diagnostics.Act("local address", local);
        Diagnostics.Assert("local address", IPAddress.Parse(expected), local);
        Assert.AreEqual(IPAddress.Parse(expected), binding.LocalAddressFor(family, FindEth0));
    }

    [TestMethod]
    [DataRow(AddressFamily.InterNetwork, "172.26.99.197")]
    [DataRow(AddressFamily.InterNetworkV6, "fe80::1")]
    public void LocalAddressFor_AnInterface_IsItsFirstAddressOfTheFamily(AddressFamily family, string expected)
    {
        var binding = new DnsSourceBinding("eth0", null, null);
        Diagnostics.Arrange("binding", binding);
        Diagnostics.Arrange("family", family);
        Diagnostics.Arrange("eth0 addresses", $"{InterfaceSix}, {InterfaceFour}");

        var local = binding.LocalAddressFor(family, FindEth0);

        Diagnostics.Act("local address", local);
        Diagnostics.Assert("local address", IPAddress.Parse(expected), local);
        Assert.AreEqual(IPAddress.Parse(expected), binding.LocalAddressFor(family, FindEth0));
    }

    [TestMethod]
    [DataRow(AddressFamily.InterNetwork, "0.0.0.0")]
    [DataRow(AddressFamily.InterNetworkV6, "::")]
    public void LocalAddressFor_NothingToBind_IsAnyAddress(AddressFamily family, string expected)
    {
        Diagnostics.Arrange("family", family);
        Diagnostics.Arrange("unknown interface", "nosuchif0");

        var noneLocal = DnsSourceBinding.None.LocalAddressFor(family, FindEth0);
        var unknownLocal = new DnsSourceBinding("nosuchif0", null, null).LocalAddressFor(family, FindEth0);

        Diagnostics.Act("no binding's local address", noneLocal);
        Diagnostics.Act("unknown interface's local address", unknownLocal);
        Diagnostics.Assert("no binding's local address", IPAddress.Parse(expected), noneLocal);
        Diagnostics.Assert("unknown interface's local address", IPAddress.Parse(expected), unknownLocal);
        Assert.AreEqual(IPAddress.Parse(expected), DnsSourceBinding.None.LocalAddressFor(family, FindEth0));
        Assert.AreEqual(IPAddress.Parse(expected), new DnsSourceBinding("nosuchif0", null, null).LocalAddressFor(family, FindEth0));
    }

    [TestMethod]
    public void LocalAddressFor_AnInterfaceWithAGlobalIPv6Address_PrefersItToTheLinkLocalOne()
    {
        var binding = new DnsSourceBinding("eth0", null, null);
        Diagnostics.Arrange("binding", binding);
        Diagnostics.Arrange("eth0 addresses", $"{InterfaceSix}, 2001:db8::5");

        var local = binding.LocalAddressFor(AddressFamily.InterNetworkV6, _ => [InterfaceSix, IPAddress.Parse("2001:db8::5")]);

        Diagnostics.Act("local address", local);
        Diagnostics.Assert("local address", IPAddress.Parse("2001:db8::5"), local);
        Assert.AreEqual(IPAddress.Parse("2001:db8::5"), local);
    }

    [TestMethod]
    public void LocalAddressFor_AnInterfaceWithoutTheFamily_IsAnyAddress()
    {
        var binding = new DnsSourceBinding("lo6", null, null);
        Diagnostics.Arrange("binding", binding);
        Diagnostics.Arrange("lo6 addresses", IPAddress.IPv6Loopback);

        var local = binding.LocalAddressFor(AddressFamily.InterNetwork, _ => [IPAddress.IPv6Loopback]);

        Diagnostics.Act("local address", local);
        Diagnostics.Assert("local address", IPAddress.Any, local);
        Assert.AreEqual(IPAddress.Any, binding.LocalAddressFor(AddressFamily.InterNetwork, _ => [IPAddress.IPv6Loopback]));
    }

    [TestMethod]
    public void LocalAddressFor_WithNullLookup_ThrowsArgumentNullException()
    {
        Diagnostics.Arrange("lookup", "(null)");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => DnsSourceBinding.None.LocalAddressFor(AddressFamily.InterNetwork, null!));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception", nameof(ArgumentNullException), exception.GetType().Name);
    }

    private static IReadOnlyList<IPAddress>? FindEth0(string name) =>
        name == "eth0" ? [InterfaceSix, InterfaceFour] : null;
}
