using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Tls;
using CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace Curl.Extended.Protocol.Coap.Dtls.Handshake;

[TestClass]
public sealed class DtlsServerKeyExchangeSignatureTests
{
    private static readonly byte[] Content = [1, 2, 3, 4];

    [TestMethod]
    public void Verify_Rfc4492Section5_4_EcdsaSignatureByTheLeaf_IsValid()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] signature = key.SignData(Content, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        bool valid = DtlsServerKeyExchangeSignature.Verify(CertificateOf(key), TlsSignatureScheme.EcdsaSecp256r1Sha256, Content, signature);

        Assert.IsTrue(valid);
    }

    [TestMethod]
    [DataRow(TlsSignatureScheme.RsaPssRsaeSha256)]
    [DataRow(TlsSignatureScheme.RsaPkcs1Sha256)]
    public void Verify_Rfc5246Section7_4_3_RsaSignatureByTheLeaf_IsValid(ushort scheme)
    {
        using RSA key = RSA.Create(2048);
        RSASignaturePadding padding = scheme == TlsSignatureScheme.RsaPssRsaeSha256 ? RSASignaturePadding.Pss : RSASignaturePadding.Pkcs1;
        byte[] signature = key.SignData(Content, HashAlgorithmName.SHA256, padding);

        bool valid = DtlsServerKeyExchangeSignature.Verify(CertificateOf(key), scheme, Content, signature);

        Assert.IsTrue(valid);
    }

    [TestMethod]
    public void Verify_EcdsaSchemeWithAnRsaLeaf_IsInvalid()
    {
        using RSA key = RSA.Create(2048);

        bool valid = DtlsServerKeyExchangeSignature.Verify(CertificateOf(key), TlsSignatureScheme.EcdsaSecp256r1Sha256, Content, [0x30, 0]);

        Assert.IsFalse(valid);
    }

    [TestMethod]
    public void Verify_RsaSchemeWithAnEcdsaLeaf_IsInvalid()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        bool valid = DtlsServerKeyExchangeSignature.Verify(CertificateOf(key), TlsSignatureScheme.RsaPssRsaeSha256, Content, new byte[256]);

        Assert.IsFalse(valid);
    }

    [TestMethod]
    public void Verify_SchemeTheClientDidNotOffer_IsInvalid()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] signature = key.SignData(Content, HashAlgorithmName.SHA384, DSASignatureFormat.Rfc3279DerSequence);

        bool valid = DtlsServerKeyExchangeSignature.Verify(CertificateOf(key), TlsSignatureScheme.EcdsaSecp384r1Sha384, Content, signature);

        Assert.IsFalse(valid);
    }

    [TestMethod]
    public void Verify_LeafThatIsNotACertificate_IsInvalid()
    {
        bool valid = DtlsServerKeyExchangeSignature.Verify([0x30, 0x03, 1, 2, 3], TlsSignatureScheme.EcdsaSecp256r1Sha256, Content, [0x30, 0]);

        Assert.IsFalse(valid);
    }

    private static byte[] CertificateOf(ECDsa key)
    {
        using X509Certificate2 certificate = new CertificateRequest("CN=signer", key, HashAlgorithmName.SHA256).CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return certificate.RawData;
    }

    private static byte[] CertificateOf(RSA key)
    {
        using X509Certificate2 certificate = new CertificateRequest("CN=signer", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return certificate.RawData;
    }
}
