using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Testing;

namespace Curl.Output;

/// <summary>
/// Pins <see cref="OpenSslCertificateText"/> to the <c>Server certificate:</c> and
/// <c>Certificate level</c> lines of curl 8.21.0's OpenSSL build (<c>lib/vtls/openssl.c</c>),
/// the names and dates checked against OpenSSL 3.5.7's <c>openssl x509</c> (BL-356 Notes).
/// </summary>
[TestClass]
public sealed class OpenSslCertificateTextTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void PeerCertificate_LoopbackLeaf_PrintsNamesAndDatesAsOpenSsl()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("leaf DER length", LoopbackChain.Certificates[0].Length);
        diagnostics.Bytes("leaf DER", LoopbackChain.Certificates[0].Span);
        using var leaf = X509CertificateLoader.LoadCertificate(LoopbackChain.Certificates[0].Span);

        string[] expected =
            [
                "Server certificate:",
                "  subject: CN=localhost; O=Café Ünïcode; serialNumber=42; title=Dr; UID=u1; L=Salford",
                "  start date: Sep 27 05:24:52 2026 GMT",
                "  expire date: Nov  1 05:24:52 2027 GMT",
                "  issuer: C=GB; O=BL303; OU=Intermediates; CN=BL303 Intermediate; emailAddress=ca@example.test",
            ];

        string[] actual = OpenSslCertificateText.PeerCertificate(leaf, isProxy: false).ToArray();

        ReportLines(diagnostics, expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    // Linux (OpenSSL) and macOS (Security framework) refuse to load a certificate whose name
    // holds an odd-length BMPString, so the [NONE] fallback is only reachable on Windows.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void PeerCertificate_NamesOpenSslCannotPrint_PrintNone()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("subject and issuer", "CN=<odd-length BMPString>");
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        X500DistinguishedName brokenName = new([0x30, 0x0C, 0x31, 0x0A, 0x30, 0x08, 0x06, 0x03, 0x55, 0x04, 0x03, 0x1E, 0x01, 0x41]);
        using var certificate = new CertificateRequest(brokenName, key, HashAlgorithmName.SHA256)
            .CreateSelfSigned(new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 9, 28, 0, 0, 0, TimeSpan.Zero));

        var lines = OpenSslCertificateText.PeerCertificate(certificate, isProxy: false);

        diagnostics.Act("lines", string.Join("\n", lines));
        diagnostics.Assert("subject line", "  subject: [NONE]", lines[1]);
        diagnostics.Assert("issuer line", "  issuer: [NONE]", lines[4]);
        Assert.AreEqual("  subject: [NONE]", lines[1]);
        Assert.AreEqual("  issuer: [NONE]", lines[4]);
    }

    [TestMethod]
    public void CertificateLevel_LoopbackChain_DescribesEachKeyAndSignature()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("certificate count", LoopbackChain.Certificates.Length);
        var levels = LoopbackChain.Certificates
            .Select((der, level) => OpenSslCertificateText.CertificateLevel(level, X509CertificateLoader.LoadCertificate(der.Span)))
            .ToArray();

        string[] expected =
            [
                "  Certificate level 0: Public key type RSA (3072/128 Bits/secBits), signed using ecdsa-with-SHA512",
                "  Certificate level 1: Public key type EC/secp384r1 (384/192 Bits/secBits), signed using sha384WithRSAEncryption",
                "  Certificate level 2: Public key type RSA (2048/112 Bits/secBits), signed using sha256WithRSAEncryption",
            ];

        ReportLines(diagnostics, expected, levels);
        CollectionAssert.AreEqual(expected, levels);
    }

    [TestMethod]
    public void CertificateLevel_SignatureAlgorithmOpenSslCannotName_PrintsItDotted()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("key", "RSA 2048");
        diagnostics.Arrange("signature algorithm", "1.2.3.4");
        using var key = RSA.Create(2048);
        X500DistinguishedName name = new("CN=x");
        using var certificate = new CertificateRequest(name, new PublicKey(key), HashAlgorithmName.SHA256)
            .Create(name, new UnnamedAlgorithmSignatureGenerator(), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(1), [1]);

        string? actual = OpenSslCertificateText.CertificateLevel(0, certificate);

        const string expected = "  Certificate level 0: Public key type RSA (2048/112 Bits/secBits), signed using 1.2.3.4";
        diagnostics.Act("level", actual);
        diagnostics.Diff("level", expected, actual ?? string.Empty);
        Assert.AreEqual(
            expected,
            actual);
    }

    [TestMethod]
    public void CertificateLevel_KeyNotDescribed_IsNull()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("key", "brainpoolP160r1");
        using var certificate = CertificateWithBrainpoolP160r1Key();

        string? actual = OpenSslCertificateText.CertificateLevel(0, certificate);

        diagnostics.Act("level", actual);
        diagnostics.Assert("level", null, actual);
        Assert.IsNull(actual);
    }

    [TestMethod]
    public void PublicKeyText_NamedCurve_PrintsEcGroupAndSizes()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("curve", "nistP256");
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        string? actual = OpenSslCertificateText.PublicKeyText(new PublicKey(key));

        diagnostics.Act("key text", actual);
        diagnostics.Assert("key text", "EC/prime256v1 (256/128", actual);
        Assert.AreEqual("EC/prime256v1 (256/128", actual);
    }

    [TestMethod]
    public void PublicKeyText_RsaPss_PrintsRsaPssAndSizes()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("algorithm", "1.2.840.113549.1.1.10 (RSA-PSS), 1024 bits");
        using var key = RSA.Create(1024);
        PublicKey publicKey = new(new Oid("1.2.840.113549.1.1.10"), null, new AsnEncodedData(key.ExportRSAPublicKey()));

        string? actual = OpenSslCertificateText.PublicKeyText(publicKey);

        diagnostics.Act("key text", actual);
        diagnostics.Assert("key text", "RSA-PSS (1024/80", actual);
        Assert.AreEqual("RSA-PSS (1024/80", actual);
    }

    [TestMethod]
    [DataRow("1.3.101.112", "ED25519 (253/128")]
    [DataRow("1.3.101.113", "ED448 (456/224")]
    [DataRow("1.2.840.10040.4.1", null)]
    public void PublicKeyText_FixedSizeOrUnknownKey_PrintsOpenSslsSizesOrNothing(string algorithm, string? expected)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("algorithm", algorithm);
        PublicKey publicKey = new(new Oid(algorithm), null, new AsnEncodedData(new byte[] { 0x00 }));

        string? actual = OpenSslCertificateText.PublicKeyText(publicKey);

        diagnostics.Act("key text", actual);
        diagnostics.Assert("key text", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void PublicKeyText_EcKeyWithoutCurveName_IsNull()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("keys", "EC key without parameters; EC key with explicit curve parameters");
        AsnWriter explicitParameters = new(AsnEncodingRules.DER);
        using (explicitParameters.PushSequence())
        {
            explicitParameters.WriteInteger(1);
        }

        PublicKey withoutParameters = new(new Oid("1.2.840.10045.2.1"), null, new AsnEncodedData(new byte[] { 0x04 }));
        PublicKey withExplicitCurve = new(new Oid("1.2.840.10045.2.1"), new AsnEncodedData(explicitParameters.Encode()), new AsnEncodedData(new byte[] { 0x04 }));

        string? withoutParametersText = OpenSslCertificateText.PublicKeyText(withoutParameters);
        string? withExplicitCurveText = OpenSslCertificateText.PublicKeyText(withExplicitCurve);

        diagnostics.Act("key text without parameters", withoutParametersText);
        diagnostics.Act("key text with explicit curve", withExplicitCurveText);
        diagnostics.Assert("key text without parameters", null, withoutParametersText);
        diagnostics.Assert("key text with explicit curve", null, withExplicitCurveText);
        Assert.IsNull(withoutParametersText);
        Assert.IsNull(withExplicitCurveText);
    }

    private static void ReportLines(TestDiagnostics diagnostics, string?[] expected, string?[] actual)
    {
        string expectedText = string.Join("\n", expected);
        string actualText = string.Join("\n", actual);
        diagnostics.Act("lines", actualText);
        diagnostics.Diff("lines", expectedText, actualText);
    }

    // A certificate whose key is on brainpoolP160r1, a curve OpenSslCertificateText does not
    // describe. The key is written by hand and signed with a P-256 key, because macOS cannot
    // generate a brainpool key (BL-426).
    private static X509Certificate2 CertificateWithBrainpoolP160r1Key()
    {
        AsnWriter curve = new(AsnEncodingRules.DER);
        curve.WriteObjectIdentifier("1.3.36.3.3.2.8.1.1.1");
        var uncompressedPoint = new byte[41];
        uncompressedPoint[0] = 0x04;
        PublicKey publicKey = new(new Oid("1.2.840.10045.2.1"), new AsnEncodedData(curve.Encode()), new AsnEncodedData(uncompressedPoint));

        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        X500DistinguishedName name = new("CN=x");
        return new CertificateRequest(name, publicKey, HashAlgorithmName.SHA256)
            .Create(name, X509SignatureGenerator.CreateForECDsa(signingKey), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(1), [1]);
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
