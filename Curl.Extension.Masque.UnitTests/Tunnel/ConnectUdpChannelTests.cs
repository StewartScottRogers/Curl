using System.Net;
using System.Net.Sockets;
using Curl.Extension.Masque.Wire;

namespace Curl.Extension.Masque.Tunnel;

/// <summary>Checks that a CONNECT-UDP tunnel carries datagrams as RFC 9298 section 5 DATAGRAM capsules.</summary>
[TestClass]
public sealed class ConnectUdpChannelTests
{
    private static readonly byte[] Reply = MasqueProxyBytes.SwitchingProtocols("connect-udp");

    [TestMethod]
    public async Task SendAsync_Datagram_WritesOneDatagramCapsuleWithContextIdZero()
    {
        var (channel, connection) = await MasqueProxyBytes.OpenAsync(MasqueTunnelKind.ConnectUdp, "192.0.2.1", 69, Reply);
        var requestLength = connection.Written.Length;

        await channel.SendAsync(new byte[] { 1, 2, 3 }, new IPEndPoint(IPAddress.Loopback, 9), CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 0x00, 0x04, 0x00, 1, 2, 3 }, connection.Written[requestLength..]);
    }

    [TestMethod]
    public async Task ReceiveAsync_UnknownCapsuleAndOtherContextId_AreDroppedAndTheDatagramIsHandedBack()
    {
        byte[] stream = [.. Reply, .. MasqueProxyBytes.Capsule(0x2a, 9, 9), .. MasqueProxyBytes.Capsule(0x00, 0x01, 7), .. MasqueProxyBytes.Datagram([5, 6])];
        var (channel, _) = await MasqueProxyBytes.OpenAsync(MasqueTunnelKind.ConnectUdp, "192.0.2.1", 69, stream[..^2], stream[^2..]);
        var buffer = new byte[16];

        var received = await channel.ReceiveAsync(buffer, CancellationToken.None);

        Assert.AreEqual(2, received.Length);
        CollectionAssert.AreEqual(new byte[] { 5, 6 }, buffer[..2]);
        Assert.AreEqual(new IPEndPoint(IPAddress.Parse("192.0.2.1"), 69), received.RemoteEndPoint);
    }

    [TestMethod]
    public async Task ReceiveAsync_DatagramLongerThanBuffer_IsCutToFit()
    {
        var (channel, _) = await MasqueProxyBytes.OpenAsync(MasqueTunnelKind.ConnectUdp, "192.0.2.1", 69, [.. Reply, .. MasqueProxyBytes.Datagram([1, 2, 3])]);
        var buffer = new byte[2];

        var received = await channel.ReceiveAsync(buffer, CancellationToken.None);

        Assert.AreEqual(2, received.Length);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, buffer);
    }

    [TestMethod]
    public async Task ReceiveAsync_ProxyClosedTheTunnel_FailsAsAResetSocket()
    {
        var (channel, _) = await MasqueProxyBytes.OpenAsync(MasqueTunnelKind.ConnectUdp, "192.0.2.1", 69, Reply);

        var exception = await Assert.ThrowsExactlyAsync<SocketException>(async () => await channel.ReceiveAsync(new byte[8], CancellationToken.None));

        Assert.AreEqual(SocketError.ConnectionReset, exception.SocketErrorCode);
    }

    [TestMethod]
    public async Task ServerEndPoint_HostName_IsTheNamedHostAndPort()
    {
        var (channel, _) = await MasqueProxyBytes.OpenAsync(MasqueTunnelKind.ConnectUdp, "tftp.example", 69, Reply);

        Assert.AreEqual(new DnsEndPoint("tftp.example", 69), channel.ServerEndPoint);
    }

    [TestMethod]
    public async Task DisposeAsync_Channel_DisposesTheProxyConnection()
    {
        var (channel, connection) = await MasqueProxyBytes.OpenAsync(MasqueTunnelKind.ConnectUdp, "192.0.2.1", 69, Reply);

        await channel.DisposeAsync();

        Assert.IsTrue(connection.IsDisposed);
    }
}
