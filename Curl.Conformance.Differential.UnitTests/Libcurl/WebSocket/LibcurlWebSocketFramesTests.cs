using Curl.Conformance.Differential.Libcurl.WebSocket;

namespace Curl.Conformance.Differential.UnitTests.Libcurl.WebSocket;

[TestClass]
public sealed class LibcurlWebSocketFramesTests
{
    [TestMethod]
    public void Frame_ShortFinalPayload_WritesFinOpcodeAndOneLengthByte() =>
        CollectionAssert.AreEqual(new byte[] { 0x81, 2, (byte)'h', (byte)'i' }, LibcurlWebSocketFrames.Frame(LibcurlWebSocketFrames.Text, "hi"u8.ToArray()));

    [TestMethod]
    public void Frame_NotFinal_ClearsTheFinBit() =>
        CollectionAssert.AreEqual(new byte[] { 0x02, 0 }, LibcurlWebSocketFrames.Frame(LibcurlWebSocketFrames.Binary, [], final: false));

    [TestMethod]
    public void Frame_PayloadOf126Bytes_WritesTheTwoByteExtendedLength()
    {
        var frame = LibcurlWebSocketFrames.Frame(LibcurlWebSocketFrames.Binary, new byte[300]);

        CollectionAssert.AreEqual(new byte[] { 0x82, 126, 1, 44 }, frame[..4]);
        Assert.HasCount(304, frame);
    }

    [TestMethod]
    public void Frame_NullPayload_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => LibcurlWebSocketFrames.Frame(LibcurlWebSocketFrames.Ping, null!));
}
