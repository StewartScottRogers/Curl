using System.Net;

namespace Curl.Extension.EncryptedDns.Wire;

/// <summary>
/// Checks <see cref="DnsQueryEncoder" /> and <see cref="DnsAnswerDecoder" /> against RFC 1035 section 4.1 with
/// recorded bytes: the header (4.1.1), the question (4.1.2), resource records (4.1.3) and message compression (4.1.4).
/// </summary>
[TestClass]
public sealed class DnsWireTests
{
    // RFC 1035 section 4.1.2: example.com as labels, then QTYPE A and QCLASS IN.
    private static readonly byte[] ExampleComQuestionA =
    [
        0x07, (byte)'e', (byte)'x', (byte)'a', (byte)'m', (byte)'p', (byte)'l', (byte)'e',
        0x03, (byte)'c', (byte)'o', (byte)'m', 0x00,
        0x00, 0x01, 0x00, 0x01,
    ];

    [TestMethod]
    public void Encode_Rfc1035Section4_1_1And4_1_2_WritesTheHeaderWithRecursionDesiredAndOneQuestion()
    {
        byte[] query = DnsQueryEncoder.Encode(0x1234, "example.com", DnsRecordType.A);

        CollectionAssert.AreEqual(
            (byte[])[0x12, 0x34, 0x01, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, .. ExampleComQuestionA],
            query);
    }

    [TestMethod]
    public void Encode_Rfc3596Section2_1_AaaaQueryHasQtype28AndATrailingDotIsTheSameName()
    {
        byte[] query = DnsQueryEncoder.Encode(2, "example.com.", DnsRecordType.Aaaa);

        CollectionAssert.AreEqual((byte[])[0x00, 0x1C, 0x00, 0x01], query[^4..]);
        CollectionAssert.AreEqual(ExampleComQuestionA[..^4], query[12..^4]);
    }

    [TestMethod]
    [DataRow("a..example")]
    [DataRow(".")]
    [DataRow("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.example")]
    public void Encode_Rfc1035Section2_3_4_LabelEmptyOrOver63Octets_IsRefused(string host) =>
        Assert.ThrowsExactly<ArgumentException>(() => DnsQueryEncoder.Encode(1, host, DnsRecordType.A));

    [TestMethod]
    public void Encode_Rfc1035Section2_3_4_NameOver255Octets_IsRefused()
    {
        string label = new('a', 63);

        Assert.ThrowsExactly<ArgumentException>(() => DnsQueryEncoder.Encode(1, $"{label}.{label}.{label}.{label}", DnsRecordType.A));
    }

    [TestMethod]
    public void Decode_Rfc1035Section4_1_4_AnswerNamedByACompressionPointer_GivesItsAddress()
    {
        byte[] answer =
        [
            0x00, 0x01, 0x81, 0x80, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, .. ExampleComQuestionA,
            0xC0, 0x0C, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x0E, 0x10, 0x00, 0x04, 93, 184, 215, 14,
        ];

        DnsAnswer decoded = DnsAnswerDecoder.Decode(answer, 1, DnsRecordType.A);

        Assert.AreEqual(DnsAnswerFailure.None, decoded.Failure);
        CollectionAssert.AreEqual(new[] { IPAddress.Parse("93.184.215.14") }, decoded.Addresses.ToArray());
    }

    [TestMethod]
    public void Decode_Rfc1035Section4_1_3_KeepsOnlyRecordsOfTheTypeAskedForInClassIn()
    {
        byte[] answer =
        [
            0x00, 0x02, 0x81, 0x80, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00,

            // A CNAME (type 5) with an uncompressed owner name, then AAAA records: one in class CH, one with
            // four octets of data, one well formed.
            0x01, (byte)'w', 0x00, 0x00, 0x05, 0x00, 0x01, 0x00, 0x00, 0x00, 0x3C, 0x00, 0x02, 0xC0, 0x0C,
            0x01, (byte)'w', 0x00, 0x00, 0x1C, 0x00, 0x03, 0x00, 0x00, 0x00, 0x3C, 0x00, 0x10, .. new byte[16],
            0x01, (byte)'w', 0x00, 0x00, 0x1C, 0x00, 0x01, 0x00, 0x00, 0x00, 0x3C, 0x00, 0x04, 1, 2, 3, 4,
            0x01, (byte)'w', 0x00, 0x00, 0x1C, 0x00, 0x01, 0x00, 0x00, 0x00, 0x3C, 0x00, 0x10,
            0x20, 0x01, 0x0D, 0xB8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x01,
        ];

        DnsAnswer decoded = DnsAnswerDecoder.Decode(answer, 2, DnsRecordType.Aaaa);

        CollectionAssert.AreEqual(new[] { IPAddress.Parse("2001:db8::1") }, decoded.Addresses.ToArray());
    }

    [TestMethod]
    public void Decode_Rfc1035Section4_1_1_NonZeroRcode_IsAResponseCodeFailure()
    {
        DnsAnswer decoded = DnsAnswerDecoder.Decode(Header(1, 0x8183), 1, DnsRecordType.A);

        Assert.AreEqual(DnsAnswerFailure.ResponseCode, decoded.Failure);
        Assert.AreEqual(3, decoded.ResponseCode);
        Assert.AreEqual(0, decoded.Addresses.Count);
    }

    [TestMethod]
    public void Decode_Rfc1035Section4_1_1_TruncationBit_IsATruncatedFailure() =>
        Assert.AreEqual(DnsAnswerFailure.Truncated, DnsAnswerDecoder.Decode(Header(1, 0x8380), 1, DnsRecordType.A).Failure);

    [TestMethod]
    [DataRow((ushort)2, (ushort)0x8180)]
    [DataRow((ushort)1, (ushort)0x0180)]
    public void Decode_Rfc1035Section4_1_1_OtherIdOrQrBitClear_IsNotTheAnswer(ushort id, ushort flags) =>
        Assert.AreEqual(DnsAnswerFailure.NotTheAnswer, DnsAnswerDecoder.Decode(Header(id, flags), 1, DnsRecordType.A).Failure);

    [TestMethod]
    public void Decode_HeaderWithNoRecords_DecodesToNoAddresses()
    {
        DnsAnswer decoded = DnsAnswerDecoder.Decode(Header(1, 0x8180), 1, DnsRecordType.A);

        Assert.AreEqual(DnsAnswerFailure.None, decoded.Failure);
        Assert.AreEqual(0, decoded.Addresses.Count);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x00, 0x01, 0x81 })]
    [DataRow(new byte[] { 0x00, 0x01, 0x81, 0x80, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, (byte)'c' })]
    [DataRow(new byte[] { 0x00, 0x01, 0x81, 0x80, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x01, 0x00, 0x01 })]
    public void Decode_Rfc1035Section4_1_MessageCutShortOrUndefinedLabelType_IsMalformed(byte[] message) =>
        Assert.AreEqual(DnsAnswerFailure.Malformed, DnsAnswerDecoder.Decode(message, 1, DnsRecordType.A).Failure);

    private static byte[] Header(ushort id, ushort flags) =>
        [(byte)(id >> 8), (byte)id, (byte)(flags >> 8), (byte)flags, 0, 0, 0, 0, 0, 0, 0, 0];
}
