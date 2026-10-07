using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    /// <summary>Gets or sets the test's context, which carries its diagnostics (BL-1457).</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ListenAsync_WithNullTarget_ThrowsArgumentNullException()
    {
        Diagnostics.Arrange("target", "null");

        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await new TcpConnectionListener().ListenAsync(null!, CancellationToken.None));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("parameter name", "target", exception.ParamName);
        Assert.AreEqual("target", exception.ParamName);
    }

    [TestMethod]
    public async Task ListenAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        Diagnostics.Arrange("cancelled", "before listening");

        var exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await new TcpConnectionListener().ListenAsync(AnyLoopbackPort, cancellation.Token));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception", nameof(OperationCanceledException), exception.GetType().Name);
    }

    [TestMethod]
    public async Task ListenAsync_OnLoopbackPortZero_ReturnsAPendingConnectionOnANonZeroLoopbackPort()
    {
        Diagnostics.Arrange("target", "127.0.0.1, port 0");

        var listened = await ListenAsync(new TcpConnectionListener(), AnyLoopbackPort);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, listened.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, listened.ExitCode);
        Assert.IsNull(listened.ErrorMessage);
        await using var pending = listened.PendingConnection!;
        var listening = (IPEndPoint)pending.LocalEndPoint;
        Assert.AreEqual(IPAddress.Loopback, listening.Address);
        Assert.AreNotEqual(0, listening.Port);
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
        Diagnostics.Arrange("port range", "only a port held listening");

        var listened = await ListenAsync(new TcpConnectionListener(), new ListenTarget(IPAddress.Loopback, heldPort, heldPort));

        Diagnostics.Act("error message", listened.ErrorMessage);
        Diagnostics.Assert("exit code", CurlExitCode.FtpPortFailed, listened.ExitCode);
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
        Diagnostics.Arrange("port range", "a port held listening and up to 64 after it");

        // Up to 64 ports after the held one, so a port another process holds is skipped too.
        var listened = await ListenAsync(
            new TcpConnectionListener(), new ListenTarget(IPAddress.Loopback, heldPort, Math.Min(heldPort + 64, 65535)));

        Diagnostics.Assert("exit code", CurlExitCode.Ok, listened.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, listened.ExitCode);
        await using var pending = listened.PendingConnection!;
        Diagnostics.Act("ports past the held one", ((IPEndPoint)pending.LocalEndPoint).Port - heldPort);
        Assert.IsTrue(((IPEndPoint)pending.LocalEndPoint).Port > heldPort);
    }

    [TestMethod]
    public async Task ListenAsync_OnAnAddressThatIsNotLocal_FailsWithTheNonLocalBindLineForTheHandlerToRetry()
    {
        // curl 8.21.0 -v -P 192.0.2.1 ftp://127.0.0.1:47464/f.txt (Schannel, measured
        // 2026-09-27 with Record-CurlExchange.ps1 -Ftp, BL-464) prints
        // "* bind(port=0) on non-local address failed: Address not available" and binds again
        // on the control connection's address; the listener reports that line and the FTP
        // handler retries (ADR-0107). 192.0.2.1 is TEST-NET-1 (RFC 5737), never local.
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var expected = Assert.ThrowsExactly<SocketException>(() => socket.Bind(new IPEndPoint(IPAddress.Parse("192.0.2.1"), 0)));
        socket.Dispose();
        Diagnostics.Arrange("address", "192.0.2.1, ports 0-5");

        var listened = await ListenAsync(new TcpConnectionListener(), new ListenTarget(IPAddress.Parse("192.0.2.1"), 0, 5));

        // The reason is the platform's own words, so only whether the line starts as curl's does is written.
        Diagnostics.Assert("exit code", CurlExitCode.FtpPortFailed, listened.ExitCode);
        Diagnostics.Assert("starts with the non-local line", true, listened.ErrorMessage?.StartsWith("bind(port=0) on non-local address failed: ", StringComparison.Ordinal));
        Assert.AreEqual(SocketError.AddressNotAvailable, expected.SocketErrorCode);
        Assert.AreEqual(CurlExitCode.FtpPortFailed, listened.ExitCode);
        Assert.AreEqual(
            "bind(port=0) on non-local address failed: " + ConnectFailureReason.Describe(expected, OperatingSystem.IsWindows()),
            listened.ErrorMessage);
        Assert.IsNull(listened.PendingConnection);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ListenAsync_OnAnAddressThatIsNotLocal_OnWindows_GivesCurlsWinsockWording()
    {
        Diagnostics.Arrange("address", "192.0.2.1, port 0");

        var listened = await ListenAsync(new TcpConnectionListener(), new ListenTarget(IPAddress.Parse("192.0.2.1"), 0, 0));

        Diagnostics.Act("error message", listened.ErrorMessage);
        Diagnostics.Assert("error message", "bind(port=0) on non-local address failed: Address not available", listened.ErrorMessage);
        Assert.AreEqual("bind(port=0) on non-local address failed: Address not available", listened.ErrorMessage);
    }

    [TestMethod]
    public async Task ListenAsync_WhenTheBindFailsForAnotherReason_FailsWithFtpPortFailedAndTheBindReason()
    {
        // curl 8.21.0's lib/ftp.c, ftp_port_bind_socket: an error other than EADDRNOTAVAIL,
        // EADDRINUSE or EACCES is failf(data, "bind(port=%hu) failed: %s", ...) -> exit 30.
        // Binding a socket that is already bound fails with EINVAL.
        var listener = new TcpConnectionListener
        {
            OpenSocket = family =>
            {
                var bound = new Socket(family, SocketType.Stream, ProtocolType.Tcp);
                bound.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                return bound;
            },
        };
        Diagnostics.Arrange("opened socket", "already bound");

        var listened = await ListenAsync(listener, new ListenTarget(IPAddress.Loopback, 0, 0));

        Diagnostics.Assert("exit code", CurlExitCode.FtpPortFailed, listened.ExitCode);
        Assert.AreEqual(CurlExitCode.FtpPortFailed, listened.ExitCode);
        StringAssert.StartsWith(listened.ErrorMessage, "bind(port=0) failed: ");
        Assert.IsNull(listened.PendingConnection);
    }

    [TestMethod]
    public async Task ListenAsync_WhenNoSocketCanBeOpened_FailsWithFtpPortFailedAndSocketFailure()
    {
        var failure = new SocketException((int)SocketError.AddressFamilyNotSupported);
        var listener = new TcpConnectionListener { OpenSocket = _ => throw failure };
        Diagnostics.Arrange("socket open failure", SocketError.AddressFamilyNotSupported);

        var listened = await ListenAsync(listener, AnyLoopbackPort);

        Diagnostics.Assert("exit code", CurlExitCode.FtpPortFailed, listened.ExitCode);
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
        Diagnostics.Arrange("listen failure", SocketError.TooManyOpenSockets);

        var listened = await ListenAsync(listener, AnyLoopbackPort);

        Diagnostics.Assert("exit code", CurlExitCode.FtpPortFailed, listened.ExitCode);
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
        var first = await ListenAsync(listener, AnyLoopbackPort);
        var port = ((IPEndPoint)first.PendingConnection!.LocalEndPoint).Port;
        Diagnostics.Arrange("port", "the one the first listen bound, then released");

        await first.PendingConnection.DisposeAsync();
        var second = await ListenAsync(listener, new ListenTarget(IPAddress.Loopback, port, port));

        Diagnostics.Assert("exit code", CurlExitCode.Ok, second.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, second.ExitCode);
        await using var pending = second.PendingConnection!;
        Diagnostics.Assert("same port", true, ((IPEndPoint)pending.LocalEndPoint).Port == port);
        Assert.AreEqual(port, ((IPEndPoint)pending.LocalEndPoint).Port);
    }

    [TestMethod]
    [DataRow(SocketError.AddressAlreadyInUse, null)]
    [DataRow(SocketError.AccessDenied, null)]
    [DataRow(SocketError.AddressNotAvailable, "bind(port=40000) on non-local address failed: ")]
    [DataRow(SocketError.InvalidArgument, "bind(port=40000) failed: ")]
    public void BindFailureMessage_MovesOnForAPortInUseOrNotPermittedAndTellsANonLocalAddressApart(SocketError error, string? expectedBeforeReason)
    {
        var exception = new SocketException((int)error);
        Diagnostics.Arrange("socket error", error);

        var message = TcpConnectionListener.BindFailureMessage(exception, 40000);

        // The reason after the prefix is the platform's own words, so only the prefix is written.
        Diagnostics.Act("message is null", message is null);
        Diagnostics.Assert("prefix", expectedBeforeReason, message?[..Math.Min(expectedBeforeReason?.Length ?? 0, message.Length)]);
        Assert.AreEqual(
            expectedBeforeReason is null ? null : expectedBeforeReason + ConnectFailureReason.Describe(exception, OperatingSystem.IsWindows()),
            message);
    }

    /// <summary>
    /// Listens on <paramref name="target" /> as the tests did directly, timing the bind and listen
    /// as one PHASE and writing the exit code as ACT.
    /// </summary>
    private async Task<ListenResult> ListenAsync(TcpConnectionListener listener, ListenTarget target)
    {
        ListenResult listened;
        using (Diagnostics.Phase("bind and listen"))
        {
            listened = await listener.ListenAsync(target, CancellationToken.None);
        }

        Diagnostics.Act("exit code", listened.ExitCode);
        return listened;
    }
}
