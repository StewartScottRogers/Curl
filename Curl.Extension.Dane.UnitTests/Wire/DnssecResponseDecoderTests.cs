namespace Curl.Extension.Dane.Wire;

/// <summary>
/// Checks <see cref="DnssecResponseDecoder" /> with recorded bytes: the header (RFC 1035 section 4.1.1, RFC 4035
/// section 3.2), compression (RFC 1035 section 4.1.4), and the RDATA of CNAME, TLSA, DNSKEY, RRSIG, DS, NSEC and NSEC3
/// as their RFCs lay it out; and that a malformed or truncated message gives false, never an exception.
/// </summary>
[TestClass]
public sealed class DnssecResponseDecoderTests
{
    // "example.com." at offset 12, the question's name, which later names point to with 0xC0 0x0C.
    private static readonly byte[] ExampleCom =
    [
        0x07, (byte)'e', (byte)'x', (byte)'a', (byte)'m', (byte)'p', (byte)'l', (byte)'e', 0x03, (byte)'c', (byte)'o', (byte)'m', 0x00,
    ];

    private static readonly byte[] PointerToExampleCom = [0xC0, 0x0C];

    // RFC 6698 section 2.3: _443._tcp.www.example.com. IN TLSA 0 0 1 d2abde240d7cd3ee6b4b28c54df034b97983a1d16e8a410e4561cb106618e971
    private static readonly byte[] TlsaRdata =
    [
        0x00, 0x00, 0x01,
        0xD2, 0xAB, 0xDE, 0x24, 0x0D, 0x7C, 0xD3, 0xEE, 0x6B, 0x4B, 0x28, 0xC5, 0x4D, 0xF0, 0x34, 0xB9,
        0x79, 0x83, 0xA1, 0xD1, 0x6E, 0x8A, 0x41, 0x0E, 0x45, 0x61, 0xCB, 0x10, 0x66, 0x18, 0xE9, 0x71,
    ];

    // RFC 4034 section 2.3: flags 256, protocol 3, algorithm 5, the key's first octets AQPSKmyn.
    private static readonly byte[] DnskeyRdata = [0x01, 0x00, 0x03, 0x05, 0x01, 0x03, 0xD2, 0x2A, 0x6C, 0xA7];

    // RFC 4034 section 3.3: A 5 3 86400 20030322173103 20030220173103 2642 example.com. and the signature's first octets.
    private static readonly byte[] RrsigRdata =
    [
        0x00, 0x01, 0x05, 0x03, 0x00, 0x01, 0x51, 0x80, 0x3E, 0x7C, 0x9D, 0xD7, 0x3E, 0x55, 0x10, 0xD7, 0x0A, 0x52,
        .. ExampleCom,
        0x2B, 0xB1, 0x83, 0xAF,
    ];

    // RFC 4034 section 5.3: 60485 5 1 2BB183AF5F22588179A53B0A98631FAD1A292118.
    private static readonly byte[] DsRdata =
    [
        0xEC, 0x45, 0x05, 0x01,
        0x2B, 0xB1, 0x83, 0xAF, 0x5F, 0x22, 0x58, 0x81, 0x79, 0xA5, 0x3B, 0x0A, 0x98, 0x63, 0x1F, 0xAD, 0x1A, 0x29, 0x21, 0x18,
    ];

    // RFC 4034 section 4.3: host.example.com. A MX RRSIG NSEC TYPE1234.
    private static readonly byte[] NsecRdata =
    [
        0x04, (byte)'h', (byte)'o', (byte)'s', (byte)'t', .. ExampleCom,
        0x00, 0x06, 0x40, 0x01, 0x00, 0x00, 0x00, 0x03,
        0x04, 0x1B, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x20,
    ];

    // RFC 5155 appendix A: 1 1 12 aabbccdd, a 20-octet next hashed owner name, NS SOA MX RRSIG DNSKEY NSEC3PARAM.
    private static readonly byte[] Nsec3Rdata =
    [
        0x01, 0x01, 0x00, 0x0C, 0x04, 0xAA, 0xBB, 0xCC, 0xDD,
        0x14, 0x17, 0x4E, 0xB2, 0x40, 0x9F, 0xE2, 0x8B, 0xCB, 0x48, 0x87, 0xA1, 0x83, 0x6F, 0x95, 0x7F, 0x0A, 0x84, 0x25, 0xE2, 0x7B,
        0x00, 0x07, 0x22, 0x01, 0x00, 0x00, 0x00, 0x02, 0x90,
    ];

    [TestMethod]
    public void TryDecode_Rfc1035Section4_1_1AndRfc4035Section3_2_ReadsTheIdTcAdCdAndRcode()
    {
        Assert.IsTrue(DnssecResponseDecoder.TryDecode(Response(0x83B3, [], []), out DnssecResponse? response));

        Assert.AreEqual(0x1234, response.Id);
        Assert.IsTrue(response.IsTruncated);
        Assert.IsTrue(response.IsAuthenticData);
        Assert.IsTrue(response.IsCheckingDisabled);
        Assert.AreEqual(3, response.ResponseCode);
        Assert.IsEmpty(response.Answers);
        Assert.IsEmpty(response.Authority);
    }

    [TestMethod]
    public void TryDecode_Rfc1035Section4_1_1_ClearFlagsReadAsClear()
    {
        Assert.IsTrue(DnssecResponseDecoder.TryDecode(Response(0x8180, [], []), out DnssecResponse? response));

        Assert.IsFalse(response.IsTruncated);
        Assert.IsFalse(response.IsAuthenticData);
        Assert.IsFalse(response.IsCheckingDisabled);
        Assert.AreEqual(0, response.ResponseCode);
    }

    [TestMethod]
    public void TryDecode_Rfc1035Section4_1_1_AQueryWithQrClearIsNotAResponse()
    {
        Assert.IsFalse(DnssecResponseDecoder.TryDecode(Response(0x0100, [], []), out DnssecResponse? response));
        Assert.IsNull(response);
    }

    [TestMethod]
    public void TryDecode_Rfc1035Section3_3_1And4_1_4_CnameTargetFollowsAChainOfPointers()
    {
        // The answer's owner points at the question's name; the target is "www" then a pointer to that owner pointer.
        byte[] cname = [0x03, (byte)'w', (byte)'w', (byte)'w', 0xC0, 0x1D];
        byte[] message = Response(0x8180, [Record(PointerToExampleCom, DnssecRecordType.Cname, cname)], []);

        Assert.IsTrue(DnssecResponseDecoder.TryDecode(message, out DnssecResponse? response));

        DnsResourceRecord record = response.Answers.Single();
        Assert.AreEqual("example.com.", record.Owner.ToString());
        Assert.AreEqual(2, record.Owner.LabelCount);
        Assert.AreEqual(DnssecRecordType.Cname, record.Type);
        Assert.AreEqual(1, record.Class);
        Assert.AreEqual(3600u, record.TimeToLive);
        CollectionAssert.AreEqual(cname, record.RawData);
        DnsName target = ((CnameRecordData)record.Data!).CanonicalName;
        Assert.AreEqual("www.example.com.", target.ToString());
        CollectionAssert.AreEqual((byte[])[0x03, (byte)'w', (byte)'w', (byte)'w', .. ExampleCom], target.Wire.ToArray());
    }

    [TestMethod]
    public void TryDecode_Rfc6698Section2_1_ReadsTheTlsaFields()
    {
        TlsaRecordData tlsa = (TlsaRecordData)SingleAnswer(DnssecRecordType.Tlsa, TlsaRdata).Data!;

        Assert.AreEqual(0, tlsa.CertificateUsage);
        Assert.AreEqual(0, tlsa.Selector);
        Assert.AreEqual(1, tlsa.MatchingType);
        CollectionAssert.AreEqual(TlsaRdata[3..], tlsa.CertificateAssociationData);
    }

    [TestMethod]
    public void TryDecode_Rfc4034Section2_1_ReadsTheDnskeyFields()
    {
        DnskeyRecordData dnskey = (DnskeyRecordData)SingleAnswer(DnssecRecordType.Dnskey, DnskeyRdata).Data!;

        Assert.AreEqual(256, dnskey.Flags);
        Assert.AreEqual(3, dnskey.Protocol);
        Assert.AreEqual(5, dnskey.Algorithm);
        CollectionAssert.AreEqual(DnskeyRdata[4..], dnskey.PublicKey);
    }

    [TestMethod]
    public void TryDecode_Rfc4034Section3_1_ReadsTheRrsigFieldsAndAnUncompressedSignerName()
    {
        DnsResourceRecord record = SingleAnswer(DnssecRecordType.Rrsig, RrsigRdata);
        RrsigRecordData rrsig = (RrsigRecordData)record.Data!;

        Assert.AreEqual(DnssecRecordType.A, rrsig.TypeCovered);
        Assert.AreEqual(5, rrsig.Algorithm);
        Assert.AreEqual(3, rrsig.Labels);
        Assert.AreEqual(86400u, rrsig.OriginalTtl);
        Assert.AreEqual((uint)new DateTimeOffset(2003, 3, 22, 17, 31, 3, TimeSpan.Zero).ToUnixTimeSeconds(), rrsig.SignatureExpiration);
        Assert.AreEqual((uint)new DateTimeOffset(2003, 2, 20, 17, 31, 3, TimeSpan.Zero).ToUnixTimeSeconds(), rrsig.SignatureInception);
        Assert.AreEqual(2642, rrsig.KeyTag);
        Assert.AreEqual("example.com.", rrsig.SignerName.ToString());
        CollectionAssert.AreEqual(RrsigRdata[^4..], rrsig.Signature);
        CollectionAssert.AreEqual(RrsigRdata, record.RawData);
    }

    [TestMethod]
    public void TryDecode_Rfc4034Section5_1_ReadsTheDsFields()
    {
        DsRecordData ds = (DsRecordData)SingleAnswer(DnssecRecordType.Ds, DsRdata).Data!;

        Assert.AreEqual(60485, ds.KeyTag);
        Assert.AreEqual(5, ds.Algorithm);
        Assert.AreEqual(1, ds.DigestType);
        CollectionAssert.AreEqual(DsRdata[4..], ds.Digest);
    }

    [TestMethod]
    public void TryDecode_Rfc4034Section4_1_ReadsTheNsecNextNameAndTypeBitMaps()
    {
        NsecRecordData nsec = (NsecRecordData)SingleAnswer(DnssecRecordType.Nsec, NsecRdata).Data!;

        Assert.AreEqual("host.example.com.", nsec.NextDomainName.ToString());
        CollectionAssert.AreEqual(
            new[] { DnssecRecordType.A, (DnssecRecordType)15, DnssecRecordType.Rrsig, DnssecRecordType.Nsec, (DnssecRecordType)1234 },
            nsec.Types.ToArray());
    }

    [TestMethod]
    public void TryDecode_Rfc5155Section3_2_ReadsTheNsec3Fields()
    {
        Nsec3RecordData nsec3 = (Nsec3RecordData)SingleAnswer(DnssecRecordType.Nsec3, Nsec3Rdata).Data!;

        Assert.AreEqual(1, nsec3.HashAlgorithm);
        Assert.AreEqual(1, nsec3.Flags);
        Assert.AreEqual(12, nsec3.Iterations);
        CollectionAssert.AreEqual((byte[])[0xAA, 0xBB, 0xCC, 0xDD], nsec3.Salt);
        CollectionAssert.AreEqual(Nsec3Rdata[10..30], nsec3.NextHashedOwnerName);
        CollectionAssert.AreEqual(
            new[]
            {
                (DnssecRecordType)2, (DnssecRecordType)6, (DnssecRecordType)15, DnssecRecordType.Rrsig, DnssecRecordType.Dnskey,
                (DnssecRecordType)51,
            },
            nsec3.Types.ToArray());
    }

    [TestMethod]
    public void TryDecode_Rfc1035Section3_4_1_AnUntypedRecordKeepsItsRawDataAndNoFields()
    {
        DnsResourceRecord record = SingleAnswer(DnssecRecordType.A, [192, 0, 2, 1]);

        Assert.IsNull(record.Data);
        CollectionAssert.AreEqual((byte[])[192, 0, 2, 1], record.RawData);
    }

    [TestMethod]
    public void TryDecode_Rfc1035Section4_1_ReadsTheAuthoritySectionAfterTheAnswers()
    {
        byte[] message = Response(
            0x8183,
            [Record(PointerToExampleCom, DnssecRecordType.Rrsig, RrsigRdata)],
            [Record([0x00], DnssecRecordType.Nsec, NsecRdata), Record(PointerToExampleCom, DnssecRecordType.Nsec3, Nsec3Rdata)]);

        Assert.IsTrue(DnssecResponseDecoder.TryDecode(message, out DnssecResponse? response));

        Assert.HasCount(1, response.Answers);
        Assert.AreEqual(".", response.Authority[0].Owner.ToString());
        Assert.AreEqual(0, response.Authority[0].Owner.LabelCount);
        Assert.AreEqual(DnssecRecordType.Nsec3, response.Authority[1].Type);
    }

    [TestMethod]
    public void TryDecode_EveryTruncationOfAWellFormedResponseGivesFalse()
    {
        byte[] message = Response(
            0x8180,
            [
                Record(PointerToExampleCom, DnssecRecordType.Cname, [0x03, (byte)'w', (byte)'w', (byte)'w', 0xC0, 0x0C]),
                Record(PointerToExampleCom, DnssecRecordType.Tlsa, TlsaRdata),
                Record(PointerToExampleCom, DnssecRecordType.Dnskey, DnskeyRdata),
                Record(PointerToExampleCom, DnssecRecordType.Ds, DsRdata),
            ],
            [Record(PointerToExampleCom, DnssecRecordType.Rrsig, RrsigRdata), Record(PointerToExampleCom, DnssecRecordType.Nsec, NsecRdata)]);
        Assert.IsTrue(DnssecResponseDecoder.TryDecode(message, out _));

        for (int length = 0; length < message.Length; length++)
        {
            Assert.IsFalse(DnssecResponseDecoder.TryDecode(message.AsSpan(0, length), out _), $"Cut at {length} octets.");
        }
    }

    [TestMethod]
    public void TryDecode_EveryShortenedRdataGivesFalse()
    {
        foreach ((DnssecRecordType type, byte[] rdata) in new[]
        {
            (DnssecRecordType.Tlsa, TlsaRdata[..2]),
            (DnssecRecordType.Dnskey, DnskeyRdata[..3]),
            (DnssecRecordType.Ds, DsRdata[..3]),
            (DnssecRecordType.Rrsig, RrsigRdata[..20]),
            (DnssecRecordType.Nsec, NsecRdata[..20]),
            (DnssecRecordType.Nsec3, Nsec3Rdata[..12]),
            (DnssecRecordType.Cname, [0x03, (byte)'w', (byte)'w']),
        })
        {
            Assert.IsFalse(DnssecResponseDecoder.TryDecode(Response(0x8180, [Record(PointerToExampleCom, type, rdata)], []), out _), type.ToString());
        }
    }

    [TestMethod]
    [DataRow(new byte[] { 0x40, 0x00 }, DisplayName = "RFC 1035 section 4.1.4: label type 01 is not defined")]
    [DataRow(new byte[] { 0x80, 0x00 }, DisplayName = "RFC 1035 section 4.1.4: label type 10 is not defined")]
    [DataRow(new byte[] { 0xC0, 0x30 }, DisplayName = "A compression pointer that points forwards")]
    [DataRow(new byte[] { 0xC0, 0x1D }, DisplayName = "A compression pointer that points at itself")]
    public void TryDecode_Rfc1035Section4_1_4_AnOwnerNameRfc1035DoesNotAllowGivesFalse(byte[] owner) =>
        Assert.IsFalse(DnssecResponseDecoder.TryDecode(Response(0x8180, [Record(owner, DnssecRecordType.A, [192, 0, 2, 1])], []), out _));

    [TestMethod]
    public void TryDecode_Rfc1035Section2_3_4_ANameOver255OctetsGivesFalse()
    {
        byte[] label = [63, .. Enumerable.Repeat((byte)'a', 63)];
        byte[] owner = [.. label, .. label, .. label, .. label, 0x00];

        Assert.IsFalse(DnssecResponseDecoder.TryDecode(Response(0x8180, [Record(owner, DnssecRecordType.A, [192, 0, 2, 1])], []), out _));
    }

    [TestMethod]
    public void TryDecode_Rfc4034Section3_1_7_ACompressedSignerNameGivesFalse()
    {
        byte[] rrsig = [.. RrsigRdata[..18], .. PointerToExampleCom, 0x2B];

        Assert.IsFalse(DnssecResponseDecoder.TryDecode(Response(0x8180, [Record(PointerToExampleCom, DnssecRecordType.Rrsig, rrsig)], []), out _));
    }

    [TestMethod]
    [DataRow(new byte[] { 0x00, 0x00 }, DisplayName = "A bitmap length of 0")]
    [DataRow(new byte[] { 0x00, 0x21 }, DisplayName = "A bitmap length of 33")]
    public void TryDecode_Rfc4034Section4_1_2_ATypeBitMapLengthOutside1To32GivesFalse(byte[] window)
    {
        byte[] nsec = [0x00, .. window, .. new byte[33]];

        Assert.IsFalse(DnssecResponseDecoder.TryDecode(Response(0x8180, [Record(PointerToExampleCom, DnssecRecordType.Nsec, nsec)], []), out _));
    }

    [TestMethod]
    public void TryDecode_Rfc1035Section3_3_1_OctetsAfterTheCnameTargetGiveFalse() =>
        Assert.IsFalse(DnssecResponseDecoder.TryDecode(
            Response(0x8180, [Record(PointerToExampleCom, DnssecRecordType.Cname, [.. PointerToExampleCom, 0x00])], []),
            out _));

    private static DnsResourceRecord SingleAnswer(DnssecRecordType type, byte[] rdata)
    {
        Assert.IsTrue(DnssecResponseDecoder.TryDecode(Response(0x8180, [Record(PointerToExampleCom, type, rdata)], []), out DnssecResponse? response));
        DnsResourceRecord record = response.Answers.Single();
        Assert.AreEqual(type, record.Type);
        return record;
    }

    // A response with ID 0x1234, the given flags, one question (example.com. IN TLSA) and the given sections. The first
    // answer's owner starts at offset 29.
    private static byte[] Response(ushort flags, byte[][] answers, byte[][] authority) =>
    [
        0x12, 0x34, (byte)(flags >> 8), (byte)flags, 0x00, 0x01, 0x00, (byte)answers.Length, 0x00, (byte)authority.Length, 0x00, 0x00,
        .. ExampleCom, 0x00, 0x34, 0x00, 0x01,
        .. answers.SelectMany(record => record),
        .. authority.SelectMany(record => record),
    ];

    // A resource record of class IN and TTL 3600.
    private static byte[] Record(byte[] owner, DnssecRecordType type, byte[] rdata) =>
    [
        .. owner, (byte)((ushort)type >> 8), (byte)type, 0x00, 0x01, 0x00, 0x00, 0x0E, 0x10, (byte)(rdata.Length >> 8), (byte)rdata.Length, .. rdata,
    ];
}
