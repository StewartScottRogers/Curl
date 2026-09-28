using System.Net;
using System.Net.NetworkInformation;

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

    [TestMethod]
    public void FindAddresses_OffWindows_ReturnsTheNamedInterfacesAddresses()
    {
        var lookup = new SystemNetworkInterfaceLookup(onWindows: false, () => Interfaces);

        CollectionAssert.AreEqual(new[] { IPAddress.Parse("192.0.2.7") }, lookup.FindAddresses("eth0")!.ToArray());
    }

    [TestMethod]
    public void FindAddresses_OffWindows_ComparesTheNameWithoutRegardToCase()
    {
        var lookup = new SystemNetworkInterfaceLookup(onWindows: false, () => Interfaces);

        CollectionAssert.AreEqual(new[] { IPAddress.Loopback, IPAddress.IPv6Loopback }, lookup.FindAddresses("LO")!.ToArray());
    }

    [TestMethod]
    public void FindAddresses_OffWindowsForANameNoInterfaceHas_ReturnsNull()
    {
        var lookup = new SystemNetworkInterfaceLookup(onWindows: false, () => Interfaces);

        Assert.IsNull(lookup.FindAddresses("nosuch.invalid"));
    }

    [TestMethod]
    public void FindAddresses_OnWindows_FindsNoInterfaceEvenForOneThatExists()
    {
        var lookup = new SystemNetworkInterfaceLookup(onWindows: true, () => Interfaces);

        Assert.IsNull(lookup.FindAddresses("lo"));
    }

    [TestMethod]
    public void FindAddresses_WithNullName_Throws()
    {
        var lookup = new SystemNetworkInterfaceLookup();

        Assert.ThrowsExactly<ArgumentNullException>(() => lookup.FindAddresses(null!));
    }

    [TestMethod]
    public void ListSystemInterfaces_ListsTheLoopbackInterfaceWithItsLoopbackAddress()
    {
        string loopbackName = NetworkInterface.GetAllNetworkInterfaces()
            .First(networkInterface => networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            .Name;

        (string _, IPAddress[] addresses) = SystemNetworkInterfaceLookup.ListSystemInterfaces()
            .First(listed => listed.Name == loopbackName);

        CollectionAssert.Contains(addresses, IPAddress.Loopback);
    }
}
