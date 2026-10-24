using Curl.Api.Codes;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Options;
using Curl.Api.Options.Network;
using Libcurl.Easy;
using Libcurl.Options;
using Libcurl.WebSocket;

namespace Libcurl.UnitTests.WebSocket;

[TestClass]
public sealed class CurlEasyWebSocketTests
{
    private static readonly CurlWebSocketFrame TextFrame = new(0, CurlWebSocketFlags.Text, 0, 2, 3);

    private static (CurlEasy Easy, FakeEasyWebSocket WebSocket) NewHandle()
    {
        var transfer = new FakeWebSocketTransfer();
        var easy = new CurlEasy(transfer, new EasyOptionDispatch(new Dictionary<CurlOption, EasyOptionSetter>()));
        return (easy, transfer.FakeWebSocket);
    }

    private static CurlEasy CleanedUpHandle()
    {
        var (easy, _) = NewHandle();
        easy.Dispose();
        return easy;
    }

    [TestMethod]
    public void WebSocketRecv_Ok_FillsTheBufferAndAnswersTheCountAndFrame()
    {
        var (easy, webSocket) = NewHandle();
        webSocket.ReceivePayload = "abc"u8.ToArray();
        webSocket.ReceiveResult = new CurlWebSocketReceiveResult(CurlCode.Ok, 3, TextFrame);
        var buffer = new byte[8];

        var code = easy.WebSocketRecv(buffer, out var received, out var meta);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(3, received);
        Assert.AreEqual(TextFrame, meta);
        Assert.AreEqual(8, webSocket.ReceiveBufferLength);
        CollectionAssert.AreEqual("abc"u8.ToArray(), buffer[..3]);
        Assert.AreEqual(CancellationToken.None, webSocket.LastToken);
    }

    [TestMethod]
    public void WebSocketRecv_Again_AnswersNoBytesAndNoFrame()
    {
        var (easy, webSocket) = NewHandle();
        webSocket.ReceiveResult = new CurlWebSocketReceiveResult(CurlCode.Again, 5, TextFrame);

        var code = easy.WebSocketRecv(new byte[8], out var received, out var meta);

        Assert.AreEqual(CurlCode.Again, code);
        Assert.AreEqual(0, received);
        Assert.IsNull(meta);
    }

    [TestMethod]
    public async Task WebSocketRecvAsync_PassesTheTokenAndAnswersTheEnginesResult()
    {
        var (easy, webSocket) = NewHandle();
        var expected = new CurlWebSocketReceiveResult(CurlCode.GotNothing, 0, default);
        webSocket.ReceiveResult = expected;
        using var source = new CancellationTokenSource();

        var result = await easy.WebSocketRecvAsync(new byte[4], source.Token);

        Assert.AreEqual(expected, result);
        Assert.AreEqual(source.Token, webSocket.LastToken);
    }

    [TestMethod]
    public void WebSocketSend_PassesBufferFragmentSizeAndFlagsAndAnswersTheCount()
    {
        var (easy, webSocket) = NewHandle();
        webSocket.SendResult = new CurlWebSocketSendResult(CurlCode.Ok, 2);
        var flags = CurlWebSocketFlags.Binary | CurlWebSocketFlags.Offset;

        var code = easy.WebSocketSend("hi"u8.ToArray(), out var sent, 10, flags);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(2, sent);
        CollectionAssert.AreEqual("hi"u8.ToArray(), webSocket.Sent);
        Assert.AreEqual(10L, webSocket.SentFragmentSize);
        Assert.AreEqual(flags, webSocket.LastFlags);
        Assert.AreEqual(CancellationToken.None, webSocket.LastToken);
    }

    [TestMethod]
    public void WebSocketSend_Again_AnswersTheEnginesCodeAndCount()
    {
        var (easy, webSocket) = NewHandle();
        webSocket.SendResult = new CurlWebSocketSendResult(CurlCode.Again, 0);

        var code = easy.WebSocketSend("x"u8.ToArray(), out var sent, 0, CurlWebSocketFlags.Text);

        Assert.AreEqual(CurlCode.Again, code);
        Assert.AreEqual(0, sent);
    }

    [TestMethod]
    public async Task WebSocketSendAsync_PassesTheTokenAndAnswersTheEnginesResult()
    {
        var (easy, webSocket) = NewHandle();
        webSocket.SendResult = new CurlWebSocketSendResult(CurlCode.SendError, 1);
        using var source = new CancellationTokenSource();

        var result = await easy.WebSocketSendAsync("ab"u8.ToArray(), 0, CurlWebSocketFlags.Ping, source.Token);

        Assert.AreEqual(new CurlWebSocketSendResult(CurlCode.SendError, 1), result);
        Assert.AreEqual(source.Token, webSocket.LastToken);
        Assert.AreEqual(CurlWebSocketFlags.Ping, webSocket.LastFlags);
    }

    [TestMethod]
    public void WebSocketMeta_AnswersTheEnginesCurrentFrame()
    {
        var (easy, webSocket) = NewHandle();
        webSocket.CurrentFrame = TextFrame;

        Assert.AreEqual(TextFrame, easy.WebSocketMeta());
    }

    [TestMethod]
    public void WebSocketMeta_ConnectOnlyHandleAfterAReceive_AnswersNull()
    {
        var transfer = new FakeWebSocketTransfer
        {
            Options = new EasyOptions { Network = new EasyNetworkOptions { ConnectOnly = ConnectOnly.WebSocket } },
        };
        var easy = new CurlEasy(transfer, new EasyOptionDispatch(new Dictionary<CurlOption, EasyOptionSetter>()));
        var webSocket = transfer.FakeWebSocket;
        webSocket.ReceiveResult = new CurlWebSocketReceiveResult(CurlCode.Ok, 1, TextFrame);
        webSocket.CurrentFrame = TextFrame;

        Assert.AreEqual(CurlCode.Ok, easy.WebSocketRecv(new byte[4], out _, out _));
        Assert.IsNull(easy.WebSocketMeta());
    }

    [TestMethod]
    public void WebSocketMeta_OutsideTheCallback_AnswersNull()
    {
        var (easy, _) = NewHandle();

        Assert.IsNull(easy.WebSocketMeta());
    }

    [TestMethod]
    public void WebSocketStartFrame_PassesFlagsAndLengthAndAnswersTheEnginesCode()
    {
        var (easy, webSocket) = NewHandle();
        webSocket.StartFrameResult = CurlCode.Ok;

        var code = easy.WebSocketStartFrame(CurlWebSocketFlags.Text, 42);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(CurlWebSocketFlags.Text, webSocket.LastFlags);
        Assert.AreEqual(42L, webSocket.StartedFrameLength);
        Assert.AreEqual(CancellationToken.None, webSocket.LastToken);
    }

    [TestMethod]
    public async Task WebSocketStartFrameAsync_PassesTheTokenAndAnswersTheEnginesCode()
    {
        var (easy, webSocket) = NewHandle();
        webSocket.StartFrameResult = CurlCode.SendError;
        using var source = new CancellationTokenSource();

        var code = await easy.WebSocketStartFrameAsync(CurlWebSocketFlags.Binary, 7, source.Token);

        Assert.AreEqual(CurlCode.SendError, code);
        Assert.AreEqual(source.Token, webSocket.LastToken);
    }

    [TestMethod]
    public void EveryCall_OnANullHandle_AnswersBadFunctionArgument()
    {
        CurlEasy? easy = null;

        Assert.AreEqual(CurlCode.BadFunctionArgument, easy.WebSocketRecv(new byte[1], out var received, out var meta));
        Assert.AreEqual(0, received);
        Assert.IsNull(meta);
        Assert.AreEqual(CurlCode.BadFunctionArgument, easy.WebSocketSend(new byte[1], out var sent, 0, CurlWebSocketFlags.Text));
        Assert.AreEqual(0, sent);
        Assert.AreEqual(CurlCode.BadFunctionArgument, easy.WebSocketStartFrame(CurlWebSocketFlags.Text, 1));
        Assert.IsNull(easy.WebSocketMeta());
    }

    [TestMethod]
    public void EveryCall_OnACleanedUpHandle_AnswersBadFunctionArgumentWithoutCallingTheEngine()
    {
        var (easy, webSocket) = NewHandle();
        webSocket.CurrentFrame = TextFrame;
        easy.Dispose();

        Assert.AreEqual(CurlCode.BadFunctionArgument, easy.WebSocketRecv(new byte[1], out _, out _));
        Assert.AreEqual(CurlCode.BadFunctionArgument, easy.WebSocketSend(new byte[1], out _, 0, CurlWebSocketFlags.Text));
        Assert.AreEqual(CurlCode.BadFunctionArgument, easy.WebSocketStartFrame(CurlWebSocketFlags.Text, 1));
        Assert.IsNull(easy.WebSocketMeta());
        Assert.AreEqual(0, webSocket.CallCount);
    }

    [TestMethod]
    public async Task AsyncCalls_OnACleanedUpHandle_AnswerBadFunctionArgument()
    {
        var easy = CleanedUpHandle();

        Assert.AreEqual(new CurlWebSocketReceiveResult(CurlCode.BadFunctionArgument, 0, default), await easy.WebSocketRecvAsync(new byte[1]));
        Assert.AreEqual(new CurlWebSocketSendResult(CurlCode.BadFunctionArgument, 0), await easy.WebSocketSendAsync(new byte[1], 0, CurlWebSocketFlags.Text));
        Assert.AreEqual(CurlCode.BadFunctionArgument, await easy.WebSocketStartFrameAsync(CurlWebSocketFlags.Text, 1));
    }
}
