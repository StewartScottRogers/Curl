using Curl.Http2;
using Curl.WebSocket;
using Curl.WebSocket.Fakes;

using static Curl.Extension.WebSocketExtendedConnect.Http2.ScriptedServerFrames;

namespace Curl.Extension.WebSocketExtendedConnect.Http2;

[TestClass]
public sealed class Http2WebSocketTests
{
    [TestMethod]
    public async Task SendAsync_CarriesTheMaskedWebSocketFrameInData()
    {
        // RFC 8441 section 5: the WebSocket's bytes travel in DATA frames on the CONNECT stream.
        var (webSocket, stream) = await OpenOverAsync();

        await webSocket.SendAsync(WsOpcode.Text, "hi"u8.ToArray(), CancellationToken.None);

        var data = (await stream.ReadClientFramesAsync()).Single(frame => frame.Type == Http2FrameType.Data);
        Assert.AreEqual(StreamId, data.StreamId);
        Assert.IsFalse(data.HasFlag(Http2FrameFlags.EndStream));
        CollectionAssert.AreEqual(WsFrameEncoder.Encode(WsOpcode.Text, "hi"u8, new FixedRandomSource()), data.Payload.ToArray());
    }

    [TestMethod]
    public async Task SendAsync_WindowShort_WaitsForWindowUpdateAndKeepsDataReadMeanwhile()
    {
        // The 8-byte frame fits a 4-byte stream window only after the server's WINDOW_UPDATE; its DATA read on the way is kept.
        var stream = new ScriptedServerStream(
            ServerSettings(EnableConnectProtocol, new(Http2SettingIdentifier.InitialWindowSize, 4)),
            Response("200"),
            ServerData([0x81, 0x01, (byte)'a']),
            Http2FrameFactory.CreateWindowUpdate(StreamId, 100));
        var webSocket = await OpenAsync(stream);

        await webSocket.SendAsync(WsOpcode.Text, "hi"u8.ToArray(), CancellationToken.None);
        var runs = await webSocket.ReceiveAsync(CancellationToken.None);

        var sent = (await stream.ReadClientFramesAsync()).Where(frame => frame.Type == Http2FrameType.Data).SelectMany(frame => frame.Payload.ToArray());
        CollectionAssert.AreEqual(WsFrameEncoder.Encode(WsOpcode.Text, "hi"u8, new FixedRandomSource()), sent.ToArray());
        Assert.AreEqual("a", System.Text.Encoding.ASCII.GetString(runs.Single().Payload.Span));
    }

    [TestMethod]
    public async Task SendAsync_ClosedWhileWaitingForWindow_Fails()
    {
        var stream = new ScriptedServerStream(ServerSettings(EnableConnectProtocol, new(Http2SettingIdentifier.InitialWindowSize, 4)), Response("200"));
        var webSocket = await OpenAsync(stream);

        var failure = await Assert.ThrowsExactlyAsync<Http2WebSocketException>(() => webSocket.SendAsync(WsOpcode.Text, "hi"u8.ToArray(), CancellationToken.None));

        StringAssert.Contains(failure.Message, "closed the HTTP/2 connection under the WebSocket");
    }

    [TestMethod]
    public async Task ReceiveAsync_DataCarriesWebSocketFrames()
    {
        // RFC 8441 section 5: the server's DATA carries its WebSocket frames.
        var (webSocket, _) = await OpenOverAsync(ServerData([0x81, 0x02, (byte)'o', (byte)'k']));

        var runs = await webSocket.ReceiveAsync(CancellationToken.None);

        var run = runs.Single();
        Assert.AreEqual(WsOpcode.Text, run.Opcode);
        Assert.IsTrue(run.IsFinal);
        Assert.AreEqual("ok", System.Text.Encoding.ASCII.GetString(run.Payload.Span));
        Assert.IsFalse(webSocket.IsClosedByServer);
    }

    [TestMethod]
    public async Task ReceiveAsync_EndStream_ClosesTheServersSide()
    {
        // RFC 8441 section 5: END_STREAM is the equivalent of TCP's FIN.
        var (webSocket, _) = await OpenOverAsync(ServerData([], isEndStream: true));

        var runs = await webSocket.ReceiveAsync(CancellationToken.None);

        Assert.IsEmpty(runs);
        Assert.IsTrue(webSocket.IsClosedByServer);
    }

    [TestMethod]
    public async Task ReceiveAsync_RstStream_FailsAsAnAbruptClose()
    {
        // RFC 8441 section 5: RST_STREAM is the equivalent of TCP's RST.
        var (webSocket, _) = await OpenOverAsync(Http2FrameFactory.CreateRstStream(StreamId, Http2ErrorCode.Cancel));

        var failure = await Assert.ThrowsExactlyAsync<Http2WebSocketException>(() => webSocket.ReceiveAsync(CancellationToken.None));

        StringAssert.Contains(failure.Message, "reset the WebSocket's HTTP/2 stream with Cancel");
        Assert.IsInstanceOfType<Http2StreamResetException>(failure.InnerException);
    }

    [TestMethod]
    public async Task ReceiveAsync_ConnectionClosed_Fails()
    {
        var (webSocket, _) = await OpenOverAsync();

        var failure = await Assert.ThrowsExactlyAsync<Http2WebSocketException>(() => webSocket.ReceiveAsync(CancellationToken.None));

        StringAssert.Contains(failure.Message, "closed the HTTP/2 connection under the WebSocket");
    }

    [TestMethod]
    public async Task ReceiveAsync_HeadersAfterResponse_Fails()
    {
        var trailers = Http2FrameFactory.CreateHeaders(StreamId, new HpackEncoder().Encode([new("x-trailer", "1")]), isEndStream: true, isEndHeaders: true);
        var (webSocket, _) = await OpenOverAsync(trailers);

        var failure = await Assert.ThrowsExactlyAsync<Http2WebSocketException>(() => webSocket.ReceiveAsync(CancellationToken.None));

        StringAssert.Contains(failure.Message, "only DATA may follow");
    }

    [TestMethod]
    public async Task ReceiveAsync_MaskedServerFrame_FailsWithTheViolation()
    {
        // RFC 6455 section 5.1: a server never masks its frames.
        var (webSocket, _) = await OpenOverAsync(ServerData([0x81, 0x81, 0, 0, 0, 0, (byte)'a']));

        var failure = await Assert.ThrowsExactlyAsync<Http2WebSocketException>(() => webSocket.ReceiveAsync(CancellationToken.None));

        Assert.IsFalse(string.IsNullOrEmpty(failure.Message));
    }

    [TestMethod]
    public async Task CloseAsync_SendsEmptyDataWithEndStream()
    {
        // RFC 8441 section 5: the client's END_STREAM is its TCP FIN.
        var (webSocket, stream) = await OpenOverAsync();

        await webSocket.CloseAsync(CancellationToken.None);

        var data = (await stream.ReadClientFramesAsync()).Single(frame => frame.Type == Http2FrameType.Data);
        Assert.IsTrue(data.HasFlag(Http2FrameFlags.EndStream));
        Assert.AreEqual(0, data.Payload.Length);
    }
}
