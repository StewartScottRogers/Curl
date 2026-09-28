using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="TcpPendingConnection" /> through its accept seam, over a socket bound to
/// loopback that nothing connects to; the real accept is in
/// <see cref="TcpConnectionListenerTests" />' <c>Integration</c> test (BL-456).
/// </summary>
[TestClass]
public sealed class TcpPendingConnectionTests
{
    [TestMethod]
    public async Task AcceptAsync_WhenAConnectionIsAccepted_ReturnsItWithItsLocalEndPoint()
    {
        var localEndPoint = new IPEndPoint(IPAddress.Loopback, 50000);
        var accepted = new StreamConnection(new MemoryStream(), new IPEndPoint(IPAddress.Loopback, 21), localEndPoint);
        await using var pending = new TcpPendingConnection(BoundSocket())
        {
            AcceptConnectionAsync = (_, _) => ValueTask.FromResult(accepted),
        };

        var result = await pending.AcceptAsync(CancellationToken.None);

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
        await using var pending = new TcpPendingConnection(BoundSocket())
        {
            AcceptConnectionAsync = (_, _) => throw failure,
        };

        var result = await pending.AcceptAsync(CancellationToken.None);

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

        Assert.AreEqual(socket.LocalEndPoint, pending.LocalEndPoint);
        await pending.DisposeAsync();
        Assert.ThrowsExactly<ObjectDisposedException>(() => socket.Listen(1));
    }

    private static Socket BoundSocket()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return socket;
    }
}
