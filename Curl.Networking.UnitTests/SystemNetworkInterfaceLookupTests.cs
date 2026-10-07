using System.Net;
using System.Net.NetworkInformation;

using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="SystemNetworkInterfaceLookup" />: off Windows it finds an interface by
/// its name without regard to case and returns its addresses, and on Windows it finds none,
/// as the Schannel build of curl 8.21.0 finds none (ADR-0110).
/// </summary>
[TestClass]
public sealed class SystemNetworkInterfaceLookupTests
{
    private static readonly (string Name, IPAddress[] Addresses)[] Interfaces =
    [
        ("lo", [IPAddress.Loopback, IPAddress.IPv6Loopback]),
        ("eth0", [IPAddress.Parse("192.0.2.7")]),
    ];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void FindAddresses_OffWindows_ReturnsTheNamedInterfacesAddresses()
    {
        var lookup = new SystemNetworkInterfaceLookup(onWindows: false, () => Interfaces);

        Diagnostics.Arrange("on Windows, name", "false, eth0");

        var addresses = lookup.FindAddresses("eth0")!.ToArray();

        Diagnostics.Act("addresses", string.Join<IPAddress>(", ", addresses));
        Diagnostics.Assert("addresses", "192.0.2.7", string.Join<IPAddress>(", ", addresses));

        CollectionAssert.AreEqual(new[] { IPAddress.Parse("192.0.2.7") }, addresses);
    }

    [TestMethod]
    public void FindAddresses_OffWindows_ComparesTheNameWithoutRegardToCase()
    {
        var lookup = new SystemNetworkInterfaceLookup(onWindows: false, () => Interfaces);

        Diagnostics.Arrange("on Windows, name", "false, LO");

        var addresses = lookup.FindAddresses("LO")!.ToArray();

        Diagnostics.Act("addresses", string.Join<IPAddress>(", ", addresses));
        Diagnostics.Assert("addresses", "127.0.0.1, ::1", string.Join<IPAddress>(", ", addresses));

        CollectionAssert.AreEqual(new[] { IPAddress.Loopback, IPAddress.IPv6Loopback }, addresses);
    }

    [TestMethod]
    public void FindAddresses_OffWindowsForANameNoInterfaceHas_ReturnsNull()
    {
        var lookup = new SystemNetworkInterfaceLookup(onWindows: false, () => Interfaces);

        Diagnostics.Arrange("on Windows, name", "false, nosuch.invalid");

        var addresses = lookup.FindAddresses("nosuch.invalid");

        Diagnostics.Act("found", addresses is not null);
        Diagnostics.Assert("found", false, addresses is not null);

        Assert.IsNull(addresses);
    }

    [TestMethod]
    public void FindAddresses_OnWindows_FindsNoInterfaceEvenForOneThatExists()
    {
        var lookup = new SystemNetworkInterfaceLookup(onWindows: true, () => Interfaces);

        Diagnostics.Arrange("on Windows, name", "true, lo");

        var addresses = lookup.FindAddresses("lo");

        Diagnostics.Act("found", addresses is not null);
        Diagnostics.Assert("found", false, addresses is not null);

        Assert.IsNull(addresses);
    }

    [TestMethod]
    public void FindAddresses_WithNullName_Throws()
    {
        var lookup = new SystemNetworkInterfaceLookup();

        Diagnostics.Arrange("name", null);

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => lookup.FindAddresses(null!));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception", nameof(ArgumentNullException), exception.GetType().Name);
    }

    [TestMethod]
    public void ListSystemInterfaces_ListsTheLoopbackInterfaceWithItsLoopbackAddress()
    {
        string loopbackName = NetworkInterface.GetAllNetworkInterfaces()
            .First(networkInterface => networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            .Name;

        Diagnostics.Arrange("loopback interface", "found among this machine's interfaces");

        (string _, IPAddress[] addresses) = SystemNetworkInterfaceLookup.ListSystemInterfaces()
            .First(listed => listed.Name == loopbackName);

        var loopbackAddressListed = addresses.Contains(IPAddress.Loopback);
        Diagnostics.Act("loopback address listed", loopbackAddressListed);
        Diagnostics.Assert("loopback address listed", true, loopbackAddressListed);

        CollectionAssert.Contains(addresses, IPAddress.Loopback);
    }
}
