namespace Curl.Extended.Protocol.Amqp1.Frames;

[TestClass]
public sealed class Amqp1ProtocolHeaderAndFrameTests
{
    [TestMethod]
    public void Write_AmqpAndSaslHeaders_AreAmqpProtocolIdAndVersion_Part2Section2_2_Part5Section5_3_1()
    {
        Assert.AreEqual("414D515000010000", Convert.ToHexString(Amqp1ProtocolHeader.Write(Amqp1ProtocolHeader.AmqpProtocolId)));
        Assert.AreEqual("414D515003010000", Convert.ToHexString(Amqp1ProtocolHeader.Write(Amqp1ProtocolHeader.SaslProtocolId)));
    }

    [TestMethod]
    public void Read_EachKnownHeader_ReturnsItsProtocolId()
    {
        Assert.AreEqual(Amqp1ProtocolHeader.AmqpProtocolId, Amqp1ProtocolHeader.Read(Convert.FromHexString("414D515000010000")));
        Assert.AreEqual(Amqp1ProtocolHeader.TlsProtocolId, Amqp1ProtocolHeader.Read(Convert.FromHexString("414D515002010000")));
        Assert.AreEqual(Amqp1ProtocolHeader.SaslProtocolId, Amqp1ProtocolHeader.Read(Convert.FromHexString("414D515003010000")));
    }

    [TestMethod]
    [DataRow("414D5150000100", DisplayName = "seven bytes")]
    [DataRow("485454502F312E31", DisplayName = "not AMQP")]
    [DataRow("414D515000000901", DisplayName = "AMQP 0-9-1")]
    [DataRow("414D515001010000", DisplayName = "protocol id 1")]
    public void Read_NotAnAmqp1Header_Throws(string hex)
    {
        Assert.ThrowsExactly<Amqp1MalformedEncodingException>(() => Amqp1ProtocolHeader.Read(Convert.FromHexString(hex)));
    }

    [TestMethod]
    public void RequireProtocol_SameHeader_Passes_OtherHeader_IsReported()
    {
        Amqp1ProtocolHeader.RequireProtocol(Amqp1ProtocolHeader.Write(3), Amqp1ProtocolHeader.SaslProtocolId);

        var exception = Assert.ThrowsExactly<Amqp1MalformedEncodingException>(
            () => Amqp1ProtocolHeader.RequireProtocol(Amqp1ProtocolHeader.Write(0), Amqp1ProtocolHeader.SaslProtocolId));
        Assert.AreEqual("The peer answered with protocol header AMQP 0 1.0.0 to AMQP 3 1.0.0.", exception.Message);
    }

    [TestMethod]
    public void Write_Frame_IsSizeDoffTypeChannelBody_Section2_3()
    {
        var frame = new Amqp1Frame(Amqp1Frame.AmqpType, 0x0102, new byte[] { 0xAA, 0xBB });

        Assert.AreEqual("0000000A02000102AABB", Convert.ToHexString(Amqp1FrameCodec.Write(frame)));
    }

    [TestMethod]
    public void TryRead_WholeFrameAndMore_ReadsOneFrame()
    {
        var buffer = Convert.FromHexString("0000000A02010000AABB" + "FF");

        Assert.IsTrue(Amqp1FrameCodec.TryRead(buffer, out var frame, out var consumed));

        Assert.AreEqual(10, consumed);
        Assert.AreEqual(Amqp1Frame.SaslType, frame!.Type);
        Assert.AreEqual((ushort)0, frame.Channel);
        Assert.AreEqual("AABB", Convert.ToHexString(frame.Body.Span));
    }

    [TestMethod]
    public void TryRead_ExtendedHeader_IsSkipped()
    {
        var buffer = Convert.FromHexString("0000000E03000005" + "11223344" + "AABB");

        Assert.IsTrue(Amqp1FrameCodec.TryRead(buffer, out var frame, out _));

        Assert.AreEqual((ushort)5, frame!.Channel);
        Assert.AreEqual("AABB", Convert.ToHexString(frame.Body.Span));
    }

    [TestMethod]
    [DataRow("000000", DisplayName = "no whole SIZE")]
    [DataRow("0000000A0200000000", DisplayName = "SIZE past the buffer")]
    public void TryRead_PartOfAFrame_IsFalse(string hex)
    {
        Assert.IsFalse(Amqp1FrameCodec.TryRead(Convert.FromHexString(hex), out var frame, out var consumed));

        Assert.IsNull(frame);
        Assert.AreEqual(0, consumed);
    }

    [TestMethod]
    [DataRow("00000007020000", DisplayName = "SIZE below 8")]
    [DataRow("0000000801000000", DisplayName = "DOFF below 2")]
    [DataRow("0000000803000000", DisplayName = "DOFF past SIZE")]
    [DataRow("0000000802020000", DisplayName = "unknown TYPE")]
    public void TryRead_MalformedFrame_Throws(string hex)
    {
        Assert.ThrowsExactly<Amqp1MalformedEncodingException>(() => Amqp1FrameCodec.TryRead(Convert.FromHexString(hex), out _, out _));
    }

    [TestMethod]
    public void EmptyAmqpFrame_IsHeartbeat_Section2_4_5()
    {
        Assert.AreEqual("0000000802000000", Convert.ToHexString(Amqp1FrameCodec.Write(Amqp1Frame.Heartbeat())));

        Assert.IsTrue(Amqp1FrameCodec.TryRead(Convert.FromHexString("0000000802000003"), out var frame, out _));
        Assert.IsTrue(frame!.IsHeartbeat);
        Assert.IsFalse(new Amqp1Frame(Amqp1Frame.SaslType, 0, ReadOnlyMemory<byte>.Empty).IsHeartbeat);
        Assert.IsFalse(new Amqp1Frame(Amqp1Frame.AmqpType, 0, new byte[] { 0x40 }).IsHeartbeat);
    }
}
