using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

[TestClass]
public sealed class UdpDatagramChannelIntegrationTests
{
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
