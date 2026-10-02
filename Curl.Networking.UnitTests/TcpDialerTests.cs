using System.Net;
using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="TcpDialer" />. The loopback tests open real sockets and are this project's
/// <c>Integration</c> dialer tests; the argument checks need none.
/// </summary>
[TestClass]
public sealed class TcpDialerTests
{
    [TestMethod]
    public async Task DialAsync_WithNullEndPoint_ThrowsArgumentNullException()
    {
        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await new TcpDialer().DialAsync(null!, CancellationToken.None));

        Assert.AreEqual("endPoint", exception.ParamName);
    }

    [TestMethod]
    public async Task DialUnixSocketAsync_WithNullAddress_ThrowsArgumentNullException()
    {
        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await new TcpDialer().DialUnixSocketAsync(null!, CancellationToken.None));

        Assert.AreEqual("address", exception.ParamName);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task DialUnixSocketAsync_ToAListeningSocket_ConnectsAndCarriesBytes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bl507-{Guid.NewGuid():N}.sock");
        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(path));
        listener.Listen(1);
        try
        {
            var accepting = listener.AcceptAsync();

            await using var connection = await new TcpDialer().DialUnixSocketAsync(new UnixSocketAddress(path, IsAbstract: false), CancellationToken.None);
            using var accepted = await accepting;
            await connection.WriteAsync("hi"u8.ToArray(), CancellationToken.None);
            await connection.FlushAsync(CancellationToken.None);
            var received = new byte[2];
            var count = await accepted.ReceiveAsync(received, SocketFlags.None);

            Assert.AreEqual(2, count);
            CollectionAssert.AreEqual("hi"u8.ToArray(), received);
            Assert.AreEqual(path, connection.RemoteEndPoint!.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task DialUnixSocketAsync_ToAMissingSocket_ThrowsSocketException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bl507-missing-{Guid.NewGuid():N}.sock");

        await Assert.ThrowsExactlyAsync<SocketException>(
            async () => await new TcpDialer().DialUnixSocketAsync(new UnixSocketAddress(path, IsAbstract: false), CancellationToken.None));
    }

    [TestMethod]
    public void Constructor_WithNullSocketOptions_ThrowsArgumentNullException()
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new TcpDialer(null!));

        Assert.AreEqual("socketOptions", exception.ParamName);
    }

    [TestMethod]
    public void Constructor_WithoutSocketOptions_UsesCurlsDefaults()
    {
        Assert.AreEqual(new TcpSocketOptions(NoDelay: true, KeepAlive: true), new TcpDialer().SocketOptions);
    }

    [TestMethod]
    public void ApplySocketOptions_ByDefault_SetsNoDelayAndKeepAliveEverySixtySeconds()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        new TcpDialer().ApplySocketOptions(socket);

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

        new TcpDialer(new TcpSocketOptions(KeepAliveSeconds: 5, KeepAliveProbeCount: 3)).ApplySocketOptions(socket);

        Assert.AreEqual(5, (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime)!);
        Assert.AreEqual(5, (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval)!);
        Assert.AreEqual(3, (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount)!);
    }

    [TestMethod]
    public void ApplySocketOptions_WithAProbeCountThePlatformRefuses_KeepsKeepAliveAndTheTimesItAccepts()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        new TcpDialer(new TcpSocketOptions(KeepAliveSeconds: 5, KeepAliveProbeCount: -1)).ApplySocketOptions(socket);

        Assert.AreNotEqual(0, (int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive)!);
        Assert.AreEqual(5, (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime)!);
        Assert.AreNotEqual(-1, (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount)!);
    }

    [TestMethod]
    public void ApplySocketOptions_WithNoKeepAlive_SetsNoneOfTheTimers()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        new TcpDialer(new TcpSocketOptions(KeepAlive: false, KeepAliveSeconds: 5, KeepAliveProbeCount: 3)).ApplySocketOptions(socket);

        Assert.AreNotEqual(5, (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime)!);
        Assert.AreNotEqual(5, (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval)!);
        Assert.AreNotEqual(3, (int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount)!);
    }

    [TestMethod]
    public void ApplySocketOptions_WithNoTcpNoDelayAndNoKeepAlive_LeavesNagleOnAndKeepAliveOff()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        new TcpDialer(new TcpSocketOptions(NoDelay: false, KeepAlive: false)).ApplySocketOptions(socket);

        Assert.IsFalse(socket.NoDelay);
        Assert.AreEqual(0, (int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive)!);
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void ApplySocketOptions_WithOneSwitchOff_SetsTheOtherAlone(bool noDelay, bool keepAlive)
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        new TcpDialer(new TcpSocketOptions(noDelay, keepAlive)).ApplySocketOptions(socket);

        Assert.AreEqual(noDelay, socket.NoDelay);
        Assert.AreEqual(keepAlive, (int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive)! != 0);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task DialAsync_OverLoopback_CarriesBytesBothWaysThenFailsOnceTheListenerStops()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endPoint = (IPEndPoint)listener.LocalEndpoint;
        var dialer = new TcpDialer();

        var dialed = await dialer.DialAsync(endPoint, cancellation.Token);
        await using (var connection = dialed.Connection)
        {
            using var accepted = await listener.AcceptTcpClientAsync(cancellation.Token);
            var serverStream = accepted.GetStream();

            await connection.WriteAsync(new byte[] { 1, 2, 3 }, cancellation.Token);
            await connection.FlushAsync(cancellation.Token);
            var received = new byte[3];
            await serverStream.ReadExactlyAsync(received, cancellation.Token);
            await serverStream.WriteAsync(new byte[] { 9 }, cancellation.Token);
            var reply = new byte[1];
            var replyLength = await connection.ReadAsync(reply, cancellation.Token);

            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, received);
            Assert.AreEqual(1, replyLength);
            Assert.AreEqual(9, reply[0]);
            Assert.IsFalse(connection.IsSecure);
            Assert.AreEqual(endPoint, connection.RemoteEndPoint);
            Assert.AreEqual(accepted.Client.RemoteEndPoint, dialed.LocalEndPoint);
        }

        listener.Stop();

        await Assert.ThrowsAsync<SocketException>(
            async () => await dialer.DialAsync(endPoint, cancellation.Token));
    }

    [TestMethod]
    public async Task DialFromDeviceAsync_WithNullChooser_ThrowsArgumentNullException()
    {
        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await new TcpDialer().DialFromDeviceAsync(new IPEndPoint(IPAddress.Loopback, 80), "lo", false, null!, 1, NoTransferEvents.Instance, CancellationToken.None));

        Assert.AreEqual("chooseLocalEndAsync", exception.ParamName);
    }

    // curl 8.18.0 on Linux, as uid 1000 and as root alike (BL-1026 Notes): --interface lo ->
    // "socket successfully bound to interface 'lo'".
    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public void TryBindToDevice_OnLinuxWithTheLoopbackDevice_BindsIt()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        Assert.IsTrue(TcpDialer.TryBindToDevice(socket, "lo"));
    }

    // curl 8.18.0 on Linux: if!bogus0 -> errno 19, "No such device", and curl carries on without it.
    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public void TryBindToDevice_OnLinuxWithNoSuchDevice_ReturnsFalse()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        Assert.IsFalse(TcpDialer.TryBindToDevice(socket, "bogus0"));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Linux)]
    public void TryBindToDevice_OffLinux_ReturnsFalse()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        Assert.IsFalse(TcpDialer.TryBindToDevice(socket, "lo"));
    }

    [TestMethod]
    public void BindLocalEnd_WithAFreePort_BindsIt()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        TcpDialer.BindLocalEnd(socket, new IPEndPoint(IPAddress.Loopback, 0), 0, NoTransferEvents.Instance);

        var bound = (IPEndPoint)socket.LocalEndPoint!;
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

            TcpDialer.BindLocalEnd(socket, new IPEndPoint(IPAddress.Loopback, busy), 2, NoTransferEvents.Instance);

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

        var exception = Assert.ThrowsExactly<LocalBindException>(
            () => TcpDialer.BindLocalEnd(socket, new IPEndPoint(IPAddress.Loopback, busy), 1, NoTransferEvents.Instance));

        Assert.AreEqual(LocalBindFailure.InterfaceFailed, exception.Failure);
        Assert.AreEqual(SocketError.Success, exception.SocketErrorCode);
    }

    [TestMethod]
    public void BindLocalEnd_WithNoPortAskedFor_ReportsLocalPortZero()
    {
        // curl --interface 127.0.0.1 to localhost, both platforms: "Local port: 0", whatever port the system gave.
        var events = new RecordingTransferEvents();
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        TcpDialer.BindLocalEnd(socket, new IPEndPoint(IPAddress.Loopback, 0), 1, events);

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

            TcpDialer.BindLocalEnd(socket, new IPEndPoint(IPAddress.Loopback, busy), 2, events);
        }

        CollectionAssert.AreEqual(new[] { $"Bind to local port {busy} failed, trying next", $"Local port: {busy + 1}" }, events.Info);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void BindLocalEnd_WhenEveryPortIsInUseOnWindows_ReportsErrno10048()
    {
        // curl 8.21.0 Windows, --local-port 40000-40002 all in use: "bind failed with errno 10048: Address already in use".
        Assert.AreEqual("bind failed with errno 10048: Address already in use", LastLineOfABusyPortBind());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public void BindLocalEnd_WhenEveryPortIsInUseOnLinux_ReportsErrno98()
    {
        // curl 8.18.0 Linux, --local-port 40000-40001 both in use: "bind failed with errno 98: Address already in use".
        Assert.AreEqual("bind failed with errno 98: Address already in use", LastLineOfABusyPortBind());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.OSX)]
    public void BindLocalEnd_WhenEveryPortIsInUseOnMacOS_ReportsErrno48()
    {
        // macOS's EADDRINUSE is 48 (sys/errno.h), its strerror "Address already in use".
        Assert.AreEqual("bind failed with errno 48: Address already in use", LastLineOfABusyPortBind());
    }

    // curl 8.18.0 on Linux: --interface lo -> "socket successfully bound to interface 'lo'", and
    // ifhost!lo!127.0.0.1 binds the device silently and goes on to "Local port: 0".
    [TestMethod]
    [TestCategory("Integration")]
    [OSCondition(OperatingSystems.Linux)]
    [DataRow(false, "socket successfully bound to interface 'lo'", DisplayName = "lo")]
    [DataRow(true, "Local port: 0", DisplayName = "ifhost!lo!127.0.0.1")]
    public async Task DialFromDeviceAsync_OnLinuxWithTheLoopbackDevice_ReportsCurlsLine(bool bindsAddressAfterDevice, string expected)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var events = new RecordingTransferEvents();

        try
        {
            var dialed = await new TcpDialer().DialFromDeviceAsync(
                (IPEndPoint)listener.LocalEndpoint,
                "lo",
                bindsAddressAfterDevice,
                _ => ValueTask.FromResult(new IPEndPoint(IPAddress.Loopback, 0)),
                1,
                events,
                cancellation.Token);
            await dialed.Connection.DisposeAsync();
        }
        finally
        {
            listener.Stop();
        }

        CollectionAssert.AreEqual(new[] { expected }, events.Info);
    }

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

        CollectionAssert.AreEqual(new[] { "lo" }, devicesTried);
        CollectionAssert.AreEqual(expectedLine is null ? Array.Empty<string>() : new[] { expectedLine }, events.Info);
        CollectionAssert.AreEqual(bindsAddress ? new[] { chosen } : Array.Empty<IPEndPoint>(), bound);
    }

    private static string LastLineOfABusyPortBind()
    {
        var events = new RecordingTransferEvents();
        using var holder = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        holder.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        // Listening, so the port is in use on Linux too (SO_REUSEADDR).
        holder.Listen();
        var busy = ((IPEndPoint)holder.LocalEndPoint!).Port;
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        Assert.ThrowsExactly<LocalBindException>(
            () => TcpDialer.BindLocalEnd(socket, new IPEndPoint(IPAddress.Loopback, busy), 1, events));

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
