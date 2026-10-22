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
    public async Task ReceiveAsync_ADatagramCapsule_ReturnsTheDatagramFromTheProxy()
    {
        byte[] stream = [0x00, 0x03, 0x00, 0x07, 0x08];
        await using var channel = new CapsuleDatagramChannel(new ScriptedConnection(stream), Proxy);
        var buffer = new byte[16];
        Diagnostics.Arrange("proxy", Proxy);
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
    [DataRow(new byte[] { 0x40, 0x41, 0x02, 0xAA, 0xBB }, DisplayName = "another capsule type")]
    [DataRow(new byte[] { 0x00, 0x03, 0x02, 0xCC, 0xDD }, DisplayName = "another context ID")]
    [DataRow(new byte[] { 0x00, 0x00, 0x00 }, DisplayName = "capsule length 0")]
    public async Task ReceiveAsync_ACapsuleCurlRejects_FailsWithConnectionAborted(byte[] stream)
    {
        await using var channel = new CapsuleDatagramChannel(new ScriptedConnection([.. stream, .. CapsuleDatagramChannel.BuildCapsule([9])]), Proxy);
        Diagnostics.Bytes("stream", stream);

        var exception = await Assert.ThrowsExactlyAsync<SocketException>(() => channel.ReceiveAsync(new byte[8], CancellationToken.None).AsTask());
        Diagnostics.Act("socket error code", exception.SocketErrorCode);

        Diagnostics.Assert("socket error code", SocketError.ConnectionAborted, exception.SocketErrorCode);
        Assert.AreEqual(SocketError.ConnectionAborted, exception.SocketErrorCode);
    }

    [TestMethod]
    public async Task ReceiveAsync_ADatagramLongerThanTheBuffer_FailsAndIsDiscarded()
    {
        var connection = new ScriptedConnection([.. CapsuleDatagramChannel.BuildCapsule([1, 2, 3, 4, 5]), .. CapsuleDatagramChannel.BuildCapsule([9])]);
        await using var channel = new CapsuleDatagramChannel(connection, Proxy);
        var buffer = new byte[2];
        Diagnostics.Arrange("first datagram length", 5);
        Diagnostics.Arrange("buffer length", buffer.Length);

        var exception = await Assert.ThrowsExactlyAsync<SocketException>(() => channel.ReceiveAsync(buffer, CancellationToken.None).AsTask());
        var second = await channel.ReceiveAsync(buffer, CancellationToken.None);
        Diagnostics.Act("socket error code", exception.SocketErrorCode);
        Diagnostics.Act("second length", second.Length);

        Diagnostics.Assert("socket error code", SocketError.ConnectionAborted, exception.SocketErrorCode);
        Assert.AreEqual(SocketError.ConnectionAborted, exception.SocketErrorCode);
        Diagnostics.Assert("second length", 1, second.Length);
        Assert.AreEqual(1, second.Length);
        Diagnostics.Assert("second first byte", 9, buffer[0]);
        Assert.AreEqual(9, buffer[0]);
    }

    [TestMethod]
    public async Task ReceiveAsync_AnEmptyBuffer_ThrowsArgumentException()
    {
        await using var channel = new CapsuleDatagramChannel(new ScriptedConnection(CapsuleDatagramChannel.BuildCapsule([9])), Proxy);

        var exception = await Assert.ThrowsExactlyAsync<ArgumentException>(() => channel.ReceiveAsync(Memory<byte>.Empty, CancellationToken.None).AsTask());
        Diagnostics.Act("parameter name", exception.ParamName);

        Diagnostics.Assert("parameter name", "buffer", exception.ParamName);
        Assert.AreEqual("buffer", exception.ParamName);
    }

    [TestMethod]
    public async Task ReceiveAsync_ADatagramLongerThanTheFirstReceiveBuffer_ReturnsItWhole()
    {
        var datagram = new byte[10000];
        datagram[^1] = 0x5A;
        await using var channel = new CapsuleDatagramChannel(new ScriptedConnection(CapsuleDatagramChannel.BuildCapsule(datagram)), Proxy);
        var buffer = new byte[datagram.Length];
        Diagnostics.Arrange("datagram length", datagram.Length);

        var received = await channel.ReceiveAsync(buffer, CancellationToken.None);
        Diagnostics.Act("received length", received.Length);

        Diagnostics.Assert("received length", datagram.Length, received.Length);
        Assert.AreEqual(datagram.Length, received.Length);
        Diagnostics.Assert("last byte", 0x5A, buffer[^1]);
        Assert.AreEqual(0x5A, buffer[^1]);
    }

    [TestMethod]
    public void DecodeDatagram_ALengthPastIntMaxValue_IsMalformedAndConsumesNothing()
    {
        byte[] received = [0x00, 0xC0, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00];

        var status = CapsuleDatagramChannel.DecodeDatagram(received, new byte[8], out var consumed, out var payloadLength);
        Diagnostics.Act("status", status);
        Diagnostics.Act("consumed", consumed);

        Diagnostics.Assert("status", CapsuleDatagramChannel.DecodeStatus.Malformed, status);
        Assert.AreEqual(CapsuleDatagramChannel.DecodeStatus.Malformed, status);
        Assert.AreEqual(0, consumed);
        Assert.AreEqual(0, payloadLength);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x00, 0x40 }, DisplayName = "length cut short")]
    [DataRow(new byte[] { 0x00, 0x02 }, DisplayName = "context ID not yet received")]
    public void DecodeDatagram_AHeaderNotAllReceived_NeedsMoreBytesAndConsumesNothing(byte[] received)
    {
        var status = CapsuleDatagramChannel.DecodeDatagram(received, new byte[8], out var consumed, out _);
        Diagnostics.Act("status", status);

        Diagnostics.Assert("status", CapsuleDatagramChannel.DecodeStatus.NeedMoreBytes, status);
        Assert.AreEqual(CapsuleDatagramChannel.DecodeStatus.NeedMoreBytes, status);
        Assert.AreEqual(0, consumed);
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
