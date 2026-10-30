namespace Curl.HttpDatagram.UnitTests;

[TestClass]
public sealed class Http3DatagramTests
{
    [TestMethod]
    [DataRow(0UL, new byte[] { 0x00, 0xAB })]
    [DataRow(4UL, new byte[] { 0x01, 0xAB })]
    [DataRow(256UL, new byte[] { 0x40, 0x40, 0xAB })]
    public void Encode_ClientBidirectionalStream_PrefixesQuarterStreamId(ulong streamId, byte[] expected)
    {
        var bytes = Http3Datagram.Encode(streamId, [0xAB]);

        CollectionAssert.AreEqual(expected, bytes);
    }

    [TestMethod]
    [DataRow(1UL)]
    [DataRow(2UL)]
    [DataRow(3UL)]
    [DataRow(1UL << 62)]
    public void Encode_NotAClientBidirectionalStream_Throws(ulong streamId)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Http3Datagram.Encode(streamId, []));
    }

    [TestMethod]
    public void Decode_PrefixedDatagram_ReturnsStreamIdAndPayload()
    {
        byte[] datagram = [0x40, 0x40, 0x01, 0x02];

        var payload = Http3Datagram.Decode(datagram, out var streamId);

        Assert.AreEqual(256UL, streamId);
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x02 }, payload.ToArray());
    }

    [TestMethod]
    public void Decode_LargestQuarterStreamId_ReturnsLargestStreamId()
    {
        var datagram = Http3Datagram.Encode(Http3Datagram.MaximumQuarterStreamId * 4, []);

        var payload = Http3Datagram.Decode(datagram, out var streamId);

        Assert.AreEqual(Http3Datagram.MaximumQuarterStreamId * 4, streamId);
        Assert.IsTrue(payload.IsEmpty);
    }

    [TestMethod]
    [DataRow(new byte[0])]
    [DataRow(new byte[] { 0x40 })]
    [DataRow(new byte[] { 0xD0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 })]
    public void Decode_MalformedQuarterStreamId_ThrowsDatagramError(byte[] datagram)
    {
        var exception = Assert.ThrowsExactly<HttpDatagramException>(() => Http3Datagram.Decode(datagram, out _));

        Assert.AreEqual(HttpDatagramErrorCode.DatagramError, exception.ErrorCode);
    }
}
