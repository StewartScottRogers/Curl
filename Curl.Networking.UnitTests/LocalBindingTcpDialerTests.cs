using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins the members of <see cref="LocalBindingTcpDialer" /> that <see cref="TcpConnector" /> does not
/// reach; its choice of local address is pinned through the connector in
/// <c>TcpConnectorTests.LocalBinding</c> (BL-600).
/// </summary>
[TestClass]
public sealed class LocalBindingTcpDialerTests
{
    [TestMethod]
    public async Task DialAsync_WithNullEndPoint_ThrowsArgumentNullException()
    {
        var dialer = new LocalBindingTcpDialer(new FakeTcpDialer(), new LocalBinding(null, null, null, 0, 1), new SystemNetworkInterfaceLookup(), new FakeDnsResolver(), NoTransferEvents.Instance);

        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await dialer.DialAsync(null!, CancellationToken.None));

        Assert.AreEqual("endPoint", exception.ParamName);
    }

    [TestMethod]
    public async Task DialFromAsync_PassesTheLocalEndAsGiven()
    {
        var inner = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var dialer = new LocalBindingTcpDialer(inner, new LocalBinding("127.0.0.1", "127.0.0.1", null, 40000, 2), new SystemNetworkInterfaceLookup(), new FakeDnsResolver(), NoTransferEvents.Instance);
        var endPoint = new IPEndPoint(IPAddress.Loopback, 80);
        var localEndPoint = new IPEndPoint(IPAddress.Loopback, 50001);

        await dialer.DialFromAsync(endPoint, localEndPoint, 7, CancellationToken.None);

        Assert.AreEqual((endPoint, localEndPoint, 7), inner.BoundDials.Single());
    }

    [TestMethod]
    public async Task DialFromAsync_WithEventsByDefault_DialsThroughTheOverloadWithoutThem()
    {
        var inner = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        ITcpDialer dialer = new LocalBindingTcpDialer(inner, new LocalBinding(null, null, null, 0, 1), new SystemNetworkInterfaceLookup(), new FakeDnsResolver(), NoTransferEvents.Instance);
        var endPoint = new IPEndPoint(IPAddress.Loopback, 80);
        var localEndPoint = new IPEndPoint(IPAddress.Loopback, 50001);

        await dialer.DialFromAsync(endPoint, localEndPoint, 7, new RecordingTransferEvents(), CancellationToken.None);

        Assert.AreEqual((endPoint, localEndPoint, 7), inner.BoundDials.Single());
        Assert.IsEmpty(inner.BoundDialEvents);
    }

    [TestMethod]
    public async Task DialAsync_WithAnInterfaceName_HandsTheDeviceDialItsEvents()
    {
        var events = new RecordingTransferEvents();
        var inner = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var deviceDialer = new FakeDeviceBindingTcpDialer(inner, deviceBinds: false);
        var dialer = new LocalBindingTcpDialer(deviceDialer, new LocalBinding("lo", null, null, 40000, 11), Interfaces(("lo", [IPAddress.Loopback])), new FakeDnsResolver(), events);

        await dialer.DialAsync(new IPEndPoint(IPAddress.Loopback, 80), CancellationToken.None);

        Assert.AreSame(events, inner.BoundDialEvents.Single());
    }

    [TestMethod]
    public async Task DialUnixSocketAsync_DialsTheSocketUnbound()
    {
        var connection = new FakeConnection();
        var inner = new FakeTcpDialer { UnixSocketDialOutcome = _ => connection };
        var dialer = new LocalBindingTcpDialer(inner, new LocalBinding(null, null, null, 40000, 2), new SystemNetworkInterfaceLookup(), new FakeDnsResolver(), NoTransferEvents.Instance);
        var address = new UnixSocketAddress("/tmp/curl.sock", false);

        var dialed = await dialer.DialUnixSocketAsync(address, CancellationToken.None);

        Assert.AreSame(connection, dialed);
        Assert.AreSame(address, inner.DialedUnixSockets.Single());
        Assert.IsEmpty(inner.BoundDials);
    }

    // curl 8.18.0 on Linux, BL-1026 Notes: --interface lo and if!lo -> "socket successfully bound to
    // interface 'lo'", no address and no --local-port bound.
    [TestMethod]
    [DataRow("lo", "lo", DisplayName = "plain name")]
    [DataRow("lo", null, DisplayName = "if!")]
    public async Task DialAsync_WithAnInterfaceNameTheDeviceBindTakes_BindsTheDeviceAlone(string interfaceName, string? hostName)
    {
        var inner = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var deviceDialer = new FakeDeviceBindingTcpDialer(inner, deviceBinds: true);
        var dialer = new LocalBindingTcpDialer(deviceDialer, new LocalBinding(interfaceName, hostName, null, 40000, 11), Interfaces(), new FakeDnsResolver(), NoTransferEvents.Instance);
        var endPoint = new IPEndPoint(IPAddress.Loopback, 80);

        await dialer.DialAsync(endPoint, CancellationToken.None);

        Assert.AreEqual(("lo", false), deviceDialer.DeviceDials.Single());
        Assert.IsEmpty(inner.BoundDials);
        Assert.AreEqual(endPoint, inner.DialedEndPoints.Single());
    }

    [TestMethod]
    public async Task DialAsync_WithAnInterfaceNameTheDeviceBindRefuses_BindsTheInterfaceAddress()
    {
        var inner = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var deviceDialer = new FakeDeviceBindingTcpDialer(inner, deviceBinds: false);
        var dialer = new LocalBindingTcpDialer(deviceDialer, new LocalBinding("lo", null, null, 40000, 11), Interfaces(("lo", [IPAddress.Loopback])), new FakeDnsResolver(), NoTransferEvents.Instance);
        var endPoint = new IPEndPoint(IPAddress.Loopback, 80);

        await dialer.DialAsync(endPoint, CancellationToken.None);

        Assert.AreEqual(("lo", false), deviceDialer.DeviceDials.Single());
        Assert.AreEqual((endPoint, new IPEndPoint(IPAddress.Loopback, 40000), 11), inner.BoundDials.Single());
    }

    // curl 8.18.0 on Linux, BL-1026 Notes: ifhost!lo!127.0.0.1 binds the device silently and goes on
    // to "Name '127.0.0.1' family 2 resolved to '127.0.0.1' family 2".
    [TestMethod]
    [DataRow(true, DisplayName = "device bound")]
    [DataRow(false, DisplayName = "device refused")]
    public async Task DialAsync_WithIfhost_BindsTheDeviceThenTheHostAddress(bool deviceBinds)
    {
        var inner = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var deviceDialer = new FakeDeviceBindingTcpDialer(inner, deviceBinds);
        var dialer = new LocalBindingTcpDialer(deviceDialer, new LocalBinding(null, "127.0.0.1", "lo", 0, 1), Interfaces(), new FakeDnsResolver(), NoTransferEvents.Instance);
        var endPoint = new IPEndPoint(IPAddress.Loopback, 80);

        await dialer.DialAsync(endPoint, CancellationToken.None);

        Assert.AreEqual(("lo", true), deviceDialer.DeviceDials.Single());
        Assert.AreEqual((endPoint, new IPEndPoint(IPAddress.Loopback, 0), 1), inner.BoundDials.Single());
    }

    [TestMethod]
    public async Task DialAsync_WithAHostNameOnly_BindsNoDevice()
    {
        var inner = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var deviceDialer = new FakeDeviceBindingTcpDialer(inner, deviceBinds: true);
        var dialer = new LocalBindingTcpDialer(deviceDialer, new LocalBinding(null, "127.0.0.1", null, 0, 1), Interfaces(), new FakeDnsResolver(), NoTransferEvents.Instance);
        var endPoint = new IPEndPoint(IPAddress.Loopback, 80);

        await dialer.DialAsync(endPoint, CancellationToken.None);

        Assert.IsEmpty(deviceDialer.DeviceDials);
        Assert.AreEqual((endPoint, new IPEndPoint(IPAddress.Loopback, 0), 1), inner.BoundDials.Single());
    }

    [TestMethod]
    public async Task DialFromDeviceAsync_ByDefault_BindsTheLocalEndChosen()
    {
        ITcpDialer inner = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var endPoint = new IPEndPoint(IPAddress.Loopback, 80);
        var localEndPoint = new IPEndPoint(IPAddress.Loopback, 40000);

        await inner.DialFromDeviceAsync(endPoint, "lo", false, _ => ValueTask.FromResult(localEndPoint), 3, NoTransferEvents.Instance, CancellationToken.None);

        Assert.AreEqual((endPoint, localEndPoint, 3), ((FakeTcpDialer)inner).BoundDials.Single());
    }

    [TestMethod]
    public async Task DialFromDeviceAsync_ByDefaultWithNoChooser_ThrowsArgumentNullException()
    {
        ITcpDialer inner = new FakeTcpDialer();

        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await inner.DialFromDeviceAsync(new IPEndPoint(IPAddress.Loopback, 80), "lo", false, null!, 1, NoTransferEvents.Instance, CancellationToken.None));

        Assert.AreEqual("chooseLocalEndAsync", exception.ParamName);
    }

    private static SystemNetworkInterfaceLookup Interfaces(params (string Name, IPAddress[] Addresses)[] interfaces) =>
        new(onWindows: false, () => interfaces);
}
