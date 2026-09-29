using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using X509CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace Curl.Tls;

/// <summary>
/// A generated certificate, its signing key and the signature scheme it signs with, for
/// the in-memory server (and as a client certificate). RSA and ECDSA certificates come
/// from the BCL's <see cref="X509CertificateRequest" />; the RSA-PSS and Ed25519 keys, which
/// it cannot certify, get a certificate whose <c>SubjectPublicKeyInfo</c> is written here
/// and whose issuer signature is made with a throwaway ECDSA key - the client never
/// checks it, the verifier does.
/// </summary>
internal sealed record TestServerCredential(byte[] Certificate, TlsSigningKey SigningKey, ushort Scheme)
{
    /// <summary>Gets the RSA private key of an <see cref="Rsa" /> credential, for TLS 1.2 RSA key exchange and TLS 1.0 and 1.1 signatures.</summary>
    public RSA? RsaKey { get; init; }

    public static TestServerCredential Rsa(ushort scheme)
    {
        RSA key = RSA.Create(2048);
        return new(SelfSigned(new X509CertificateRequest("CN=rsa", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)), new RsaTlsSigningKey(key), scheme)
        {
            RsaKey = key,
        };
    }

    /// <summary>A certificate whose <c>SubjectPublicKeyInfo</c> says <paramref name="algorithmOid" /> over <paramref name="keyBits" />, with no usable private key.</summary>
    public static TestServerCredential Foreign(string algorithmOid, byte[] keyBits) =>
        new(WithForeignKey("CN=foreign", new PublicKey(new Oid(algorithmOid), null, new AsnEncodedData(keyBits))), new Ed25519TlsSigningKey(new byte[32]), TlsSignatureScheme.Ed25519);

    public static TestServerCredential RsaPss(ushort scheme)
    {
        RSA key = RSA.Create(2048);
        PublicKey publicKey = new(new Oid(TlsSignatureScheme.RsaSsaPssOid), null, new AsnEncodedData(key.ExportRSAPublicKey()));
        return new(WithForeignKey("CN=rsa-pss", publicKey), new RsaTlsSigningKey(key, certifiedAsPss: true), scheme);
    }

    public static TestServerCredential Ecdsa(ECCurve curve, ushort scheme)
    {
        ECDsa key = ECDsa.Create(curve);
        return new(SelfSigned(new X509CertificateRequest("CN=ecdsa", key, HashAlgorithmName.SHA256)), new EcdsaTlsSigningKey(key), scheme);
    }

    public static TestServerCredential Ed25519()
    {
        byte[] privateKey = RandomNumberGenerator.GetBytes(Cryptography.Ed25519.PrivateKeySize);
        byte[] rawPublicKey = new byte[Cryptography.Ed25519.PublicKeySize];
        Cryptography.Ed25519.ComputePublicKey(privateKey, rawPublicKey);
        PublicKey publicKey = new(new Oid(TlsSignatureScheme.Ed25519Oid), null, new AsnEncodedData(rawPublicKey));
        return new(WithForeignKey("CN=ed25519", publicKey), new Ed25519TlsSigningKey(privateKey), TlsSignatureScheme.Ed25519);
    }

    private static byte[] SelfSigned(X509CertificateRequest request)
    {
        using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        return certificate.RawData;
    }

    private static byte[] WithForeignKey(string subject, PublicKey publicKey)
    {
        using ECDsa issuerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        X500DistinguishedName name = new(subject);
        X509CertificateRequest request = new(name, publicKey, HashAlgorithmName.SHA256);
        using X509Certificate2 certificate = request.Create(
            name,
            X509SignatureGenerator.CreateForECDsa(issuerKey),
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(30),
            [1]);
        return certificate.RawData;
    }
}
