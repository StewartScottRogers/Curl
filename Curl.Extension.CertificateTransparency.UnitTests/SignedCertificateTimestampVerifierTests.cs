using System.Security.Cryptography;

namespace Curl.Extension.CertificateTransparency.UnitTests;

[TestClass]
public sealed class SignedCertificateTimestampVerifierTests
{
    private static readonly DateTimeOffset Now = CertificateTransparencyChain.Logged.AddHours(1);

    [TestMethod]
    public void RefusalOf_Rfc6962Section3_3_EmbeddedEcdsaTimestampFromAKnownLog_LetsTheChainStand()
    {
        using var chain = new CertificateTransparencyChain();
        var leaf = chain.LeafWith(CertificateTransparencyChain.ListOf(chain.EcdsaTimestamp(CertificateTransparencyChain.Logged)));

        var refusal = SignedCertificateTimestampVerifier.RefusalOf([leaf, chain.IssuerDer], KeysOf(chain.LogKey.ExportSubjectPublicKeyInfo()), Now);

        Assert.IsNull(refusal);
    }

    [TestMethod]
    public void RefusalOf_Rfc9162Section8_1_2_ExtensionMarkedCriticalIsStillRemovedFromTheTbs_LetsTheChainStand()
    {
        using var chain = new CertificateTransparencyChain();
        var leaf = chain.LeafWith(CertificateTransparencyChain.ListOf(chain.EcdsaTimestamp(CertificateTransparencyChain.Logged)), critical: true);

        var refusal = SignedCertificateTimestampVerifier.RefusalOf([leaf, chain.IssuerDer], KeysOf(chain.LogKey.ExportSubjectPublicKeyInfo()), Now);

        Assert.IsNull(refusal);
    }

    [TestMethod]
    public void RefusalOf_Rfc6962Section3_2_RsaLogSignature_LetsTheChainStand()
    {
        using var chain = new CertificateTransparencyChain();
        using var rsa = RSA.Create(2048);
        var rsaKey = rsa.ExportSubjectPublicKeyInfo();
        var timestamp = chain.Timestamp(data => rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1), rsaKey, CertificateTransparencyChain.Logged, signatureAlgorithm: 1);
        var leaf = chain.LeafWith(CertificateTransparencyChain.ListOf(timestamp));

        var refusal = SignedCertificateTimestampVerifier.RefusalOf([leaf, chain.IssuerDer], KeysOf(rsaKey), Now);

        Assert.IsNull(refusal);
    }

    [TestMethod]
    public void RefusalOf_Rfc6962Section5_2_OneInvalidAndOneValidTimestamp_LetsTheChainStand()
    {
        using var chain = new CertificateTransparencyChain();
        using var unknownLog = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var unknown = chain.Timestamp(data => unknownLog.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence), unknownLog.ExportSubjectPublicKeyInfo(), CertificateTransparencyChain.Logged);
        var leaf = chain.LeafWith(CertificateTransparencyChain.ListOf(unknown, chain.EcdsaTimestamp(CertificateTransparencyChain.Logged)));

        var refusal = SignedCertificateTimestampVerifier.RefusalOf([leaf, chain.IssuerDer], KeysOf(chain.LogKey.ExportSubjectPublicKeyInfo()), Now);

        Assert.IsNull(refusal);
    }

    [TestMethod]
    public void RefusalOf_Rfc6962Section5_2_TimestampFromALogNotInTheList_IsRefused()
    {
        using var chain = new CertificateTransparencyChain();
        using var otherLog = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var leaf = chain.LeafWith(CertificateTransparencyChain.ListOf(chain.EcdsaTimestamp(CertificateTransparencyChain.Logged)));

        var refusal = SignedCertificateTimestampVerifier.RefusalOf([leaf, chain.IssuerDer], KeysOf(otherLog.ExportSubjectPublicKeyInfo()), Now);

        Assert.AreEqual(SignedCertificateTimestampVerifier.NoValidTimestamp, refusal);
    }

    [TestMethod]
    public void RefusalOf_Rfc9162Section8_1_3_TimestampLaterThanNow_IsRefused()
    {
        using var chain = new CertificateTransparencyChain();
        var leaf = chain.LeafWith(CertificateTransparencyChain.ListOf(chain.EcdsaTimestamp(Now.AddMinutes(1))));

        var refusal = SignedCertificateTimestampVerifier.RefusalOf([leaf, chain.IssuerDer], KeysOf(chain.LogKey.ExportSubjectPublicKeyInfo()), Now);

        Assert.AreEqual(SignedCertificateTimestampVerifier.NoValidTimestamp, refusal);
    }

    [TestMethod]
    public void RefusalOf_Rfc6962Section3_2_SignatureOverADifferentTbs_IsRefused()
    {
        using var chain = new CertificateTransparencyChain();
        var otherTbs = chain.PrecertificateTbs();
        otherTbs[^1] ^= 1;
        var timestamp = chain.Timestamp(data => chain.LogKey.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence), chain.LogKey.ExportSubjectPublicKeyInfo(), CertificateTransparencyChain.Logged, tbs: otherTbs);
        var leaf = chain.LeafWith(CertificateTransparencyChain.ListOf(timestamp));

        var refusal = SignedCertificateTimestampVerifier.RefusalOf([leaf, chain.IssuerDer], KeysOf(chain.LogKey.ExportSubjectPublicKeyInfo()), Now);

        Assert.AreEqual(SignedCertificateTimestampVerifier.NoValidTimestamp, refusal);
    }

    [TestMethod]
    public void RefusalOf_Rfc6962Section3_2_SignedByAnotherIssuer_IsRefused()
    {
        using var chain = new CertificateTransparencyChain();
        using var otherChain = new CertificateTransparencyChain();
        var leaf = chain.LeafWith(CertificateTransparencyChain.ListOf(chain.EcdsaTimestamp(CertificateTransparencyChain.Logged)));

        var refusal = SignedCertificateTimestampVerifier.RefusalOf([leaf, otherChain.IssuerDer], KeysOf(chain.LogKey.ExportSubjectPublicKeyInfo()), Now);

        Assert.AreEqual(SignedCertificateTimestampVerifier.NoValidTimestamp, refusal);
    }

    [TestMethod]
    public void RefusalOf_Rfc6962Section3_2_UnknownVersionIsSkipped_IsRefused()
    {
        using var chain = new CertificateTransparencyChain();
        var timestamp = chain.Timestamp(data => chain.LogKey.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence), chain.LogKey.ExportSubjectPublicKeyInfo(), CertificateTransparencyChain.Logged, version: 1);
        var leaf = chain.LeafWith(CertificateTransparencyChain.ListOf(timestamp));

        var refusal = SignedCertificateTimestampVerifier.RefusalOf([leaf, chain.IssuerDer], KeysOf(chain.LogKey.ExportSubjectPublicKeyInfo()), Now);

        Assert.AreEqual(SignedCertificateTimestampVerifier.NoValidTimestamp, refusal);
    }

    [TestMethod]
    public void RefusalOf_Rfc6962Section3_3_NoEmbeddedList_IsRefused()
    {
        using var chain = new CertificateTransparencyChain();

        var refusal = SignedCertificateTimestampVerifier.RefusalOf([chain.LeafWithout(), chain.IssuerDer], KeysOf(chain.LogKey.ExportSubjectPublicKeyInfo()), Now);

        Assert.AreEqual(SignedCertificateTimestampVerifier.NoValidTimestamp, refusal);
    }

    [TestMethod]
    public void RefusalOf_Rfc6962Section3_2_NoIssuerCertificate_IsRefused()
    {
        using var chain = new CertificateTransparencyChain();
        var leaf = chain.LeafWith(CertificateTransparencyChain.ListOf(chain.EcdsaTimestamp(CertificateTransparencyChain.Logged)));

        var refusal = SignedCertificateTimestampVerifier.RefusalOf([leaf], KeysOf(chain.LogKey.ExportSubjectPublicKeyInfo()), Now);

        Assert.AreEqual(SignedCertificateTimestampVerifier.NoIssuer, refusal);
    }

    [TestMethod]
    public void RefusalOf_Rfc6962Section3_3_TruncatedList_IsRefusedAsMalformed()
    {
        using var chain = new CertificateTransparencyChain();
        var leaf = chain.LeafWith([0x00, 0x10, 0x00]);

        var refusal = SignedCertificateTimestampVerifier.RefusalOf([leaf, chain.IssuerDer], KeysOf(chain.LogKey.ExportSubjectPublicKeyInfo()), Now);

        Assert.AreEqual(SignedCertificateTimestampVerifier.Malformed, refusal);
    }

    [TestMethod]
    public void RefusalOf_CertificateIsNotDer_IsRefusedAsMalformed()
    {
        using var chain = new CertificateTransparencyChain();

        var refusal = SignedCertificateTimestampVerifier.RefusalOf([new byte[] { 0x04, 0x01 }, chain.IssuerDer], KeysOf(chain.LogKey.ExportSubjectPublicKeyInfo()), Now);

        Assert.AreEqual(SignedCertificateTimestampVerifier.Malformed, refusal);
    }

    [TestMethod]
    public void RefusalOf_IssuerIsNotACertificate_IsRefusedAsMalformed()
    {
        using var chain = new CertificateTransparencyChain();
        var leaf = chain.LeafWith(CertificateTransparencyChain.ListOf(chain.EcdsaTimestamp(CertificateTransparencyChain.Logged)));

        var refusal = SignedCertificateTimestampVerifier.RefusalOf([leaf, new byte[] { 0x30, 0x00 }], KeysOf(chain.LogKey.ExportSubjectPublicKeyInfo()), Now);

        Assert.AreEqual(SignedCertificateTimestampVerifier.Malformed, refusal);
    }

    [TestMethod]
    public void SignatureVerifies_Rfc6962Section3_2_HashOtherThanSha256_IsFalse()
    {
        var timestamp = new SignedCertificateTimestamp(new byte[32], 0, Array.Empty<byte>(), 2, SignedCertificateTimestampVerifier.Ecdsa, Array.Empty<byte>());

        Assert.IsFalse(SignedCertificateTimestampVerifier.SignatureVerifies([], timestamp, []));
    }

    [TestMethod]
    public void SignatureVerifies_Rfc6962Section3_2_SignatureAlgorithmOtherThanRsaOrEcdsa_IsFalse()
    {
        var timestamp = new SignedCertificateTimestamp(new byte[32], 0, Array.Empty<byte>(), SignedCertificateTimestampVerifier.Sha256, 2, Array.Empty<byte>());

        Assert.IsFalse(SignedCertificateTimestampVerifier.SignatureVerifies([], timestamp, []));
    }

    [TestMethod]
    public void SignatureVerifies_LogKeyOfAnotherAlgorithm_IsFalse()
    {
        using var rsa = RSA.Create(2048);
        var timestamp = new SignedCertificateTimestamp(new byte[32], 0, Array.Empty<byte>(), SignedCertificateTimestampVerifier.Sha256, SignedCertificateTimestampVerifier.Ecdsa, new byte[] { 1 });

        Assert.IsFalse(SignedCertificateTimestampVerifier.SignatureVerifies(rsa.ExportSubjectPublicKeyInfo(), timestamp, [1, 2]));
    }

    private static Dictionary<string, byte[]> KeysOf(byte[] publicKey) =>
        new(StringComparer.Ordinal) { [Convert.ToHexString(CertificateTransparencyChain.LogIdOf(publicKey))] = publicKey };
}
