using System.Net;
using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="TcpDialer" />. The loopback tests open real sockets and are this project's
/// <c>Integration</c> dialer tests; the argument checks need none.
/// </summary>
[TestClass]
public sealed class TcpDialerTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task DialAsync_WithNullEndPoint_ThrowsArgumentNullException()
    {
        Diagnostics.Arrange("end point", "null");

        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await new TcpDialer().DialAsync(null!, CancellationToken.None));

        Diagnostics.Act("exception parameter name", exception.ParamName);
        Diagnostics.Assert("exception parameter name", "endPoint", exception.ParamName);

        Assert.AreEqual("endPoint", exception.ParamName);
    }

    [TestMethod]
    public async Task DialUnixSocketAsync_WithNullAddress_ThrowsArgumentNullException()
    {
        Diagnostics.Arrange("address", "null");

        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await new TcpDialer().DialUnixSocketAsync(null!, CancellationToken.None));

        Diagnostics.Act("exception parameter name", exception.ParamName);
        Diagnostics.Assert("exception parameter name", "address", exception.ParamName);

        Assert.AreEqual("address", exception.ParamName);
    }

    [TestMethod]
    public void Constructor_WithNullSocketOptions_ThrowsArgumentNullException()
    {
        Diagnostics.Arrange("socket options", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new TcpDialer(null!));

        Diagnostics.Act("exception parameter name", exception.ParamName);
        Diagnostics.Assert("exception parameter name", "socketOptions", exception.ParamName);

        Assert.AreEqual("socketOptions", exception.ParamName);
    }

    [TestMethod]
    public void Constructor_WithoutSocketOptions_UsesCurlsDefaults()
    {
        Diagnostics.Arrange("socket options", "none");

        var options = new TcpDialer().SocketOptions;

        Diagnostics.Act("socket options", options);
        Diagnostics.Assert("socket options", new TcpSocketOptions(NoDelay: true, KeepAlive: true), options);

        Assert.AreEqual(new TcpSocketOptions(NoDelay: true, KeepAlive: true), new TcpDialer().SocketOptions);
    }

    [TestMethod]
    public void ApplySocketOptions_ByDefault_SetsNoDelayAndKeepAliveEverySixtySeconds()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Diagnostics.Arrange("socket options", "defaults");

        new TcpDialer().ApplySocketOptions(socket);

        Diagnostics.Act("no delay", socket.NoDelay);
        Diagnostics.Act("keep alive", (int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive)!);
        Diagnostics.Act("keep-alive time", (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime)!);
        Diagnostics.Act("keep-alive interval", (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval)!);
        Diagnostics.Act("keep-alive retry count", (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount)!);
        Diagnostics.Assert("no delay", true, socket.NoDelay);
        Diagnostics.Assert("keep-alive time", 60, (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime)!);
        Diagnostics.Assert("keep-alive retry count", 9, (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount)!);

        Assert.IsTrue(socket.NoDelay);
        Assert.AreNotEqual(0, (int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive)!);
        Assert.AreEqual(60, (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime)!);
        Assert.AreEqual(60, (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval)!);
        Assert.AreEqual(9, (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount)!);
    }

    [TestMethod]
    public void ApplySocketOptions_WithKeepAliveTimeAndCount_SetsTheTimersToThem()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Diagnostics.Arrange("keep-alive seconds, probe count", "5, 3");

        new TcpDialer(new TcpSocketOptions(KeepAliveSeconds: 5, KeepAliveProbeCount: 3)).ApplySocketOptions(socket);

        Diagnostics.Act("keep-alive time", (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime)!);
        Diagnostics.Act("keep-alive interval", (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval)!);
        Diagnostics.Act("keep-alive retry count", (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount)!);
        Diagnostics.Assert("keep-alive time", 5, (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime)!);
        Diagnostics.Assert("keep-alive retry count", 3, (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount)!);

        Assert.AreEqual(5, (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime)!);
        Assert.AreEqual(5, (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval)!);
        Assert.AreEqual(3, (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount)!);
    }

    [TestMethod]
    public void ApplySocketOptions_WithAProbeCountThePlatformRefuses_KeepsKeepAliveAndTheTimesItAccepts()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Diagnostics.Arrange("keep-alive seconds, probe count", "5, -1");

        new TcpDialer(new TcpSocketOptions(KeepAliveSeconds: 5, KeepAliveProbeCount: -1)).ApplySocketOptions(socket);

        Diagnostics.Act("keep alive", (int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive)!);
        Diagnostics.Act("keep-alive time", (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime)!);
        Diagnostics.Act("keep-alive retry count is -1", (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount)! == -1);
        Diagnostics.Assert("keep-alive time", 5, (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime)!);
        Diagnostics.Assert("keep-alive retry count is -1", false, (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount)! == -1);

        Assert.AreNotEqual(0, (int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive)!);
        Assert.AreEqual(5, (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime)!);
        Assert.AreNotEqual(-1, (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount)!);
    }

    [TestMethod]
    public void ApplySocketOptions_WithNoKeepAlive_SetsNoneOfTheTimers()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Diagnostics.Arrange("keep alive, seconds, probe count", "False, 5, 3");

        new TcpDialer(new TcpSocketOptions(KeepAlive: false, KeepAliveSeconds: 5, KeepAliveProbeCount: 3)).ApplySocketOptions(socket);

        Diagnostics.Act("keep-alive time is 5", (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime)! == 5);
        Diagnostics.Act("keep-alive interval is 5", (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval)! == 5);
        Diagnostics.Act("keep-alive retry count is 3", (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount)! == 3);
        Diagnostics.Assert("keep-alive time is 5", false, (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime)! == 5);

        Assert.AreNotEqual(5, (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime)!);
        Assert.AreNotEqual(5, (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval)!);
        Assert.AreNotEqual(3, (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount)!);
    }

    [TestMethod]
    public void ApplySocketOptions_WithNoTcpNoDelayAndNoKeepAlive_LeavesNagleOnAndKeepAliveOff()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Diagnostics.Arrange("no delay, keep alive", "False, False");

        new TcpDialer(new TcpSocketOptions(NoDelay: false, KeepAlive: false)).ApplySocketOptions(socket);

        Diagnostics.Act("no delay", socket.NoDelay);
        Diagnostics.Act("keep alive", (int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive)!);
        Diagnostics.Assert("no delay", false, socket.NoDelay);
        Diagnostics.Assert("keep alive", 0, (int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive)!);

        Assert.IsFalse(socket.NoDelay);
        Assert.AreEqual(0, (int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive)!);
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void ApplySocketOptions_WithOneSwitchOff_SetsTheOtherAlone(bool noDelay, bool keepAlive)
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Diagnostics.Arrange("no delay, keep alive", $"{noDelay}, {keepAlive}");

        new TcpDialer(new TcpSocketOptions(noDelay, keepAlive)).ApplySocketOptions(socket);

        Diagnostics.Act("no delay", socket.NoDelay);
        Diagnostics.Act("keep alive on", (int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive)! != 0);
        Diagnostics.Assert("no delay", noDelay, socket.NoDelay);
        Diagnostics.Assert("keep alive on", keepAlive, (int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive)! != 0);

        Assert.AreEqual(noDelay, socket.NoDelay);
        Assert.AreEqual(keepAlive, (int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive)! != 0);
    }

    [TestMethod]
    public async Task DialFromDeviceAsync_WithNullChooser_ThrowsArgumentNullException()
    {
        Diagnostics.Arrange("choose local end", "null");

        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await new TcpDialer().DialFromDeviceAsync(new IPEndPoint(IPAddress.Loopback, 80), "lo", false, null!, 1, NoTransferEvents.Instance, CancellationToken.None));

        Diagnostics.Act("exception parameter name", exception.ParamName);
        Diagnostics.Assert("exception parameter name", "chooseLocalEndAsync", exception.ParamName);

        Assert.AreEqual("chooseLocalEndAsync", exception.ParamName);
    }

    // curl 8.18.0 on Linux, as uid 1000 and as root alike (BL-1026 Notes): --interface lo ->
    // "socket successfully bound to interface 'lo'".
    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public void TryBindToDevice_OnLinuxWithTheLoopbackDevice_BindsIt()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Diagnostics.Arrange("device", "lo");

        var bound = TcpDialer.TryBindToDevice(socket, "lo");

        Diagnostics.Act("bound", bound);
        Diagnostics.Assert("bound", true, bound);

        Assert.IsTrue(bound);
    }

    // curl 8.18.0 on Linux: if!bogus0 -> errno 19, "No such device", and curl carries on without it.
    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public void TryBindToDevice_OnLinuxWithNoSuchDevice_ReturnsFalse()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Diagnostics.Arrange("device", "bogus0");

        var bound = TcpDialer.TryBindToDevice(socket, "bogus0");

        Diagnostics.Act("bound", bound);
        Diagnostics.Assert("bound", false, bound);

        Assert.IsFalse(bound);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Linux)]
    public void TryBindToDevice_OffLinux_ReturnsFalse()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Diagnostics.Arrange("device", "lo");

        var bound = TcpDialer.TryBindToDevice(socket, "lo");

        Diagnostics.Act("bound", bound);
        Diagnostics.Assert("bound", false, bound);

        Assert.IsFalse(bound);
    }

    [TestMethod]
    public void BindLocalEnd_WithAFreePort_BindsIt()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Diagnostics.Arrange("requested end point, extra ports", "127.0.0.1:0, 0");

        TcpDialer.BindLocalEnd(socket, new IPEndPoint(IPAddress.Loopback, 0), 0, NoTransferEvents.Instance);

        var bound = (IPEndPoint)socket.LocalEndPoint!;
        Diagnostics.Act("bound address", bound.Address);
        Diagnostics.Act("bound port is not zero", bound.Port != 0);
        Diagnostics.Assert("bound address", IPAddress.Loopback, bound.Address);
        Diagnostics.Assert("bound port is not zero", true, bound.Port != 0);

        Assert.AreEqual(IPAddress.Loopback, bound.Address);
        Assert.AreNotEqual(0, bound.Port);
    }

    [TestMethod]
    public void BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext()
    {
        // curl --local-port 40000-40005 with 40000-40002 in use -> %{local_port} 40003.
        var (busy, holder) = HoldAPortWithTheNextFree();
        using (holder)
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            Diagnostics.Arrange("first port in use, extra ports", "busy, 2");

            TcpDialer.BindLocalEnd(socket, new IPEndPoint(IPAddress.Loopback, busy), 2, NoTransferEvents.Instance);

            Diagnostics.Act("bound port minus busy port", ((IPEndPoint)socket.LocalEndPoint!).Port - busy);
            Diagnostics.Assert("bound port minus busy port", 1, ((IPEndPoint)socket.LocalEndPoint!).Port - busy);

            Assert.AreEqual(busy + 1, ((IPEndPoint)socket.LocalEndPoint!).Port);
        }
    }

    [TestMethod]
    public void BindLocalEnd_WhenEveryPortIsInUse_ThrowsInterfaceFailed()
    {
        using var holder = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        holder.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        // Listening, so the port is in use on Linux too, where .NET
        // sets SO_REUSEADDR and a merely bound port can be bound again.
        holder.Listen();
        var busy = ((IPEndPoint)holder.LocalEndPoint!).Port;
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        Diagnostics.Arrange("ports in use, extra ports", "1 (listening), 1");

        var exception = Assert.ThrowsExactly<LocalBindException>(
            () => TcpDialer.BindLocalEnd(socket, new IPEndPoint(IPAddress.Loopback, busy), 1, NoTransferEvents.Instance));

        Diagnostics.Act("failure", exception.Failure);
        Diagnostics.Act("socket error code", exception.SocketErrorCode);
        Diagnostics.Assert("failure", LocalBindFailure.InterfaceFailed, exception.Failure);
        Diagnostics.Assert("socket error code", SocketError.Success, exception.SocketErrorCode);

        Assert.AreEqual(LocalBindFailure.InterfaceFailed, exception.Failure);
        Assert.AreEqual(SocketError.Success, exception.SocketErrorCode);
    }

    [TestMethod]
    public void BindLocalEnd_WithNoPortAskedFor_ReportsLocalPortZero()
    {
        // curl --interface 127.0.0.1 to localhost, both platforms: "Local port: 0", whatever port the system gave.
        var events = new RecordingTransferEvents();
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        Diagnostics.Arrange("requested end point, extra ports", "127.0.0.1:0, 1");

        TcpDialer.BindLocalEnd(socket, new IPEndPoint(IPAddress.Loopback, 0), 1, events);

        Diagnostics.Act("info lines", string.Join(" | ", events.Info));
        Diagnostics.Assert("info lines", "Local port: 0", string.Join(" | ", events.Info));

        CollectionAssert.AreEqual(new[] { "Local port: 0" }, events.Info);
    }

    [TestMethod]
    public void BindLocalEnd_WhenTheFirstPortIsInUse_ReportsItThenTheLocalPortBound()
    {
        // curl --local-port 40000-40005 with 40000-40002 in use, both platforms:
        // "Bind to local port 40000 failed, trying next" for each, then "Local port: 40003".
        var events = new RecordingTransferEvents();
        var (busy, holder) = HoldAPortWithTheNextFree();
        using (holder)
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            Diagnostics.Arrange("first port in use, extra ports", "busy, 2");

            TcpDialer.BindLocalEnd(socket, new IPEndPoint(IPAddress.Loopback, busy), 2, events);
        }

        Diagnostics.Act("info lines", string.Join(" | ", events.Info));
        Diagnostics.Assert("info lines", $"Bind to local port {busy} failed, trying next | Local port: {busy + 1}", string.Join(" | ", events.Info));

        CollectionAssert.AreEqual(new[] { $"Bind to local port {busy} failed, trying next", $"Local port: {busy + 1}" }, events.Info);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void BindLocalEnd_WhenEveryPortIsInUseOnWindows_ReportsErrno10048()
    {
        // curl 8.21.0 Windows, --local-port 40000-40002 all in use: "bind failed with errno 10048: Address already in use".
        Diagnostics.Arrange("ports in use, extra ports", "1 (listening), 1");

        var lastLine = LastLineOfABusyPortBind();

        Diagnostics.Act("last info line", lastLine);
        Diagnostics.Assert("last info line", "bind failed with errno 10048: Address already in use", lastLine);

        Assert.AreEqual("bind failed with errno 10048: Address already in use", LastLineOfABusyPortBind());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public void BindLocalEnd_WhenEveryPortIsInUseOnLinux_ReportsErrno98()
    {
        // curl 8.18.0 Linux, --local-port 40000-40001 both in use: "bind failed with errno 98: Address already in use".
        Diagnostics.Arrange("ports in use, extra ports", "1 (listening), 1");

        var lastLine = LastLineOfABusyPortBind();

        Diagnostics.Act("last info line", lastLine);
        Diagnostics.Assert("last info line", "bind failed with errno 98: Address already in use", lastLine);

        Assert.AreEqual("bind failed with errno 98: Address already in use", LastLineOfABusyPortBind());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.OSX)]
    public void BindLocalEnd_WhenEveryPortIsInUseOnMacOS_ReportsErrno48()
    {
        // macOS's EADDRINUSE is 48 (sys/errno.h), its strerror "Address already in use".
        Diagnostics.Arrange("ports in use, extra ports", "1 (listening), 1");

        var lastLine = LastLineOfABusyPortBind();

        Diagnostics.Act("last info line", lastLine);
        Diagnostics.Assert("last info line", "bind failed with errno 48: Address already in use", lastLine);

        Assert.AreEqual("bind failed with errno 48: Address already in use", LastLineOfABusyPortBind());
    }

    // curl 8.18.0 on Linux: --interface lo -> "socket successfully bound to interface 'lo'", and
    // ifhost!lo!127.0.0.1 binds the device silently and goes on to "Local port: 0".
    // curl 8.18.0 on Linux (BL-1026, BL-1076 Notes): --interface lo and if!lo -> the line and no address;
    // ifhost!lo!127.0.0.1 binds the device silently, then its host; a refused device bind binds the address.
    [TestMethod]
    [DataRow(true, false, "socket successfully bound to interface 'lo'", false, DisplayName = "plain or if!, device bound")]
    [DataRow(false, false, null, true, DisplayName = "plain or if!, device refused")]
    [DataRow(true, true, null, true, DisplayName = "ifhost!, device bound")]
    [DataRow(false, true, null, true, DisplayName = "ifhost!, device refused")]
    public async Task BindDeviceOrLocalEndAsync_ForEachDeviceBindOutcome_ReportsCurlsLineOrBindsTheAddress(
        bool deviceBinds, bool bindsAddressAfterDevice, string? expectedLine, bool bindsAddress)
    {
        var events = new RecordingTransferEvents();
        var devicesTried = new List<string>();
        var bound = new List<IPEndPoint>();
        var chosen = new IPEndPoint(IPAddress.Loopback, 40000);
        Diagnostics.Arrange("device binds, binds address after device, expected line, binds address", $"{deviceBinds}, {bindsAddressAfterDevice}, {expectedLine ?? "none"}, {bindsAddress}");

        await TcpDialer.BindDeviceOrLocalEndAsync(
            "lo",
            bindsAddressAfterDevice,
            name =>
            {
                devicesTried.Add(name);
                return deviceBinds;
            },
            _ => ValueTask.FromResult(chosen),
            bound.Add,
            events,
            CancellationToken.None);

        Diagnostics.Act("devices tried", string.Join(", ", devicesTried));
        Diagnostics.Act("info lines", string.Join(" | ", events.Info));
        Diagnostics.Act("addresses bound", string.Join(", ", bound));
        Diagnostics.Assert("devices tried", "lo", string.Join(", ", devicesTried));
        Diagnostics.Assert("info lines", expectedLine ?? string.Empty, string.Join(" | ", events.Info));
        Diagnostics.Assert("addresses bound", bindsAddress ? chosen.ToString() : string.Empty, string.Join(", ", bound));

        CollectionAssert.AreEqual(new[] { "lo" }, devicesTried);
        CollectionAssert.AreEqual(expectedLine is null ? Array.Empty<string>() : new[] { expectedLine }, events.Info);
        CollectionAssert.AreEqual(bindsAddress ? new[] { chosen } : Array.Empty<IPEndPoint>(), bound);
    }

    private string LastLineOfABusyPortBind()
    {
        var events = new RecordingTransferEvents();
        using var holder = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        holder.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        // Listening, so the port is in use on Linux too (SO_REUSEADDR).
        holder.Listen();
        var busy = ((IPEndPoint)holder.LocalEndPoint!).Port;
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        var exception = Assert.ThrowsExactly<LocalBindException>(
            () => TcpDialer.BindLocalEnd(socket, new IPEndPoint(IPAddress.Loopback, busy), 1, events));
        Diagnostics.Act("failure", exception.Failure);
        Diagnostics.Assert("failure", LocalBindFailure.InterfaceFailed, exception.Failure);
        return events.Info.Single();
    }

    /// <summary>
    /// Binds a loopback port whose next port is free, and returns the port and the socket holding it.
    /// </summary>
    /// <remarks>
    /// The pair is taken below 32768, under the ephemeral range of Windows (49152 up), Linux
    /// (32768 up) and macOS (49152 up). An ephemeral pair raced: Windows hands out ephemeral ports
    /// in order, so a test running in parallel that bound port 0 could take the next port in the
    /// moment between the probe letting it go and the dialer binding it.
    /// </remarks>
    private static (int Port, Socket Holder) HoldAPortWithTheNextFree()
    {
        for (var port = Random.Shared.Next(20000, 30000); ; port = port >= 32766 ? 20000 : port + 2)
        {
            var holder = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            using var probe = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                holder.Bind(new IPEndPoint(IPAddress.Loopback, port));
                // Listening, so the port is in use on Linux too, where .NET sets SO_REUSEADDR
                // and a merely bound port can be bound again.
                holder.Listen();
                probe.Bind(new IPEndPoint(IPAddress.Loopback, port + 1));
                return (port, holder);
            }
            catch (SocketException)
            {
                holder.Dispose();
            }
        }
    }
}
