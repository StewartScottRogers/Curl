using Curl.Api.Codes;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Options.Protocols;
using Curl.Original.Protocol.Ws;

namespace Curl.Engine;

/// <summary>
/// Pins CGR-123 (BL-3652): curl 8.22.0's <c>curl_ws_send</c> and <c>curl_ws_start_frame</c> refuse a close, ping or
/// pong frame whose payload is over 125 bytes with <c>CURLE_TOO_LARGE</c> and sent 0, writing nothing (measured
/// against libcurl 8.22.0 Schannel with CONNECT_ONLY 2); a 125-byte ping still goes out.
/// </summary>
[TestClass]
public sealed class Cgr123Tests
{
    private static async Task<(ManagedEasyWebSocket WebSocket, QueuedConnection Peer)> OpenAsync()
    {
        ManagedEasyWebSocket webSocket = new(() => true, () => WebSocketOptionBits.None, new ManagedEasyWebSocketTests.ZeroMaskSource());
        QueuedConnection peer = new();
        await webSocket.OpenAsync(peer, ReadOnlyMemory<byte>.Empty);
        return (webSocket, peer);
    }

    [TestMethod]
    [DataRow(CurlWebSocketFlags.Ping, 126)]
    [DataRow(CurlWebSocketFlags.Close, 200)]
    [DataRow(CurlWebSocketFlags.Pong, 126)]
    public async Task SendAsync_ControlFrameOver125Bytes_AnswersTooLargeAndWritesNothing(CurlWebSocketFlags flags, int length)
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync();

        CurlWebSocketSendResult sent = await webSocket.SendAsync(new byte[length], 0, flags, CancellationToken.None);

        Assert.AreEqual(new CurlWebSocketSendResult(CurlCode.TooLarge, 0), sent);
        Assert.IsEmpty(peer.Written);
    }

    [TestMethod]
    public async Task SendAsync_PingOver125BytesUnderOffset_AnswersTooLargeAndWritesNothing()
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync();

        CurlWebSocketSendResult sent = await webSocket.SendAsync(new byte[10], 126, CurlWebSocketFlags.Ping | CurlWebSocketFlags.Offset, CancellationToken.None);

        Assert.AreEqual(new CurlWebSocketSendResult(CurlCode.TooLarge, 0), sent);
        Assert.IsEmpty(peer.Written);
    }

    [TestMethod]
    public async Task StartFrameAsync_PingOver125Bytes_AnswersTooLargeAndWritesNothing()
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync();

        CurlCode started = await webSocket.StartFrameAsync(CurlWebSocketFlags.Ping, 126, CancellationToken.None);

        Assert.AreEqual(CurlCode.TooLarge, started);
        Assert.IsEmpty(peer.Written);
    }

    [TestMethod]
    public async Task SendAsync_PingOf125BytesThenA126ByteBinaryFrame_SendsBoth()
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync();

        CurlWebSocketSendResult ping = await webSocket.SendAsync(new byte[125], 0, CurlWebSocketFlags.Ping, CancellationToken.None);
        CurlWebSocketSendResult binary = await webSocket.SendAsync(new byte[126], 0, CurlWebSocketFlags.Binary, CancellationToken.None);

        Assert.AreEqual(new CurlWebSocketSendResult(CurlCode.Ok, 125), ping);
        Assert.AreEqual(new CurlWebSocketSendResult(CurlCode.Ok, 126), binary);
        Assert.HasCount(2 + 4 + 125 + 2 + 2 + 4 + 126, peer.Written);
        Assert.AreEqual(0x89, peer.Written[0]);
        Assert.AreEqual(0x82, peer.Written[131]);
    }
}
