namespace Curl.Extension.GrpcWeb;

[TestClass]
public sealed class GrpcWebFramingTests
{
    [TestMethod]
    public void Frame_AMessage_GivesFlagsZeroItsBigEndianLengthAndTheMessage_MessageFraming()
    {
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0, 3, 0x08, 0x96, 0x01 }, GrpcWebFraming.Frame([0x08, 0x96, 0x01]));
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0, 0 }, GrpcWebFraming.Frame([]));
    }

    [TestMethod]
    public void Parse_DataFramesThenTrailers_GivesTheMessagesInOrderAndTheTrailers_MessageFraming()
    {
        byte[] body = [0, 0, 0, 0, 1, 0x41, 0, 0, 0, 0, 2, 0x42, 0x43, 0x80, 0, 0, 0, 15, .. "grpc-status:0\r\n"u8];

        GrpcWebReply reply = GrpcWebFraming.Parse(body);

        Assert.HasCount(2, reply.Messages);
        CollectionAssert.AreEqual(new byte[] { 0x41 }, reply.Messages[0].ToArray());
        CollectionAssert.AreEqual(new byte[] { 0x42, 0x43 }, reply.Messages[1].ToArray());
        CollectionAssert.AreEqual(new[] { new KeyValuePair<string, string>("grpc-status", "0") }, reply.Trailers!.ToArray());
    }

    [TestMethod]
    public void Parse_AnEmptyBody_GivesNoMessagesAndNoTrailers_TrailersOnlyResponses()
    {
        GrpcWebReply reply = GrpcWebFraming.Parse(ReadOnlyMemory<byte>.Empty);

        Assert.IsEmpty(reply.Messages);
        Assert.IsNull(reply.Trailers);
    }

    [TestMethod]
    [DataRow(new byte[] { 0, 0, 0 }, DisplayName = "prefix cut short")]
    [DataRow(new byte[] { 0, 0, 0, 0, 2, 0x41 }, DisplayName = "message cut short")]
    public void Parse_AFrameCutShort_ThrowsFormatException_MessageFraming(byte[] body)
    {
        FormatException thrown = Assert.ThrowsExactly<FormatException>(() => GrpcWebFraming.Parse(body));
        StringAssert.Contains(thrown.Message, "ends inside a frame");
    }

    [TestMethod]
    public void Parse_AFrameAfterTheTrailers_ThrowsFormatException_TrailersMustBeTheLastMessage()
    {
        byte[] body = [0x80, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0x41];

        FormatException thrown = Assert.ThrowsExactly<FormatException>(() => GrpcWebFraming.Parse(body));
        StringAssert.Contains(thrown.Message, "after its trailers");
    }

    [TestMethod]
    public void Parse_ACompressedFrame_ThrowsNotSupportedException_CompressedFlag()
    {
        Assert.ThrowsExactly<NotSupportedException>(() => GrpcWebFraming.Parse(new byte[] { 1, 0, 0, 0, 1, 0x41 }));
    }
}
