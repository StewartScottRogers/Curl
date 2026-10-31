using Curl.Api.EngineContracts.WebSocket;
using Curl.Original.Protocol.Ws;

namespace Curl.Engine;

[TestClass]
public sealed class WebSocketFrameFlagsTests
{
    [TestMethod]
    [DataRow(WsOpcode.Text, true, CurlWebSocketFlags.None, CurlWebSocketFlags.Text)]
    [DataRow(WsOpcode.Binary, false, CurlWebSocketFlags.None, CurlWebSocketFlags.Binary | CurlWebSocketFlags.Continuation)]
    [DataRow(WsOpcode.Continuation, false, CurlWebSocketFlags.Text, CurlWebSocketFlags.Text | CurlWebSocketFlags.Continuation)]
    [DataRow(WsOpcode.Continuation, true, CurlWebSocketFlags.Binary, CurlWebSocketFlags.Binary)]
    [DataRow(WsOpcode.Close, true, CurlWebSocketFlags.Text, CurlWebSocketFlags.Close)]
    [DataRow(WsOpcode.Ping, true, CurlWebSocketFlags.None, CurlWebSocketFlags.Ping)]
    [DataRow(WsOpcode.Pong, true, CurlWebSocketFlags.None, CurlWebSocketFlags.Pong)]
    public void Of_ReceivedRun_ReportsLibcurlsBits(WsOpcode opcode, bool isFinal, CurlWebSocketFlags messageType, CurlWebSocketFlags expected)
    {
        WsFrameRun run = new(opcode, isFinal, 0, 0, ReadOnlyMemory<byte>.Empty);

        Assert.AreEqual(expected, WebSocketFrameFlags.Of(run, messageType));
    }

    [TestMethod]
    [DataRow(CurlWebSocketFlags.Text, false, WsOpcode.Text, true)]
    [DataRow(CurlWebSocketFlags.Binary, false, WsOpcode.Binary, true)]
    [DataRow(CurlWebSocketFlags.Text | CurlWebSocketFlags.Continuation, false, WsOpcode.Text, false)]
    [DataRow(CurlWebSocketFlags.Text | CurlWebSocketFlags.Continuation, true, WsOpcode.Continuation, false)]
    [DataRow(CurlWebSocketFlags.Binary, true, WsOpcode.Continuation, true)]
    [DataRow(CurlWebSocketFlags.Continuation, true, WsOpcode.Continuation, false)]
    [DataRow(CurlWebSocketFlags.None, true, WsOpcode.Continuation, true)]
    [DataRow(CurlWebSocketFlags.Close, false, WsOpcode.Close, true)]
    [DataRow(CurlWebSocketFlags.Ping, true, WsOpcode.Ping, true)]
    [DataRow(CurlWebSocketFlags.Pong, false, WsOpcode.Pong, true)]
    public void TryFrameOf_AcceptedBits_ChoosesTheFrame(CurlWebSocketFlags flags, bool continuesMessage, WsOpcode expectedOpcode, bool expectedFinal)
    {
        bool accepted = WebSocketFrameFlags.TryFrameOf(flags, continuesMessage, out WsOpcode opcode, out bool isFinal);

        Assert.IsTrue(accepted);
        Assert.AreEqual(expectedOpcode, opcode);
        Assert.AreEqual(expectedFinal, isFinal);
    }

    [TestMethod]
    [DataRow(CurlWebSocketFlags.None, false)]
    [DataRow(CurlWebSocketFlags.Continuation, false)]
    [DataRow(CurlWebSocketFlags.Text | CurlWebSocketFlags.Binary, false)]
    [DataRow(CurlWebSocketFlags.Text | CurlWebSocketFlags.Binary, true)]
    [DataRow(CurlWebSocketFlags.Ping | CurlWebSocketFlags.Continuation, false)]
    [DataRow(CurlWebSocketFlags.Close | CurlWebSocketFlags.Ping, false)]
    [DataRow(CurlWebSocketFlags.Text | CurlWebSocketFlags.Pong, false)]
    public void TryFrameOf_RefusedBits_AnswersFalse(CurlWebSocketFlags flags, bool continuesMessage)
    {
        Assert.IsFalse(WebSocketFrameFlags.TryFrameOf(flags, continuesMessage, out _, out _));
    }
}
