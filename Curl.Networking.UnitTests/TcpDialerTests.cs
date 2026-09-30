using System.Net;
using System.Net.Sockets;

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
    public void BindLocalEnd_WithAFreePort_BindsIt()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        TcpDialer.BindLocalEnd(socket, new IPEndPoint(IPAddress.Loopback, 0), 0);

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

            TcpDialer.BindLocalEnd(socket, new IPEndPoint(IPAddress.Loopback, busy), 2);

            Assert.AreEqual(busy + 1, ((IPEndPoint)socket.LocalEndPoint!).Port);
        }
    }

    [TestMethod]
    public void BindLocalEnd_WhenEveryPortIsInUse_ThrowsInterfaceFailed()
    {
        using var holder = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        holder.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var busy = ((IPEndPoint)holder.LocalEndPoint!).Port;
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        var exception = Assert.ThrowsExactly<LocalBindException>(
            () => TcpDialer.BindLocalEnd(socket, new IPEndPoint(IPAddress.Loopback, busy), 1));

        Assert.AreEqual(LocalBindFailure.InterfaceFailed, exception.Failure);
        Assert.AreEqual(SocketError.Success, exception.SocketErrorCode);
    }

    /// <summary>Binds a loopback port whose next port is free, and returns the port and the socket holding it.</summary>
    private static (int Port, Socket Holder) HoldAPortWithTheNextFree()
    {
        while (true)
        {
            var holder = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            holder.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            var port = ((IPEndPoint)holder.LocalEndPoint!).Port;
            using var probe = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                probe.Bind(new IPEndPoint(IPAddress.Loopback, port + 1));
                return (port, holder);
            }
            catch (SocketException)
            {
                holder.Dispose();
            }
            catch (ArgumentOutOfRangeException)
            {
                holder.Dispose();
            }
        }
    }
}
