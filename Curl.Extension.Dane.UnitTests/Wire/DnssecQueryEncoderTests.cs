namespace Curl.Extension.Dane.Wire;

/// <summary>
/// Checks <see cref="DnssecQueryEncoder" /> against RFC 1035 section 4.1, RFC 6891 section 6.1.2 (the OPT record) and
/// RFC 4035 sections 3.2.1 (the DO bit) and 3.2.2 (the CD bit) with recorded bytes.
/// </summary>
[TestClass]
public sealed class DnssecQueryEncoderTests
{
    // RFC 6891 section 6.1.2: root name, TYPE 41, CLASS 1232 (UDP payload size), TTL 0x00008000 (extended RCODE 0,
    // version 0, DO set per RFC 4035 section 3.2.1), RDLENGTH 0.
    private static readonly byte[] OptRecordWithDnssecOk = [0x00, 0x00, 0x29, 0x04, 0xD0, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00];

    [TestMethod]
    public void Encode_Rfc6891Section6_1_2AndRfc4035Section3_2_1_WritesTheQuestionThenAnOptRecordWithTheDoBit()
    {
        byte[] query = DnssecQueryEncoder.Encode(0xBEEF, "_443._tcp.example.com", DnssecRecordType.Tlsa, checkingDisabled: false);

        CollectionAssert.AreEqual(
            (byte[])
            [
                0xBE, 0xEF, 0x01, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01,
                0x04, (byte)'_', (byte)'4', (byte)'4', (byte)'3',
                0x04, (byte)'_', (byte)'t', (byte)'c', (byte)'p',
                0x07, (byte)'e', (byte)'x', (byte)'a', (byte)'m', (byte)'p', (byte)'l', (byte)'e',
                0x03, (byte)'c', (byte)'o', (byte)'m', 0x00,
                0x00, 0x34, 0x00, 0x01,
                .. OptRecordWithDnssecOk,
            ],
            query);
    }

    [TestMethod]
    public void Encode_Rfc4035Section3_2_2_CheckingDisabledSetsTheCdBesideRecursionDesired()
    {
        byte[] query = DnssecQueryEncoder.Encode(1, "example.com.", DnssecRecordType.Dnskey, checkingDisabled: true);

        CollectionAssert.AreEqual((byte[])[0x01, 0x10], query[2..4]);
        CollectionAssert.AreEqual((byte[])[0x00, 0x30, 0x00, 0x01, .. OptRecordWithDnssecOk], query[^15..]);
    }

    [TestMethod]
    public void Encode_Rfc1035Section3_1_TheRootIsTheZeroOctetAlone()
    {
        byte[] query = DnssecQueryEncoder.Encode(7, ".", DnssecRecordType.Dnskey, checkingDisabled: false);

        CollectionAssert.AreEqual((byte[])[0x00, 0x00, 0x30, 0x00, 0x01, .. OptRecordWithDnssecOk], query[12..]);
    }

    [TestMethod]
    [DataRow("a..example")]
    [DataRow("..")]
    [DataRow("a234567890123456789012345678901234567890123456789012345678901234.example")]
    public void Encode_Rfc1035Section2_3_4_ALabelThatIsEmptyOrOver63OctetsIsRefused(string name) =>
        Assert.ThrowsExactly<ArgumentException>(() => DnssecQueryEncoder.Encode(1, name, DnssecRecordType.Tlsa, false));

    [TestMethod]
    public void Encode_Rfc1035Section2_3_4_ANameOver255OctetsIsRefused()
    {
        string name = string.Join('.', Enumerable.Repeat(new string('a', 63), 4));

        Assert.ThrowsExactly<ArgumentException>(() => DnssecQueryEncoder.Encode(1, name, DnssecRecordType.Tlsa, false));
    }

    [TestMethod]
    public void Encode_AnEmptyNameIsRefused() =>
        Assert.ThrowsExactly<ArgumentException>(() => DnssecQueryEncoder.Encode(1, string.Empty, DnssecRecordType.Tlsa, false));
}
