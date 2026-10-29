using System.Text;
using Curl.Testing;

namespace Curl.WebSocket;

/// <summary>
/// Pins the public frame decoder behind libcurl's <c>curl_ws_recv</c> and <c>curl_ws_meta</c>
/// (BL-2471): each run of payload with its frame's opcode, FIN, offset and bytes left, across reads,
/// the completed ping for auto-pong, and violations with the tool decoder's own messages.
/// </summary>
[TestClass]
public sealed class WsFrameMetadataDecoderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Decode_TwoFramesInOneRead_ReportsEachWithItsMetadata()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("received", "81 02 'hi', 82 00");

        WsDecodedFrames decoded = new WsFrameMetadataDecoder().Decode(Hex("81 02 68 69 82 00"));

        string[] expected = ["Text FIN offset 0 left 0 'hi'", "Binary FIN offset 0 left 0 ''"];
        string[] actual = Describe(decoded);
        diagnostics.Act("runs", string.Join(" | ", actual));
        CollectionAssert.AreEqual(expected, actual);
        Assert.IsNull(decoded.CompletedPing);
        Assert.IsNull(decoded.ViolationMessage);
    }

    [TestMethod]
    public void Decode_FrameSplitOverThreeReads_ReportsOffsetAndBytesLeftForEachRun()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var decoder = new WsFrameMetadataDecoder();
        diagnostics.Arrange("reads", "82 | 05 'ab' | 'cde'");

        string[] headOnly = Describe(decoder.Decode(Hex("82")));
        string[] first = Describe(decoder.Decode(Hex("05 61 62")));
        string[] second = Describe(decoder.Decode(Hex("63 64 65")));

        diagnostics.Act("runs", $"{headOnly.Length} | {string.Join(",", first)} | {string.Join(",", second)}");
        Assert.IsEmpty(headOnly);
        CollectionAssert.AreEqual(new[] { "Binary FIN offset 0 left 3 'ab'" }, first);
        CollectionAssert.AreEqual(new[] { "Binary FIN offset 2 left 0 'cde'" }, second);
    }

    [TestMethod]
    public void Decode_FragmentedMessage_ReportsFinClearThenContinuation()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("received", "01 01 'a', 80 01 'b'");

        WsDecodedFrames decoded = new WsFrameMetadataDecoder().Decode(Hex("01 01 61 80 01 62"));

        string[] expected = ["Text more offset 0 left 0 'a'", "Continuation FIN offset 0 left 0 'b'"];
        string[] actual = Describe(decoded);
        diagnostics.Act("runs", string.Join(" | ", actual));
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Decode_PingAndClose_ReportsBothAndTheCompletedPing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("received", "89 02 'hi', 88 02 03 e8");

        WsDecodedFrames decoded = new WsFrameMetadataDecoder().Decode(Hex("89 02 68 69 88 02 03 e8"));

        string[] expected = ["Ping FIN offset 0 left 0 'hi'", "Close FIN offset 0 left 0 '\u0003è'"];
        string[] actual = Describe(decoded);
        diagnostics.Act("runs", string.Join(" | ", actual));
        CollectionAssert.AreEqual(expected, actual);
        Assert.IsNotNull(decoded.CompletedPing);
        Assert.AreEqual("hi", Encoding.Latin1.GetString(decoded.CompletedPing.Value.Span));
    }

    [TestMethod]
    [DataRow("81 01 61 81 85 01 02 03 04", 1, DisplayName = "masked input frame after a frame")]
    [DataRow("c1 00", 0, DisplayName = "reserved bits")]
    [DataRow("83 00", 0, DisplayName = "invalid opcode")]
    [DataRow("80 00", 0, DisplayName = "continuation with no message")]
    [DataRow("09 00", 0, DisplayName = "fragmented ping")]
    [DataRow("89 7e", 0, DisplayName = "ping too big")]
    public void Decode_Violation_FailsWithTheToolDecodersMessageAndKeepsEarlierRuns(string receivedHex, int runsBefore)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("received", receivedHex);
        string? toolMessage = new WsFrameDecoder().Decode(Hex(receivedHex)).ViolationMessage;

        WsDecodedFrames decoded = new WsFrameMetadataDecoder().Decode(Hex(receivedHex));

        diagnostics.Assert("violation message", toolMessage, decoded.ViolationMessage);
        Assert.IsNotNull(toolMessage);
        Assert.AreEqual(toolMessage, decoded.ViolationMessage);
        Assert.HasCount(runsBefore, decoded.Runs);
    }

    private static string[] Describe(WsDecodedFrames decoded) =>
        [.. decoded.Runs.Select(run => $"{run.Opcode} {(run.IsFinal ? "FIN" : "more")} offset {run.Offset} left {run.BytesLeft} '{Encoding.Latin1.GetString(run.Payload.Span)}'")];

    private static byte[] Hex(string spaced) => Convert.FromHexString(spaced.Replace(" ", string.Empty, StringComparison.Ordinal));
}
