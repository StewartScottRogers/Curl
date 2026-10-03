using Curl.Protocol.Ws.Fakes;

namespace Curl.Protocol.Ws;

/// <summary>
/// Pins <see cref="WsFrameTrace" />'s opcode names and its off switch against curl 8.21.0's
/// <c>--trace-config ws</c> lines (BL-1164 Notes).
/// </summary>
[TestClass]
public sealed class WsFrameTraceTests
{
    [TestMethod]
    [DataRow(0x0, "CONT")]
    [DataRow(0x1, "TEXT")]
    [DataRow(0x2, "BIN")]
    [DataRow(0x8, "CLOSE")]
    [DataRow(0x9, "PING")]
    [DataRow(0xA, "PONG")]
    public void Name_EachOpcode_IsCurlsTraceName(int opcode, string expected) =>
        Assert.AreEqual(expected, WsFrameTrace.Name((WsOpcode)opcode));

    [TestMethod]
    public void FrameDecoded_NonFinalPong_WritesItsNameAndNonFinal()
    {
        var events = new RecordingTransferEvents();

        new WsFrameTrace(events, enabled: true).FrameDecoded(WsOpcode.Pong, isFinal: false, 4);

        CollectionAssert.AreEqual((string[])["* [WS] decoded decoded [PONG NON-FINAL payload=0/4]"], events.Transcript.ToArray());
    }

    [TestMethod]
    public void Disabled_WritesNothing()
    {
        var events = new RecordingTransferEvents();
        var trace = new WsFrameTrace(events, enabled: false);

        trace.UsingChunkSize();
        trace.Established();
        trace.Flushed(6);

        Assert.IsEmpty(events.Transcript);
    }
}
