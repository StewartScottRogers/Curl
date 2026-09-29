using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>
/// TLS 1.2 and below signatures: the TLS 1.2 schemes (PKCS #1 v1.5, ECDSA on any curve),
/// TLS 1.0 and 1.1's MD5 and SHA-1 RSA block and SHA-1 ECDSA, what each key can sign,
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
        Assert.IsFalse(TlsSignatureScheme.IsTls12Scheme(0x0202));
        Assert.IsFalse(TlsSignatureScheme.IsCertificateVerifyScheme(TlsSignatureScheme.RsaPkcs1Sha512));
    }

    [TestMethod]
    public void AnRsaKeySignsAndVerifiesPkcs1ButCannotSignTheLegacyBlock()
    {
        TestServerCredential credential = TestServerCredential.Rsa(TlsSignatureScheme.RsaPkcs1Sha256);
        TlsSignatureRule rule = TlsSignatureScheme.FindTls12Rule(TlsSignatureScheme.RsaPkcs1Sha384)!;
        TlsCertificatePublicKey key = TlsCertificatePublicKey.Read(credential.Certificate)!;

        byte[] signature = credential.SigningKey.SignByRule(rule, Content);

        Assert.IsNull(key.VerifySignature(rule, Content, signature));
        Assert.IsFalse(credential.SigningKey.CanSign(TlsSignatureScheme.LegacyRules[0]));
        Assert.IsFalse(credential.SigningKey.CanSign((TlsSignatureRule?)null));
        Assert.IsFalse(credential.SigningKey.CanSign(TlsSignatureScheme.RsaPkcs1Sha256));
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
    public void OnlyRsaAndEcdsaKeysHaveALegacySignature()
    {
        Assert.IsNull(TlsSignatureScheme.FindLegacyRule(TlsSignatureScheme.Ed25519Oid));
        Assert.AreEqual(TlsSignatureKind.RsaMd5Sha1, TlsSignatureScheme.FindLegacyRule(TlsSignatureScheme.RsaEncryptionOid)!.Kind);
        Assert.AreEqual(HashAlgorithmName.SHA1, TlsSignatureScheme.FindLegacyRule(TlsSignatureScheme.EcPublicKeyOid)!.Hash);
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
}
