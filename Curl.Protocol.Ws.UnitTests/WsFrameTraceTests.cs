using Curl.Protocol.Ws.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ws;

/// <summary>
/// Pins <see cref="WsFrameTrace" />'s opcode names and its off switch against curl 8.21.0's
/// <c>--trace-config ws</c> lines (BL-1164 Notes).
/// </summary>
[TestClass]
public sealed class WsFrameTraceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(0x0, "CONT")]
    [DataRow(0x1, "TEXT")]
    [DataRow(0x2, "BIN")]
    [DataRow(0x8, "CLOSE")]
    [DataRow(0x9, "PING")]
    [DataRow(0xA, "PONG")]
    public void Name_EachOpcode_IsCurlsTraceName(int opcode, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("opcode", "0x" + opcode.ToString("x", System.Globalization.CultureInfo.InvariantCulture));

        string actual = WsFrameTrace.Name((WsOpcode)opcode);

        diagnostics.Act("name", actual);
        diagnostics.Assert("name", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void FrameDecoded_NonFinalPong_WritesItsNameAndNonFinal()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var events = new RecordingTransferEvents();
        diagnostics.Arrange("frame", "PONG, isFinal false, length 4");

        new WsFrameTrace(events, enabled: true).FrameDecoded(WsOpcode.Pong, isFinal: false, 4);

        string[] expected = ["* [WS] decoded decoded [PONG NON-FINAL payload=0/4]"];
        string[] actual = events.Transcript.ToArray();
        diagnostics.Act("trace lines", string.Join(" | ", actual));
        diagnostics.Assert("trace lines", string.Join(" | ", expected), string.Join(" | ", actual));
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Disabled_WritesNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var events = new RecordingTransferEvents();
        var trace = new WsFrameTrace(events, enabled: false);
        diagnostics.Arrange("enabled", false);

        trace.UsingChunkSize();
        trace.Established();
        trace.Flushed(6);

        diagnostics.Act("trace line count", events.Transcript.Count);
        diagnostics.Assert("trace line count", 0, events.Transcript.Count);
        Assert.IsEmpty(events.Transcript);
    }
}
