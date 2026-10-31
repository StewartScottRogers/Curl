using Curl.Tls;

namespace Curl.Extension.Coap.Dtls.Wire;

[TestClass]
public sealed class DtlsRecordHeaderTests
{
    private static readonly byte[] HandshakeRecordHeader =
        [0x16, 0xFE, 0xFD, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x10];


    [TestMethod]
    public void Encode_Rfc6347Section4_1_WritesTypeVersionEpochSequenceAndLength()
    {
        DtlsRecordHeader header = new(TlsContentType.Handshake, DtlsRecordHeader.Dtls12Version, 1, 5, 16);

        CollectionAssert.AreEqual(HandshakeRecordHeader, header.Encode());
    }


    [TestMethod]
    public void Decode_Rfc6347Section4_1_ReadsTheHeaderBeforeItsFragment()
    {
        byte[] record = [.. HandshakeRecordHeader, .. new byte[16]];

        Assert.AreEqual(new DtlsRecordHeader(TlsContentType.Handshake, DtlsRecordHeader.Dtls12Version, 1, 5, 16), DtlsRecordHeader.Decode(record));
    }


    [TestMethod]
    public void Decode_Rfc6347Section4_1_SplitsTheLargestEpochFromTheLargestSequenceNumber()
    {
        DtlsRecordHeader header = new(TlsContentType.ApplicationData, DtlsRecordHeader.Dtls12Version, ushort.MaxValue, DtlsRecordHeader.MaximumSequenceNumber, 0);

        Assert.AreEqual(header, DtlsRecordHeader.Decode(header.Encode()));
        Assert.AreEqual(ulong.MaxValue, header.EpochAndSequenceNumber);
    }


    [TestMethod]
    public void AdditionalData_Rfc6347Section4_1_2_1_IsEpochSequenceTypeVersionAndPlaintextLength()
    {
        DtlsRecordHeader header = new(TlsContentType.Handshake, DtlsRecordHeader.Dtls12Version, 1, 5, 0x30);

        byte[] expected = [0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x16, 0xFE, 0xFD, 0x00, 0x20];
        CollectionAssert.AreEqual(expected, header.AdditionalData(0x20));
    }


    [TestMethod]
    public void Encode_Rfc6347Section4_1_RejectsASequenceNumberPast48Bits()
    {
        DtlsRecordHeader header = new(TlsContentType.Handshake, DtlsRecordHeader.Dtls12Version, 0, DtlsRecordHeader.MaximumSequenceNumber + 1, 0);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => header.Encode());
    }


    [TestMethod]
    public void Encode_Rfc6347Section4_1_RejectsALengthPastTheMaximum()
    {
        DtlsRecordHeader header = new(TlsContentType.Handshake, DtlsRecordHeader.Dtls12Version, 0, 0, DtlsRecordHeader.MaximumLength + 1);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => header.Encode());
    }


    [TestMethod]
    public void Decode_Rfc6347Section4_1_RejectsARecordShorterThanItsHeader()
    {
        Assert.ThrowsExactly<FormatException>(() => DtlsRecordHeader.Decode(HandshakeRecordHeader.AsSpan(0, 12)));
    }


    [TestMethod]
    public void Decode_Rfc6347Section4_1_RejectsALengthPastTheRecord()
    {
        Assert.ThrowsExactly<FormatException>(() => DtlsRecordHeader.Decode([.. HandshakeRecordHeader, .. new byte[15]]));
    }


    [TestMethod]
    public void Decode_Rfc6347Section4_1_RejectsALengthPastTheMaximum()
    {
        byte[] record = new byte[DtlsRecordHeader.HeaderLength + DtlsRecordHeader.MaximumLength + 1];
        record[11] = (DtlsRecordHeader.MaximumLength + 1) >> 8;
        record[12] = (DtlsRecordHeader.MaximumLength + 1) & 0xFF;

        Assert.ThrowsExactly<FormatException>(() => DtlsRecordHeader.Decode(record));
    }
}
