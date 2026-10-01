using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>
/// An ECDSA key on P-256, P-384 or P-521 that signs with the <c>ecdsa_*</c> scheme of its
/// curve, or in TLS 1.2 and below with any <c>ecdsa_*</c> hash, SHA-1 and SHA-224 included
/// (SHA-224 hashed by <c>Curl.Cryptography</c>, since the BCL does not compute it).
/// </summary>
public sealed class EcdsaTlsSigningKey : TlsSigningKey
{
    private readonly ECDsa key;
    private readonly string? curveOid;

    /// <summary>Creates the key.</summary>
    /// <param name="key">The ECDSA private key.</param>
    public EcdsaTlsSigningKey(ECDsa key)
    {
        ArgumentNullException.ThrowIfNull(key);
        this.key = key;
        curveOid = TlsCertificatePublicKey.ReadSubjectPublicKeyInfo(key.ExportSubjectPublicKeyInfo()).CurveOid;
    }

    private protected override bool Fits(TlsSignatureRule rule) =>
        rule.Kind == TlsSignatureKind.Ecdsa && (rule.CurveOid is null || rule.CurveOid == curveOid);

    private protected override byte[] Sign(TlsSignatureRule rule, byte[] content) =>
        key.SignHash(Cryptography.DsaSignature.HashData(content, rule.Hash), DSASignatureFormat.Rfc3279DerSequence);
}
