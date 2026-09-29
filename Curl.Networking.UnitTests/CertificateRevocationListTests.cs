using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Networking.Fakes;

namespace Curl.Networking;

/// <summary>
/// <see cref="CertificateRevocationList" /> decodes an RFC 5280 list, as
/// <see cref="CertificateRevocationListBuilder" /> writes one and in the shapes it never writes,
/// and checks its issuer, signature and revoked serial numbers (BL-609).
/// </summary>
[TestClass]
public sealed class CertificateRevocationListTests
{
    private static X509Certificate2 s_rsaAuthority = null!;

    private static X509Certificate2 s_ecdsaAuthority = null!;

    private static X509Certificate2 s_leaf = null!;

    [ClassInitialize]
    public static void CreateCertificates(TestContext context)
    {
        s_rsaAuthority = CreateAuthority("CN=BL609 RSA Authority", RSA.Create(2048));
        s_ecdsaAuthority = CreateAuthority("CN=BL609 ECDSA Authority", ECDsa.Create(ECCurve.NamedCurves.nistP256));
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=leaf.example", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        s_leaf = request.Create(s_rsaAuthority, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1), [0x00, 0x9A, 0x01]);
    }

    [ClassCleanup]
    public static void DisposeCertificates()
    {
        s_rsaAuthority.Dispose();
        s_ecdsaAuthority.Dispose();
        s_leaf.Dispose();
    }

    [TestMethod]
    public void Decode_WithAListTheBuilderWrote_ReadsItsDatesSignatureAndRevokedCertificate()
    {
        var thisUpdate = new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);
        var builder = new CertificateRevocationListBuilder();
        builder.AddEntry(s_leaf);
        var der = builder.Build(s_rsaAuthority, BigInteger.One, thisUpdate.AddDays(1), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1, thisUpdate);

        var list = CertificateRevocationList.Decode(der);

        Assert.AreEqual(thisUpdate, list.ThisUpdate);
        Assert.AreEqual(thisUpdate.AddDays(1), list.NextUpdate);
        Assert.IsTrue(list.CoversCertificatesIssuedFor(s_leaf));
        Assert.IsTrue(list.IsSignedBy(s_rsaAuthority));
        Assert.IsTrue(list.Revokes(s_leaf));
    }

    [TestMethod]
    public void Decode_WithAnEmptyListTheBuilderWroteForAnEcdsaAuthority_VerifiesAndRevokesNothing()
    {
        var der = new CertificateRevocationListBuilder().Build(s_ecdsaAuthority, BigInteger.One, DateTimeOffset.UtcNow.AddDays(1), HashAlgorithmName.SHA384);

        var list = CertificateRevocationList.Decode(der);

        Assert.IsTrue(list.IsSignedBy(s_ecdsaAuthority));
        Assert.IsFalse(list.IsSignedBy(s_rsaAuthority));
        Assert.IsFalse(list.CoversCertificatesIssuedFor(s_leaf));
        Assert.IsFalse(list.Revokes(s_leaf));
    }

    [TestMethod]
    public void Decode_WithNoVersionNoNextUpdateAndNothingAfter_ReadsTheDatesAndRevokesNothing()
    {
        var thisUpdate = new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);

        var list = CertificateRevocationList.Decode(TestRevocationList.Write(s_rsaAuthority, thisUpdate: thisUpdate, writeVersion: false));

        Assert.AreEqual(thisUpdate, list.ThisUpdate);
        Assert.IsNull(list.NextUpdate);
        Assert.IsFalse(list.Revokes(s_leaf));
        Assert.IsTrue(list.IsSignedBy(s_rsaAuthority));
    }

    [TestMethod]
    public void Decode_WithGeneralizedTimesAndARevokedSerialNumber_ReadsThemAll()
    {
        var thisUpdate = new DateTimeOffset(2051, 1, 2, 3, 4, 5, TimeSpan.Zero);

        var list = CertificateRevocationList.Decode(TestRevocationList.Write(
            s_rsaAuthority, thisUpdate: thisUpdate, nextUpdate: thisUpdate.AddDays(7), revokedSerialNumbers: [[0x9A, 0x01]], generalizedTimes: true));

        Assert.AreEqual(thisUpdate, list.ThisUpdate);
        Assert.AreEqual(thisUpdate.AddDays(7), list.NextUpdate);
        Assert.IsTrue(list.Revokes(s_leaf));
    }

    [TestMethod]
    public void Decode_WithRevokedCertificatesButNoNextUpdate_ReadsTheRevokedSerialNumber()
    {
        var list = CertificateRevocationList.Decode(TestRevocationList.Write(s_rsaAuthority, revokedSerialNumbers: [[0x9A, 0x01]]));

        Assert.IsNull(list.NextUpdate);
        Assert.IsTrue(list.Revokes(s_leaf));
    }

    [TestMethod]
    public void IsSignedBy_WithAnRsaAlgorithmAndAnEcdsaIssuer_IsFalse()
    {
        var list = CertificateRevocationList.Decode(TestRevocationList.Write(
            s_ecdsaAuthority, signatureAlgorithm: TestRevocationList.Sha256WithRsa));

        Assert.IsFalse(list.IsSignedBy(s_ecdsaAuthority));
    }

    [TestMethod]
    public void IsSignedBy_WithAnAlgorithmTheReaderDoesNotVerify_IsFalse()
    {
        var list = CertificateRevocationList.Decode(TestRevocationList.Write(s_rsaAuthority, signatureAlgorithm: TestRevocationList.Ed25519));

        Assert.IsFalse(list.IsSignedBy(s_rsaAuthority));
    }

    [TestMethod]
    public void IsSignedBy_WithAnotherAuthoritysSignatureUnderTheSameName_IsFalse()
    {
        using var impostor = CreateAuthority(s_rsaAuthority.Subject, RSA.Create(2048));

        var list = CertificateRevocationList.Decode(TestRevocationList.Write(impostor));

        Assert.IsTrue(list.CoversCertificatesIssuedFor(s_leaf));
        Assert.IsFalse(list.IsSignedBy(s_rsaAuthority));
    }

    [TestMethod]
    [DataRow(new byte[] { 0x04, 0x01, 0x00 })]
    [DataRow(new byte[] { 0x30, 0x00 })]
    public void Decode_WithBytesThatAreNotAList_ThrowsAsnContentException(byte[] der) =>
        Assert.ThrowsExactly<AsnContentException>(() => CertificateRevocationList.Decode(der));

    [TestMethod]
    public void Decode_WithBytesAfterTheList_ThrowsAsnContentException()
    {
        var der = TestRevocationList.Write(s_rsaAuthority);

        Assert.ThrowsExactly<AsnContentException>(() => CertificateRevocationList.Decode(der.Append((byte)0x00).ToArray()));
    }

    [TestMethod]
    public void Decode_WithSomethingAfterTheSignature_ThrowsAsnContentException()
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        var inner = new AsnReader(TestRevocationList.Write(s_rsaAuthority), AsnEncodingRules.DER).ReadSequence();
        using (writer.PushSequence())
        {
            writer.WriteEncodedValue(inner.ReadEncodedValue().Span);
            writer.WriteEncodedValue(inner.ReadEncodedValue().Span);
            writer.WriteEncodedValue(inner.ReadEncodedValue().Span);
            writer.WriteNull();
        }

        Assert.ThrowsExactly<AsnContentException>(() => CertificateRevocationList.Decode(writer.Encode()));
    }

    // A CA with a subject key identifier and a key usage allowing CRL signing, as
    // CertificateRevocationListBuilder.Build asks of an issuer.
    internal static X509Certificate2 CreateAuthority(string subject, AsymmetricAlgorithm key)
    {
        using (key)
        {
            var request = key is RSA rsa
                ? new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
                : new CertificateRequest(subject, (ECDsa)key, HashAlgorithmName.SHA256);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
            return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddYears(-1), DateTimeOffset.UtcNow.AddYears(1));
        }
    }
}
