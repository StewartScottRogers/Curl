using Curl.Api.Codes;
using Curl.Api.EngineContracts.WebSocket;

namespace Curl.Api.UnitTests.EngineContracts.WebSocket;

[TestClass]
public sealed class WebSocketValueTypeTests
{
    [TestMethod]
    public void CurlWebSocketFrame_Default_IsEveryFieldZero()
    {
        var frame = default(CurlWebSocketFrame);

        Assert.AreEqual(0, frame.Age);
        Assert.AreEqual(CurlWebSocketFlags.None, frame.Flags);
        Assert.AreEqual(0L, frame.Offset);
        Assert.AreEqual(0L, frame.BytesLeft);
        Assert.AreEqual(0L, frame.Length);
    }

    [TestMethod]
    public void CurlWebSocketFrame_KeepsEveryField()
    {
        var frame = new CurlWebSocketFrame(0, CurlWebSocketFlags.Binary | CurlWebSocketFlags.Continuation, 10, 20, 30);

        Assert.AreEqual(0, frame.Age);
        Assert.AreEqual(CurlWebSocketFlags.Binary | CurlWebSocketFlags.Continuation, frame.Flags);
        Assert.AreEqual(10L, frame.Offset);
        Assert.AreEqual(20L, frame.BytesLeft);
        Assert.AreEqual(30L, frame.Length);
    }

    [TestMethod]
    public void CurlWebSocketReceiveResult_KeepsResultCountAndFrame()
    {
        var frame = new CurlWebSocketFrame(0, CurlWebSocketFlags.Text, 0, 0, 5);

        var result = new CurlWebSocketReceiveResult(CurlCode.Ok, 5, frame);

        Assert.AreEqual(CurlCode.Ok, result.Result);
        Assert.AreEqual(5, result.BytesReceived);
        Assert.AreEqual(frame, result.Frame);
    }

    [TestMethod]
    public void CurlWebSocketReceiveResult_Default_IsOkWithNothingReceived()
    {
        var result = default(CurlWebSocketReceiveResult);

        Assert.AreEqual(CurlCode.Ok, result.Result);
        Assert.AreEqual(0, result.BytesReceived);
        Assert.AreEqual(default, result.Frame);
    }

    [TestMethod]
    public void CurlWebSocketSendResult_KeepsResultAndCount()
    {
        var result = new CurlWebSocketSendResult(CurlCode.Again, 0);

        Assert.AreEqual(CurlCode.Again, result.Result);
        Assert.AreEqual(0, result.BytesSent);
    }
}
