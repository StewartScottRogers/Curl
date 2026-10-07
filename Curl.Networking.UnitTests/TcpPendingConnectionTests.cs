using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="TcpPendingConnection" /> through its accept seam, over a socket bound to
/// loopback that nothing connects to; the real accept is in
/// <see cref="TcpConnectionListenerTests" />' <c>Integration</c> test (BL-456).
/// </summary>
[TestClass]
public sealed class TcpPendingConnectionTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task AcceptAsync_WhenAConnectionIsAccepted_ReturnsItWithItsLocalEndPoint()
    {
        var localEndPoint = new IPEndPoint(IPAddress.Loopback, 50000);
        var accepted = new StreamConnection(new MemoryStream(), new IPEndPoint(IPAddress.Loopback, 21), localEndPoint);
        Diagnostics.Arrange("accepted connection local end point", localEndPoint);
        await using var pending = new TcpPendingConnection(BoundSocket())
        {
            AcceptConnectionAsync = (_, _) => ValueTask.FromResult(accepted),
        };

        ConnectResult result;
        using (Diagnostics.Phase("accept"))
        {
            result = await pending.AcceptAsync(CancellationToken.None);
        }

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("connection is the accepted one", ReferenceEquals(accepted, result.Connection));
        Diagnostics.Act("local end point", result.LocalEndPoint);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("connection is the accepted one", true, ReferenceEquals(accepted, result.Connection));
        Diagnostics.Assert("local end point", localEndPoint, result.LocalEndPoint);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreSame(accepted, result.Connection);
        Assert.AreSame(localEndPoint, result.LocalEndPoint);
    }

    [TestMethod]
    public async Task AcceptAsync_WhenTheAcceptFails_FailsWithFtpAcceptFailedAndCurlsReason()
    {
        // curl 8.21.0's lib/cf-socket.c, cf_tcp_accept_connect:
        // failf(data, "Error accept()ing server connect: %s", curlx_strerror(...)) -> CURLE_FTP_ACCEPT_FAILED.
        var failure = new SocketException((int)SocketError.ConnectionReset);
        Diagnostics.Arrange("accept failure", SocketError.ConnectionReset);
        await using var pending = new TcpPendingConnection(BoundSocket())
        {
            AcceptConnectionAsync = (_, _) => throw failure,
        };

        ConnectResult result;
        using (Diagnostics.Phase("accept"))
        {
            result = await pending.AcceptAsync(CancellationToken.None);
        }

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("connection is null", result.Connection is null);
        Diagnostics.Assert("exit code", CurlExitCode.FtpAcceptFailed, result.ExitCode);
        Diagnostics.Assert(
            "error message starts with",
            true,
            result.ErrorMessage?.StartsWith("Error accept()ing server connect: ", StringComparison.Ordinal));
        Diagnostics.Assert("connection is null", true, result.Connection is null);

        Assert.AreEqual(CurlExitCode.FtpAcceptFailed, result.ExitCode);
        Assert.AreEqual(
            "Error accept()ing server connect: " + ConnectFailureReason.Describe(failure, OperatingSystem.IsWindows()),
            result.ErrorMessage);
        Assert.IsNull(result.Connection);
    }

    [TestMethod]
    public async Task LocalEndPoint_IsTheListeningSocketsAndDisposeClosesIt()
    {
        var socket = BoundSocket();
        var pending = new TcpPendingConnection(socket);
        Diagnostics.Arrange("socket", "bound to loopback, port chosen by the system");

        Diagnostics.Act("pending local end point is the socket's", Equals(socket.LocalEndPoint, pending.LocalEndPoint));
        Diagnostics.Assert("pending local end point is the socket's", true, Equals(socket.LocalEndPoint, pending.LocalEndPoint));
        Assert.AreEqual(socket.LocalEndPoint, pending.LocalEndPoint);

        using (Diagnostics.Phase("dispose"))
        {
            await pending.DisposeAsync();
        }

        var exception = Assert.ThrowsExactly<ObjectDisposedException>(() => socket.Listen(1));

        Diagnostics.Act("exception type after dispose", exception.GetType().Name);
        Diagnostics.Assert("exception type after dispose", nameof(ObjectDisposedException), exception.GetType().Name);
    }

    [TestMethod]
    public void TurnOffNagle_SetsNoDelayOnTheAcceptedSocket()
    {
        // AF-0030: the accept step sets TCP_NODELAY, as curl does on every connection.
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.NoDelay = false;
        Diagnostics.Arrange("no delay before", socket.NoDelay);

        TcpPendingConnection.TurnOffNagle(socket);

        Diagnostics.Act("no delay after", socket.NoDelay);
        Diagnostics.Assert("no delay after", true, socket.NoDelay);

        Assert.IsTrue(socket.NoDelay);
    }

    private static Socket BoundSocket()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return socket;
    }
}
