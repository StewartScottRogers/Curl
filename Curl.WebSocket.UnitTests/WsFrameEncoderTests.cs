using Curl.Testing;
using Curl.WebSocket.Fakes;

namespace Curl.WebSocket;

/// <summary>
/// Pins the client frames against the bytes curl 8.21.0 sent on 2026-09-28 (BL-581), with the
/// mask set to the one curl drew for each.
/// </summary>
[TestClass]
public sealed class WsFrameEncoderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Encode_PongEchoingHi_IsCurlsMaskedPong()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("opcode", WsOpcode.Pong);
        diagnostics.Arrange("payload", "hi");
        diagnostics.Arrange("mask", "44 22 90 af");

        byte[] frame = WsFrameEncoder.Encode(WsOpcode.Pong, "hi"u8, new ScriptedRandomSource(0x44, 0x22, 0x90, 0xaf));

        byte[] expected = Convert.FromHexString("8a82442290af2c4b");
        WriteFrame(diagnostics, frame);
        diagnostics.Diff("frame", expected, frame);
        CollectionAssert.AreEqual(expected, frame);
    }

    [TestMethod]
    public void Encode_EmptyPong_IsHeadAndMaskOnly()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("opcode", WsOpcode.Pong);
        diagnostics.Arrange("payload", "empty");
        diagnostics.Arrange("mask", "8a 44 48 0d");

        byte[] frame = WsFrameEncoder.Encode(WsOpcode.Pong, [], new ScriptedRandomSource(0x8a, 0x44, 0x48, 0x0d));

        byte[] expected = Convert.FromHexString("8a808a44480d");
        WriteFrame(diagnostics, frame);
        diagnostics.Diff("frame", expected, frame);
        CollectionAssert.AreEqual(expected, frame);
    }

    [TestMethod]
    public void Encode_125BytePayload_Uses7BitLength()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] payload = Enumerable.Repeat((byte)'b', 125).ToArray();
        diagnostics.Arrange("opcode", WsOpcode.Pong);
        diagnostics.Arrange("payload", "125 bytes of 'b'");
        diagnostics.Arrange("mask", "11 95 60 cb");

        byte[] frame = WsFrameEncoder.Encode(WsOpcode.Pong, payload, new ScriptedRandomSource(0x11, 0x95, 0x60, 0xcb));

        byte[] maskedB = [0x73, 0xf7, 0x02, 0xa9];
        byte[] expected = [0x8a, 0xfd, 0x11, 0x95, 0x60, 0xcb, .. Enumerable.Range(0, 125).Select(index => maskedB[index % 4])];
        WriteFrame(diagnostics, frame);
        diagnostics.Diff("frame", expected, frame);
        CollectionAssert.AreEqual(expected, frame);
    }

    [TestMethod]
    public void Encode_BinaryUpload_IsCurlsMaskedBinaryFrame()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("opcode", WsOpcode.Binary);
        diagnostics.Arrange("payload", "abc");
        diagnostics.Arrange("mask", "00 01 02 03 (fixed random source)");

        byte[] frame = WsFrameEncoder.Encode(WsOpcode.Binary, "abc"u8, new FixedRandomSource());

        byte[] expected = Convert.FromHexString("828300010203616361");
        WriteFrame(diagnostics, frame);
        diagnostics.Diff("frame", expected, frame);
        CollectionAssert.AreEqual(expected, frame);
    }

    [TestMethod]
    public void Encode_126BytePayload_Uses16BitLength()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("opcode", WsOpcode.Binary);
        diagnostics.Arrange("payload", "126 zero bytes");
        diagnostics.Arrange("mask", "00 01 02 03 (fixed random source)");

        byte[] frame = WsFrameEncoder.Encode(WsOpcode.Binary, new byte[126], new FixedRandomSource());

        byte[] expectedHead = Convert.FromHexString("82fe007e00010203");
        byte[] expectedMasked = [0, 1, 2, 3, 0];
        WriteFrame(diagnostics, frame);
        diagnostics.Assert("frame length", 2 + 2 + 4 + 126, frame.Length);
        diagnostics.Diff("frame head", expectedHead, frame[..8]);
        diagnostics.Diff("masked payload start", expectedMasked, frame[8..13]);
        Assert.AreEqual(2 + 2 + 4 + 126, frame.Length);
        CollectionAssert.AreEqual(expectedHead, frame[..8]);
        CollectionAssert.AreEqual(expectedMasked, frame[8..13]);
    }

    [TestMethod]
    public void Encode_65535BytePayload_Uses16BitLength()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("opcode", WsOpcode.Text);
        diagnostics.Arrange("payload", "65535 zero bytes");
        diagnostics.Arrange("mask", "00 01 02 03 (fixed random source)");

        byte[] frame = WsFrameEncoder.Encode(WsOpcode.Text, new byte[65535], new FixedRandomSource());

        byte[] expectedHead = Convert.FromHexString("81feffff00010203");
        WriteFrame(diagnostics, frame);
        diagnostics.Diff("frame head", expectedHead, frame[..8]);
        diagnostics.Assert("frame length", 8 + 65535, frame.Length);
        CollectionAssert.AreEqual(expectedHead, frame[..8]);
        Assert.AreEqual(8 + 65535, frame.Length);
    }

    [TestMethod]
    public void Encode_65536BytePayload_Uses64BitLength()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("opcode", WsOpcode.Binary);
        diagnostics.Arrange("payload", "65536 zero bytes");
        diagnostics.Arrange("mask", "00 01 02 03 (fixed random source)");

        byte[] frame = WsFrameEncoder.Encode(WsOpcode.Binary, new byte[65536], new FixedRandomSource());

        byte[] expectedHead = Convert.FromHexString("82ff000000000001000000010203");
        WriteFrame(diagnostics, frame);
        diagnostics.Diff("frame head", expectedHead, frame[..14]);
        diagnostics.Assert("frame length", 14 + 65536, frame.Length);
        CollectionAssert.AreEqual(expectedHead, frame[..14]);
        Assert.AreEqual(14 + 65536, frame.Length);
    }

    [TestMethod]
    [DataRow(WsOpcode.Text, true, "6869", "818201020304696b", DisplayName = "text 'hi', FIN")]
    [DataRow(WsOpcode.Binary, false, "ff", "028101020304fe", DisplayName = "binary first fragment, FIN clear")]
    [DataRow(WsOpcode.Continuation, false, "61", "00810102030460", DisplayName = "continuation, FIN clear")]
    [DataRow(WsOpcode.Continuation, true, "", "808001020304", DisplayName = "last continuation, empty")]
    [DataRow(WsOpcode.Close, true, "03e8", "88820102030402ea", DisplayName = "close 1000")]
    [DataRow(WsOpcode.Ping, true, "70", "89810102030471", DisplayName = "ping 'p'")]
    [DataRow(WsOpcode.Pong, true, "", "8a8001020304", DisplayName = "empty pong")]
    public void EncodeWithFin_EachOpcode_WritesTheMaskedFrame(WsOpcode opcode, bool isFinal, string payloadHex, string expectedHex)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("opcode", opcode);
        diagnostics.Arrange("FIN", isFinal);
        diagnostics.Arrange("payload", payloadHex);

        byte[] frame = WsFrameEncoder.Encode(opcode, isFinal, Convert.FromHexString(payloadHex), new ScriptedRandomSource(1, 2, 3, 4));

        byte[] expected = Convert.FromHexString(expectedHex);
        diagnostics.Diff("frame", expected, frame);
        CollectionAssert.AreEqual(expected, frame);
    }

    [TestMethod]
    public void EncodeWithFin_126BytePayload_Uses16BitLength()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] payload = Enumerable.Repeat((byte)'x', 126).ToArray();
        diagnostics.Arrange("payload", "126 bytes of 'x', mask 00 00 00 00");

        byte[] frame = WsFrameEncoder.Encode(WsOpcode.Binary, isFinal: true, payload, new ScriptedRandomSource(0, 0, 0, 0));

        byte[] expected = [0x82, 0xfe, 0x00, 0x7e, 0, 0, 0, 0, .. payload];
        diagnostics.Diff("frame", expected, frame);
        CollectionAssert.AreEqual(expected, frame);
    }

    [TestMethod]
    public void EncodeWithFinClear_65536BytePayload_Uses64BitLengthAndClearsFin()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] payload = Enumerable.Repeat((byte)'y', 65536).ToArray();
        diagnostics.Arrange("payload", "65536 bytes of 'y', mask 00 00 00 00, FIN clear");

        byte[] frame = WsFrameEncoder.Encode(WsOpcode.Text, isFinal: false, payload, new ScriptedRandomSource(0, 0, 0, 0));

        byte[] expected = [0x01, 0xff, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, .. payload];
        diagnostics.Act("frame length", frame.Length);
        diagnostics.Assert("frame length", expected.Length, frame.Length);
        CollectionAssert.AreEqual(expected, frame);
    }

    [TestMethod]
    public void EncodeWithFin_NullRandomSource_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("random source", "(null)");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => WsFrameEncoder.Encode(WsOpcode.Text, isFinal: true, [], null!));

        diagnostics.Assert("parameter", "randomSource", exception.ParamName);
        Assert.AreEqual("randomSource", exception.ParamName);
    }

    [TestMethod]
    public void EncodeHeadAndMaskPart_TwoParts_ReadAsTheWholeFrameEncodedAtOnce()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] payload = "helloworld"u8.ToArray();
        diagnostics.Arrange("payload", "'helloworld' in parts of 3 and 7, mask 01 02 03 04");

        byte[] head = WsFrameEncoder.EncodeHead(WsOpcode.Text, isFinal: true, payload.Length, new ScriptedRandomSource(1, 2, 3, 4));
        byte[] mask = head[^WsFrameEncoder.MaskLength..];
        byte[] parts = [.. head, .. WsFrameEncoder.MaskPart(payload.AsSpan(0, 3), mask, 0), .. WsFrameEncoder.MaskPart(payload.AsSpan(3), mask, 3)];

        byte[] whole = WsFrameEncoder.Encode(WsOpcode.Text, isFinal: true, payload, new ScriptedRandomSource(1, 2, 3, 4));
        diagnostics.Diff("frame", whole, parts);
        CollectionAssert.AreEqual(whole, parts);
    }

    [TestMethod]
    [DataRow(125L, "81fd01020304", DisplayName = "125 bytes, 7-bit length")]
    [DataRow(126L, "81fe007e01020304", DisplayName = "126 bytes, 16-bit length")]
    [DataRow(4294967296L, "81ff000000010000000001020304", DisplayName = "4 GiB, 64-bit length")]
    public void EncodeHead_PayloadLength_AnnouncesItInTheShortestForm(long payloadLength, string expectedHex)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("payload length", payloadLength);

        byte[] head = WsFrameEncoder.EncodeHead(WsOpcode.Text, isFinal: true, payloadLength, new ScriptedRandomSource(1, 2, 3, 4));

        byte[] expected = Convert.FromHexString(expectedHex);
        diagnostics.Diff("head", expected, head);
        CollectionAssert.AreEqual(expected, head);
    }

    [TestMethod]
    public void EncodeHead_NegativeLengthOrNullRandomSource_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("payload length", -1);

        ArgumentOutOfRangeException negative = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => WsFrameEncoder.EncodeHead(WsOpcode.Text, true, -1, new FixedRandomSource()));
        ArgumentNullException missing = Assert.ThrowsExactly<ArgumentNullException>(() => WsFrameEncoder.EncodeHead(WsOpcode.Text, true, 0, null!));

        diagnostics.Assert("parameter", "payloadLength", negative.ParamName);
        Assert.AreEqual("payloadLength", negative.ParamName);
        Assert.AreEqual("randomSource", missing.ParamName);
    }

    [TestMethod]
    public void MaskPart_WrongMaskLengthOrNegativeOffset_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mask", "3 bytes; offset -1");

        ArgumentException shortMask = Assert.ThrowsExactly<ArgumentException>(() => WsFrameEncoder.MaskPart("x"u8, [1, 2, 3], 0));
        ArgumentOutOfRangeException negative = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => WsFrameEncoder.MaskPart("x"u8, [1, 2, 3, 4], -1));

        diagnostics.Assert("parameter", "mask", shortMask.ParamName);
        Assert.AreEqual("mask", shortMask.ParamName);
        Assert.AreEqual("offset", negative.ParamName);
    }

    private static void WriteFrame(TestDiagnostics diagnostics, byte[] frame)
    {
        diagnostics.Bytes("frame", frame);
        diagnostics.Act(
            "frame decoded",
            $"FIN {frame[0] >> 7}, opcode 0x{frame[0] & 0x0f:x}, masked {frame[1] >> 7}, length indicator {frame[1] & 0x7f}, total {frame.Length} bytes");
    }
}
