using System.Net;
using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// <c>--interface</c> and <c>--local-port</c> on an <c>--http3</c> or <c>--http3-only</c> connect
/// (BL-1025): the local end QUIC's UDP socket is asked for, and exit 45, 43 and 7 as curl.se's
/// ngtcp2 build ends a QUIC connect whose local end cannot be bound (measured on Windows
/// 2026-10-01, BL-1025 Notes).
/// </summary>
public sealed partial class TcpConnectorQuicTests
{
    [TestMethod]
    public async Task ConnectMultiplexedAsync_WithALocalBinding_BindsTheUdpSocketToTheAddressAndPortRange()
    {
        // curl --http3-only --interface 127.0.0.1 --local-port 41000-41010: "Local port: 41000".
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };
        var connector = BindingConnector(opener, new LocalBinding("127.0.0.1", "127.0.0.1", null, 41000, 11));

        var result = await connector.ConnectMultiplexedAsync(Target(), CancellationToken.None);

        await using var connection = result.Connection!;
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual((new IPEndPoint(IPAddress.Loopback, 41000), 11), opener.BoundFrom.Single());
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WithOnlyALocalPort_BindsTheUnspecifiedAddressOfTheFamilyDialled()
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };
        var connector = BindingConnector(opener, new LocalBinding(null, null, null, 41000, 3), new FakeDnsResolver(IPAddress.IPv6Loopback));

        var result = await connector.ConnectMultiplexedAsync(Target(), CancellationToken.None);

        await using var connection = result.Connection!;
        Assert.AreEqual((new IPEndPoint(IPAddress.IPv6Any, 41000), 3), opener.BoundFrom.Single());
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WithoutALocalBinding_OpensTheUdpSocketUnbound()
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };

        var result = await Connector(opener, new ManualTimeProvider()).ConnectMultiplexedAsync(Target(), CancellationToken.None);

        await using var connection = result.Connection!;
        Assert.IsEmpty(opener.BoundFrom);
        Assert.HasCount(1, opener.Opened);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheIfInterfaceIsNotFound_FailsWithExit45BeforeTheTrustAnchors()
    {
        // curl --http3-only -v --interface if!bogus0: Trying, "Could not bind to interface 'bogus0'
        // with errno 0: ...", then "Failed to connect to ... Failed binding local connection end",
        // no QUIC connect line, exit 45 (BL-1025, BL-1078).
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };
        var events = new RecordingTransferEvents();
        var connector = BindingConnector(opener, new LocalBinding("bogus0", null, null, 0, 1));

        var result = await connector.ConnectMultiplexedAsync(Target(events), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.InterfaceFailed, result.ExitCode);
        Assert.AreEqual("Failed to connect to quic.test port 443 after 0 ms: Failed binding local connection end", result.ErrorMessage);
        CollectionAssert.AreEqual(
            new[]
            {
                "  Trying 127.0.0.1:443...",
                LocalBindLines.CouldNotBindInterface("bogus0", OperatingSystem.IsWindows()),
                result.ErrorMessage,
            },
            events.Info.Skip(3).ToArray());
        Assert.IsEmpty(events.TlsEvents);
        Assert.IsEmpty(opener.Opened);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheInterfaceNameIsNeitherInterfaceNorHost_WritesCurlsCouldNotLines()
    {
        // curl --http3-only -v --interface bogus0: Trying, "Could not resolve
        // host", "Could not bind to 'bogus0' with errno 0: ...", then "Failed to connect to 127.0.0.1 port
        // 47611 after 2861 ms: Failed binding local connection end", exit 45 (BL-1025). 8.18.0 names the
        // URL's host in the resolve line; the bind host is named, as on the TCP path (ADR-0295).
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };
        var events = new RecordingTransferEvents();
        var connector = BindingConnector(opener, new LocalBinding("bogus0", "bogus0", null, 0, 1), new FakeDnsResolver(IPAddress.Loopback) { HostsWithNoAddress = new HashSet<string> { "bogus0" } });

        var result = await connector.ConnectMultiplexedAsync(Target(events), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.InterfaceFailed, result.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "  Trying 127.0.0.1:443...",
                LocalBindLines.CouldNotResolveHost("bogus0"),
                LocalBindLines.CouldNotBindHost("bogus0", OperatingSystem.IsWindows()),
                result.ErrorMessage,
            },
            events.Info.SkipWhile(line => !line.StartsWith("  Trying", StringComparison.Ordinal)).ToArray());
        Assert.IsEmpty(events.TlsEvents);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenNoPortOfTheRangeBinds_FailsWithExit45()
    {
        // curl --http3-only --local-port 41000-41002 with all three in use: exit 45.
        var opener = new QuicServerChannelOpener { OpenOutcome = _ => throw new LocalBindException(LocalBindFailure.InterfaceFailed) };
        var connector = BindingConnector(opener, new LocalBinding(null, null, null, 41000, 3));

        var result = await connector.ConnectMultiplexedAsync(Target(), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.InterfaceFailed, result.ExitCode);
        Assert.AreEqual("Failed to connect to quic.test port 443 after 0 ms: Failed binding local connection end", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheIfhostDeviceNameIsTooLong_FailsWithExit43()
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };
        var connector = BindingConnector(opener, new LocalBinding(null, "127.0.0.1", new string('a', LocalBinding.LongestDeviceName + 1), 0, 1));

        var result = await connector.ConnectMultiplexedAsync(Target(), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.BadFunctionArgument, result.ExitCode);
        Assert.AreEqual("Failed to connect to quic.test port 443 after 0 ms: A libcurl function was given a bad argument", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheLocalAddressIsOfTheOtherFamily_FailsWithExit7()
    {
        // curl --http3-only -v --interface ::1 https://127.0.0.1:47611/: "Failed to connect to
        // 127.0.0.1 port 47611 after 0 ms: Could not connect to server", exit 7.
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };
        var events = new RecordingTransferEvents();
        var connector = BindingConnector(opener, new LocalBinding("::1", "::1", null, 0, 1));

        var result = await connector.ConnectMultiplexedAsync(Target(events), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to quic.test port 443 after 0 ms: Could not connect to server", result.ErrorMessage);
        Assert.IsFalse(events.Info.Any(line => line.StartsWith("QUIC connect to", StringComparison.Ordinal)));

        // "Name '::1' family 2 resolved to '::1' family 23", then the failure (BL-1078).
        CollectionAssert.AreEqual(
            new[]
            {
                "  Trying 127.0.0.1:443...",
                LocalBindLines.NameResolved("::1", AddressFamily.InterNetwork, IPAddress.IPv6Loopback, OperatingSystem.IsWindows(), OperatingSystem.IsLinux()),
                result.ErrorMessage,
            },
            events.Info.SkipWhile(line => !line.StartsWith("  Trying", StringComparison.Ordinal)).ToArray());
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WithALocalBinding_WritesCurlsBindLinesBetweenTryingAndTheTrustAnchors()
    {
        // curl --http3-only -v --interface 127.0.0.1 --local-port 41000-41010: Trying, "Name '127.0.0.1'
        // family 2 resolved to '127.0.0.1' family 2", "Local port: 41000", then the trust anchors (BL-1025).
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };
        var events = new RecordingTransferEvents();
        var connector = BindingConnector(opener, new LocalBinding("127.0.0.1", "127.0.0.1", null, 41000, 11));

        var result = await connector.ConnectMultiplexedAsync(Target(events), CancellationToken.None);

        await using var connection = result.Connection!;
        CollectionAssert.AreEqual(
            new[]
            {
                "  Trying 127.0.0.1:443...",
                LocalBindLines.NameResolved("127.0.0.1", AddressFamily.InterNetwork, IPAddress.Loopback, OperatingSystem.IsWindows(), OperatingSystem.IsLinux()),
                LocalBindLines.LocalPort(41000),
            },
            events.Info.SkipWhile(line => !line.StartsWith("  Trying", StringComparison.Ordinal)).Take(3).ToArray());
    }

    [TestMethod]
    public async Task UdpChannelOpener_OpenFrom_WritesEachBusyPortThenTheLocalPortBound()
    {
        // curl --http3-only -v --local-port 41000-41005 with 41000-41002 held: "Bind to local port N
        // failed, trying next" for each, then "Local port: 41003" (BL-1025).
        using var taken = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        taken.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var takenPort = ((IPEndPoint)taken.LocalEndPoint!).Port;
        var events = new RecordingTransferEvents();

        await using var channel = OpenFromOrNull(new IPEndPoint(IPAddress.Loopback, takenPort), 2, events);

        if (channel is null)
        {
            Assert.Inconclusive($"Port {takenPort + 1}, after the one taken, belongs to another process.");
        }

        CollectionAssert.AreEqual(
            new[] { LocalBindLines.PortFailedTryingNext(takenPort), LocalBindLines.LocalPort(takenPort + 1) },
            events.Info.ToArray());
    }

    [TestMethod]
    public void UdpChannelOpener_OpenFrom_WhenEveryPortIsTaken_WritesTheBindFailedLine()
    {
        // curl --http3-only -v --local-port 41000-41002, all held: "bind failed with errno 10048:
        // Address already in use" on Windows (BL-1025).
        using var taken = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        taken.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var events = new RecordingTransferEvents();

        Assert.ThrowsExactly<LocalBindException>(() => new UdpChannelOpener().OpenFrom(new IPEndPoint(IPAddress.Loopback, 9), (IPEndPoint)taken.LocalEndPoint!, 1, events));

        var line = events.Info.Single();
        Assert.StartsWith("bind failed with errno ", line);
    }

    [TestMethod]
    public void UdpChannelOpener_OpenFrom_WithNullEvents_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new UdpChannelOpener().OpenFrom(new IPEndPoint(IPAddress.Loopback, 9), new IPEndPoint(IPAddress.Loopback, 0), 1, null!));
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheFirstAddressCannotBeBound_MovesOnToTheNextAddress()
    {
        // A bind failure moves on to the next address, as libcurl's does for TCP (BL-600 Notes).
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };
        var connector = BindingConnector(opener, new LocalBinding(null, "127.0.0.1", null, 0, 1), new FakeDnsResolver(IPAddress.IPv6Loopback, IPAddress.Loopback));

        var result = await connector.ConnectMultiplexedAsync(Target(), CancellationToken.None);

        await using var connection = result.Connection!;
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 443), connection.RemoteEndPoint);
        Assert.AreEqual((new IPEndPoint(IPAddress.Loopback, 0), 1), opener.BoundFrom.Single());
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WithAPlainInterfaceNameItsDeviceBinds_BindsNoAddressOrPort()
    {
        // libcurl's bindlocal runs for QUIC's socket too: SO_BINDTODEVICE alone, then no address (BL-1077).
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server(), DeviceBinds = true };
        var connector = BindingConnector(opener, new LocalBinding("eth0", "eth0", null, 41000, 3));

        var result = await connector.ConnectMultiplexedAsync(Target(), CancellationToken.None);

        await using var connection = result.Connection!;
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(("eth0", false), opener.DeviceBoundTo.Single());
        Assert.IsEmpty(opener.BoundFrom);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WithAnIfNameItsDeviceBinds_BindsNoAddressOrPort()
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server(), DeviceBinds = true };
        var connector = BindingConnector(opener, new LocalBinding("eth0", null, null, 0, 1));

        var result = await connector.ConnectMultiplexedAsync(Target(), CancellationToken.None);

        await using var connection = result.Connection!;
        Assert.AreEqual(("eth0", false), opener.DeviceBoundTo.Single());
        Assert.IsEmpty(opener.BoundFrom);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WithIfhostItsDeviceBinds_BindsTheHostAfterTheDevice()
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server(), DeviceBinds = true };
        var connector = BindingConnector(opener, new LocalBinding(null, "127.0.0.1", "eth0", 41000, 2));

        var result = await connector.ConnectMultiplexedAsync(Target(), CancellationToken.None);

        await using var connection = result.Connection!;
        Assert.AreEqual(("eth0", true), opener.DeviceBoundTo.Single());
        Assert.AreEqual((new IPEndPoint(IPAddress.Loopback, 41000), 2), opener.BoundFrom.Single());
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WithOnlyALocalPort_AsksForNoDeviceBind()
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server(), DeviceBinds = true };
        var connector = BindingConnector(opener, new LocalBinding(null, null, null, 41000, 1));

        var result = await connector.ConnectMultiplexedAsync(Target(), CancellationToken.None);

        await using var connection = result.Connection!;
        Assert.IsEmpty(opener.DeviceBoundTo);
        Assert.AreEqual((new IPEndPoint(IPAddress.Any, 41000), 1), opener.BoundFrom.Single());
    }

    [TestMethod]
    public async Task UdpChannelOpener_OpenFromDeviceAsync_WhenTheDeviceBindsAlone_BindsAnyAddressAndChoosesNoLocalEnd()
    {
        var devicesAsked = new List<string>();
        var opener = new UdpChannelOpener((_, name) => { devicesAsked.Add(name); return true; });
        var chooserCalls = 0;

        await using var channel = await opener.OpenFromDeviceAsync(
            new IPEndPoint(IPAddress.Loopback, 9),
            "eth0",
            bindsAddressAfterDevice: false,
            _ => { chooserCalls++; return ValueTask.FromResult(new IPEndPoint(IPAddress.Loopback, 41000)); },
            1,
            NoTransferEvents.Instance,
            CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "eth0" }, devicesAsked);
        Assert.AreEqual(0, chooserCalls);
        Assert.AreNotEqual(41000, ((IPEndPoint)channel.LocalEndPoint!).Port);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 9), channel.ServerEndPoint);
    }

    [TestMethod]
    public async Task UdpChannelOpener_OpenFromDeviceAsync_ForIfhost_BindsTheChosenLocalEndAfterTheDevice()
    {
        var opener = new UdpChannelOpener((_, _) => true);

        await using var channel = await opener.OpenFromDeviceAsync(
            new IPEndPoint(IPAddress.Loopback, 9),
            "eth0",
            bindsAddressAfterDevice: true,
            _ => ValueTask.FromResult(new IPEndPoint(IPAddress.Loopback, 0)),
            1,
            NoTransferEvents.Instance,
            CancellationToken.None);

        var bound = (IPEndPoint)channel.LocalEndPoint!;
        Assert.AreEqual(IPAddress.Loopback, bound.Address);
        Assert.AreNotEqual(0, bound.Port);
    }

    [TestMethod]
    public async Task UdpChannelOpener_OpenFromDeviceAsync_WhenTheDeviceDoesNotBind_BindsTheChosenLocalEnd()
    {
        var opener = new UdpChannelOpener((_, _) => false);

        await using var channel = await opener.OpenFromDeviceAsync(
            new IPEndPoint(IPAddress.IPv6Loopback, 9),
            "bogus0",
            bindsAddressAfterDevice: false,
            _ => ValueTask.FromResult(new IPEndPoint(IPAddress.IPv6Loopback, 0)),
            1,
            NoTransferEvents.Instance,
            CancellationToken.None);

        Assert.AreEqual(IPAddress.IPv6Loopback, ((IPEndPoint)channel.LocalEndPoint!).Address);
    }

    [TestMethod]
    public async Task UdpChannelOpener_OpenFromDeviceAsync_WhenTheChosenPortIsTaken_ThrowsInterfaceFailed()
    {
        using var taken = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        taken.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var takenEndPoint = (IPEndPoint)taken.LocalEndPoint!;
        var opener = new UdpChannelOpener((_, _) => false);

        var exception = await Assert.ThrowsExactlyAsync<LocalBindException>(async () => await opener.OpenFromDeviceAsync(
            new IPEndPoint(IPAddress.Loopback, 9),
            "eth0",
            bindsAddressAfterDevice: false,
            _ => ValueTask.FromResult(takenEndPoint),
            1,
            NoTransferEvents.Instance,
            CancellationToken.None));

        Assert.AreEqual(LocalBindFailure.InterfaceFailed, exception.Failure);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public async Task UdpChannelOpener_OpenFromDeviceAsync_OnLinuxWithTheLoopbackDevice_BindsItAlone()
    {
        var chooserCalls = 0;

        await using var channel = await new UdpChannelOpener().OpenFromDeviceAsync(
            new IPEndPoint(IPAddress.Loopback, 9),
            "lo",
            bindsAddressAfterDevice: false,
            _ => { chooserCalls++; return ValueTask.FromResult(new IPEndPoint(IPAddress.Loopback, 0)); },
            1,
            NoTransferEvents.Instance,
            CancellationToken.None);

        Assert.AreEqual(0, chooserCalls);
    }

    [TestMethod]
    public async Task UdpChannelOpener_OpenFromDeviceAsync_WithANullArgument_ThrowsArgumentNullException()
    {
        var opener = new UdpChannelOpener();
        var server = new IPEndPoint(IPAddress.Loopback, 9);
        Func<CancellationToken, ValueTask<IPEndPoint>> choose = _ => ValueTask.FromResult(new IPEndPoint(IPAddress.Loopback, 0));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await opener.OpenFromDeviceAsync(null!, "eth0", false, choose, 1, NoTransferEvents.Instance, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await opener.OpenFromDeviceAsync(server, null!, false, choose, 1, NoTransferEvents.Instance, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await opener.OpenFromDeviceAsync(server, "eth0", false, null!, 1, NoTransferEvents.Instance, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await opener.OpenFromDeviceAsync(server, "eth0", false, choose, 1, null!, CancellationToken.None));
    }

    [TestMethod]
    public async Task UdpChannelOpener_OpenFrom_BindsTheFirstFreePortOfTheRange()
    {
        using var taken = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        taken.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var takenPort = ((IPEndPoint)taken.LocalEndPoint!).Port;

        await using var channel = OpenFromOrNull(new IPEndPoint(IPAddress.Loopback, takenPort), 2);

        if (channel is null)
        {
            Assert.Inconclusive($"Port {takenPort + 1}, after the one taken, belongs to another process.");
        }

        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, takenPort + 1), channel.LocalEndPoint);
    }

    [TestMethod]
    public void UdpChannelOpener_OpenFrom_WhenEveryPortIsTaken_ThrowsInterfaceFailed()
    {
        using var taken = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        taken.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var takenEndPoint = (IPEndPoint)taken.LocalEndPoint!;

        var exception = Assert.ThrowsExactly<LocalBindException>(() => new UdpChannelOpener().OpenFrom(new IPEndPoint(IPAddress.Loopback, 9), takenEndPoint, 1, NoTransferEvents.Instance));

        Assert.AreEqual(LocalBindFailure.InterfaceFailed, exception.Failure);
    }

    [TestMethod]
    public void UdpChannelOpener_OpenFrom_WithANullLocalEndPoint_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new UdpChannelOpener().OpenFrom(new IPEndPoint(IPAddress.Loopback, 9), null!, 1, NoTransferEvents.Instance));
    }

    private static IDatagramChannel? OpenFromOrNull(IPEndPoint localEndPoint, int localPortCount, ITransferEvents? events = null)
    {
        try
        {
            return new UdpChannelOpener().OpenFrom(new IPEndPoint(IPAddress.Loopback, 9), localEndPoint, localPortCount, events ?? NoTransferEvents.Instance);
        }
        catch (LocalBindException)
        {
            return null;
        }
    }

    private static TcpConnector BindingConnector(QuicServerChannelOpener opener, LocalBinding binding, FakeDnsResolver? resolver = null)
    {
        var clock = new ManualTimeProvider();
        return new(
            resolver ?? new FakeDnsResolver(IPAddress.Loopback),
            new FakeTcpDialer { DialOutcome = _ => new FakeConnection() },
            new FakeTlsProvider(),
            clock,
            quicDialer: new QuicDialer(opener, new TlsClientOptions(Insecure: true), true, clock, SystemTlsRandomSource.Instance),
            localBinding: binding,
            networkInterfaceLookup: new SystemNetworkInterfaceLookup(onWindows: false, () => []));
    }
}
