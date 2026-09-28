using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="TcpConnectionListener" />. The fast tests bind local sockets and send
/// nothing, as <see cref="UdpDatagramChannelTests" /> does; the one test in which a client
/// connects is an <c>Integration</c> test (BL-456).
/// </summary>
[TestClass]
public sealed class TcpConnectionListenerTests
{
    private static readonly ListenTarget AnyLoopbackPort = new(IPAddress.Loopback, 0, 0);

    [TestMethod]
    public async Task ListenAsync_WithNullTarget_ThrowsArgumentNullException()
    {
        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await new TcpConnectionListener().ListenAsync(null!, CancellationToken.None));

        Assert.AreEqual("target", exception.ParamName);
    }

    [TestMethod]
    public async Task ListenAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await new TcpConnectionListener().ListenAsync(AnyLoopbackPort, cancellation.Token));
    }

    [TestMethod]
    public async Task ListenAsync_OnLoopbackPortZero_ReturnsAPendingConnectionOnANonZeroLoopbackPort()
    {
        var listened = await new TcpConnectionListener().ListenAsync(AnyLoopbackPort, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, listened.ExitCode);
        Assert.IsNull(listened.ErrorMessage);
        await using var pending = listened.PendingConnection!;
        var listening = (IPEndPoint)pending.LocalEndPoint;
        Assert.AreEqual(IPAddress.Loopback, listening.Address);
        Assert.AreNotEqual(0, listening.Port);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task ListenAsync_OnLoopbackPortZero_AcceptsTheClientThatConnectsAndKeepsItOpenAfterDispose()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var listened = await new TcpConnectionListener().ListenAsync(AnyLoopbackPort, cancellation.Token);
        var pending = listened.PendingConnection!;
        var listening = (IPEndPoint)pending.LocalEndPoint;
        using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        await client.ConnectAsync(listening, cancellation.Token);
        var accepted = await pending.AcceptAsync(cancellation.Token);
        await pending.DisposeAsync();

        Assert.AreEqual(CurlExitCode.Ok, accepted.ExitCode);
        await using var connection = accepted.Connection!;
        Assert.IsFalse(connection.IsSecure);
        Assert.AreEqual(listening, connection.LocalEndPoint);
        Assert.AreEqual(listening, accepted.LocalEndPoint);
        Assert.AreEqual(client.LocalEndPoint, connection.RemoteEndPoint);
        await connection.WriteAsync(new byte[] { 42 }, cancellation.Token);
        await connection.FlushAsync(cancellation.Token);
        var received = new byte[1];
        await client.ReceiveAsync(received, cancellation.Token);
        Assert.AreEqual(42, received[0]);
    }

    [TestMethod]
    public async Task ListenAsync_WhenEveryPortOfTheRangeIsTaken_FailsWithFtpPortFailedAndRanOutOfPorts()
    {
        // curl 8.21.0 (Schannel): curl -P 127.0.0.1:<a port held listening> ftp://127.0.0.1:18456/f.txt
        // -> exit 30, curl: (30) bind() failed, ran out of ports (measured 2026-09-27 with
        // Record-CurlExchange.ps1 -Ftp, BL-456).
        using var held = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        held.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        held.Listen(1);
        var heldPort = ((IPEndPoint)held.LocalEndPoint!).Port;

        var listened = await new TcpConnectionListener().ListenAsync(
            new ListenTarget(IPAddress.Loopback, heldPort, heldPort), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.FtpPortFailed, listened.ExitCode);
        Assert.AreEqual("bind() failed, ran out of ports", listened.ErrorMessage);
        Assert.IsNull(listened.PendingConnection);
    }

    [TestMethod]
    public async Task ListenAsync_WhenTheFirstPortIsTaken_ListensOnTheNextFreeOne()
    {
        using var held = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        held.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        held.Listen(1);
        var heldPort = ((IPEndPoint)held.LocalEndPoint!).Port;

        // Up to 64 ports after the held one, so a port another process holds is skipped too.
        var listened = await new TcpConnectionListener().ListenAsync(
            new ListenTarget(IPAddress.Loopback, heldPort, Math.Min(heldPort + 64, 65535)), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, listened.ExitCode);
        await using var pending = listened.PendingConnection!;
        Assert.IsTrue(((IPEndPoint)pending.LocalEndPoint).Port > heldPort);
    }

    [TestMethod]
    public async Task ListenAsync_OnAnAddressThatIsNotLocal_FailsWithFtpPortFailedAndTheBindReason()
    {
        // curl 8.21.0's lib/ftp.c, ftp_port_bind_socket: an error other than EADDRINUSE or
        // EACCES is failf(data, "bind(port=%hu) failed: %s", ...) -> CURLE_FTP_PORT_FAILED.
        // 192.0.2.1 is TEST-NET-1 (RFC 5737), never a local address.
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var expected = Assert.ThrowsExactly<SocketException>(() => socket.Bind(new IPEndPoint(IPAddress.Parse("192.0.2.1"), 0)));
        socket.Dispose();

        var listened = await new TcpConnectionListener().ListenAsync(
            new ListenTarget(IPAddress.Parse("192.0.2.1"), 0, 5), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.FtpPortFailed, listened.ExitCode);
        Assert.AreEqual(
            "bind(port=0) failed: " + ConnectFailureReason.Describe(expected, OperatingSystem.IsWindows()),
            listened.ErrorMessage);
    }

    [TestMethod]
    public async Task ListenAsync_WhenNoSocketCanBeOpened_FailsWithFtpPortFailedAndSocketFailure()
    {
        var failure = new SocketException((int)SocketError.AddressFamilyNotSupported);
        var listener = new TcpConnectionListener { OpenSocket = _ => throw failure };

        var listened = await listener.ListenAsync(AnyLoopbackPort, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.FtpPortFailed, listened.ExitCode);
        Assert.AreEqual(
            "socket failure: " + ConnectFailureReason.Describe(failure, OperatingSystem.IsWindows()),
            listened.ErrorMessage);
    }

    [TestMethod]
    public async Task ListenAsync_WhenListenFails_ClosesTheSocketAndFailsWithSocketFailure()
    {
        // curl 8.21.0's lib/ftp.c, ftp_port_listen: failf(data, "socket failure: %s", ...).
        var failure = new SocketException((int)SocketError.TooManyOpenSockets);
        Socket? opened = null;
        var listener = new TcpConnectionListener
        {
            OpenSocket = family => opened = new Socket(family, SocketType.Stream, ProtocolType.Tcp),
            StartListening = _ => throw failure,
        };

        var listened = await listener.ListenAsync(AnyLoopbackPort, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.FtpPortFailed, listened.ExitCode);
        Assert.AreEqual(
            "socket failure: " + ConnectFailureReason.Describe(failure, OperatingSystem.IsWindows()),
            listened.ErrorMessage);
        Assert.ThrowsExactly<ObjectDisposedException>(() => opened!.Listen(1));
    }

    [TestMethod]
    public async Task DisposeAsync_ReleasesThePortSoItCanBeBoundAgain()
    {
        var listener = new TcpConnectionListener();
        var first = await listener.ListenAsync(AnyLoopbackPort, CancellationToken.None);
        var port = ((IPEndPoint)first.PendingConnection!.LocalEndPoint).Port;

        await first.PendingConnection.DisposeAsync();
        var second = await listener.ListenAsync(new ListenTarget(IPAddress.Loopback, port, port), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, second.ExitCode);
        await using var pending = second.PendingConnection!;
        Assert.AreEqual(port, ((IPEndPoint)pending.LocalEndPoint).Port);
    }

    [TestMethod]
    [DataRow(SocketError.AddressAlreadyInUse, true)]
    [DataRow(SocketError.AccessDenied, true)]
    [DataRow(SocketError.AddressNotAvailable, false)]
    public void MovesOnToTheNextPort_MovesOnOnlyForAPortInUseOrNotPermitted(SocketError error, bool expected)
    {
        Assert.AreEqual(expected, TcpConnectionListener.MovesOnToTheNextPort(error));
    }
}
