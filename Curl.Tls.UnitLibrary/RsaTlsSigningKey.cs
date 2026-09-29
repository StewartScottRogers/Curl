using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>
/// An RSA key that signs with RSA-PSS (the <c>rsa_pss_rsae_*</c> schemes, or
/// <c>rsa_pss_pss_*</c> for a key certified as RSASSA-PSS) or, in TLS 1.2, with PKCS #1
/// v1.5 (<c>rsa_pkcs1_*</c>). TLS 1.0 and 1.1's MD5 and SHA-1 signature, which carries no
/// DigestInfo, is not one the BCL can make, so this key cannot sign it.
/// </summary>
/// <param name="key">The RSA private key.</param>
/// <param name="certifiedAsPss"><see langword="true" /> when the certificate names the key <c>id-RSASSA-PSS</c> rather than <c>rsaEncryption</c>.</param>
public sealed class RsaTlsSigningKey(RSA key, bool certifiedAsPss = false) : TlsSigningKey
{
    private protected override bool Fits(TlsSignatureRule rule) =>
        rule.Kind != TlsSignatureKind.RsaMd5Sha1
        && rule.KeyOid == (certifiedAsPss ? TlsSignatureScheme.RsaSsaPssOid : TlsSignatureScheme.RsaEncryptionOid);

    private protected override byte[] Sign(TlsSignatureRule rule, byte[] content) =>
        key.SignData(content, rule.Hash, rule.Kind == TlsSignatureKind.RsaPkcs1 ? RSASignaturePadding.Pkcs1 : RSASignaturePadding.Pss);
}
