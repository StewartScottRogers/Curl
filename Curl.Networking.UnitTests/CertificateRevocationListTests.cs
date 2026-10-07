using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Networking.Fakes;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

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
        Diagnostics.Arrange("this update", thisUpdate);
        Diagnostics.Arrange("revoked leaf subject", s_leaf.Subject);
        Diagnostics.Arrange("issuer subject", s_rsaAuthority.Subject);
        Diagnostics.Arrange("DER length", der.Length);

        var list = CertificateRevocationList.Decode(der);
        var covers = list.CoversCertificatesIssuedFor(s_leaf);
        var signed = list.IsSignedBy(s_rsaAuthority);
        var revokes = list.Revokes(s_leaf);
        Diagnostics.Act("this update", list.ThisUpdate);
        Diagnostics.Act("next update", list.NextUpdate);
        Diagnostics.Act("covers", covers);
        Diagnostics.Act("signed by authority", signed);
        Diagnostics.Act("revokes leaf", revokes);

        Diagnostics.Assert("this update", thisUpdate, list.ThisUpdate);
        Assert.AreEqual(thisUpdate, list.ThisUpdate);
        Diagnostics.Assert("next update", thisUpdate.AddDays(1), list.NextUpdate);
        Assert.AreEqual(thisUpdate.AddDays(1), list.NextUpdate);
        Diagnostics.Assert("covers", true, covers);
        Assert.IsTrue(covers);
        Diagnostics.Assert("signed by authority", true, signed);
        Assert.IsTrue(signed);
        Diagnostics.Assert("revokes leaf", true, revokes);
        Assert.IsTrue(revokes);
    }

    [TestMethod]
    public void Decode_WithAnEmptyListTheBuilderWroteForAnEcdsaAuthority_VerifiesAndRevokesNothing()
    {
        var der = new CertificateRevocationListBuilder().Build(s_ecdsaAuthority, BigInteger.One, DateTimeOffset.UtcNow.AddDays(1), HashAlgorithmName.SHA384);
        Diagnostics.Arrange("issuer subject", s_ecdsaAuthority.Subject);
        Diagnostics.Arrange("DER length", der.Length);

        var list = CertificateRevocationList.Decode(der);
        var signedByEcdsa = list.IsSignedBy(s_ecdsaAuthority);
        var signedByRsa = list.IsSignedBy(s_rsaAuthority);
        var covers = list.CoversCertificatesIssuedFor(s_leaf);
        var revokes = list.Revokes(s_leaf);
        Diagnostics.Act("signed by ECDSA authority", signedByEcdsa);
        Diagnostics.Act("signed by RSA authority", signedByRsa);
        Diagnostics.Act("covers leaf", covers);
        Diagnostics.Act("revokes leaf", revokes);

        Diagnostics.Assert("signed by ECDSA authority", true, signedByEcdsa);
        Assert.IsTrue(signedByEcdsa);
        Diagnostics.Assert("signed by RSA authority", false, signedByRsa);
        Assert.IsFalse(signedByRsa);
        Diagnostics.Assert("covers leaf", false, covers);
        Assert.IsFalse(covers);
        Diagnostics.Assert("revokes leaf", false, revokes);
        Assert.IsFalse(revokes);
    }

    [TestMethod]
    public void Decode_WithNoVersionNoNextUpdateAndNothingAfter_ReadsTheDatesAndRevokesNothing()
    {
        var thisUpdate = new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);
        Diagnostics.Arrange("this update", thisUpdate);
        Diagnostics.Arrange("write version", false);

        var list = CertificateRevocationList.Decode(TestRevocationList.Write(s_rsaAuthority, thisUpdate: thisUpdate, writeVersion: false));
        var revokes = list.Revokes(s_leaf);
        var signed = list.IsSignedBy(s_rsaAuthority);
        Diagnostics.Act("this update", list.ThisUpdate);
        Diagnostics.Act("next update", list.NextUpdate);
        Diagnostics.Act("revokes leaf", revokes);
        Diagnostics.Act("signed by authority", signed);

        Diagnostics.Assert("this update", thisUpdate, list.ThisUpdate);
        Assert.AreEqual(thisUpdate, list.ThisUpdate);
        Diagnostics.Assert("next update is null", true, list.NextUpdate is null);
        Assert.IsNull(list.NextUpdate);
        Diagnostics.Assert("revokes leaf", false, revokes);
        Assert.IsFalse(revokes);
        Diagnostics.Assert("signed by authority", true, signed);
        Assert.IsTrue(signed);
    }

    [TestMethod]
    public void Decode_WithGeneralizedTimesAndARevokedSerialNumber_ReadsThemAll()
    {
        var thisUpdate = new DateTimeOffset(2051, 1, 2, 3, 4, 5, TimeSpan.Zero);
        Diagnostics.Arrange("this update", thisUpdate);
        Diagnostics.Arrange("next update", thisUpdate.AddDays(7));
        Diagnostics.Arrange("revoked serial number", "9A 01");
        Diagnostics.Arrange("generalized times", true);

        var list = CertificateRevocationList.Decode(TestRevocationList.Write(
            s_rsaAuthority, thisUpdate: thisUpdate, nextUpdate: thisUpdate.AddDays(7), revokedSerialNumbers: [[0x9A, 0x01]], generalizedTimes: true));
        var revokes = list.Revokes(s_leaf);
        Diagnostics.Act("this update", list.ThisUpdate);
        Diagnostics.Act("next update", list.NextUpdate);
        Diagnostics.Act("revokes leaf", revokes);

        Diagnostics.Assert("this update", thisUpdate, list.ThisUpdate);
        Assert.AreEqual(thisUpdate, list.ThisUpdate);
        Diagnostics.Assert("next update", thisUpdate.AddDays(7), list.NextUpdate);
        Assert.AreEqual(thisUpdate.AddDays(7), list.NextUpdate);
        Diagnostics.Assert("revokes leaf", true, revokes);
        Assert.IsTrue(revokes);
    }

    [TestMethod]
    public void Decode_WithRevokedCertificatesButNoNextUpdate_ReadsTheRevokedSerialNumber()
    {
        Diagnostics.Arrange("revoked serial number", "9A 01");

        var list = CertificateRevocationList.Decode(TestRevocationList.Write(s_rsaAuthority, revokedSerialNumbers: [[0x9A, 0x01]]));
        var revokes = list.Revokes(s_leaf);
        Diagnostics.Act("next update", list.NextUpdate);
        Diagnostics.Act("revokes leaf", revokes);

        Diagnostics.Assert("next update is null", true, list.NextUpdate is null);
        Assert.IsNull(list.NextUpdate);
        Diagnostics.Assert("revokes leaf", true, revokes);
        Assert.IsTrue(revokes);
    }

    [TestMethod]
    public void IsSignedBy_WithAnRsaAlgorithmAndAnEcdsaIssuer_IsFalse()
    {
        Diagnostics.Arrange("issuer subject", s_ecdsaAuthority.Subject);
        Diagnostics.Arrange("signature algorithm", "sha256WithRSA");

        var list = CertificateRevocationList.Decode(TestRevocationList.Write(
            s_ecdsaAuthority, signatureAlgorithm: TestRevocationList.Sha256WithRsa));
        var signed = list.IsSignedBy(s_ecdsaAuthority);
        Diagnostics.Act("signed by ECDSA authority", signed);

        Diagnostics.Assert("signed by ECDSA authority", false, signed);
        Assert.IsFalse(signed);
    }

    [TestMethod]
    public void IsSignedBy_WithAnAlgorithmTheReaderDoesNotVerify_IsFalse()
    {
        Diagnostics.Arrange("issuer subject", s_rsaAuthority.Subject);
        Diagnostics.Arrange("signature algorithm", "Ed25519");

        var list = CertificateRevocationList.Decode(TestRevocationList.Write(s_rsaAuthority, signatureAlgorithm: TestRevocationList.Ed25519));
        var signed = list.IsSignedBy(s_rsaAuthority);
        Diagnostics.Act("signed by RSA authority", signed);

        Diagnostics.Assert("signed by RSA authority", false, signed);
        Assert.IsFalse(signed);
    }

    [TestMethod]
    public void IsSignedBy_WithAnotherAuthoritysSignatureUnderTheSameName_IsFalse()
    {
        using var impostor = CreateAuthority(s_rsaAuthority.Subject, RSA.Create(2048));
        Diagnostics.Arrange("impostor subject", impostor.Subject);
        Diagnostics.Arrange("authority subject", s_rsaAuthority.Subject);

        var list = CertificateRevocationList.Decode(TestRevocationList.Write(impostor));
        var covers = list.CoversCertificatesIssuedFor(s_leaf);
        var signed = list.IsSignedBy(s_rsaAuthority);
        Diagnostics.Act("covers leaf", covers);
        Diagnostics.Act("signed by authority", signed);

        Diagnostics.Assert("covers leaf", true, covers);
        Assert.IsTrue(covers);
        Diagnostics.Assert("signed by authority", false, signed);
        Assert.IsFalse(signed);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x04, 0x01, 0x00 })]
    [DataRow(new byte[] { 0x30, 0x00 })]
    public void Decode_WithBytesThatAreNotAList_ThrowsAsnContentException(byte[] der)
    {
        Diagnostics.Arrange("DER length", der.Length);
        Diagnostics.Bytes("DER", der);

        var exception = Assert.ThrowsExactly<AsnContentException>(() => CertificateRevocationList.Decode(der));
        Diagnostics.Act("exception type", exception.GetType().Name);

        Diagnostics.Assert("exception type", nameof(AsnContentException), exception.GetType().Name);
        Assert.IsInstanceOfType<AsnContentException>(exception);
    }

    [TestMethod]
    public void Decode_WithBytesAfterTheList_ThrowsAsnContentException()
    {
        var der = TestRevocationList.Write(s_rsaAuthority);
        var withTrailingByte = der.Append((byte)0x00).ToArray();
        Diagnostics.Arrange("list length", der.Length);
        Diagnostics.Arrange("length with trailing byte", withTrailingByte.Length);

        var exception = Assert.ThrowsExactly<AsnContentException>(() => CertificateRevocationList.Decode(withTrailingByte));
        Diagnostics.Act("exception type", exception.GetType().Name);

        Diagnostics.Assert("exception type", nameof(AsnContentException), exception.GetType().Name);
        Assert.IsInstanceOfType<AsnContentException>(exception);
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

        var encoded = writer.Encode();
        Diagnostics.Arrange("encoded length", encoded.Length);
        Diagnostics.Arrange("extra element", "NULL after the signature");

        var exception = Assert.ThrowsExactly<AsnContentException>(() => CertificateRevocationList.Decode(encoded));
        Diagnostics.Act("exception type", exception.GetType().Name);

        Diagnostics.Assert("exception type", nameof(AsnContentException), exception.GetType().Name);
        Assert.IsInstanceOfType<AsnContentException>(exception);
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
