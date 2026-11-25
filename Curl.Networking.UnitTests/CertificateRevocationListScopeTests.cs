using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Networking.Fakes;

namespace Curl.Networking;

/// <summary>
/// <see cref="CertificateRevocationList.CoversScopeOf" /> treats a list whose extensions do not
/// cover the certificate as unusable, never as "not revoked" (STD-113).
/// </summary>
[TestClass]
public sealed class CertificateRevocationListScopeTests
{
    private const string IssuingDistributionPoint = "2.5.29.28";
    private const string DeltaCrlIndicator = "2.5.29.27";
    private const string CrlNumber = "2.5.29.20";
    private const string ReasonCode = "2.5.29.21";
    private const string CertificateIssuer = "2.5.29.29";
    private const string UnknownOid = "1.3.6.1.4.1.99999.1";

    private static readonly Uri s_address = new("http://127.0.0.1:40080/b/leaf.crl");

    private static X509Certificate2 s_authority = null!;

    private static X509Certificate2 s_leaf = null!;

    [ClassInitialize]
    public static void CreateCertificates(TestContext context)
    {
        s_authority = CertificateRevocationListTests.CreateAuthority("CN=STD113 Authority", RSA.Create(2048));
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=leaf.example", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        s_leaf = request.Create(s_authority, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1), [0x01, 0x02]);
    }

    [ClassCleanup]
    public static void DisposeCertificates()
    {
        s_authority.Dispose();
        s_leaf.Dispose();
    }

    [TestMethod]
    public void CoversScopeOf_ListWithNoExtensions_IsTrue() => Assert.IsTrue(Decode().CoversScopeOf(s_leaf, s_address));

    [TestMethod]
    public void CoversScopeOf_ListWithOnlyNonCriticalUnknownExtension_IsTrue() =>
        Assert.IsTrue(Decode(Ext(UnknownOid, false, [0x05, 0x00])).CoversScopeOf(s_leaf, s_address));

    [TestMethod]
    public void CoversScopeOf_ListWithRecognisedCriticalExtension_IsTrue() =>
        Assert.IsTrue(Decode(Ext(CrlNumber, true, [0x02, 0x01, 0x01])).CoversScopeOf(s_leaf, s_address));

    [TestMethod]
    public void CoversScopeOf_ListWithCriticalUnknownExtension_IsFalse() =>
        Assert.IsFalse(Decode(Ext(UnknownOid, true, [0x05, 0x00])).CoversScopeOf(s_leaf, s_address));

    [TestMethod]
    public void CoversScopeOf_DeltaList_IsFalse() =>
        Assert.IsFalse(Decode(Ext(DeltaCrlIndicator, true, [0x02, 0x01, 0x01])).CoversScopeOf(s_leaf, s_address));

    [TestMethod]
    public void CoversScopeOf_ListWithExtensionsThatDoNotParse_IsFalse() =>
        Assert.IsFalse(Decode(Ext(IssuingDistributionPoint, true, [0x01])).CoversScopeOf(s_leaf, s_address));

    [TestMethod]
    public void CoversScopeOf_IssuingDistributionPointNamingTheAddress_IsTrue() =>
        Assert.IsTrue(Decode(Idp(NameField(s_address.AbsoluteUri))).CoversScopeOf(s_leaf, s_address));

    [TestMethod]
    public void CoversScopeOf_IssuingDistributionPointNamingAnotherAddress_IsFalse() =>
        Assert.IsFalse(Decode(Idp(NameField("http://127.0.0.1:40080/partition/other.crl"))).CoversScopeOf(s_leaf, s_address));

    [TestMethod]
    public void CoversScopeOf_IssuingDistributionPointNamingANonUriAndNoUri_IsFalse()
    {
        var name = new AsnWriter(AsnEncodingRules.DER);
        using (name.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0)))
        {
            using (name.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0)))
            {
                name.WriteCharacterString(UniversalTagNumber.IA5String, "host", new Asn1Tag(TagClass.ContextSpecific, 2));
                name.WriteCharacterString(UniversalTagNumber.IA5String, "not a uri", new Asn1Tag(TagClass.ContextSpecific, 6));
            }
        }

        Assert.IsFalse(Decode(Idp(name.Encode())).CoversScopeOf(s_leaf, s_address));
    }

    [TestMethod]
    public void CoversScopeOf_IssuingDistributionPointWithARelativeName_IsFalse()
    {
        var name = new AsnWriter(AsnEncodingRules.DER);
        using (name.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0)))
        {
            using (name.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 1)))
            {
            }
        }

        Assert.IsFalse(Decode(Idp(name.Encode())).CoversScopeOf(s_leaf, s_address));
    }

    [TestMethod]
    public void CoversScopeOf_OnlyContainsCaCertsForALeaf_IsFalse() =>
        Assert.IsFalse(Decode(Idp(Flag(2, true))).CoversScopeOf(s_leaf, s_address));

    [TestMethod]
    public void CoversScopeOf_OnlyContainsCaCertsForACa_IsTrue() =>
        Assert.IsTrue(Decode(Idp(Flag(2, true))).CoversScopeOf(s_authority, s_address));

    [TestMethod]
    public void CoversScopeOf_OnlyContainsUserCertsForALeaf_IsTrue() =>
        Assert.IsTrue(Decode(Idp(Flag(1, true))).CoversScopeOf(s_leaf, s_address));

    [TestMethod]
    public void CoversScopeOf_OnlyContainsUserCertsForACa_IsFalse() =>
        Assert.IsFalse(Decode(Idp(Flag(1, true))).CoversScopeOf(s_authority, s_address));

    [TestMethod]
    public void CoversScopeOf_OnlySomeReasons_IsFalse() =>
        Assert.IsFalse(Decode(Idp([0x83, 0x02, 0x07, 0x20])).CoversScopeOf(s_leaf, s_address));

    [TestMethod]
    public void CoversScopeOf_IndirectList_IsFalse() =>
        Assert.IsFalse(Decode(Idp(Flag(4, true))).CoversScopeOf(s_leaf, s_address));

    [TestMethod]
    public void CoversScopeOf_AttributeCertificateList_IsFalse() =>
        Assert.IsFalse(Decode(Idp(Flag(5, true))).CoversScopeOf(s_leaf, s_address));

    [TestMethod]
    public void CoversScopeOf_IssuingDistributionPointWithEveryFlagFalse_IsTrue() =>
        Assert.IsTrue(Decode(Idp([.. Flag(1, false), .. Flag(2, false), .. Flag(4, false), .. Flag(5, false)])).CoversScopeOf(s_leaf, s_address));

    [TestMethod]
    public void CoversScopeOf_IssuingDistributionPointWithAnUnknownField_IsFalse() =>
        Assert.IsFalse(Decode(Idp([0x86, 0x01, 0x00])).CoversScopeOf(s_leaf, s_address));

    [TestMethod]
    public void CoversScopeOf_EntryWithAReasonCode_IsTrue() =>
        Assert.IsTrue(DecodeWithEntries(Ext(ReasonCode, true, [0x0A, 0x01, 0x01])).CoversScopeOf(s_leaf, s_address));

    [TestMethod]
    public void CoversScopeOf_EntryWithANonCriticalUnknownExtension_IsTrue() =>
        Assert.IsTrue(DecodeWithEntries(Ext(UnknownOid, false, [0x05, 0x00])).CoversScopeOf(s_leaf, s_address));

    [TestMethod]
    public void CoversScopeOf_EntryWithACriticalUnknownExtension_IsTrue() =>
        Assert.IsTrue(DecodeWithEntries(Ext(UnknownOid, true, [0x05, 0x00])).CoversScopeOf(s_leaf, s_address));

    [TestMethod]
    public void CoversScopeOf_EntryWithACertificateIssuer_IsTrue() =>
        Assert.IsTrue(DecodeWithEntries(Ext(CertificateIssuer, false, [0x30, 0x00])).CoversScopeOf(s_leaf, s_address));

    [TestMethod]
    public void CoversScopeOf_EntryWithExtensionsThatDoNotParse_IsFalse() =>
        Assert.IsFalse(DecodeWithEntries(Ext(ReasonCode, false, [0x0A, 0x01, 0x01]), [0x30, 0x01, 0x05]).CoversScopeOf(s_leaf, s_address));

    [TestMethod]
    public void Revokes_ListWithACriticalUnknownExtension_StillNamesTheRevokedSerialNumber()
    {
        var der = TestRevocationList.Write(
            s_authority,
            nextUpdate: DateTimeOffset.UtcNow.AddDays(1),
            revokedSerialNumbers: [[0x01, 0x02]],
            extensions: [Ext(UnknownOid, true, [0x05, 0x00])]);

        var list = CertificateRevocationList.Decode(der);

        Assert.IsTrue(list.Revokes(s_leaf));
        Assert.IsFalse(list.CoversScopeOf(s_leaf, s_address));
    }

    private static byte[] Ext(string oid, bool critical, byte[] value) => TestRevocationList.Extension(oid, critical, value);

    private static CertificateRevocationList Decode(params byte[][] extensions) =>
        CertificateRevocationList.Decode(TestRevocationList.Write(s_authority, nextUpdate: DateTimeOffset.UtcNow.AddDays(1), extensions: extensions));

    private static CertificateRevocationList DecodeWithEntries(params byte[][] entryExtensions) =>
        CertificateRevocationList.Decode(TestRevocationList.Write(
            s_authority,
            nextUpdate: DateTimeOffset.UtcNow.AddDays(1),
            revokedSerialNumbers: [[0x7F]],
            entryExtensions: entryExtensions));

    // A critical IssuingDistributionPoint extension whose SEQUENCE holds the given field bytes.
    private static byte[] Idp(byte[] fields)
    {
        return TestRevocationList.Extension(IssuingDistributionPoint, true, [0x30, (byte)fields.Length, .. fields]);
    }

    private static byte[] Flag(int number, bool value) =>
        [(byte)(0x80 | number), 0x01, value ? (byte)0xFF : (byte)0x00];

    // distributionPoint [0] { fullName [0] { uniformResourceIdentifier [6] } }.
    private static byte[] NameField(string address)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0)))
        {
            using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0)))
            {
                writer.WriteCharacterString(UniversalTagNumber.IA5String, address, new Asn1Tag(TagClass.ContextSpecific, 6));
            }
        }

        return writer.Encode();
    }
}
