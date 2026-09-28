using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ws;

/// <summary>
/// Pins the frame reader against what curl 8.21.0 did with each server frame, measured on
/// 2026-09-28 with <c>Record-CurlExchange.ps1</c> (BL-581): which payloads reach the output,
/// which ping is answered, and the exit 56 message of each violation.
/// </summary>
[TestClass]
public sealed class WsFrameDecoderTests
{
    private static readonly string Long = new('a', 126);

    [TestMethod]
    public void Decode_TextFrameWith7BitLength_PassesPayloadOn()
    {
        WsDecodedBytes decoded = new WsFrameDecoder().Decode(Frames("81 05", "hello"));

        Assert.AreEqual("hello", Text(decoded.Payload));
        Assert.IsNull(decoded.LastPing);
        Assert.IsNull(decoded.Failure);
    }

    [TestMethod]
    public void Decode_BinaryFrameWith16BitLength_PassesPayloadOn()
    {
        WsDecodedBytes decoded = new WsFrameDecoder().Decode(Frames("82 7e 00 7e", Long));

        Assert.AreEqual(Long, Text(decoded.Payload));
    }

    [TestMethod]
    public void Decode_16BitLengthOfAShortPayload_IsAccepted()
    {
        WsDecodedBytes decoded = new WsFrameDecoder().Decode(Frames("82 7e 00 02", "ok"));

        Assert.AreEqual("ok", Text(decoded.Payload));
    }

    [TestMethod]
    public void Decode_16BitLengthOfZero_EndsTheFrameAtItsHead()
    {
        WsDecodedBytes decoded = new WsFrameDecoder().Decode(Frames("82 7e 00 00 81 02", "ok"));

        Assert.AreEqual("ok", Text(decoded.Payload));
    }

    [TestMethod]
    public void Decode_BinaryFrameWith64BitLength_PassesPayloadOn()
    {
        WsDecodedBytes decoded = new WsFrameDecoder().Decode(Frames("82 7f 00 00 00 00 00 00 00 7e", Long));

        Assert.AreEqual(Long, Text(decoded.Payload));
    }

    [TestMethod]
    public void Decode_64BitLengthWithTheTopBitSet_FailsWith56()
    {
        WsDecodedBytes decoded = new WsFrameDecoder().Decode(Frames("82 7f 80 00 00 00 00 00 00 02", "ok"));

        AssertViolation("[WS] frame length longer than 63 bits not supported", decoded);
        Assert.IsEmpty(decoded.Payload);
    }

    [TestMethod]
    public void Decode_TextMessageInThreeFragments_PassesEachFragmentOn()
    {
        WsDecodedBytes decoded = new WsFrameDecoder().Decode(Frames("01 03", "hel", "00 01", "l", "80 01", "o"));

        Assert.AreEqual("hello", Text(decoded.Payload));
        Assert.IsNull(decoded.Failure);
    }

    [TestMethod]
    public void Decode_FramesSplitIntoSingleBytes_DecodeAsOneRead()
    {
        byte[] frames = Frames("89 02", "hi", "01 03", "hel", "80 7e 00 02", "lo", "88 02 03 e8");
        var decoder = new WsFrameDecoder();
        var payload = new List<byte>();
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

        CollectionAssert.AreEqual(Frames("hello", "03 e8"), payload.ToArray());
        CollectionAssert.AreEqual(new[] { "hi" }, pings);
    }

    [TestMethod]
    public void Decode_PingSplitAcrossReads_IsAnsweredOnceComplete()
    {
        var decoder = new WsFrameDecoder();

        WsDecodedBytes first = decoder.Decode(Frames("89 02", "h"));
        WsDecodedBytes second = decoder.Decode(Frames("i", "81 02", "ok"));

        Assert.IsNull(first.LastPing);
        Assert.AreEqual("hi", Text(second.LastPing!));
        Assert.AreEqual("ok", Text(second.Payload));
    }

    [TestMethod]
    public void Decode_TextThenCloseWithStatus_PassesBothPayloadsOn()
    {
        WsDecodedBytes decoded = new WsFrameDecoder().Decode(Frames("81 05", "hello", "88 02 03 e8"));

        CollectionAssert.AreEqual(Frames("hello", "03 e8"), decoded.Payload);
    }

    [TestMethod]
    public void Decode_CloseWithStatusAndReason_PassesPayloadOn()
    {
        WsDecodedBytes decoded = new WsFrameDecoder().Decode(Frames("88 05 03 e8", "bye"));

        CollectionAssert.AreEqual(Frames("03 e8", "bye"), decoded.Payload);
    }

    [TestMethod]
    public void Decode_EmptyClose_PassesNothingOn()
    {
        WsDecodedBytes decoded = new WsFrameDecoder().Decode(Frames("81 05", "hello", "88 00"));

        Assert.AreEqual("hello", Text(decoded.Payload));
        Assert.IsNull(decoded.Failure);
    }

    [TestMethod]
    public void Decode_OneByteClose_PassesItOnUnchecked()
    {
        WsDecodedBytes decoded = new WsFrameDecoder().Decode(Frames("88 01", "x", "81 02", "ok"));

        Assert.AreEqual("xok", Text(decoded.Payload));
    }

    [TestMethod]
    public void Decode_ServerPong_PassesPayloadOn()
    {
        WsDecodedBytes decoded = new WsFrameDecoder().Decode(Frames("8a 02", "hi", "81 02", "ok"));

        Assert.AreEqual("hiok", Text(decoded.Payload));
        Assert.IsNull(decoded.LastPing);
    }

    [TestMethod]
    public void Decode_Ping_HoldsPayloadBackForThePong()
    {
        WsDecodedBytes decoded = new WsFrameDecoder().Decode(Frames("89 02", "hi", "81 05", "hello", "88 02 03 e8"));

        CollectionAssert.AreEqual(Frames("hello", "03 e8"), decoded.Payload);
        Assert.AreEqual("hi", Text(decoded.LastPing!));
    }

    [TestMethod]
    public void Decode_EmptyPing_IsAnsweredWithAnEmptyPayload()
    {
        WsDecodedBytes decoded = new WsFrameDecoder().Decode(Frames("89 00 81 05", "hello"));

        Assert.IsNotNull(decoded.LastPing);
        Assert.IsEmpty(decoded.LastPing);
    }

    [TestMethod]
    public void Decode_125BytePing_IsAccepted()
    {
        WsDecodedBytes decoded = new WsFrameDecoder().Decode(Frames("89 7d", new string('b', 125), "81 02", "ok"));

        Assert.AreEqual(new string('b', 125), Text(decoded.LastPing!));
        Assert.AreEqual("ok", Text(decoded.Payload));
    }

    [TestMethod]
    public void Decode_TwoPingsInOneRead_KeepsTheLast()
    {
        WsDecodedBytes decoded = new WsFrameDecoder().Decode(Frames("89 01", "a", "89 01", "b"));

        Assert.AreEqual("b", Text(decoded.LastPing!));
    }

    [TestMethod]
    public void Decode_PingInsideAFragmentedMessage_IsAnsweredAndTheMessageGoesOn()
    {
        WsDecodedBytes decoded = new WsFrameDecoder().Decode(Frames("01 03", "hel", "89 02", "hi", "80 02", "lo"));

        Assert.AreEqual("hello", Text(decoded.Payload));
        Assert.AreEqual("hi", Text(decoded.LastPing!));
    }

    [TestMethod]
    public void Decode_PingAfterAClose_IsStillAnswered()
    {
        WsDecodedBytes decoded = new WsFrameDecoder().Decode(Frames("88 00 89 02", "hi"));

        Assert.AreEqual("hi", Text(decoded.LastPing!));
    }

    [TestMethod]
    public void Decode_TruncatedFrame_PassesOnWhatArrived()
    {
        WsDecodedBytes decoded = new WsFrameDecoder().Decode(Frames("81 05", "hel"));

        Assert.AreEqual("hel", Text(decoded.Payload));
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
        WsDecodedBytes decoded = new WsFrameDecoder().Decode(Frames(head, "hello"));

        AssertViolation(message, decoded);
        Assert.IsEmpty(decoded.Payload);
    }

    [TestMethod]
    [DataRow("81", "TEXT")]
    [DataRow("82", "BINARY")]
    public void Decode_NewMessageBeforeTheFragmentedOneEnds_FailsAfterPassingTheFirstFragmentOn(string first, string name)
    {
        WsDecodedBytes decoded = new WsFrameDecoder().Decode(Frames("02 03", "hel", first + " 02", "ok"));

        AssertViolation($"[WS] fragmented message interrupted by new {name} msg", decoded);
        Assert.AreEqual("hel", Text(decoded.Payload));
    }

    [TestMethod]
    public void Decode_ReservedBitsOnAContinuation_FailsAfterPassingTheFirstFragmentOn()
    {
        WsDecodedBytes decoded = new WsFrameDecoder().Decode(Frames("01 01", "h", "c0 01", "i"));

        AssertViolation("[WS] invalid reserved bits: c0", decoded);
        Assert.AreEqual("h", Text(decoded.Payload));
    }

    [TestMethod]
    public void Decode_ViolationAfterAPing_ReportsNoPing()
    {
        WsDecodedBytes decoded = new WsFrameDecoder().Decode(Frames("89 02", "hi", "81 85 01 02 03 04"));

        Assert.IsNull(decoded.LastPing);
        Assert.IsNotNull(decoded.Failure);
    }

    [TestMethod]
    public void Decode_NothingReceived_DecodesNothing()
    {
        WsDecodedBytes decoded = new WsFrameDecoder().Decode([]);

        Assert.IsEmpty(decoded.Payload);
        Assert.IsNull(decoded.LastPing);
        Assert.IsNull(decoded.Failure);
    }

    private static void AssertViolation(string message, WsDecodedBytes decoded)
    {
        Assert.IsNotNull(decoded.Failure);
        Assert.AreEqual(CurlExitCode.RecvError, decoded.Failure.ExitCode);
        Assert.AreEqual(message, decoded.Failure.Message);
        Assert.IsNull(decoded.LastPing);
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
