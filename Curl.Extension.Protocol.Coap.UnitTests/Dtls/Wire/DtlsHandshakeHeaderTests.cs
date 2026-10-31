using Curl.Tls;

namespace Curl.Extension.Protocol.Coap.Dtls.Wire;

[TestClass]
public sealed class DtlsHandshakeHeaderTests
{
    private static readonly byte[] ClientHelloFragmentHeader =
        [0x01, 0x01, 0x02, 0x03, 0x00, 0x02, 0x00, 0x00, 0x10, 0x00, 0x00, 0x20];


    [TestMethod]
    public void Encode_Rfc6347Section4_2_2_WritesTypeLengthMessageSeqOffsetAndFragmentLength()
    {
        DtlsHandshakeHeader header = new(HandshakeType.ClientHello, 0x010203, 2, 0x10, 0x20);

        CollectionAssert.AreEqual(ClientHelloFragmentHeader, header.Encode());
    }


    [TestMethod]
    public void Decode_Rfc6347Section4_2_2_ReadsTheTwelveByteHeader()
    {
        Assert.AreEqual(new DtlsHandshakeHeader(HandshakeType.ClientHello, 0x010203, 2, 0x10, 0x20), DtlsHandshakeHeader.Decode(ClientHelloFragmentHeader));
    }


    [TestMethod]
    [DataRow(-1, 0, 0, DisplayName = "negative length")]
    [DataRow(DtlsHandshakeHeader.MaximumUInt24 + 1, 0, 0, DisplayName = "length past 24 bits")]
    [DataRow(10, -1, 0, DisplayName = "negative offset")]
    [DataRow(10, DtlsHandshakeHeader.MaximumUInt24 + 1, 0, DisplayName = "offset past 24 bits")]
    [DataRow(10, 0, -1, DisplayName = "negative fragment length")]
    [DataRow(10, 0, DtlsHandshakeHeader.MaximumUInt24 + 1, DisplayName = "fragment length past 24 bits")]
    [DataRow(10, 5, 6, DisplayName = "fragment past the message")]
    public void Encode_Rfc6347Section4_2_2_RejectsFieldsOutOfRange(int length, int offset, int fragmentLength)
    {
        DtlsHandshakeHeader header = new(HandshakeType.Finished, length, 0, offset, fragmentLength);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => header.Encode());
    }


    [TestMethod]
    public void Decode_Rfc6347Section4_2_2_RejectsFewerThanTwelveBytes()
    {
        Assert.ThrowsExactly<FormatException>(() => DtlsHandshakeHeader.Decode(ClientHelloFragmentHeader.AsSpan(0, 11)));
    }


    [TestMethod]
    public void Decode_Rfc6347Section4_2_2_RejectsAFragmentPastItsMessage()
    {
        byte[] header = [0x01, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x09];

        Assert.ThrowsExactly<FormatException>(() => DtlsHandshakeHeader.Decode(header));
    }
}
