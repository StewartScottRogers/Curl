using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>An RSA key that signs with RSA-PSS: the <c>rsa_pss_rsae_*</c> schemes, or <c>rsa_pss_pss_*</c> for a key certified as RSASSA-PSS.</summary>
/// <param name="key">The RSA private key.</param>
/// <param name="certifiedAsPss"><see langword="true" /> when the certificate names the key <c>id-RSASSA-PSS</c> rather than <c>rsaEncryption</c>.</param>
public sealed class RsaTlsSigningKey(RSA key, bool certifiedAsPss = false) : TlsSigningKey
{
    private protected override bool Fits(TlsSignatureRule rule) =>
        rule.KeyOid == (certifiedAsPss ? TlsSignatureScheme.RsaSsaPssOid : TlsSignatureScheme.RsaEncryptionOid);

    private protected override byte[] Sign(TlsSignatureRule rule, byte[] content) =>
        key.SignData(content, rule.Hash, RSASignaturePadding.Pss);
}
