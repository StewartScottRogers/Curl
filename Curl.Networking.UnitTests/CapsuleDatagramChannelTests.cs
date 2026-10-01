using System.Net;
using System.Net.Sockets;

using Curl.Networking.Fakes;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="CapsuleDatagramChannel" /> to RFC 9297's <c>DATAGRAM</c> capsule as curl
/// 8.22.0 writes it (BL-942 Notes: <c>00 44 b1 00</c> before a 1200-byte QUIC packet).
/// </summary>
[TestClass]
public sealed class CapsuleDatagramChannelTests
{
    private static readonly IPEndPoint Proxy = new(IPAddress.Parse("192.0.2.10"), 3128);

    [TestMethod]
    public void BuildCapsule_ForA1200ByteDatagram_WritesCurlsTypeLengthAndContextId()
    {
        var capsule = CapsuleDatagramChannel.BuildCapsule(new byte[1200]);

        CollectionAssert.AreEqual(new byte[] { 0x00, 0x44, 0xB1, 0x00 }, capsule[..4]);
        Assert.HasCount(1204, capsule);
    }

    [TestMethod]
    public async Task SendAsync_WritesOneCapsuleAndFlushes()
    {
        var connection = new ScriptedConnection([]);
        await using var channel = new CapsuleDatagramChannel(connection, Proxy);

        await channel.SendAsync(new byte[] { 1, 2, 3 }, new IPEndPoint(IPAddress.Loopback, 443), CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 0x00, 0x04, 0x00, 1, 2, 3 }, connection.Written);
        Assert.AreEqual(1, connection.FlushCount);
    }

    [TestMethod]
    public async Task ReceiveAsync_SkipsOtherCapsulesAndOtherContexts_AndReturnsTheDatagramFromTheProxy()
    {
        byte[] stream =
        [
            0x40, 0x41, 0x02, 0xAA, 0xBB, // capsule type 0x41, two bytes
            0x00, 0x00, // a DATAGRAM capsule with no context ID
            0x00, 0x03, 0x02, 0xCC, 0xDD, // context ID 2
            0x00, 0x03, 0x00, 0x07, 0x08, // the datagram 07 08
        ];
        await using var channel = new CapsuleDatagramChannel(new ScriptedConnection(stream), Proxy);
        var buffer = new byte[16];

        var received = await channel.ReceiveAsync(buffer, CancellationToken.None);

        Assert.AreEqual(2, received.Length);
        CollectionAssert.AreEqual(new byte[] { 0x07, 0x08 }, buffer[..2]);
        Assert.AreEqual(Proxy, received.RemoteEndPoint);
        Assert.AreEqual(Proxy, channel.ServerEndPoint);
    }

    [TestMethod]
    public async Task ReceiveAsync_ADatagramLongerThanTheBuffer_IsCutToItAndTheRestSkipped()
    {
        var datagram = CapsuleDatagramChannel.BuildCapsule([1, 2, 3, 4, 5]);
        var connection = new ScriptedConnection([.. datagram, .. CapsuleDatagramChannel.BuildCapsule([9])]);
        await using var channel = new CapsuleDatagramChannel(connection, Proxy);
        var buffer = new byte[2];

        var first = await channel.ReceiveAsync(buffer, CancellationToken.None);
        var firstBytes = buffer.ToArray();
        var second = await channel.ReceiveAsync(buffer, CancellationToken.None);

        Assert.AreEqual(2, first.Length);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, firstBytes);
        Assert.AreEqual(1, second.Length);
        Assert.AreEqual(9, buffer[0]);
    }

    [TestMethod]
    public async Task ReceiveAsync_WhenTheTunnelEndsMidCapsule_ThrowsConnectionReset()
    {
        await using var channel = new CapsuleDatagramChannel(new ScriptedConnection([0x00, 0x05, 0x00, 1]), Proxy);

        var exception = await Assert.ThrowsExactlyAsync<SocketException>(() => channel.ReceiveAsync(new byte[8], CancellationToken.None).AsTask());

        Assert.AreEqual(SocketError.ConnectionReset, exception.SocketErrorCode);
    }

    [TestMethod]
    public async Task ReceiveAsync_ASkippedCapsuleLongerThanTheSkipBuffer_IsSkippedWhole()
    {
        byte[] large = [0x41, 0x00, 0x52, 0x08, .. new byte[0x1208], .. CapsuleDatagramChannel.BuildCapsule([6])];
        await using var channel = new CapsuleDatagramChannel(new ScriptedConnection(large), Proxy);
        var buffer = new byte[4];

        var received = await channel.ReceiveAsync(buffer, CancellationToken.None);

        Assert.AreEqual(1, received.Length);
        Assert.AreEqual(6, buffer[0]);
    }

    [TestMethod]
    public async Task LocalEndPoint_IsTheConnections()
    {
        await using var channel = new CapsuleDatagramChannel(new CapsuleQuicProxyConnection(string.Empty, null), Proxy);

        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 50000), channel.LocalEndPoint);
    }

    [TestMethod]
    public async Task DisposeAsync_Twice_DisposesTheConnectionOnce()
    {
        var connection = new ScriptedConnection([]);
        var channel = new CapsuleDatagramChannel(connection, Proxy);

        await channel.DisposeAsync();
        await channel.DisposeAsync();

        Assert.IsTrue(connection.IsDisposed);
    }
}
