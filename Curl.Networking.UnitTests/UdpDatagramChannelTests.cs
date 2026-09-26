using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="UdpDatagramChannel" />. Opening, disposing and cancelling touch only a
/// local loopback socket and send nothing; the round trip sends datagrams and is tagged
/// <c>Integration</c>.
/// </summary>
[TestClass]
public sealed class UdpDatagramChannelTests
{
    private static readonly IPEndPoint TftpOnLoopback = new(IPAddress.Loopback, 69);

    [TestMethod]
    public void Constructor_WithNullServerEndPoint_ThrowsArgumentNullException()
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new UdpDatagramChannel(null!));

        Assert.AreEqual("serverEndPoint", exception.ParamName);
    }

    [TestMethod]
    public void Constructor_WhenTheSocketCannotBeBound_DisposesItAndRethrows()
    {
        Socket? boundSocket = null;

        var exception = Assert.ThrowsExactly<SocketException>(
            () => new UdpDatagramChannel(
                TftpOnLoopback,
                (socket, _) =>
                {
                    boundSocket = socket;
                    throw new SocketException((int)SocketError.AddressAlreadyInUse);
                }));

        Assert.AreEqual(SocketError.AddressAlreadyInUse, exception.SocketErrorCode);
        Assert.IsNotNull(boundSocket);
        Assert.ThrowsExactly<ObjectDisposedException>(() => boundSocket.Bind(new IPEndPoint(IPAddress.Any, 0)));
    }

    [TestMethod]
    public async Task Constructor_ForAnIPv4ServerEndPoint_BindsAnIPv4SocketToAnEphemeralPort()
    {
        await using var channel = new UdpDatagramChannel(TftpOnLoopback);

        var local = (IPEndPoint)channel.LocalEndPoint;
        Assert.AreEqual(AddressFamily.InterNetwork, local.AddressFamily);
        Assert.AreNotEqual(0, local.Port);
        Assert.AreEqual(TftpOnLoopback, channel.ServerEndPoint);
    }

    [TestMethod]
    public async Task Constructor_ForAnIPv6ServerEndPoint_BindsAnIPv6Socket()
    {
        await using var channel = new UdpDatagramChannel(new IPEndPoint(IPAddress.IPv6Loopback, 69));

        Assert.AreEqual(AddressFamily.InterNetworkV6, channel.LocalEndPoint.AddressFamily);
    }

    [TestMethod]
    public async Task SendAsync_WithNullDestination_ThrowsArgumentNullException()
    {
        await using var channel = new UdpDatagramChannel(TftpOnLoopback);

        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await channel.SendAsync(new byte[] { 1 }, null!, CancellationToken.None));

        Assert.AreEqual("destination", exception.ParamName);
    }

    [TestMethod]
    public async Task ReceiveAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        await using var channel = new UdpDatagramChannel(TftpOnLoopback);
        using var cancellation = new CancellationTokenSource();

        var receive = channel.ReceiveAsync(new byte[16], cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await receive);
    }

    [TestMethod]
    public async Task DisposeAsync_ClosesTheSocket()
    {
        var channel = new UdpDatagramChannel(TftpOnLoopback);

        await channel.DisposeAsync();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
            async () => await channel.ReceiveAsync(new byte[16], CancellationToken.None));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SendAndReceive_OverLoopback_ReportTheReplyFromTheTransferPort()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var wellKnownPort = BindLoopbackUdpSocket();
        using var transferPort = BindLoopbackUdpSocket();
        var serverEndPoint = (IPEndPoint)wellKnownPort.LocalEndPoint!;
        var transferEndPoint = (IPEndPoint)transferPort.LocalEndPoint!;
        await using var channel = new UdpDatagramChannel(serverEndPoint);

        await channel.SendAsync(new byte[] { 0, 1, 2 }, channel.ServerEndPoint, cancellation.Token);
        var request = await wellKnownPort.ReceiveFromAsync(
            new byte[16], SocketFlags.None, new IPEndPoint(IPAddress.Any, 0), cancellation.Token);
        var clientPort = ((IPEndPoint)request.RemoteEndPoint).Port;
        await transferPort.SendToAsync(
            new byte[] { 0, 3, 0, 1, 9 }, SocketFlags.None, new IPEndPoint(IPAddress.Loopback, clientPort), cancellation.Token);
        DatagramReceived received = await channel.ReceiveAsync(new byte[16], cancellation.Token);

        Assert.AreEqual(3, request.ReceivedBytes);
        Assert.AreEqual(5, received.Length);
        Assert.AreEqual(transferEndPoint, received.RemoteEndPoint);
        Assert.AreNotEqual(serverEndPoint.Port, ((IPEndPoint)received.RemoteEndPoint).Port);
    }

    private static Socket BindLoopbackUdpSocket()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return socket;
    }
}
