using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;

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

    [TestMethod]
    public void TlsOneTwoSchemesIncludePkcs1AndSha1()
    {
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

        byte[] signature = credential.SigningKey.SignByRule(rule, Content);

        Assert.IsNull(key.VerifySignature(rule, Content, signature));
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

        byte[] signature = credential.SigningKey.SignByRule(rule, Content);

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

        Assert.IsFalse(signingKey.CanSign(TlsSignatureScheme.LegacyRules[0]));
        Assert.IsFalse(signingKey.CanSign(TlsSignatureScheme.LegacyRules[0]));
        Assert.AreEqual(1, rsa.ExportAttempts);
        Assert.IsTrue(signingKey.CanSign(TlsSignatureScheme.RsaPssRsaeSha256));
    }

    [TestMethod]
    public void AnRsaKeyCertifiedAsPssCannotSignTheLegacyBlock()
    {
        using RSA rsa = RSA.Create(2048);

        Assert.IsFalse(new RsaTlsSigningKey(rsa, certifiedAsPss: true).CanSign(TlsSignatureScheme.LegacyRules[0]));
    }

    [TestMethod]
    public void TheLegacyRsaBlockVerifiesAndRefusesAnyOtherContent()
    {
        TestServerCredential credential = TestServerCredential.Rsa(TlsSignatureScheme.RsaPkcs1Sha256);
        TlsCertificatePublicKey key = TlsCertificatePublicKey.Read(credential.Certificate)!;
        TlsSignatureRule rule = TlsSignatureScheme.FindLegacyRule(key.AlgorithmOid)!;

        byte[] signature = Tls12TestServer.SignMd5Sha1(credential.RsaKey!, Content);

        Assert.IsNull(key.VerifySignature(rule, Content, signature));
        Assert.AreEqual(TlsAlertDescription.DecryptError, key.VerifySignature(rule, [1], signature));
        Assert.AreEqual(TlsAlertDescription.DecryptError, key.VerifySignature(rule, Content, signature[1..]));
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

        Assert.AreEqual(TlsAlertDescription.DecryptError, key.VerifySignature(TlsSignatureScheme.FindLegacyRule(TlsSignatureScheme.RsaEncryptionOid), Content, new byte[Math.Abs(modulusLength)]));
    }

    [TestMethod]
    public void ALegacyRsaExponentOverSixtyFourBitsVerifiesNothing()
    {
        TlsCertificatePublicKey key = LegacyRsaKey((BigInteger.One << 511) + 1, (BigInteger.One << 64) + 1);

        Assert.AreEqual(TlsAlertDescription.DecryptError, key.VerifySignature(TlsSignatureScheme.FindLegacyRule(TlsSignatureScheme.RsaEncryptionOid), Content, new byte[64]));
    }

    [TestMethod]
    [DataRow(new byte[] { 0x30, 0x03, 0x02, 0x01, 0x05 })]
    [DataRow(new byte[] { 0x30, 0x09, 0x02, 0x01, 0x05, 0x02, 0x01, 0x03, 0x05, 0x00, 0x00 })]
    [DataRow(new byte[] { 0x04, 0x00 })]
    public void ALegacyRsaKeyThatIsNotAnRsaPublicKeyIsABadCertificate(byte[] keyBits)
    {
        TlsCertificatePublicKey key = new(TlsSignatureScheme.RsaEncryptionOid, null, keyBits, []);

        Assert.AreEqual(TlsAlertDescription.BadCertificate, key.VerifySignature(TlsSignatureScheme.FindLegacyRule(TlsSignatureScheme.RsaEncryptionOid), Content, [1]));
    }

    [TestMethod]
    public void TheLegacyRsaBlockIsTypeOnePaddingAroundBothHashes()
    {
        byte[] block = TlsSignatureScheme.BuildMd5Sha1Block(Content, 64);

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

        Assert.IsNull(key.VerifySignature(sha256, Content, credential.SigningKey.SignByRule(sha256, Content)));
        Assert.IsNull(key.VerifySignature(legacy, Content, credential.SigningKey.SignByRule(legacy, Content)));
        Assert.IsFalse(credential.SigningKey.CanSign(TlsSignatureScheme.EcdsaSecp256r1Sha256));
    }

    [TestMethod]
    public void OnlyRsaEcdsaAndDsaKeysHaveALegacySignature()
    {
        Assert.IsNull(TlsSignatureScheme.FindLegacyRule(TlsSignatureScheme.Ed25519Oid));
        Assert.AreEqual(TlsSignatureKind.RsaMd5Sha1, TlsSignatureScheme.FindLegacyRule(TlsSignatureScheme.RsaEncryptionOid)!.Kind);
        Assert.AreEqual(HashAlgorithmName.SHA1, TlsSignatureScheme.FindLegacyRule(TlsSignatureScheme.EcPublicKeyOid)!.Hash);
        Assert.AreEqual(new TlsSignatureRule(TlsSignatureKind.Dsa, TlsSignatureScheme.DsaOid, null, HashAlgorithmName.SHA1), TlsSignatureScheme.FindLegacyRule(TlsSignatureScheme.DsaOid));
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

        Assert.IsTrue(TlsSignatureScheme.IsTls12Scheme((ushort)scheme));
        Assert.IsFalse(TlsSignatureScheme.IsCertificateVerifyScheme((ushort)scheme));
        Assert.AreEqual(new TlsSignatureRule(TlsSignatureKind.Dsa, TlsSignatureScheme.DsaOid, null, new HashAlgorithmName(hash)), rule);
        Assert.IsNull(key.VerifySignature(rule, Content, TestDsaKey.Sign(rule.Hash, Content)));
    }

    [TestMethod]
    [DataRow(TlsSignatureScheme.DsaSha256, "EACE8BDBBE353C432A795D9EC556C6D021F7A03F42C36E9BC87E4AC7932CC809", "7081E175455F9247B812B74583E9E94F9EA79BD640DC962533B0680793A38D53", DisplayName = "dsa_sha256")]
    [DataRow(0, "3A1B2DBD7489D6ED7E608FD036C83AF396E290DBD602408E8677DAABD6E7445A", "D26FCBA19FA3E3058FFC02CA1596CDBB6E0D20CB37B06054F7E36DED0CDBBCCF", DisplayName = "TLS 1.0 and 1.1, SHA-1")]
    public void ADsaKeyVerifiesRfc6979sPublishedSignatureAndRejectsAFlippedBit(int scheme, string r, string s)
    {
        TlsSignatureRule rule = scheme == 0 ? TlsSignatureScheme.FindLegacyRule(TlsSignatureScheme.DsaOid)! : TlsSignatureScheme.FindTls12Rule((ushort)scheme)!;
        byte[] signature = TestDsaKey.EncodeIntegers(Convert.FromHexString(r), Convert.FromHexString(s));
        TlsCertificatePublicKey key = DsaKey();

        Assert.IsNull(key.VerifySignature(rule, "sample"u8.ToArray(), signature));
        signature[^1] ^= 1;
        Assert.AreEqual(TlsAlertDescription.DecryptError, key.VerifySignature(rule, "sample"u8.ToArray(), signature));
    }

    [TestMethod]
    [DataRow(new byte[] { 1, 2, 3 }, DisplayName = "not DER")]
    [DataRow(new byte[] { 0x30, 0x06, 0x02, 0x01, 0x01, 0x02, 0x01, 0x01, 0x00 }, DisplayName = "a byte after the SEQUENCE")]
    [DataRow(new byte[] { 0x30, 0x09, 0x02, 0x01, 0x01, 0x02, 0x01, 0x01, 0x02, 0x01, 0x01 }, DisplayName = "a third INTEGER")]
    [DataRow(new byte[] { 0x30, 0x06, 0x02, 0x01, 0xff, 0x02, 0x01, 0x01 }, DisplayName = "a negative r")]
    [DataRow(new byte[] { 0x30, 0x06, 0x02, 0x01, 0x01, 0x02, 0x01, 0xff }, DisplayName = "a negative s")]
    [DataRow(new byte[] { 0x30, 0x07, 0x02, 0x02, 0x00, 0x01, 0x02, 0x01, 0x01 }, DisplayName = "an INTEGER not minimally encoded")]
    public void ADssSigValueThatDoesNotDecodeDoesNotVerify(byte[] signature) =>
        Assert.AreEqual(TlsAlertDescription.DecryptError, DsaKey().VerifySignature(TlsSignatureScheme.FindTls12Rule(TlsSignatureScheme.DsaSha256), Content, signature));

    [TestMethod]
    public void ADssSigValueWhoseRIsLongerThanQDoesNotVerify()
    {
        byte[] signature = TestDsaKey.EncodeIntegers([.. Enumerable.Repeat((byte)0x7f, 33)], [1]);

        Assert.AreEqual(TlsAlertDescription.DecryptError, DsaKey().VerifySignature(TlsSignatureScheme.FindTls12Rule(TlsSignatureScheme.DsaSha256), Content, signature));
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

        Assert.AreEqual(TlsAlertDescription.BadCertificate, key.VerifySignature(TlsSignatureScheme.FindTls12Rule(TlsSignatureScheme.DsaSha256), Content, TestDsaKey.Sign(HashAlgorithmName.SHA256, Content)));
    }

    [TestMethod]
    public void ADsaRuleDoesNotFitAnotherKeyNorAnotherRuleADsaKey()
    {
        TlsCertificatePublicKey rsa = TlsCertificatePublicKey.Read(TestServerCredential.Rsa(TlsSignatureScheme.RsaPkcs1Sha256).Certificate)!;

        Assert.AreEqual(TlsAlertDescription.IllegalParameter, rsa.VerifySignature(TlsSignatureScheme.FindTls12Rule(TlsSignatureScheme.DsaSha256), Content, [1]));
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, DsaKey().VerifySignature(TlsSignatureScheme.FindTls12Rule(TlsSignatureScheme.RsaPkcs1Sha256), Content, [1]));
    }

    [TestMethod]
    public void ARuleForAnotherKeyTypeIsIllegal()
    {
        TlsCertificatePublicKey key = TlsCertificatePublicKey.Read(TestServerCredential.Ed25519().Certificate)!;

        Assert.AreEqual(TlsAlertDescription.IllegalParameter, key.VerifySignature(TlsSignatureScheme.FindTls12Rule(TlsSignatureScheme.EcdsaSha1), Content, [1]));
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, key.VerifySignature((TlsSignatureRule?)null, Content, [1]));
    }

    [TestMethod]
    public void OnlyAnImportableRsaEncryptionKeyTakesThePreMasterSecret()
    {
        TestServerCredential rsa = TestServerCredential.Rsa(TlsSignatureScheme.RsaPkcs1Sha256);
        byte[] secret = new byte[48];

        byte[]? encrypted = TlsCertificatePublicKey.Read(rsa.Certificate)!.EncryptPkcs1(secret);

        CollectionAssert.AreEqual(secret, rsa.RsaKey!.Decrypt(encrypted!, RSAEncryptionPadding.Pkcs1));
        Assert.IsNull(TlsCertificatePublicKey.Read(TestServerCredential.Ed25519().Certificate)!.EncryptPkcs1(secret));
        Assert.IsNull(TlsCertificatePublicKey.Read(TestServerCredential.Foreign(TlsSignatureScheme.RsaEncryptionOid, [1, 2, 3]).Certificate)!.EncryptPkcs1(secret));
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
        Assert.HasCount(36, TlsPrf.Md5Sha1.HashHandshake(Content));
        CollectionAssert.AreEqual(SHA256.HashData(Content), TlsPrf.Sha256.HashHandshake(Content));
        CollectionAssert.AreEqual(SHA384.HashData(Content), TlsPrf.Sha384.HashHandshake(Content));
    }

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
