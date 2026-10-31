using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Tls;

namespace Curl.Extension.Protocol.Coap.Dtls.Handshake;

/// <summary>
/// Verifies the server's signature over its ECDHE parameters (RFC 5246 section 7.4.3, RFC 4492 section
/// 5.4) with the public key of the leaf certificate it sent.
/// </summary>
internal static class DtlsServerKeyExchangeSignature
{
    /// <summary>Gets the signature schemes the client offers and verifies, in preference order.</summary>
    public static IReadOnlyList<ushort> Schemes { get; } =
        [TlsSignatureScheme.EcdsaSecp256r1Sha256, TlsSignatureScheme.RsaPssRsaeSha256, TlsSignatureScheme.RsaPkcs1Sha256];

    /// <summary>Checks <paramref name="signature" /> over <paramref name="signedContent" /> under <paramref name="scheme" />.</summary>
    /// <returns><see langword="true" /> only for a valid signature in one of <see cref="Schemes" /> by the leaf certificate's key.</returns>
    public static bool Verify(byte[] leafCertificate, ushort scheme, byte[] signedContent, byte[] signature)
    {
        try
        {
            using X509Certificate2 certificate = X509CertificateLoader.LoadCertificate(leafCertificate);
            return scheme switch
            {
                TlsSignatureScheme.EcdsaSecp256r1Sha256 => VerifyEcdsa(certificate, signedContent, signature),
                TlsSignatureScheme.RsaPssRsaeSha256 => VerifyRsa(certificate, signedContent, signature, RSASignaturePadding.Pss),
                TlsSignatureScheme.RsaPkcs1Sha256 => VerifyRsa(certificate, signedContent, signature, RSASignaturePadding.Pkcs1),
                _ => false,
            };
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static bool VerifyEcdsa(X509Certificate2 certificate, byte[] signedContent, byte[] signature)
    {
        using ECDsa? key = certificate.GetECDsaPublicKey();
        return key is not null && key.VerifyData(signedContent, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
    }

    private static bool VerifyRsa(X509Certificate2 certificate, byte[] signedContent, byte[] signature, RSASignaturePadding padding)
    {
        using RSA? key = certificate.GetRSAPublicKey();
        return key is not null && key.VerifyData(signedContent, signature, HashAlgorithmName.SHA256, padding);
    }
}
