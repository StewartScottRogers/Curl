namespace Curl.Original.Protocol.Ws;

/// <summary>
/// Pins CGR-123 (BL-3652): curl 8.22.0's <c>curl_ws_send</c> refuses a close, ping or pong frame whose payload is
/// over 125 bytes with <c>CURLE_TOO_LARGE</c> (measured: 126-byte ping and pong and a 200-byte close answer 100 with
/// nothing sent; a 125-byte ping is sent), while data frames have no such limit.
/// </summary>
[TestClass]
public sealed class Cgr123Tests
{
    [TestMethod]
    [DataRow(WsOpcode.Ping, 126L)]
    [DataRow(WsOpcode.Close, 200L)]
    [DataRow(WsOpcode.Pong, 126L)]
    public void IsControlPayloadTooLong_ControlFrameOver125Bytes_IsTrue(WsOpcode opcode, long payloadLength)
    {
        Assert.IsTrue(WsFrameEncoder.IsControlPayloadTooLong(opcode, payloadLength));
    }

    [TestMethod]
    [DataRow(WsOpcode.Ping, 125L)]
    [DataRow(WsOpcode.Close, 0L)]
    [DataRow(WsOpcode.Binary, 126L)]
    [DataRow(WsOpcode.Text, 200L)]
    [DataRow(WsOpcode.Continuation, 126L)]
    public void IsControlPayloadTooLong_ControlFrameUpTo125BytesOrADataFrame_IsFalse(WsOpcode opcode, long payloadLength)
    {
        Assert.IsFalse(WsFrameEncoder.IsControlPayloadTooLong(opcode, payloadLength));
    }
}
