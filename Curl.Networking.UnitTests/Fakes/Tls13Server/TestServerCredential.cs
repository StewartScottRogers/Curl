using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Tls;
using X509CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace Curl.Networking.Fakes.Tls13Server;

/// <summary>
/// A generated certificate, its signing key and the signature scheme it signs with, for
/// <see cref="Tls13TestServer" />: a copy of <c>Curl.Tls.UnitTests</c>' credential with its
/// RSA and ECDSA certificates only, made by the BCL's <see cref="X509CertificateRequest" />.
/// </summary>
internal sealed record TestServerCredential(byte[] Certificate, TlsSigningKey SigningKey, ushort Scheme)
{
    public static TestServerCredential Rsa(ushort scheme)
    {
        RSA key = RSA.Create(2048);
        return new(SelfSigned(new X509CertificateRequest("CN=rsa", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)), new RsaTlsSigningKey(key), scheme);
    }

    public static TestServerCredential Ecdsa(ECCurve curve, ushort scheme)
    {
        ECDsa key = ECDsa.Create(curve);
        return new(SelfSigned(new X509CertificateRequest("CN=ecdsa", key, HashAlgorithmName.SHA256)), new EcdsaTlsSigningKey(key), scheme);
    }

    private static byte[] SelfSigned(X509CertificateRequest request)
    {
        using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        return certificate.RawData;
    }
}
