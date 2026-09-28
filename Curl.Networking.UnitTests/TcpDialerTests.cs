using System.Net;
using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="TcpDialer" />. The loopback test opens real sockets and is the one
/// <c>Integration</c> test in this project; the argument check needs none.
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
}
