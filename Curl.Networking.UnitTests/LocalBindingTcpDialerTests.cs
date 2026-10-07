using System.Net;
using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins the members of <see cref="LocalBindingTcpDialer" /> that <see cref="TcpConnector" /> does not
/// reach; its choice of local address is pinned through the connector in
/// <c>TcpConnectorTests.LocalBinding</c> (BL-600).
/// </summary>
[TestClass]
public sealed class LocalBindingTcpDialerTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task DialAsync_WithNullEndPoint_ThrowsArgumentNullException()
    {
        var dialer = new LocalBindingTcpDialer(new FakeTcpDialer(), new LocalBinding(null, null, null, 0, 1), new SystemNetworkInterfaceLookup(), new FakeDnsResolver(), NoTransferEvents.Instance);
        Diagnostics.Arrange("local binding", new LocalBinding(null, null, null, 0, 1));

        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await dialer.DialAsync(null!, CancellationToken.None));

        Diagnostics.Act("exception parameter", exception.ParamName);
        Diagnostics.Assert("exception parameter", "endPoint", exception.ParamName);
        Assert.AreEqual("endPoint", exception.ParamName);
    }

    [TestMethod]
    public async Task DialFromAsync_PassesTheLocalEndAsGiven()
    {
        var inner = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var dialer = new LocalBindingTcpDialer(inner, new LocalBinding("127.0.0.1", "127.0.0.1", null, 40000, 2), new SystemNetworkInterfaceLookup(), new FakeDnsResolver(), NoTransferEvents.Instance);
        Diagnostics.Arrange("local binding", new LocalBinding("127.0.0.1", "127.0.0.1", null, 40000, 2));
        var endPoint = new IPEndPoint(IPAddress.Loopback, 80);
        var localEndPoint = new IPEndPoint(IPAddress.Loopback, 50001);

        await dialer.DialFromAsync(endPoint, localEndPoint, 7, CancellationToken.None);

        WriteDials(inner, null);
        Diagnostics.Assert("bound dial", (endPoint, localEndPoint, 7), inner.BoundDials.Single());
        Assert.AreEqual((endPoint, localEndPoint, 7), inner.BoundDials.Single());
    }

    [TestMethod]
    public async Task DialFromAsync_WithEventsByDefault_DialsThroughTheOverloadWithoutThem()
    {
        var inner = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        ITcpDialer dialer = new LocalBindingTcpDialer(inner, new LocalBinding(null, null, null, 0, 1), new SystemNetworkInterfaceLookup(), new FakeDnsResolver(), NoTransferEvents.Instance);
        Diagnostics.Arrange("local binding", new LocalBinding(null, null, null, 0, 1));
        var endPoint = new IPEndPoint(IPAddress.Loopback, 80);
        var localEndPoint = new IPEndPoint(IPAddress.Loopback, 50001);

        await dialer.DialFromAsync(endPoint, localEndPoint, 7, new RecordingTransferEvents(), CancellationToken.None);

        WriteDials(inner, null);
        Diagnostics.Assert("bound dial", (endPoint, localEndPoint, 7), inner.BoundDials.Single());
        Diagnostics.Assert("bound dials with events", 0, inner.BoundDialEvents.Count);
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
        Diagnostics.Arrange("local binding", new LocalBinding("lo", null, null, 40000, 11));

        await dialer.DialAsync(new IPEndPoint(IPAddress.Loopback, 80), CancellationToken.None);

        WriteDials(inner, deviceDialer);
        Diagnostics.Assert("bound dial events are the transfer's", true, ReferenceEquals(events, inner.BoundDialEvents.Single()));
        Assert.AreSame(events, inner.BoundDialEvents.Single());
    }

    [TestMethod]
    public void FailureToOpenSocket_AsksTheInnerDialer()
    {
        var refusal = new SocketException((int)SocketError.ProtocolNotSupported);
        var inner = new FakeTcpDialer { SocketOpenOutcome = family => family == AddressFamily.InterNetwork ? refusal : null };
        var dialer = new LocalBindingTcpDialer(inner, new LocalBinding(null, null, null, 40000, 2), new SystemNetworkInterfaceLookup(), new FakeDnsResolver(), NoTransferEvents.Instance);
        Diagnostics.Arrange("local binding", new LocalBinding(null, null, null, 40000, 2));

        Diagnostics.Arrange("inner refuses", AddressFamily.InterNetwork);

        var ipv4Failure = dialer.FailureToOpenSocket(AddressFamily.InterNetwork);
        var ipv6Failure = dialer.FailureToOpenSocket(AddressFamily.InterNetworkV6);

        Diagnostics.Act("IPv4 failure", ipv4Failure?.SocketErrorCode.ToString() ?? "none");
        Diagnostics.Act("IPv6 failure", ipv6Failure?.SocketErrorCode.ToString() ?? "none");
        Diagnostics.Assert("IPv4 failure is the inner refusal", true, ReferenceEquals(refusal, ipv4Failure));
        Diagnostics.Assert("IPv6 failure", "none", ipv6Failure?.SocketErrorCode.ToString() ?? "none");
        Assert.AreSame(refusal, dialer.FailureToOpenSocket(AddressFamily.InterNetwork));
        Assert.IsNull(dialer.FailureToOpenSocket(AddressFamily.InterNetworkV6));
    }

    [TestMethod]
    public async Task DialUnixSocketAsync_DialsTheSocketUnbound()
    {
        var connection = new FakeConnection();
        var inner = new FakeTcpDialer { UnixSocketDialOutcome = _ => connection };
        var dialer = new LocalBindingTcpDialer(inner, new LocalBinding(null, null, null, 40000, 2), new SystemNetworkInterfaceLookup(), new FakeDnsResolver(), NoTransferEvents.Instance);
        Diagnostics.Arrange("local binding", new LocalBinding(null, null, null, 40000, 2));
        var address = new UnixSocketAddress("/tmp/curl.sock", false);

        var dialed = await dialer.DialUnixSocketAsync(address, CancellationToken.None);

        WriteDials(inner, null);
        Diagnostics.Act("unix sockets dialled", inner.DialedUnixSockets.Count);
        Diagnostics.Assert("dialled connection is the inner one", true, ReferenceEquals(connection, dialed));
        Diagnostics.Assert("bound dial count", 0, inner.BoundDials.Count);
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
        Diagnostics.Arrange("local binding", new LocalBinding(interfaceName, hostName, null, 40000, 11));
        var endPoint = new IPEndPoint(IPAddress.Loopback, 80);

        await dialer.DialAsync(endPoint, CancellationToken.None);

        WriteDials(inner, deviceDialer);
        Diagnostics.Assert("device dial", ("lo", false), deviceDialer.DeviceDials.Single());
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
        Diagnostics.Arrange("local binding", new LocalBinding("lo", null, null, 40000, 11));
        var endPoint = new IPEndPoint(IPAddress.Loopback, 80);

        await dialer.DialAsync(endPoint, CancellationToken.None);

        WriteDials(inner, deviceDialer);
        Diagnostics.Assert("device dial", ("lo", false), deviceDialer.DeviceDials.Single());
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
        Diagnostics.Arrange("local binding", new LocalBinding(null, "127.0.0.1", "lo", 0, 1));
        var endPoint = new IPEndPoint(IPAddress.Loopback, 80);

        await dialer.DialAsync(endPoint, CancellationToken.None);

        WriteDials(inner, deviceDialer);
        Diagnostics.Assert("device dial", ("lo", true), deviceDialer.DeviceDials.Single());
        Assert.AreEqual(("lo", true), deviceDialer.DeviceDials.Single());
        Assert.AreEqual((endPoint, new IPEndPoint(IPAddress.Loopback, 0), 1), inner.BoundDials.Single());
    }

    [TestMethod]
    public async Task DialAsync_WithAHostNameOnly_BindsNoDevice()
    {
        var inner = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var deviceDialer = new FakeDeviceBindingTcpDialer(inner, deviceBinds: true);
        var dialer = new LocalBindingTcpDialer(deviceDialer, new LocalBinding(null, "127.0.0.1", null, 0, 1), Interfaces(), new FakeDnsResolver(), NoTransferEvents.Instance);
        Diagnostics.Arrange("local binding", new LocalBinding(null, "127.0.0.1", null, 0, 1));
        var endPoint = new IPEndPoint(IPAddress.Loopback, 80);

        await dialer.DialAsync(endPoint, CancellationToken.None);

        WriteDials(inner, deviceDialer);
        Diagnostics.Assert("device dial count", 0, deviceDialer.DeviceDials.Count);
        Assert.IsEmpty(deviceDialer.DeviceDials);
        Assert.AreEqual((endPoint, new IPEndPoint(IPAddress.Loopback, 0), 1), inner.BoundDials.Single());
    }

    [TestMethod]
    public async Task DialFromDeviceAsync_ByDefault_BindsTheLocalEndChosen()
    {
        ITcpDialer inner = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var endPoint = new IPEndPoint(IPAddress.Loopback, 80);
        var localEndPoint = new IPEndPoint(IPAddress.Loopback, 40000);

        Diagnostics.Arrange("device", "lo");
        Diagnostics.Arrange("local end chosen", localEndPoint);

        await inner.DialFromDeviceAsync(endPoint, "lo", false, _ => ValueTask.FromResult(localEndPoint), 3, NoTransferEvents.Instance, CancellationToken.None);

        WriteDials((FakeTcpDialer)inner, null);
        Diagnostics.Assert("bound dial", (endPoint, localEndPoint, 3), ((FakeTcpDialer)inner).BoundDials.Single());
        Assert.AreEqual((endPoint, localEndPoint, 3), ((FakeTcpDialer)inner).BoundDials.Single());
    }

    [TestMethod]
    public async Task DialFromDeviceAsync_ByDefaultWithNoChooser_ThrowsArgumentNullException()
    {
        ITcpDialer inner = new FakeTcpDialer();
        Diagnostics.Arrange("device", "lo");
        Diagnostics.Arrange("local end chooser", "null");

        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await inner.DialFromDeviceAsync(new IPEndPoint(IPAddress.Loopback, 80), "lo", false, null!, 1, NoTransferEvents.Instance, CancellationToken.None));

        Diagnostics.Act("exception parameter", exception.ParamName);
        Diagnostics.Assert("exception parameter", "chooseLocalEndAsync", exception.ParamName);
        Assert.AreEqual("chooseLocalEndAsync", exception.ParamName);
    }

    private void WriteDials(FakeTcpDialer inner, FakeDeviceBindingTcpDialer? deviceDialer)
    {
        Diagnostics.Act("bound dials", inner.BoundDials.Count == 0 ? "none" : string.Join("; ", inner.BoundDials));
        Diagnostics.Act("unbound dials", inner.DialedEndPoints.Count == 0 ? "none" : string.Join("; ", inner.DialedEndPoints));
        if (deviceDialer is not null)
        {
            Diagnostics.Act("device dials", deviceDialer.DeviceDials.Count == 0 ? "none" : string.Join("; ", deviceDialer.DeviceDials));
        }
    }

    private static SystemNetworkInterfaceLookup Interfaces(params (string Name, IPAddress[] Addresses)[] interfaces) =>
        new(onWindows: false, () => interfaces);
}
