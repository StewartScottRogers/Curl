using Curl.Protocol.Ws.Fakes;

namespace Curl.Protocol.Ws;

/// <summary>
/// Pins the client frames against the bytes curl 8.21.0 sent on 2026-09-28 (BL-581), with the
/// mask set to the one curl drew for each.
/// </summary>
[TestClass]
public sealed class WsFrameEncoderTests
{
    [TestMethod]
    public void Encode_PongEchoingHi_IsCurlsMaskedPong()
    {
        byte[] frame = WsFrameEncoder.Encode(WsOpcode.Pong, "hi"u8, new ScriptedRandomSource(0x44, 0x22, 0x90, 0xaf));

        CollectionAssert.AreEqual(Convert.FromHexString("8a82442290af2c4b"), frame);
    }

    [TestMethod]
    public void Encode_EmptyPong_IsHeadAndMaskOnly()
    {
        byte[] frame = WsFrameEncoder.Encode(WsOpcode.Pong, [], new ScriptedRandomSource(0x8a, 0x44, 0x48, 0x0d));

        CollectionAssert.AreEqual(Convert.FromHexString("8a808a44480d"), frame);
    }

    [TestMethod]
    public void Encode_125BytePayload_Uses7BitLength()
    {
        byte[] payload = Enumerable.Repeat((byte)'b', 125).ToArray();

        byte[] frame = WsFrameEncoder.Encode(WsOpcode.Pong, payload, new ScriptedRandomSource(0x11, 0x95, 0x60, 0xcb));

        byte[] maskedB = [0x73, 0xf7, 0x02, 0xa9];
        byte[] expected = [0x8a, 0xfd, 0x11, 0x95, 0x60, 0xcb, .. Enumerable.Range(0, 125).Select(index => maskedB[index % 4])];
        CollectionAssert.AreEqual(expected, frame);
    }

    [TestMethod]
    public void Encode_BinaryUpload_IsCurlsMaskedBinaryFrame()
    {
        byte[] frame = WsFrameEncoder.Encode(WsOpcode.Binary, "abc"u8, new FixedRandomSource());

        CollectionAssert.AreEqual(Convert.FromHexString("828300010203616361"), frame);
    }

    [TestMethod]
    public void Encode_126BytePayload_Uses16BitLength()
    {
        byte[] frame = WsFrameEncoder.Encode(WsOpcode.Binary, new byte[126], new FixedRandomSource());

        Assert.AreEqual(2 + 2 + 4 + 126, frame.Length);
        CollectionAssert.AreEqual(Convert.FromHexString("82fe007e00010203"), frame[..8]);
        CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 0 }, frame[8..13]);
    }

    [TestMethod]
    public void Encode_65535BytePayload_Uses16BitLength()
    {
        byte[] frame = WsFrameEncoder.Encode(WsOpcode.Text, new byte[65535], new FixedRandomSource());

        CollectionAssert.AreEqual(Convert.FromHexString("81feffff00010203"), frame[..8]);
        Assert.AreEqual(8 + 65535, frame.Length);
    }

    [TestMethod]
    public void Encode_65536BytePayload_Uses64BitLength()
    {
        byte[] frame = WsFrameEncoder.Encode(WsOpcode.Binary, new byte[65536], new FixedRandomSource());

        CollectionAssert.AreEqual(Convert.FromHexString("82ff000000000001000000010203"), frame[..14]);
        Assert.AreEqual(14 + 65536, frame.Length);
    }
}
