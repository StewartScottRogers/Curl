using System.Net;
using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="CapsuleDatagramChannel" /> to RFC 9297's <c>DATAGRAM</c> capsule as curl
/// 8.22.0 writes it (BL-942 Notes: <c>00 44 b1 00</c> before a 1200-byte QUIC packet).
/// </summary>
[TestClass]
public sealed class CapsuleDatagramChannelTests
{
    private static readonly IPEndPoint Proxy = new(IPAddress.Parse("192.0.2.10"), 3128);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void BuildCapsule_ForA1200ByteDatagram_WritesCurlsTypeLengthAndContextId()
    {
        Diagnostics.Arrange("datagram length", 1200);

        var capsule = CapsuleDatagramChannel.BuildCapsule(new byte[1200]);
        Diagnostics.Act("capsule length", capsule.Length);
        Diagnostics.Bytes("capsule header", capsule[..4]);

        var expectedHeader = new byte[] { 0x00, 0x44, 0xB1, 0x00 };
        Diagnostics.Diff("capsule header", expectedHeader, capsule[..4]);
        CollectionAssert.AreEqual(expectedHeader, capsule[..4]);
        Diagnostics.Assert("capsule length", 1204, capsule.Length);
        Assert.HasCount(1204, capsule);
    }

    [TestMethod]
    public async Task SendAsync_WritesOneCapsuleAndFlushes()
    {
        var connection = new ScriptedConnection([]);
        await using var channel = new CapsuleDatagramChannel(connection, Proxy);
        var destination = new IPEndPoint(IPAddress.Loopback, 443);
        Diagnostics.Arrange("proxy", Proxy);
        Diagnostics.Arrange("destination", destination);
        Diagnostics.Arrange("payload", "01 02 03");

        using (Diagnostics.Phase("send"))
        {
            await channel.SendAsync(new byte[] { 1, 2, 3 }, destination, CancellationToken.None);
        }

        Diagnostics.Act("flush count", connection.FlushCount);
        Diagnostics.Bytes("written", connection.Written.ToArray());

        var expected = new byte[] { 0x00, 0x04, 0x00, 1, 2, 3 };
        Diagnostics.Diff("written", expected, connection.Written.ToArray());
        CollectionAssert.AreEqual(expected, connection.Written);
        Diagnostics.Assert("flush count", 1, connection.FlushCount);
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
        Diagnostics.Arrange("proxy", Proxy);
        Diagnostics.Arrange("buffer length", buffer.Length);
        Diagnostics.Bytes("stream", stream);

        var received = await channel.ReceiveAsync(buffer, CancellationToken.None);
        Diagnostics.Act("received length", received.Length);
        Diagnostics.Act("received remote end point", received.RemoteEndPoint);

        Diagnostics.Assert("received length", 2, received.Length);
        Assert.AreEqual(2, received.Length);
        var expectedBytes = new byte[] { 0x07, 0x08 };
        Diagnostics.Diff("received bytes", expectedBytes, buffer[..2]);
        CollectionAssert.AreEqual(expectedBytes, buffer[..2]);
        Diagnostics.Assert("received remote end point", Proxy, received.RemoteEndPoint);
        Assert.AreEqual(Proxy, received.RemoteEndPoint);
        Diagnostics.Assert("server end point", Proxy, channel.ServerEndPoint);
        Assert.AreEqual(Proxy, channel.ServerEndPoint);
    }

    [TestMethod]
    public async Task ReceiveAsync_ADatagramLongerThanTheBuffer_IsCutToItAndTheRestSkipped()
    {
        var datagram = CapsuleDatagramChannel.BuildCapsule([1, 2, 3, 4, 5]);
        var connection = new ScriptedConnection([.. datagram, .. CapsuleDatagramChannel.BuildCapsule([9])]);
        await using var channel = new CapsuleDatagramChannel(connection, Proxy);
        var buffer = new byte[2];
        Diagnostics.Arrange("first datagram length", 5);
        Diagnostics.Arrange("second datagram length", 1);
        Diagnostics.Arrange("buffer length", buffer.Length);

        var first = await channel.ReceiveAsync(buffer, CancellationToken.None);
        var firstBytes = buffer.ToArray();
        var second = await channel.ReceiveAsync(buffer, CancellationToken.None);
        Diagnostics.Act("first length", first.Length);
        Diagnostics.Act("second length", second.Length);
        Diagnostics.Bytes("first bytes", firstBytes);

        Diagnostics.Assert("first length", 2, first.Length);
        Assert.AreEqual(2, first.Length);
        var expectedFirst = new byte[] { 1, 2 };
        Diagnostics.Diff("first bytes", expectedFirst, firstBytes);
        CollectionAssert.AreEqual(expectedFirst, firstBytes);
        Diagnostics.Assert("second length", 1, second.Length);
        Assert.AreEqual(1, second.Length);
        Diagnostics.Assert("second first byte", 9, buffer[0]);
        Assert.AreEqual(9, buffer[0]);
    }

    [TestMethod]
    public async Task ReceiveAsync_WhenTheTunnelEndsMidCapsule_ThrowsConnectionReset()
    {
        byte[] truncated = [0x00, 0x05, 0x00, 1];
        await using var channel = new CapsuleDatagramChannel(new ScriptedConnection(truncated), Proxy);
        Diagnostics.Arrange("truncated capsule length", truncated.Length);
        Diagnostics.Arrange("declared capsule length", 5);

        var exception = await Assert.ThrowsExactlyAsync<SocketException>(() => channel.ReceiveAsync(new byte[8], CancellationToken.None).AsTask());
        Diagnostics.Act("exception type", exception.GetType().Name);
        Diagnostics.Act("socket error code", exception.SocketErrorCode);

        Diagnostics.Assert("socket error code", SocketError.ConnectionReset, exception.SocketErrorCode);
        Assert.AreEqual(SocketError.ConnectionReset, exception.SocketErrorCode);
    }

    [TestMethod]
    public async Task ReceiveAsync_ASkippedCapsuleLongerThanTheSkipBuffer_IsSkippedWhole()
    {
        byte[] large = [0x41, 0x00, 0x52, 0x08, .. new byte[0x1208], .. CapsuleDatagramChannel.BuildCapsule([6])];
        await using var channel = new CapsuleDatagramChannel(new ScriptedConnection(large), Proxy);
        var buffer = new byte[4];
        Diagnostics.Arrange("stream length", large.Length);
        Diagnostics.Arrange("skipped capsule payload length", 0x1208);

        var received = await channel.ReceiveAsync(buffer, CancellationToken.None);
        Diagnostics.Act("received length", received.Length);
        Diagnostics.Act("first buffer byte", buffer[0]);

        Diagnostics.Assert("received length", 1, received.Length);
        Assert.AreEqual(1, received.Length);
        Diagnostics.Assert("first buffer byte", 6, buffer[0]);
        Assert.AreEqual(6, buffer[0]);
    }

    [TestMethod]
    public async Task LocalEndPoint_IsTheConnections()
    {
        await using var channel = new CapsuleDatagramChannel(new CapsuleQuicProxyConnection(string.Empty, null), Proxy);
        var expected = new IPEndPoint(IPAddress.Loopback, 50000);
        Diagnostics.Arrange("proxy", Proxy);

        var localEndPoint = channel.LocalEndPoint;
        Diagnostics.Act("local end point", localEndPoint);

        Diagnostics.Assert("local end point", expected, localEndPoint);
        Assert.AreEqual(expected, channel.LocalEndPoint);
    }

    [TestMethod]
    public async Task DisposeAsync_Twice_DisposesTheConnectionOnce()
    {
        var connection = new ScriptedConnection([]);
        var channel = new CapsuleDatagramChannel(connection, Proxy);
        Diagnostics.Arrange("proxy", Proxy);

        await channel.DisposeAsync();
        await channel.DisposeAsync();
        Diagnostics.Act("connection disposed", connection.IsDisposed);

        Diagnostics.Assert("connection disposed", true, connection.IsDisposed);
        Assert.IsTrue(connection.IsDisposed);
    }
}
