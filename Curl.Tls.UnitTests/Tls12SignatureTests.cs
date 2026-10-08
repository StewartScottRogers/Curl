using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;
using Curl.Testing;

namespace Curl.Tls;

/// <summary>
/// TLS 1.2 and below signatures: the TLS 1.2 schemes (PKCS #1 v1.5, ECDSA on any curve),
/// TLS 1.0 and 1.1's MD5 and SHA-1 RSA block and SHA-1 ECDSA and DSA, DSA's five schemes and its
/// Dss-Sig-Value, what each key can sign,
/// the RSA pre-master secret encryption and the handshake hashes.
/// </summary>
[TestClass]
public sealed class Tls12SignatureTests
{
    private static readonly byte[] Content = "signed content"u8.ToArray();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void TlsOneTwoSchemesIncludePkcs1AndSha1()
    {
        Diagnostics.Arrange("schemes", "rsa_pkcs1_sha1, ecdsa_sha1, rsa_pss_rsae_sha256, 0xfefe, rsa_pkcs1_sha512");

        bool unknown = TlsSignatureScheme.IsTls12Scheme(0xfefe);
        Diagnostics.Act("TLS 1.2 scheme", $"rsa_pkcs1_sha1 {TlsSignatureScheme.IsTls12Scheme(TlsSignatureScheme.RsaPkcs1Sha1)}, ecdsa_sha1 {TlsSignatureScheme.IsTls12Scheme(TlsSignatureScheme.EcdsaSha1)}, 0xfefe {unknown}");

        Diagnostics.Assert("0xfefe is a TLS 1.2 scheme", false, unknown);
        Assert.IsTrue(TlsSignatureScheme.IsTls12Scheme(TlsSignatureScheme.RsaPkcs1Sha1));
        Assert.IsTrue(TlsSignatureScheme.IsTls12Scheme(TlsSignatureScheme.EcdsaSha1));
        Assert.IsTrue(TlsSignatureScheme.IsTls12Scheme(TlsSignatureScheme.RsaPssRsaeSha256));
        Assert.IsFalse(TlsSignatureScheme.IsTls12Scheme(0xfefe));
        Assert.IsFalse(TlsSignatureScheme.IsCertificateVerifyScheme(TlsSignatureScheme.RsaPkcs1Sha512));
    }

    [TestMethod]
    public void AnRsaKeySignsAndVerifiesPkcs1()
    {
        TestServerCredential credential = TestServerCredential.Rsa(TlsSignatureScheme.RsaPkcs1Sha256);
        TlsSignatureRule rule = TlsSignatureScheme.FindTls12Rule(TlsSignatureScheme.RsaPkcs1Sha384)!;
        TlsCertificatePublicKey key = TlsCertificatePublicKey.Read(credential.Certificate)!;
        Diagnostics.Arrange("key and rule", $"RSA certificate, rule {rule}");

        byte[] signature = credential.SigningKey.SignByRule(rule, Content);
        TlsAlertDescription? verified = key.VerifySignature(rule, Content, signature);
        Diagnostics.Act("signature", $"{signature.Length} bytes, verification alert {Describe(verified)}");

        Diagnostics.Assert("verification alert", "none", Describe(verified));
        Assert.IsNull(verified);
        Assert.IsFalse(credential.SigningKey.CanSign((TlsSignatureRule?)null));
        Assert.IsFalse(credential.SigningKey.CanSign(TlsSignatureScheme.RsaPkcs1Sha256));
    }

    [TestMethod]
    public void AnRsaKeySignsTheLegacyMd5Sha1BlockWithTheRawPrivateOperation()
    {
        TestServerCredential credential = TestServerCredential.Rsa(TlsSignatureScheme.RsaPkcs1Sha256);
        TlsSignatureRule rule = TlsSignatureScheme.LegacyRules[0];
        TlsCertificatePublicKey key = TlsCertificatePublicKey.Read(credential.Certificate)!;
        RSAParameters parameters = credential.RsaKey!.ExportParameters(includePrivateParameters: true);
        Diagnostics.Arrange("key and rule", $"RSA modulus of {parameters.Modulus!.Length} bytes, rule {rule}");

        byte[] signature = credential.SigningKey.SignByRule(rule, Content);
        Diagnostics.Act("signature", $"{signature.Length} bytes");

        Diagnostics.Assert("signature length", parameters.Modulus!.Length, signature.Length);
        Assert.AreEqual(TlsSignatureKind.RsaMd5Sha1, rule.Kind);
        Assert.IsTrue(credential.SigningKey.CanSign(rule));
        Assert.IsNull(key.VerifySignature(rule, Content, signature));
        BigInteger block = ToInteger(TlsSignatureScheme.BuildMd5Sha1Block(Content, parameters.Modulus!.Length));
        BigInteger expected = BigInteger.ModPow(block, ToInteger(parameters.D!), ToInteger(parameters.Modulus!));
        Assert.AreEqual(expected, ToInteger(signature));
        Assert.HasCount(parameters.Modulus!.Length, signature);
    }

    [TestMethod]
    public void AnRsaKeyWhosePrivateParametersCannotBeExportedCannotSignTheLegacyBlock()
    {
        using NonExportableRsa rsa = new();
        var signingKey = new RsaTlsSigningKey(rsa);
        Diagnostics.Arrange("key", "RSA key whose private parameters refuse export");

        bool first = signingKey.CanSign(TlsSignatureScheme.LegacyRules[0]);
        bool second = signingKey.CanSign(TlsSignatureScheme.LegacyRules[0]);
        Diagnostics.Act("can sign the legacy block", $"first {first}, second {second}, export attempts {rsa.ExportAttempts}");

        Diagnostics.Assert("export attempts", 1, rsa.ExportAttempts);
        Assert.IsFalse(first);
        Assert.IsFalse(second);
        Assert.AreEqual(1, rsa.ExportAttempts);
        Assert.IsTrue(signingKey.CanSign(TlsSignatureScheme.RsaPssRsaeSha256));
    }

    [TestMethod]
    public void AnRsaKeyWhosePrivateParametersCannotBeExportedCannotSignRsaPkcs1Sha224()
    {
        using NonExportableRsa rsa = new();
        Diagnostics.Arrange("key", "RSA key whose private parameters refuse export");

        bool canSign = new RsaTlsSigningKey(rsa).CanSign(TlsSignatureScheme.FindTls12Rule(TlsSignatureScheme.RsaPkcs1Sha224));
        Diagnostics.Act("can sign rsa_pkcs1_sha224", canSign);

        Diagnostics.Assert("can sign", false, canSign);
        Assert.IsFalse(canSign);
    }

    [TestMethod]
    public void TheSha224SchemesAreTlsOneTwoOnlyAndEd448IsBoth()
    {
        Diagnostics.Arrange("schemes", "rsa_pkcs1_sha224, ecdsa_sha224, ed448");

        bool ed448 = TlsSignatureScheme.IsCertificateVerifyScheme(TlsSignatureScheme.Ed448);
        Diagnostics.Act("CertificateVerify scheme", $"rsa_pkcs1_sha224 {TlsSignatureScheme.IsCertificateVerifyScheme(TlsSignatureScheme.RsaPkcs1Sha224)}, ecdsa_sha224 {TlsSignatureScheme.IsCertificateVerifyScheme(TlsSignatureScheme.EcdsaSha224)}, ed448 {ed448}");

        Diagnostics.Assert("ed448 is a CertificateVerify scheme", true, ed448);
        Assert.IsTrue(TlsSignatureScheme.IsTls12Scheme(TlsSignatureScheme.RsaPkcs1Sha224));
        Assert.IsTrue(TlsSignatureScheme.IsTls12Scheme(TlsSignatureScheme.EcdsaSha224));
        Assert.IsFalse(TlsSignatureScheme.IsCertificateVerifyScheme(TlsSignatureScheme.RsaPkcs1Sha224));
        Assert.IsFalse(TlsSignatureScheme.IsCertificateVerifyScheme(TlsSignatureScheme.EcdsaSha224));
        Assert.IsTrue(TlsSignatureScheme.IsTls12Scheme(TlsSignatureScheme.Ed448));
        Assert.IsTrue(ed448);
    }

    [TestMethod]
    public void TheRsaPkcs1Sha224BlockIsTypeOnePaddingAroundTheSha224DigestInfo()
    {
        byte[] digestInfo = Convert.FromHexString("302d300d06096086480165030402040500041c" + "23097d223405d8228642a477bda255b32aadbce4bda0b3f7e36c9da7");
        Diagnostics.Arrange("input", "\"abc\" into a 64-byte block");

        byte[] block = TlsSignatureScheme.BuildHandBuiltPkcs1Block(TlsSignatureKind.RsaPkcs1Sha224, "abc"u8.ToArray(), 64);
        Diagnostics.Bytes("block", block);
        Diagnostics.Act("block", $"{block.Length} bytes, starting {Convert.ToHexString(block[..3])}");

        Diagnostics.Diff("digest info", digestInfo, block[^47..]);
        Assert.AreEqual(0, block[0]);
        Assert.AreEqual(1, block[1]);
        Assert.AreEqual(0xff, block[2]);
        Assert.AreEqual(0, block[64 - 47 - 1]);
        CollectionAssert.AreEqual(digestInfo, block[^47..]);
        Assert.AreEqual(47, TlsSignatureScheme.HandBuiltPkcs1PayloadLength(TlsSignatureKind.RsaPkcs1Sha224));
    }

    [TestMethod]
    public void AnRsaPkcs1Sha224SignatureVerifiesAndAFlippedByteIsADecryptError()
    {
        TestServerCredential credential = TestServerCredential.Rsa(TlsSignatureScheme.RsaPkcs1Sha256);
        TlsSignatureRule rule = TlsSignatureScheme.FindTls12Rule(TlsSignatureScheme.RsaPkcs1Sha224)!;
        TlsCertificatePublicKey key = TlsCertificatePublicKey.Read(credential.Certificate)!;
        Diagnostics.Arrange("key and rule", $"RSA certificate, rule {rule}");

        byte[] signature = credential.SigningKey.SignByRule(rule, Content);
        byte[] flipped = [.. signature];
        flipped[^1] ^= 0x01;
        TlsAlertDescription? flippedAlert = key.VerifySignature(rule, Content, flipped);
        Diagnostics.Act("verification", $"signature of {signature.Length} bytes; flipped last byte gives {Describe(flippedAlert)}");

        Diagnostics.Assert("flipped alert", TlsAlertDescription.DecryptError, flippedAlert);
        Assert.IsNull(key.VerifySignature(rule, Content, signature));
        Assert.AreEqual(TlsAlertDescription.DecryptError, flippedAlert);
    }

    [TestMethod]
    public void AnRsaKeyTooShortForTheSha224DigestInfoIsADecryptError()
    {
        byte[] modulus = new byte[57];
        modulus[0] = 0xc0;
        modulus[^1] = 0x01;
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteInteger(new BigInteger(modulus, isUnsigned: true, isBigEndian: true));
            writer.WriteInteger(65537);
        }

        TlsCertificatePublicKey key = new(TlsSignatureScheme.RsaEncryptionOid, null, writer.Encode(), []);
        Diagnostics.Arrange("key", "RSA modulus of 57 bytes, exponent 65537");

        TlsAlertDescription? alert = key.VerifySignature(TlsSignatureScheme.FindTls12Rule(TlsSignatureScheme.RsaPkcs1Sha224), Content, new byte[57]);
        Diagnostics.Act("verification alert", Describe(alert));

        Diagnostics.Assert("alert", TlsAlertDescription.DecryptError, alert);
        Assert.AreEqual(TlsAlertDescription.DecryptError, alert);
    }

    [TestMethod]
    public void AnEcdsaSha224SignatureVerifiesAndAFlippedByteIsADecryptError()
    {
        TestServerCredential credential = TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP384, TlsSignatureScheme.EcdsaSecp384r1Sha384);
        TlsSignatureRule rule = TlsSignatureScheme.FindTls12Rule(TlsSignatureScheme.EcdsaSha224)!;
        TlsCertificatePublicKey key = TlsCertificatePublicKey.Read(credential.Certificate)!;
        Diagnostics.Arrange("key and rule", $"P-384 ECDSA certificate, rule {rule}");

        byte[] signature = credential.SigningKey.SignByRule(rule, Content);
        byte[] flipped = [.. signature];
        flipped[^1] ^= 0x01;
        TlsAlertDescription? flippedAlert = key.VerifySignature(rule, Content, flipped);
        Diagnostics.Act("verification", $"signature of {signature.Length} bytes; flipped last byte gives {Describe(flippedAlert)}");

        Diagnostics.Assert("flipped alert", TlsAlertDescription.DecryptError, flippedAlert);
        Assert.IsNull(key.VerifySignature(rule, Content, signature));
        Assert.AreEqual(TlsAlertDescription.DecryptError, flippedAlert);
    }

    [TestMethod]
    public void AnRsaKeyCertifiedAsPssCannotSignTheLegacyBlock()
    {
        using RSA rsa = RSA.Create(2048);
        Diagnostics.Arrange("key", "2048-bit RSA key certified as PSS");

        bool canSign = new RsaTlsSigningKey(rsa, certifiedAsPss: true).CanSign(TlsSignatureScheme.LegacyRules[0]);
        Diagnostics.Act("can sign the legacy block", canSign);

        Diagnostics.Assert("can sign", false, canSign);
        Assert.IsFalse(canSign);
    }

    [TestMethod]
    public void TheLegacyRsaBlockVerifiesAndRefusesAnyOtherContent()
    {
        TestServerCredential credential = TestServerCredential.Rsa(TlsSignatureScheme.RsaPkcs1Sha256);
        TlsCertificatePublicKey key = TlsCertificatePublicKey.Read(credential.Certificate)!;
        TlsSignatureRule rule = TlsSignatureScheme.FindLegacyRule(key.AlgorithmOid)!;
        Diagnostics.Arrange("key and rule", $"RSA certificate, rule {rule}");

        byte[] signature = Tls12TestServer.SignMd5Sha1(credential.RsaKey!, Content);
        TlsAlertDescription? otherContent = key.VerifySignature(rule, [1], signature);
        TlsAlertDescription? shortSignature = key.VerifySignature(rule, Content, signature[1..]);
        Diagnostics.Act("verification", $"other content {Describe(otherContent)}, signature one byte short {Describe(shortSignature)}");

        Diagnostics.Assert("other content alert", TlsAlertDescription.DecryptError, otherContent);
        Assert.IsNull(key.VerifySignature(rule, Content, signature));
        Assert.AreEqual(TlsAlertDescription.DecryptError, otherContent);
        Assert.AreEqual(TlsAlertDescription.DecryptError, shortSignature);
    }

    [TestMethod]
    [DataRow(46, 65537L)]
    [DataRow(2049, 65537L)]
    [DataRow(-64, 65537L)]
    [DataRow(64, 0L)]
    [DataRow(64, -3L)]
    public void ALegacyRsaKeyOutsideOpenSslsLimitsVerifiesNothing(int modulusLength, long exponent)
    {
        BigInteger modulus = (BigInteger.One << ((8 * Math.Abs(modulusLength)) - 1)) + 1;
        TlsCertificatePublicKey key = LegacyRsaKey(modulusLength < 0 ? -modulus : modulus, exponent);
        Diagnostics.Arrange("key", $"modulus of {Math.Abs(modulusLength)} bytes{(modulusLength < 0 ? ", negative" : string.Empty)}, exponent {exponent}");

        TlsAlertDescription? alert = key.VerifySignature(TlsSignatureScheme.FindLegacyRule(TlsSignatureScheme.RsaEncryptionOid), Content, new byte[Math.Abs(modulusLength)]);
        Diagnostics.Act("verification alert", Describe(alert));

        Diagnostics.Assert("alert", TlsAlertDescription.DecryptError, alert);
        Assert.AreEqual(TlsAlertDescription.DecryptError, alert);
    }

    [TestMethod]
    public void ALegacyRsaExponentOverSixtyFourBitsVerifiesNothing()
    {
        TlsCertificatePublicKey key = LegacyRsaKey((BigInteger.One << 511) + 1, (BigInteger.One << 64) + 1);
        Diagnostics.Arrange("key", "modulus of 64 bytes, exponent 2^64 + 1");

        TlsAlertDescription? alert = key.VerifySignature(TlsSignatureScheme.FindLegacyRule(TlsSignatureScheme.RsaEncryptionOid), Content, new byte[64]);
        Diagnostics.Act("verification alert", Describe(alert));

        Diagnostics.Assert("alert", TlsAlertDescription.DecryptError, alert);
        Assert.AreEqual(TlsAlertDescription.DecryptError, alert);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x30, 0x03, 0x02, 0x01, 0x05 })]
    [DataRow(new byte[] { 0x30, 0x09, 0x02, 0x01, 0x05, 0x02, 0x01, 0x03, 0x05, 0x00, 0x00 })]
    [DataRow(new byte[] { 0x04, 0x00 })]
    public void ALegacyRsaKeyThatIsNotAnRsaPublicKeyIsABadCertificate(byte[] keyBits)
    {
        TlsCertificatePublicKey key = new(TlsSignatureScheme.RsaEncryptionOid, null, keyBits, []);
        Diagnostics.Arrange("key bits", Convert.ToHexString(keyBits));

        TlsAlertDescription? alert = key.VerifySignature(TlsSignatureScheme.FindLegacyRule(TlsSignatureScheme.RsaEncryptionOid), Content, [1]);
        Diagnostics.Act("verification alert", Describe(alert));

        Diagnostics.Assert("alert", TlsAlertDescription.BadCertificate, alert);
        Assert.AreEqual(TlsAlertDescription.BadCertificate, alert);
    }

    [TestMethod]
    public void TheLegacyRsaBlockIsTypeOnePaddingAroundBothHashes()
    {
        Diagnostics.Arrange("input", "\"signed content\" into a 64-byte block");

        byte[] block = TlsSignatureScheme.BuildMd5Sha1Block(Content, 64);
        Diagnostics.Bytes("block", block);
        Diagnostics.Act("block", $"{block.Length} bytes, starting {Convert.ToHexString(block[..3])}");

        Diagnostics.Diff("MD5 and SHA-1 hashes", TlsPrf.Md5Sha1.HashHandshake(Content), block[28..]);
        Assert.AreEqual(0, block[0]);
        Assert.AreEqual(1, block[1]);
        Assert.AreEqual(0xff, block[2]);
        Assert.AreEqual(0, block[27]);
        CollectionAssert.AreEqual(TlsPrf.Md5Sha1.HashHandshake(Content), block[28..]);
    }

    [TestMethod]
    public void AnEcdsaKeySignsAnyTlsOneTwoEcdsaHashAndTlsOneOnesSha1()
    {
        TestServerCredential credential = TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP384, TlsSignatureScheme.EcdsaSecp384r1Sha384);
        TlsCertificatePublicKey key = TlsCertificatePublicKey.Read(credential.Certificate)!;
        TlsSignatureRule sha256 = TlsSignatureScheme.FindTls12Rule(TlsSignatureScheme.EcdsaSecp256r1Sha256)!;
        TlsSignatureRule legacy = TlsSignatureScheme.FindLegacyRule(key.AlgorithmOid)!;
        Diagnostics.Arrange("rules", $"P-384 key with {sha256} and {legacy}");

        TlsAlertDescription? sha256Alert = key.VerifySignature(sha256, Content, credential.SigningKey.SignByRule(sha256, Content));
        TlsAlertDescription? legacyAlert = key.VerifySignature(legacy, Content, credential.SigningKey.SignByRule(legacy, Content));
        Diagnostics.Act("verification", $"SHA-256 {Describe(sha256Alert)}, legacy SHA-1 {Describe(legacyAlert)}");

        Diagnostics.Assert("legacy alert", "none", Describe(legacyAlert));
        Assert.IsNull(sha256Alert);
        Assert.IsNull(legacyAlert);
        Assert.IsFalse(credential.SigningKey.CanSign(TlsSignatureScheme.EcdsaSecp256r1Sha256));
    }

    [TestMethod]
    public void OnlyRsaEcdsaAndDsaKeysHaveALegacySignature()
    {
        Diagnostics.Arrange("key types", "Ed25519, RSA, EC, DSA");

        TlsSignatureRule? ed25519 = TlsSignatureScheme.FindLegacyRule(TlsSignatureScheme.Ed25519Oid);
        TlsSignatureRule? dsa = TlsSignatureScheme.FindLegacyRule(TlsSignatureScheme.DsaOid);
        Diagnostics.Act("legacy rules", $"Ed25519 {ed25519?.ToString() ?? "none"}, DSA {dsa}");

        Diagnostics.Assert("DSA rule", new TlsSignatureRule(TlsSignatureKind.Dsa, TlsSignatureScheme.DsaOid, null, HashAlgorithmName.SHA1), dsa);
        Assert.IsNull(ed25519);
        Assert.AreEqual(TlsSignatureKind.RsaMd5Sha1, TlsSignatureScheme.FindLegacyRule(TlsSignatureScheme.RsaEncryptionOid)!.Kind);
        Assert.AreEqual(HashAlgorithmName.SHA1, TlsSignatureScheme.FindLegacyRule(TlsSignatureScheme.EcPublicKeyOid)!.Hash);
        Assert.AreEqual(new TlsSignatureRule(TlsSignatureKind.Dsa, TlsSignatureScheme.DsaOid, null, HashAlgorithmName.SHA1), dsa);
    }

    [TestMethod]
    [DataRow(TlsSignatureScheme.DsaSha1, "SHA1")]
    [DataRow(TlsSignatureScheme.DsaSha224, "SHA224")]
    [DataRow(TlsSignatureScheme.DsaSha256, "SHA256")]
    [DataRow(TlsSignatureScheme.DsaSha384, "SHA384")]
    [DataRow(TlsSignatureScheme.DsaSha512, "SHA512")]
    public void EachDsaSchemeIsATlsOneTwoSchemeForADsaKeyThatVerifiesIt(int scheme, string hash)
    {
        TlsSignatureRule rule = TlsSignatureScheme.FindTls12Rule((ushort)scheme)!;
        TlsCertificatePublicKey key = DsaKey();
        Diagnostics.Arrange("scheme", $"0x{scheme:x4}, hash {hash}");

        TlsAlertDescription? alert = key.VerifySignature(rule, Content, TestDsaKey.Sign(rule.Hash, Content));
        Diagnostics.Act("rule and verification", $"{rule}; alert {Describe(alert)}");

        Diagnostics.Assert("rule", new TlsSignatureRule(TlsSignatureKind.Dsa, TlsSignatureScheme.DsaOid, null, new HashAlgorithmName(hash)), rule);
        Assert.IsTrue(TlsSignatureScheme.IsTls12Scheme((ushort)scheme));
        Assert.IsFalse(TlsSignatureScheme.IsCertificateVerifyScheme((ushort)scheme));
        Assert.AreEqual(new TlsSignatureRule(TlsSignatureKind.Dsa, TlsSignatureScheme.DsaOid, null, new HashAlgorithmName(hash)), rule);
        Assert.IsNull(alert);
    }

    [TestMethod]
    [DataRow(TlsSignatureScheme.DsaSha256, "EACE8BDBBE353C432A795D9EC556C6D021F7A03F42C36E9BC87E4AC7932CC809", "7081E175455F9247B812B74583E9E94F9EA79BD640DC962533B0680793A38D53", DisplayName = "dsa_sha256")]
    [DataRow(0, "3A1B2DBD7489D6ED7E608FD036C83AF396E290DBD602408E8677DAABD6E7445A", "D26FCBA19FA3E3058FFC02CA1596CDBB6E0D20CB37B06054F7E36DED0CDBBCCF", DisplayName = "TLS 1.0 and 1.1, SHA-1")]
    public void ADsaKeyVerifiesRfc6979sPublishedSignatureAndRejectsAFlippedBit(int scheme, string r, string s)
    {
        TlsSignatureRule rule = scheme == 0 ? TlsSignatureScheme.FindLegacyRule(TlsSignatureScheme.DsaOid)! : TlsSignatureScheme.FindTls12Rule((ushort)scheme)!;
        byte[] signature = TestDsaKey.EncodeIntegers(Convert.FromHexString(r), Convert.FromHexString(s));
        TlsCertificatePublicKey key = DsaKey();
        Diagnostics.Arrange("signature", $"rule {rule}, r {r}, s {s}");

        TlsAlertDescription? published = key.VerifySignature(rule, "sample"u8.ToArray(), signature);
        signature[^1] ^= 1;
        TlsAlertDescription? flipped = key.VerifySignature(rule, "sample"u8.ToArray(), signature);
        Diagnostics.Act("verification", $"published {Describe(published)}, flipped bit {Describe(flipped)}");

        Diagnostics.Assert("flipped alert", TlsAlertDescription.DecryptError, flipped);
        Assert.IsNull(published);
        Assert.AreEqual(TlsAlertDescription.DecryptError, flipped);
    }

    [TestMethod]
    [DataRow(new byte[] { 1, 2, 3 }, DisplayName = "not DER")]
    [DataRow(new byte[] { 0x30, 0x06, 0x02, 0x01, 0x01, 0x02, 0x01, 0x01, 0x00 }, DisplayName = "a byte after the SEQUENCE")]
    [DataRow(new byte[] { 0x30, 0x09, 0x02, 0x01, 0x01, 0x02, 0x01, 0x01, 0x02, 0x01, 0x01 }, DisplayName = "a third INTEGER")]
    [DataRow(new byte[] { 0x30, 0x06, 0x02, 0x01, 0xff, 0x02, 0x01, 0x01 }, DisplayName = "a negative r")]
    [DataRow(new byte[] { 0x30, 0x06, 0x02, 0x01, 0x01, 0x02, 0x01, 0xff }, DisplayName = "a negative s")]
    [DataRow(new byte[] { 0x30, 0x07, 0x02, 0x02, 0x00, 0x01, 0x02, 0x01, 0x01 }, DisplayName = "an INTEGER not minimally encoded")]
    public void ADssSigValueThatDoesNotDecodeDoesNotVerify(byte[] signature)
    {
        Diagnostics.Arrange("Dss-Sig-Value", Convert.ToHexString(signature));

        TlsAlertDescription? alert = DsaKey().VerifySignature(TlsSignatureScheme.FindTls12Rule(TlsSignatureScheme.DsaSha256), Content, signature);
        Diagnostics.Act("verification alert", Describe(alert));

        Diagnostics.Assert("alert", TlsAlertDescription.DecryptError, alert);
        Assert.AreEqual(TlsAlertDescription.DecryptError, alert);
    }

    [TestMethod]
    public void ADssSigValueWhoseRIsLongerThanQDoesNotVerify()
    {
        byte[] signature = TestDsaKey.EncodeIntegers([.. Enumerable.Repeat((byte)0x7f, 33)], [1]);
        Diagnostics.Arrange("Dss-Sig-Value", "r of 33 bytes 0x7f, s 1");

        TlsAlertDescription? alert = DsaKey().VerifySignature(TlsSignatureScheme.FindTls12Rule(TlsSignatureScheme.DsaSha256), Content, signature);
        Diagnostics.Act("verification alert", Describe(alert));

        Diagnostics.Assert("alert", TlsAlertDescription.DecryptError, alert);
        Assert.AreEqual(TlsAlertDescription.DecryptError, alert);
    }

    [TestMethod]
    [DataRow(0, false, false, DisplayName = "y of 0")]
    [DataRow(1, true, false, DisplayName = "a fourth domain parameter")]
    [DataRow(1, false, true, DisplayName = "a byte after y")]
    public void ADsaKeyThatDoesNotReadIsABadCertificate(int publicKey, bool extraParameter, bool trailingByte)
    {
        byte[] domain = TestDsaKey.EncodeDomainParameters();
        if (extraParameter)
        {
            AsnReader parameters = new AsnReader(domain, AsnEncodingRules.DER).ReadSequence();
            domain = TestDsaKey.EncodeIntegers(ReadUnsigned(parameters), ReadUnsigned(parameters), ReadUnsigned(parameters), [1]);
        }

        AsnWriter y = new(AsnEncodingRules.DER);
        y.WriteInteger(publicKey);
        byte[] keyBits = trailingByte ? [.. y.Encode(), 0] : y.Encode();
        TlsCertificatePublicKey key = TlsCertificatePublicKey.ReadSubjectPublicKeyInfo(DsaSubjectPublicKeyInfo(domain, keyBits));
        Diagnostics.Arrange("key", $"y {publicKey}, fourth domain parameter {extraParameter}, byte after y {trailingByte}");

        TlsAlertDescription? alert = key.VerifySignature(TlsSignatureScheme.FindTls12Rule(TlsSignatureScheme.DsaSha256), Content, TestDsaKey.Sign(HashAlgorithmName.SHA256, Content));
        Diagnostics.Act("verification alert", Describe(alert));

        Diagnostics.Assert("alert", TlsAlertDescription.BadCertificate, alert);
        Assert.AreEqual(TlsAlertDescription.BadCertificate, alert);
    }

    [TestMethod]
    public void ADsaRuleDoesNotFitAnotherKeyNorAnotherRuleADsaKey()
    {
        TlsCertificatePublicKey rsa = TlsCertificatePublicKey.Read(TestServerCredential.Rsa(TlsSignatureScheme.RsaPkcs1Sha256).Certificate)!;
        Diagnostics.Arrange("pairs", "RSA key with dsa_sha256, DSA key with rsa_pkcs1_sha256");

        TlsAlertDescription? rsaAlert = rsa.VerifySignature(TlsSignatureScheme.FindTls12Rule(TlsSignatureScheme.DsaSha256), Content, [1]);
        TlsAlertDescription? dsaAlert = DsaKey().VerifySignature(TlsSignatureScheme.FindTls12Rule(TlsSignatureScheme.RsaPkcs1Sha256), Content, [1]);
        Diagnostics.Act("verification alerts", $"RSA key {Describe(rsaAlert)}, DSA key {Describe(dsaAlert)}");

        Diagnostics.Assert("RSA key alert", TlsAlertDescription.IllegalParameter, rsaAlert);
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, rsaAlert);
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, dsaAlert);
    }

    [TestMethod]
    public void ARuleForAnotherKeyTypeIsIllegal()
    {
        TlsCertificatePublicKey key = TlsCertificatePublicKey.Read(TestServerCredential.Ed25519().Certificate)!;
        Diagnostics.Arrange("key and rules", "Ed25519 key with ecdsa_sha1 and with no rule");

        TlsAlertDescription? ecdsaAlert = key.VerifySignature(TlsSignatureScheme.FindTls12Rule(TlsSignatureScheme.EcdsaSha1), Content, [1]);
        TlsAlertDescription? noRuleAlert = key.VerifySignature((TlsSignatureRule?)null, Content, [1]);
        Diagnostics.Act("verification alerts", $"ecdsa_sha1 {Describe(ecdsaAlert)}, no rule {Describe(noRuleAlert)}");

        Diagnostics.Assert("ecdsa_sha1 alert", TlsAlertDescription.IllegalParameter, ecdsaAlert);
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, ecdsaAlert);
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, noRuleAlert);
    }

    [TestMethod]
    public void OnlyAnImportableRsaEncryptionKeyTakesThePreMasterSecret()
    {
        TestServerCredential rsa = TestServerCredential.Rsa(TlsSignatureScheme.RsaPkcs1Sha256);
        byte[] secret = new byte[48];
        Diagnostics.Arrange("pre-master secret", "48 zero bytes, for an RSA, an Ed25519 and a foreign RSA key");

        byte[]? encrypted = TlsCertificatePublicKey.Read(rsa.Certificate)!.EncryptPkcs1(secret);
        byte[]? ed25519 = TlsCertificatePublicKey.Read(TestServerCredential.Ed25519().Certificate)!.EncryptPkcs1(secret);
        byte[]? foreign = TlsCertificatePublicKey.Read(TestServerCredential.Foreign(TlsSignatureScheme.RsaEncryptionOid, [1, 2, 3]).Certificate)!.EncryptPkcs1(secret);
        Diagnostics.Act("encrypted", $"RSA {encrypted?.Length} bytes, Ed25519 {(ed25519 is null ? "none" : "some")}, foreign {(foreign is null ? "none" : "some")}");

        byte[] decrypted = rsa.RsaKey!.Decrypt(encrypted!, RSAEncryptionPadding.Pkcs1);
        Diagnostics.Diff("decrypted secret", secret, decrypted);
        CollectionAssert.AreEqual(secret, decrypted);
        Assert.IsNull(ed25519);
        Assert.IsNull(foreign);
    }

    private static TlsCertificatePublicKey LegacyRsaKey(BigInteger modulus, BigInteger exponent)
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteInteger(modulus);
            writer.WriteInteger(exponent);
        }

        return new TlsCertificatePublicKey(TlsSignatureScheme.RsaEncryptionOid, null, writer.Encode(), []);
    }

    [TestMethod]
    public void TheHandshakeHashIsMd5AndSha1BelowTlsOneTwoAndThePrfsHashInIt()
    {
        Diagnostics.Arrange("content", "\"signed content\"");

        byte[] md5Sha1 = TlsPrf.Md5Sha1.HashHandshake(Content);
        byte[] sha256 = TlsPrf.Sha256.HashHandshake(Content);
        Diagnostics.Act("hashes", $"MD5 and SHA-1 {md5Sha1.Length} bytes, SHA-256 {Convert.ToHexString(sha256)}");

        Diagnostics.Diff("SHA-256", SHA256.HashData(Content), sha256);
        Assert.HasCount(36, md5Sha1);
        CollectionAssert.AreEqual(SHA256.HashData(Content), sha256);
        CollectionAssert.AreEqual(SHA384.HashData(Content), TlsPrf.Sha384.HashHandshake(Content));
    }

    private static string Describe(TlsAlertDescription? alert) => alert?.ToString() ?? "none";

    private static BigInteger ToInteger(ReadOnlySpan<byte> bigEndian) => new(bigEndian, isUnsigned: true, isBigEndian: true);

    private static TlsCertificatePublicKey DsaKey() => TlsCertificatePublicKey.Read(TestServerCredential.Dsa(TlsSignatureScheme.DsaSha256).Certificate)!;

    private static byte[] ReadUnsigned(AsnReader reader) => reader.ReadInteger().ToByteArray(isUnsigned: true, isBigEndian: true);

    private static byte[] DsaSubjectPublicKeyInfo(byte[] domain, byte[] keyBits)
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier(TlsSignatureScheme.DsaOid);
                writer.WriteEncodedValue(domain);
            }

            writer.WriteBitString(keyBits);
        }

        return writer.Encode();
    }

    /// <summary>An RSA key whose private parameters refuse export, as a non-exportable CNG key does; it counts the attempts.</summary>
    private sealed class NonExportableRsa : RSA
    {
        public int ExportAttempts { get; private set; }

        public override RSAParameters ExportParameters(bool includePrivateParameters)
        {
            ExportAttempts++;
            throw new CryptographicException("The key is not exportable.");
        }

        public override void ImportParameters(RSAParameters parameters) => throw new NotSupportedException();
    }
}
