using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Protocol.Ws;

/// <summary>
/// Pins the frame reader against what curl 8.21.0 did with each server frame, measured on
/// 2026-09-28 with <c>Record-CurlExchange.ps1</c> (BL-581): which payloads reach the output,
/// which ping is answered, and the exit 56 message of each violation.
/// </summary>
[TestClass]
public sealed class WsFrameDecoderTests
{
    public TestContext TestContext { get; set; } = null!;

    private static readonly string Long = new('a', 126);

    [TestMethod]
    public void Decode_TextFrameWith7BitLength_PassesPayloadOn()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        WsDecodedBytes decoded = Run(diagnostics, "81 05", "hello");

        diagnostics.Assert("Text(decoded.Payload)", "hello", Text(decoded.Payload));
        Assert.AreEqual("hello", Text(decoded.Payload));
        diagnostics.Assert("decoded.LastPing is null", true, decoded.LastPing is null);
        Assert.IsNull(decoded.LastPing);
        diagnostics.Assert("decoded.Failure is null", true, decoded.Failure is null);
        Assert.IsNull(decoded.Failure);
    }

    [TestMethod]
    public void Decode_BinaryFrameWith16BitLength_PassesPayloadOn()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        WsDecodedBytes decoded = Run(diagnostics, "82 7e 00 7e", Long);

        diagnostics.Assert("Text(decoded.Payload)", Long, Text(decoded.Payload));
        Assert.AreEqual(Long, Text(decoded.Payload));
    }

    [TestMethod]
    public void Decode_16BitLengthOfAShortPayload_IsAccepted()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        WsDecodedBytes decoded = Run(diagnostics, "82 7e 00 02", "ok");

        diagnostics.Assert("Text(decoded.Payload)", "ok", Text(decoded.Payload));
        Assert.AreEqual("ok", Text(decoded.Payload));
    }

    [TestMethod]
    public void Decode_16BitLengthOfZero_EndsTheFrameAtItsHead()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        WsDecodedBytes decoded = Run(diagnostics, "82 7e 00 00 81 02", "ok");

        diagnostics.Assert("Text(decoded.Payload)", "ok", Text(decoded.Payload));
        Assert.AreEqual("ok", Text(decoded.Payload));
    }

    [TestMethod]
    public void Decode_BinaryFrameWith64BitLength_PassesPayloadOn()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        WsDecodedBytes decoded = Run(diagnostics, "82 7f 00 00 00 00 00 00 00 7e", Long);

        diagnostics.Assert("Text(decoded.Payload)", Long, Text(decoded.Payload));
        Assert.AreEqual(Long, Text(decoded.Payload));
    }

    [TestMethod]
    public void Decode_64BitLengthWithTheTopBitSet_FailsWith56()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        WsDecodedBytes decoded = Run(diagnostics, "82 7f 80 00 00 00 00 00 00 02", "ok");

        AssertViolation(diagnostics, "[WS] frame length longer than 63 bits not supported", decoded);
        diagnostics.Assert("decoded.Payload count", 0, decoded.Payload.Length);
        Assert.IsEmpty(decoded.Payload);
    }

    [TestMethod]
    public void Decode_TextMessageInThreeFragments_PassesEachFragmentOn()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        WsDecodedBytes decoded = Run(diagnostics, "01 03", "hel", "00 01", "l", "80 01", "o");

        diagnostics.Assert("Text(decoded.Payload)", "hello", Text(decoded.Payload));
        Assert.AreEqual("hello", Text(decoded.Payload));
        diagnostics.Assert("decoded.Failure is null", true, decoded.Failure is null);
        Assert.IsNull(decoded.Failure);
    }

    [TestMethod]
    public void Decode_FramesSplitIntoSingleBytes_DecodeAsOneRead()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] frames = Frames("89 02", "hi", "01 03", "hel", "80 7e 00 02", "lo", "88 02 03 e8");
        var decoder = new WsFrameDecoder();
        var payload = new List<byte>();
        diagnostics.Arrange("frames", frames.Length + " bytes, fed one byte per read");
        diagnostics.Bytes("frames", frames);
        var pings = new List<string>();

        foreach (byte value in frames)
        {
            WsDecodedBytes decoded = decoder.Decode([value]);
            payload.AddRange(decoded.Payload);
            if (decoded.LastPing is { } ping)
            {
                pings.Add(Text(ping));
            }
        }

        diagnostics.Act("payload and pings", Text([.. payload]) + " | " + string.Join(",", pings));
        diagnostics.Diff("payload", Frames("hello", "03 e8"), payload.ToArray());
        diagnostics.Assert("pings", "hi", string.Join(",", pings));
        CollectionAssert.AreEqual(Frames("hello", "03 e8"), payload.ToArray());
        CollectionAssert.AreEqual(new[] { "hi" }, pings);
    }

    [TestMethod]
    public void Decode_PingSplitAcrossReads_IsAnsweredOnceComplete()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("first read", "89 02 h (ping head and one payload byte)");
        diagnostics.Arrange("second read", "i, then 81 02 ok");
        var decoder = new WsFrameDecoder();

        WsDecodedBytes first = decoder.Decode(Frames("89 02", "h"));
        WsDecodedBytes second = decoder.Decode(Frames("i", "81 02", "ok"));
        diagnostics.Act("first.LastPing", first.LastPing is null ? "none" : Text(first.LastPing));
        diagnostics.Act("second.LastPing", second.LastPing is null ? "none" : Text(second.LastPing));

        diagnostics.Assert("first.LastPing is null", true, first.LastPing is null);
        Assert.IsNull(first.LastPing);
        diagnostics.Assert("Text(second.LastPing!)", "hi", Text(second.LastPing!));
        Assert.AreEqual("hi", Text(second.LastPing!));
        diagnostics.Assert("Text(second.Payload)", "ok", Text(second.Payload));
        Assert.AreEqual("ok", Text(second.Payload));
    }

    [TestMethod]
    public void Decode_TextThenCloseWithStatus_PassesBothPayloadsOn()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        WsDecodedBytes decoded = Run(diagnostics, "81 05", "hello", "88 02 03 e8");

        diagnostics.Diff("payload", Frames("hello", "03 e8"), decoded.Payload);
        CollectionAssert.AreEqual(Frames("hello", "03 e8"), decoded.Payload);
    }

    [TestMethod]
    public void Decode_CloseWithStatusAndReason_PassesPayloadOn()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        WsDecodedBytes decoded = Run(diagnostics, "88 05 03 e8", "bye");

        diagnostics.Diff("payload", Frames("03 e8", "bye"), decoded.Payload);
        CollectionAssert.AreEqual(Frames("03 e8", "bye"), decoded.Payload);
    }

    [TestMethod]
    public void Decode_EmptyClose_PassesNothingOn()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        WsDecodedBytes decoded = Run(diagnostics, "81 05", "hello", "88 00");

        diagnostics.Assert("Text(decoded.Payload)", "hello", Text(decoded.Payload));
        Assert.AreEqual("hello", Text(decoded.Payload));
        diagnostics.Assert("decoded.Failure is null", true, decoded.Failure is null);
        Assert.IsNull(decoded.Failure);
    }

    [TestMethod]
    public void Decode_OneByteClose_PassesItOnUnchecked()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        WsDecodedBytes decoded = Run(diagnostics, "88 01", "x", "81 02", "ok");

        diagnostics.Assert("Text(decoded.Payload)", "xok", Text(decoded.Payload));
        Assert.AreEqual("xok", Text(decoded.Payload));
    }

    [TestMethod]
    public void Decode_ServerPong_PassesPayloadOn()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        WsDecodedBytes decoded = Run(diagnostics, "8a 02", "hi", "81 02", "ok");

        diagnostics.Assert("Text(decoded.Payload)", "hiok", Text(decoded.Payload));
        Assert.AreEqual("hiok", Text(decoded.Payload));
        diagnostics.Assert("decoded.LastPing is null", true, decoded.LastPing is null);
        Assert.IsNull(decoded.LastPing);
    }

    [TestMethod]
    public void Decode_Ping_HoldsPayloadBackForThePong()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        WsDecodedBytes decoded = Run(diagnostics, "89 02", "hi", "81 05", "hello", "88 02 03 e8");

        diagnostics.Diff("payload", Frames("hello", "03 e8"), decoded.Payload);
        CollectionAssert.AreEqual(Frames("hello", "03 e8"), decoded.Payload);
        diagnostics.Assert("Text(decoded.LastPing!)", "hi", Text(decoded.LastPing!));
        Assert.AreEqual("hi", Text(decoded.LastPing!));
    }

    [TestMethod]
    public void Decode_EmptyPing_IsAnsweredWithAnEmptyPayload()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        WsDecodedBytes decoded = Run(diagnostics, "89 00 81 05", "hello");

        diagnostics.Assert("decoded.LastPing is not null", true, decoded.LastPing is not null);
        Assert.IsNotNull(decoded.LastPing);
        diagnostics.Assert("decoded.LastPing count", 0, decoded.LastPing!.Length);
        Assert.IsEmpty(decoded.LastPing);
    }

    [TestMethod]
    public void Decode_125BytePing_IsAccepted()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        WsDecodedBytes decoded = Run(diagnostics, "89 7d", new string('b', 125), "81 02", "ok");

        diagnostics.Assert("Text(decoded.LastPing!)", new string('b', 125), Text(decoded.LastPing!));
        Assert.AreEqual(new string('b', 125), Text(decoded.LastPing!));
        diagnostics.Assert("Text(decoded.Payload)", "ok", Text(decoded.Payload));
        Assert.AreEqual("ok", Text(decoded.Payload));
    }

    [TestMethod]
    public void Decode_TwoPingsInOneRead_KeepsTheLast()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        WsDecodedBytes decoded = Run(diagnostics, "89 01", "a", "89 01", "b");

        diagnostics.Assert("Text(decoded.LastPing!)", "b", Text(decoded.LastPing!));
        Assert.AreEqual("b", Text(decoded.LastPing!));
    }

    [TestMethod]
    public void Decode_PingInsideAFragmentedMessage_IsAnsweredAndTheMessageGoesOn()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        WsDecodedBytes decoded = Run(diagnostics, "01 03", "hel", "89 02", "hi", "80 02", "lo");

        diagnostics.Assert("Text(decoded.Payload)", "hello", Text(decoded.Payload));
        Assert.AreEqual("hello", Text(decoded.Payload));
        diagnostics.Assert("Text(decoded.LastPing!)", "hi", Text(decoded.LastPing!));
        Assert.AreEqual("hi", Text(decoded.LastPing!));
    }

    [TestMethod]
    public void Decode_PingAfterAClose_IsStillAnswered()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        WsDecodedBytes decoded = Run(diagnostics, "88 00 89 02", "hi");

        diagnostics.Assert("Text(decoded.LastPing!)", "hi", Text(decoded.LastPing!));
        Assert.AreEqual("hi", Text(decoded.LastPing!));
    }

    [TestMethod]
    public void Decode_TruncatedFrame_PassesOnWhatArrived()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        WsDecodedBytes decoded = Run(diagnostics, "81 05", "hel");

        diagnostics.Assert("Text(decoded.Payload)", "hel", Text(decoded.Payload));
        Assert.AreEqual("hel", Text(decoded.Payload));
        diagnostics.Assert("decoded.Failure is null", true, decoded.Failure is null);
        Assert.IsNull(decoded.Failure);
    }

    [TestMethod]
    [DataRow("81 85 01 02 03 04", "[WS] masked input frame", DisplayName = "masked text")]
    [DataRow("89 82 01 02 03 04", "[WS] masked input frame", DisplayName = "masked ping")]
    [DataRow("c1 05", "[WS] invalid reserved bits: c1", DisplayName = "RSV1")]
    [DataRow("a1 05", "[WS] invalid reserved bits: a1", DisplayName = "RSV2")]
    [DataRow("91 05", "[WS] invalid reserved bits: 91", DisplayName = "RSV3")]
    [DataRow("c1 85 01 02 03 04", "[WS] invalid reserved bits: c1", DisplayName = "RSV1 and masked")]
    [DataRow("83 01", "[WS] invalid opcode: 83", DisplayName = "opcode 3")]
    [DataRow("8b 01", "[WS] invalid opcode: 8b", DisplayName = "opcode B")]
    [DataRow("03 01", "[WS] invalid opcode: 03", DisplayName = "opcode 3 not final")]
    [DataRow("80 02", "[WS] no ongoing fragmented message to resume", DisplayName = "continuation first")]
    [DataRow("09 02", "[WS] invalid fragmented PING frame", DisplayName = "fragmented ping")]
    [DataRow("0a 02", "[WS] invalid fragmented PONG frame", DisplayName = "fragmented pong")]
    [DataRow("08 02", "[WS] invalid fragmented CLOSE frame", DisplayName = "fragmented close")]
    [DataRow("89 7e 00 7e", "[WS] received PING frame is too big", DisplayName = "126-byte ping")]
    [DataRow("8a 7e 00 7e", "[WS] received PONG frame is too big", DisplayName = "126-byte pong")]
    [DataRow("88 7e 00 7e", "[WS] received CLOSE frame is too big", DisplayName = "126-byte close")]
    [DataRow("89 7f", "[WS] received PING frame is too big", DisplayName = "64-bit ping")]
    public void Decode_ProtocolViolation_FailsWith56AndCurlsMessage(string head, string message)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        WsDecodedBytes decoded = Run(diagnostics, head, "hello");

        AssertViolation(diagnostics, message, decoded);
        diagnostics.Assert("decoded.Payload count", 0, decoded.Payload.Length);
        Assert.IsEmpty(decoded.Payload);
    }

    [TestMethod]
    [DataRow("81", "TEXT")]
    [DataRow("82", "BINARY")]
    public void Decode_NewMessageBeforeTheFragmentedOneEnds_FailsAfterPassingTheFirstFragmentOn(string first, string name)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        WsDecodedBytes decoded = Run(diagnostics, "02 03", "hel", first + " 02", "ok");

        AssertViolation(diagnostics, $"[WS] fragmented message interrupted by new {name} msg", decoded);
        diagnostics.Assert("Text(decoded.Payload)", "hel", Text(decoded.Payload));
        Assert.AreEqual("hel", Text(decoded.Payload));
    }

    [TestMethod]
    public void Decode_ReservedBitsOnAContinuation_FailsAfterPassingTheFirstFragmentOn()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        WsDecodedBytes decoded = Run(diagnostics, "01 01", "h", "c0 01", "i");

        AssertViolation(diagnostics, "[WS] invalid reserved bits: c0", decoded);
        diagnostics.Assert("Text(decoded.Payload)", "h", Text(decoded.Payload));
        Assert.AreEqual("h", Text(decoded.Payload));
    }

    [TestMethod]
    public void Decode_ViolationAfterAPing_ReportsNoPing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        WsDecodedBytes decoded = Run(diagnostics, "89 02", "hi", "81 85 01 02 03 04");

        diagnostics.Assert("decoded.LastPing is null", true, decoded.LastPing is null);
        Assert.IsNull(decoded.LastPing);
        diagnostics.Assert("decoded.Failure is not null", true, decoded.Failure is not null);
        Assert.IsNotNull(decoded.Failure);
    }

    [TestMethod]
    public void Decode_NothingReceived_DecodesNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("input", "no bytes");
        WsDecodedBytes decoded = new WsFrameDecoder().Decode([]);
        Describe(diagnostics, decoded);

        diagnostics.Assert("decoded.Payload count", 0, decoded.Payload.Length);
        Assert.IsEmpty(decoded.Payload);
        diagnostics.Assert("decoded.LastPing is null", true, decoded.LastPing is null);
        Assert.IsNull(decoded.LastPing);
        diagnostics.Assert("decoded.Failure is null", true, decoded.Failure is null);
        Assert.IsNull(decoded.Failure);
    }

    private static void AssertViolation(TestDiagnostics diagnostics, string message, WsDecodedBytes decoded)
    {
        diagnostics.Assert("decoded.Failure is not null", true, decoded.Failure is not null);
        Assert.IsNotNull(decoded.Failure);
        diagnostics.Assert("exit code", CurlExitCode.RecvError, decoded.Failure.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, decoded.Failure.ExitCode);
        diagnostics.Assert("failure message", message, decoded.Failure.Message);
        Assert.AreEqual(message, decoded.Failure.Message);
        diagnostics.Assert("decoded.LastPing is null", true, decoded.LastPing is null);
        Assert.IsNull(decoded.LastPing);
    }

    private static WsDecodedBytes Run(TestDiagnostics diagnostics, params string[] parts)
    {
        byte[] input = Frames(parts);
        diagnostics.Arrange("input", input.Length + " bytes in one read");
        diagnostics.Bytes("input", input);
        WsDecodedBytes decoded = new WsFrameDecoder().Decode(input);
        Describe(diagnostics, decoded);
        return decoded;
    }

    private static void Describe(TestDiagnostics diagnostics, WsDecodedBytes decoded)
    {
        diagnostics.Act(
            "decoded",
            $"payload {decoded.Payload.Length} bytes, ping {(decoded.LastPing is null ? "none" : decoded.LastPing.Length + " bytes")}, failure {(decoded.Failure is null ? "none" : "exit " + decoded.Failure.ExitCode + " " + decoded.Failure.Message)}");
    }

    private static string Text(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    /// <summary>
    /// Joins the parts into one run of bytes: a part of hex byte pairs separated by spaces is
    /// those bytes, any other part is its Latin-1 text.
    /// </summary>
    private static byte[] Frames(params string[] parts) =>
        [.. parts.SelectMany(part => IsHex(part) ? Convert.FromHexString(part.Replace(" ", string.Empty, StringComparison.Ordinal)) : Encoding.Latin1.GetBytes(part))];

    private static bool IsHex(string part) =>
        part.Split(' ').All(pair => pair.Length == 2 && pair.All(char.IsAsciiHexDigitLower));
}
