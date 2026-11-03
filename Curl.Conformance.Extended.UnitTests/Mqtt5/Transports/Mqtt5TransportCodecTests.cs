using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Mqtt5;
using Curl.Conformance.Extended.Mqtt5.Transports;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.Mqtt5.Transports;

/// <summary>Checks the MQTT 5 transport front ends' own codecs and edges (BL-4486).</summary>
[TestClass]
public sealed class Mqtt5TransportCodecTests
{
    [TestMethod]
    public void TakeFrom_TwoByteAndEightByteLengths_ReadsTheWholeFrame()
    {
        byte[] payload = [.. Enumerable.Range(0, 300).Select(static value => (byte)value)];
        List<byte> sixteen = [.. Mqtt5WebSocketFrame.Encode(Mqtt5WebSocketFrame.Binary, payload, maskingKey: [9, 8, 7, 6])];
        List<byte> sixtyFour = [0x82, 127, 0, 0, 0, 0, 0, 0, 0, 3, 1, 2, 3, 0xFF];

        Mqtt5WebSocketFrame? first = Mqtt5WebSocketFrame.TakeFrom(sixteen);
        Mqtt5WebSocketFrame? second = Mqtt5WebSocketFrame.TakeFrom(sixtyFour);

        CollectionAssert.AreEqual(payload, first!.Payload);
        Assert.IsEmpty(sixteen);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, second!.Payload);
        CollectionAssert.AreEqual(new byte[] { 0xFF }, sixtyFour);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x82 })]
    [DataRow(new byte[] { 0x82, 0xFE, 0 })]
    [DataRow(new byte[] { 0x82, 2, 1 })]
    public void TakeFrom_LessThanAWholeFrame_TakesNothing(byte[] bytes)
    {
        List<byte> pending = [.. bytes];

        Assert.IsNull(Mqtt5WebSocketFrame.TakeFrom(pending));
        Assert.HasCount(bytes.Length, pending);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x30 })]
    [DataRow(new byte[] { 0x30, 0x80 })]
    [DataRow(new byte[] { 0x30, 2, 0 })]
    public void TakePacket_LessThanAWholePacket_TakesNothing(byte[] bytes)
    {
        Assert.IsNull(Mqtt5WebSocketReferenceServer.TakePacket([.. bytes]));
    }

    [TestMethod]
    public void KeyOf_HeadWithoutAKey_IsEmpty()
    {
        Assert.AreEqual(string.Empty, Mqtt5WebSocketReferenceServer.KeyOf("GET /mqtt HTTP/1.1\r\nHost: x"));
        Assert.AreEqual("abc", Mqtt5WebSocketReferenceServer.KeyOf("GET /mqtt HTTP/1.1\r\nsec-websocket-key:  abc "));
    }

    [TestMethod]
    public void ConnAckInOneFrame_PacketOtherThanConnect_IsNotAnswered()
    {
        Assert.IsEmpty(Mqtt5WebSocketReferenceServer.ConnAckInOneFrame(new Mqtt5WirePacket(0x30, [0, 0, 0])));
    }

    [TestMethod]
    public async Task ServeAsync_ClientClosesBeforeTheHandshake_RecordsNoRequest()
    {
        Mqtt5WebSocketReferenceServer server = new();

        CurlRunResult result = await ExtendedConformanceHarness.RunAsync(InProcessCurl.RunAsync, [ReferenceRoute.Tcp(1883, server)], ["-sS", "-m", "1", "mqtt5://localhost/t", "-d", "hi"]);

        Assert.AreEqual(28, result.ExitCode);
        Assert.IsNull(server.UpgradeRequest);
        Assert.IsEmpty(server.ReceivedFrames);
    }

    [TestMethod]
    public async Task DelegatingDuplexStream_PassesAsyncCallsOnAndRefusesTheRest()
    {
        List<byte> written = [];
        await using DelegatingDuplexStream stream = new(
            static (buffer, _) => { buffer.Span[0] = 7; return ValueTask.FromResult(1); },
            (bytes, _) => { written.AddRange(bytes.ToArray()); return ValueTask.CompletedTask; });
        byte[] buffer = new byte[2];

        int read = await stream.ReadAsync(buffer, 1, 1, CancellationToken.None);
        await stream.WriteAsync([1, 2, 3], 1, 2, CancellationToken.None);
        await stream.WriteAsync(new byte[] { 4 }.AsMemory(), CancellationToken.None);
        await stream.FlushAsync(CancellationToken.None);
        stream.Flush();

        Assert.AreEqual(1, read);
        Assert.AreEqual(7, buffer[1]);
        CollectionAssert.AreEqual(new byte[] { 2, 3, 4 }, written);
        Assert.IsTrue(stream.CanRead && stream.CanWrite && !stream.CanSeek);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(buffer, 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write(buffer, 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }
}
