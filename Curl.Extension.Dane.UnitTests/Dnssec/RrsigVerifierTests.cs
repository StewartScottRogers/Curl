using Curl.Extension.Dane.Wire;
using static Curl.Extension.Dane.Dnssec.DnssecTestRecords;

namespace Curl.Extension.Dane.Dnssec;

/// <summary>
/// Checks <see cref="RrsigVerifier" /> against RFC 4035 section 5.3 with recorded real-world RRsets (algorithms 8 and
/// 13) and generated keys (algorithms 10, 14, 15 and 16), each under a fixed clock.
/// </summary>
[TestClass]
public sealed class RrsigVerifierTests
{
    private static readonly RrsigVerifier AtRecordedTime = new(FixedTime(RecordedDnssecResponses.ValidAt));

    private static readonly RrsigVerifier AtGeneratedTime = new(FixedTime(GeneratedValidAt));

    [TestMethod]
    public void Verify_Rfc5702Section3RootDnskeyRrsetSignedByKsk38696Algorithm8_IsSecure()
    {
        DnssecResponse root = Decode(RecordedDnssecResponses.RootDnskey);
        List<DnsResourceRecord> dnskeys = Answers(root, DnssecRecordType.Dnskey);

        DnssecValidationResult result = AtRecordedTime.Verify(dnskeys, Rrsig(root), dnskeys);

        Assert.AreEqual(DnssecValidationResult.Secure, result);
    }

    [TestMethod]
    public void Verify_Rfc5702Section3TldDsRrsetSignedByRootZskAlgorithm8_IsSecure()
    {
        DnssecResponse se = Decode(RecordedDnssecResponses.SeDs);
        List<DnsResourceRecord> rootKeys = Answers(Decode(RecordedDnssecResponses.RootDnskey), DnssecRecordType.Dnskey);

        DnssecValidationResult result = AtRecordedTime.Verify(Answers(se, DnssecRecordType.Ds), Rrsig(se), rootKeys);

        Assert.AreEqual(DnssecValidationResult.Secure, result);
    }

    [TestMethod]
    public void Verify_Rfc6605Section4TlsaRrsetAlgorithm13InReverseOrder_IsSecureInCanonicalOrder()
    {
        DnssecResponse tlsa = Decode(RecordedDnssecResponses.HuqueTlsa);
        List<DnsResourceRecord> rrset = Answers(tlsa, DnssecRecordType.Tlsa);
        rrset.Reverse();
        List<DnsResourceRecord> keys = Answers(Decode(RecordedDnssecResponses.HuqueDnskey), DnssecRecordType.Dnskey);

        DnssecValidationResult result = AtRecordedTime.Verify(rrset, Rrsig(tlsa), keys);

        Assert.AreEqual(DnssecValidationResult.Secure, result);
    }

    [TestMethod]
    public void Verify_Rfc4034Section3_1_8_1TamperedTlsaRecord_IsBogus()
    {
        DnssecResponse tlsa = Decode(RecordedDnssecResponses.HuqueTlsa);
        List<DnsResourceRecord> rrset = Answers(tlsa, DnssecRecordType.Tlsa);
        byte[] tampered = [.. rrset[0].RawData];
        tampered[^1] ^= 1;
        rrset[0] = rrset[0] with { RawData = tampered };
        List<DnsResourceRecord> keys = Answers(Decode(RecordedDnssecResponses.HuqueDnskey), DnssecRecordType.Dnskey);

        DnssecValidationResult result = AtRecordedTime.Verify(rrset, Rrsig(tlsa), keys);

        Assert.AreEqual(DnssecValidationResult.Bogus, result);
    }

    [TestMethod]
    public void Verify_Rfc5702Section3GeneratedRsaSha512Algorithm10_IsSecure() =>
        AssertGeneratedKeyVerifies(RsaSha512());

    [TestMethod]
    public void Verify_Rfc6605Section4GeneratedEcdsaP384Algorithm14_IsSecure() =>
        AssertGeneratedKeyVerifies(EcdsaP384());

    [TestMethod]
    public void Verify_Rfc8080Section4GeneratedEd25519Algorithm15_IsSecure() =>
        AssertGeneratedKeyVerifies(Ed25519Key());

    [TestMethod]
    public void Verify_Rfc8080Section4GeneratedEd448Algorithm16_IsSecure() =>
        AssertGeneratedKeyVerifies(Ed448Key());

    [TestMethod]
    public void Verify_Rfc4034Section6_2OwnerAndCnameTargetInAnotherCase_IsSecure()
    {
        GeneratedZoneKey zoneKey = Ed25519Key();
        DnsResourceRecord lower = Record("www.example.com.", DnssecRecordType.Cname, [0xC0, 0x0C], new CnameRecordData(Name("host.example.com.")));
        DnsResourceRecord upper = Record("WWW.Example.COM.", DnssecRecordType.Cname, [0xC0, 0x0C], new CnameRecordData(Name("HOST.example.COM.")));
        RrsigRecordData signature = Sign(zoneKey, [lower]);

        DnssecValidationResult result = AtGeneratedTime.Verify([upper], RrsigRecord("www.example.com.", signature), [DnskeyRecord("example.com.", zoneKey.Key)]);

        Assert.AreEqual(DnssecValidationResult.Secure, result);
    }

    [TestMethod]
    public void Verify_Rfc4034Section6_3DuplicateRecord_IsSignedOnce()
    {
        GeneratedZoneKey zoneKey = Ed25519Key();
        List<DnsResourceRecord> rrset = AddressRrset("www.example.com.");
        RrsigRecordData signature = Sign(zoneKey, rrset);

        DnssecValidationResult result = AtGeneratedTime.Verify([.. rrset, rrset[0]], RrsigRecord("www.example.com.", signature), [DnskeyRecord("example.com.", zoneKey.Key)]);

        Assert.AreEqual(DnssecValidationResult.Secure, result);
    }

    [TestMethod]
    public void Verify_Rfc4035Section5_3_2RrsetExpandedFromWildcard_IsSecure()
    {
        GeneratedZoneKey zoneKey = Ed25519Key();
        RrsigRecordData signature = Sign(zoneKey, AddressRrset("x.example.com."), labels: 2);

        DnssecValidationResult result = AtGeneratedTime.Verify(AddressRrset("a.b.example.com."), RrsigRecord("a.b.example.com.", signature), [DnskeyRecord("example.com.", zoneKey.Key)]);

        Assert.AreEqual(DnssecValidationResult.Secure, result);
    }

    [TestMethod]
    public void Verify_Rfc4035Section5_3_1SignatureExpired_IsBogus() =>
        AssertGeneratedSignatureAtTime(DateTimeOffset.FromUnixTimeSeconds(Expiration + 1L), DnssecValidationResult.Bogus);

    [TestMethod]
    public void Verify_Rfc4035Section5_3_1SignatureNotYetValid_IsBogus() =>
        AssertGeneratedSignatureAtTime(DateTimeOffset.FromUnixTimeSeconds(Inception - 1L), DnssecValidationResult.Bogus);

    [TestMethod]
    public void Verify_Rfc4035Section5_3_1AtInceptionAndExpiration_IsSecure()
    {
        AssertGeneratedSignatureAtTime(DateTimeOffset.FromUnixTimeSeconds(Inception), DnssecValidationResult.Secure);
        AssertGeneratedSignatureAtTime(DateTimeOffset.FromUnixTimeSeconds(Expiration), DnssecValidationResult.Secure);
    }

    [TestMethod]
    public void Verify_Rfc4035Section5_3_1OwnerNotBelowSignerName_IsBogus() =>
        AssertTamperedSignature(signature => signature with { SignerName = Name("example.net.") });

    [TestMethod]
    public void Verify_Rfc4035Section5_3_1SignerNameBelowOwner_IsBogus() =>
        AssertTamperedSignature(signature => signature with { SignerName = Name("a.www.example.com.") });

    [TestMethod]
    public void Verify_Rfc4035Section5_3_1SignerNameWithNoDnskey_IsBogus() =>
        AssertTamperedSignature(signature => signature with { SignerName = Name("www.example.com.") });

    [TestMethod]
    public void Verify_Rfc4035Section5_3_1KeyTagMismatch_IsBogus() =>
        AssertTamperedSignature(signature => signature with { KeyTag = (ushort)(signature.KeyTag + 1) });

    [TestMethod]
    public void Verify_Rfc4035Section5_3_1LabelsAboveOwnerLabels_IsBogus() =>
        AssertTamperedSignature(signature => signature with { Labels = 4 });

    [TestMethod]
    public void Verify_Rfc4035Section5_3_1TypeCoveredDiffers_IsBogus() =>
        AssertTamperedSignature(signature => signature with { TypeCovered = DnssecRecordType.Aaaa });

    [TestMethod]
    public void Verify_Rfc4034Section3_1_8SignatureAltered_IsBogus() =>
        AssertTamperedSignature(signature => signature with { Signature = [.. signature.Signature[..^1], (byte)(signature.Signature[^1] ^ 1)] });

    [TestMethod]
    public void Verify_Rfc4035Section5_2UnsupportedAlgorithm_IsInsecure() =>
        AssertTamperedSignature(signature => signature with { Algorithm = 5 }, DnssecValidationResult.Insecure);

    [TestMethod]
    public void Verify_Rfc4035Section5_3_1RrsigRecordWithoutRrsigData_IsBogus()
    {
        GeneratedZoneKey zoneKey = Ed25519Key();

        DnssecValidationResult result = AtGeneratedTime.Verify(AddressRrset("www.example.com."), Record("www.example.com.", DnssecRecordType.Rrsig, []), [DnskeyRecord("example.com.", zoneKey.Key)]);

        Assert.AreEqual(DnssecValidationResult.Bogus, result);
    }

    [TestMethod]
    public void Verify_Rfc4035Section5_3_1EmptyRrset_IsBogus()
    {
        GeneratedZoneKey zoneKey = Ed25519Key();
        RrsigRecordData signature = Sign(zoneKey, AddressRrset("www.example.com."));

        DnssecValidationResult result = AtGeneratedTime.Verify([], RrsigRecord("www.example.com.", signature), [DnskeyRecord("example.com.", zoneKey.Key)]);

        Assert.AreEqual(DnssecValidationResult.Bogus, result);
    }

    [TestMethod]
    public void Verify_Rfc4035Section5_3_1RecordOfAnotherOwnerOrClass_IsBogus()
    {
        GeneratedZoneKey zoneKey = Ed25519Key();
        List<DnsResourceRecord> rrset = AddressRrset("www.example.com.");
        DnsResourceRecord rrsig = RrsigRecord("www.example.com.", Sign(zoneKey, rrset));
        DnsResourceRecord[] keys = [DnskeyRecord("example.com.", zoneKey.Key)];

        DnssecValidationResult otherOwner = AtGeneratedTime.Verify([rrset[0], Record("ftp.example.com.", DnssecRecordType.A, [192, 0, 2, 1])], rrsig, keys);
        DnssecValidationResult otherClass = AtGeneratedTime.Verify([rrset[0], rrset[1] with { Class = 3 }], rrsig, keys);

        Assert.AreEqual(DnssecValidationResult.Bogus, otherOwner);
        Assert.AreEqual(DnssecValidationResult.Bogus, otherClass);
    }

    [TestMethod]
    public void Verify_Rfc4034Section2_1KeyThatIsNotAZoneKeyOrNotProtocol3OrOfAnotherAlgorithm_IsBogus()
    {
        GeneratedZoneKey zoneKey = Ed25519Key();
        List<DnsResourceRecord> rrset = AddressRrset("www.example.com.");
        DnsResourceRecord rrsig = RrsigRecord("www.example.com.", Sign(zoneKey, rrset));

        DnssecValidationResult notZoneKey = AtGeneratedTime.Verify(rrset, rrsig, [DnskeyRecord("example.com.", zoneKey.Key with { Flags = 0 })]);
        DnssecValidationResult notProtocol3 = AtGeneratedTime.Verify(rrset, rrsig, [DnskeyRecord("example.com.", zoneKey.Key with { Protocol = 2 })]);
        DnssecValidationResult otherAlgorithm = AtGeneratedTime.Verify(rrset, rrsig, [DnskeyRecord("example.com.", zoneKey.Key with { Algorithm = 16 })]);
        DnssecValidationResult notDnskeyData = AtGeneratedTime.Verify(rrset, rrsig, [Record("example.com.", DnssecRecordType.Dnskey, [])]);

        Assert.AreEqual(DnssecValidationResult.Bogus, notZoneKey);
        Assert.AreEqual(DnssecValidationResult.Bogus, notProtocol3);
        Assert.AreEqual(DnssecValidationResult.Bogus, otherAlgorithm);
        Assert.AreEqual(DnssecValidationResult.Bogus, notDnskeyData);
    }

    private static void AssertGeneratedKeyVerifies(GeneratedZoneKey zoneKey)
    {
        List<DnsResourceRecord> rrset = AddressRrset("www.example.com.");
        RrsigRecordData signature = Sign(zoneKey, rrset);

        DnssecValidationResult result = AtGeneratedTime.Verify(rrset, RrsigRecord("www.example.com.", signature), [DnskeyRecord("example.com.", zoneKey.Key)]);

        Assert.AreEqual(DnssecValidationResult.Secure, result);
    }

    private static void AssertGeneratedSignatureAtTime(DateTimeOffset now, DnssecValidationResult expected)
    {
        GeneratedZoneKey zoneKey = Ed25519Key();
        List<DnsResourceRecord> rrset = AddressRrset("www.example.com.");
        RrsigRecordData signature = Sign(zoneKey, rrset);

        DnssecValidationResult result = new RrsigVerifier(FixedTime(now)).Verify(rrset, RrsigRecord("www.example.com.", signature), [DnskeyRecord("example.com.", zoneKey.Key)]);

        Assert.AreEqual(expected, result);
    }

    private static void AssertTamperedSignature(
        Func<RrsigRecordData, RrsigRecordData> tamper, DnssecValidationResult expected = DnssecValidationResult.Bogus)
    {
        GeneratedZoneKey zoneKey = Ed25519Key();
        List<DnsResourceRecord> rrset = AddressRrset("www.example.com.");
        RrsigRecordData signature = tamper(Sign(zoneKey, rrset));

        DnssecValidationResult result = AtGeneratedTime.Verify(rrset, RrsigRecord("www.example.com.", signature), [DnskeyRecord("example.com.", zoneKey.Key)]);

        Assert.AreEqual(expected, result);
    }
}
