using Curl.Protocol.Ws.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ws;

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

    private static void WriteFrame(TestDiagnostics diagnostics, byte[] frame)
    {
        diagnostics.Bytes("frame", frame);
        diagnostics.Act(
            "frame decoded",
            $"FIN {frame[0] >> 7}, opcode 0x{frame[0] & 0x0f:x}, masked {frame[1] >> 7}, length indicator {frame[1] & 0x7f}, total {frame.Length} bytes");
    }
}
