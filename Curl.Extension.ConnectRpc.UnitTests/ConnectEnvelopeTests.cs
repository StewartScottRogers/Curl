using System.Text;

namespace Curl.Extension.ConnectRpc;

/// <summary>Connect protocol reference, "Streaming-RPC": Envelope-Flags, Message-Length and Message.</summary>
[TestClass]
public sealed class ConnectEnvelopeTests
{
    [TestMethod]
    public void Encode_FramesEachMessageWithNoFlagsAndItsBigEndianLength()
    {
        byte[] framed = ConnectEnvelope.Encode([Encoding.UTF8.GetBytes("{}"), Encoding.UTF8.GetBytes("{\"a\":1}")]);

        byte[] expected = [0, 0, 0, 0, 2, (byte)'{', (byte)'}', 0, 0, 0, 0, 7, .. Encoding.UTF8.GetBytes("{\"a\":1}")];
        CollectionAssert.AreEqual(expected, framed);
    }

    [TestMethod]
    public void Encode_NoMessages_GivesNoBytes()
    {
        Assert.IsEmpty(ConnectEnvelope.Encode([]));
    }

    [TestMethod]
    public void Decode_ReadsEveryMessageWithItsFlags()
    {
        IReadOnlyList<ConnectEnvelopedMessage> messages = ConnectEnvelope.Decode([0, 0, 0, 0, 1, (byte)'x', 2, 0, 0, 0, 0]);

        Assert.HasCount(2, messages);
        Assert.AreEqual(0, messages[0].Flags);
        CollectionAssert.AreEqual(new[] { (byte)'x' }, messages[0].Payload);
        Assert.AreEqual(ConnectEnvelope.EndStreamFlag, messages[1].Flags);
        Assert.IsEmpty(messages[1].Payload);
    }

    [TestMethod]
    public void Decode_BytesThatEndInsideThePrefix_AreRefused()
    {
        Assert.ThrowsExactly<FormatException>(() => ConnectEnvelope.Decode([0, 0, 0]));
    }

    [TestMethod]
    public void Decode_ALengthPastTheEnd_IsRefused()
    {
        Assert.ThrowsExactly<FormatException>(() => ConnectEnvelope.Decode([0, 0, 0, 0, 2, (byte)'x']));
        Assert.ThrowsExactly<FormatException>(() => ConnectEnvelope.Decode([0, 0xFF, 0xFF, 0xFF, 0xFF, (byte)'x']));
    }
}
