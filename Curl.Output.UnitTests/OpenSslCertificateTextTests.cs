using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Output;

/// <summary>
/// Pins <see cref="OpenSslCertificateText"/> to the <c>Server certificate:</c> and
/// <c>Certificate level</c> lines of curl 8.21.0's OpenSSL build (<c>lib/vtls/openssl.c</c>),
/// the names and dates checked against OpenSSL 3.5.7's <c>openssl x509</c> (BL-356 Notes).
/// </summary>
[TestClass]
public sealed class OpenSslCertificateTextTests
{
    [TestMethod]
    public void ServerCertificate_LoopbackLeaf_PrintsNamesAndDatesAsOpenSsl()
    {
        using var leaf = X509CertificateLoader.LoadCertificate(LoopbackChain.Certificates[0].Span);

        CollectionAssert.AreEqual(
            new[]
            {
                "Server certificate:",
                "  subject: CN=localhost; O=Café Ünïcode; serialNumber=42; title=Dr; UID=u1; L=Salford",
                "  start date: Sep 27 05:24:52 2026 GMT",
                "  expire date: Nov  1 05:24:52 2027 GMT",
                "  issuer: C=GB; O=BL303; OU=Intermediates; CN=BL303 Intermediate; emailAddress=ca@example.test",
            },
            OpenSslCertificateText.ServerCertificate(leaf).ToArray());
    }

    // Linux (OpenSSL) and macOS (Security framework) refuse to load a certificate whose name
    // holds an odd-length BMPString, so the [NONE] fallback is only reachable on Windows.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ServerCertificate_NamesOpenSslCannotPrint_PrintNone()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        X500DistinguishedName brokenName = new([0x30, 0x0C, 0x31, 0x0A, 0x30, 0x08, 0x06, 0x03, 0x55, 0x04, 0x03, 0x1E, 0x01, 0x41]);
        using var certificate = new CertificateRequest(brokenName, key, HashAlgorithmName.SHA256)
            .CreateSelfSigned(new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 9, 28, 0, 0, 0, TimeSpan.Zero));

        var lines = OpenSslCertificateText.ServerCertificate(certificate);

        Assert.AreEqual("  subject: [NONE]", lines[1]);
        Assert.AreEqual("  issuer: [NONE]", lines[4]);
    }

    [TestMethod]
    public void CertificateLevel_LoopbackChain_DescribesEachKeyAndSignature()
    {
        var levels = LoopbackChain.Certificates
            .Select((der, level) => OpenSslCertificateText.CertificateLevel(level, X509CertificateLoader.LoadCertificate(der.Span)))
            .ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "  Certificate level 0: Public key type RSA (3072/128 Bits/secBits), signed using ecdsa-with-SHA512",
                "  Certificate level 1: Public key type EC/secp384r1 (384/192 Bits/secBits), signed using sha384WithRSAEncryption",
                "  Certificate level 2: Public key type RSA (2048/112 Bits/secBits), signed using sha256WithRSAEncryption",
            },
            levels);
    }

    [TestMethod]
    public void CertificateLevel_SignatureAlgorithmOpenSslCannotName_PrintsItDotted()
    {
        using var key = RSA.Create(2048);
        X500DistinguishedName name = new("CN=x");
        using var certificate = new CertificateRequest(name, new PublicKey(key), HashAlgorithmName.SHA256)
            .Create(name, new UnnamedAlgorithmSignatureGenerator(), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(1), [1]);

        Assert.AreEqual(
            "  Certificate level 0: Public key type RSA (2048/112 Bits/secBits), signed using 1.2.3.4",
            OpenSslCertificateText.CertificateLevel(0, certificate));
    }

    [TestMethod]
    public void CertificateLevel_KeyNotDescribed_IsNull()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.brainpoolP160r1);
        using var certificate = new CertificateRequest("CN=x", key, HashAlgorithmName.SHA256)
            .CreateSelfSigned(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(1));

        Assert.IsNull(OpenSslCertificateText.CertificateLevel(0, certificate));
    }

    [TestMethod]
    public void PublicKeyText_NamedCurve_PrintsEcGroupAndSizes()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        Assert.AreEqual("EC/prime256v1 (256/128", OpenSslCertificateText.PublicKeyText(new PublicKey(key)));
    }

    [TestMethod]
    public void PublicKeyText_RsaPss_PrintsRsaPssAndSizes()
    {
        using var key = RSA.Create(1024);
        PublicKey publicKey = new(new Oid("1.2.840.113549.1.1.10"), null, new AsnEncodedData(key.ExportRSAPublicKey()));

        Assert.AreEqual("RSA-PSS (1024/80", OpenSslCertificateText.PublicKeyText(publicKey));
    }

    [TestMethod]
    [DataRow("1.3.101.112", "ED25519 (253/128")]
    [DataRow("1.3.101.113", "ED448 (456/224")]
    [DataRow("1.2.840.10040.4.1", null)]
    public void PublicKeyText_FixedSizeOrUnknownKey_PrintsOpenSslsSizesOrNothing(string algorithm, string? expected)
    {
        PublicKey publicKey = new(new Oid(algorithm), null, new AsnEncodedData(new byte[] { 0x00 }));

        Assert.AreEqual(expected, OpenSslCertificateText.PublicKeyText(publicKey));
    }

    [TestMethod]
    public void PublicKeyText_EcKeyWithoutCurveName_IsNull()
    {
        AsnWriter explicitParameters = new(AsnEncodingRules.DER);
        using (explicitParameters.PushSequence())
        {
            explicitParameters.WriteInteger(1);
        }

        PublicKey withoutParameters = new(new Oid("1.2.840.10045.2.1"), null, new AsnEncodedData(new byte[] { 0x04 }));
        PublicKey withExplicitCurve = new(new Oid("1.2.840.10045.2.1"), new AsnEncodedData(explicitParameters.Encode()), new AsnEncodedData(new byte[] { 0x04 }));

        Assert.IsNull(OpenSslCertificateText.PublicKeyText(withoutParameters));
        Assert.IsNull(OpenSslCertificateText.PublicKeyText(withExplicitCurve));
    }

    // Signs with an algorithm, 1.2.3.4, that OpenSSL has no name for.
    private sealed class UnnamedAlgorithmSignatureGenerator : X509SignatureGenerator
    {
        public override byte[] GetSignatureAlgorithmIdentifier(HashAlgorithmName hashAlgorithm)
        {
            AsnWriter writer = new(AsnEncodingRules.DER);
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier("1.2.3.4");
            }

            return writer.Encode();
        }

        public override byte[] SignData(byte[] data, HashAlgorithmName hashAlgorithm) => [0];

        protected override PublicKey BuildPublicKey() => throw new NotSupportedException();
    }
}
