using Curl.Api.Codes;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Options.Protocols;
using Curl.Core;

namespace Curl.Engine;

[TestClass]
public sealed class Cgr253Tests
{
    private static byte[] BinaryFrameHead(int length) => [0x82, 126, (byte)(length >> 8), (byte)length];

    [TestMethod]
    public async Task ReceiveAsync_FrameArrivingInTwoReadsAndABufferHoldingIt_HandsItOverInOneCall()
    {
        byte[] payload = [.. Enumerable.Range(0, 300).Select(i => (byte)i)];
        ManagedEasyWebSocket webSocket = new(() => true, () => WebSocketOptionBits.None, new ManagedEasyWebSocketTests.ZeroMaskSource());
        QueuedConnection peer = new();
        await webSocket.OpenAsync(peer, BinaryFrameHead(300).Concat(payload[..120]).ToArray());
        peer.Enqueue(payload[120..]);
        byte[] buffer = new byte[1000];

        CurlWebSocketReceiveResult result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);

        Assert.AreEqual(new CurlWebSocketReceiveResult(CurlCode.Ok, 300, new CurlWebSocketFrame(0, CurlWebSocketFlags.Binary, 0, 0, 300)), result);
        CollectionAssert.AreEqual(payload, buffer[..300]);
        Assert.AreEqual(result.Frame, webSocket.CurrentFrame);
    }

    [TestMethod]
    public async Task ReceiveAsync_FrameWhoseRestHasNotArrived_HandsOverWhatCameWithBytesLeft()
    {
        byte[] payload = [.. Enumerable.Range(0, 300).Select(i => (byte)i)];
        ManagedEasyWebSocket webSocket = new(() => true, () => WebSocketOptionBits.None, new ManagedEasyWebSocketTests.ZeroMaskSource());
        await webSocket.OpenAsync(new QueuedConnection(), BinaryFrameHead(300).Concat(payload[..120]).ToArray());

        CurlWebSocketReceiveResult result = await webSocket.ReceiveAsync(new byte[1000], CancellationToken.None);

        Assert.AreEqual(new CurlWebSocketReceiveResult(CurlCode.Ok, 120, new CurlWebSocketFrame(0, CurlWebSocketFlags.Binary, 0, 180, 120)), result);
    }
}
