using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>An ECDSA key that signs with the <c>ecdsa_*</c> scheme of its curve: P-256, P-384 or P-521.</summary>
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

    private protected override bool Fits(TlsSignatureRule rule) => rule.Kind == TlsSignatureKind.Ecdsa && rule.CurveOid == curveOid;

    private protected override byte[] Sign(TlsSignatureRule rule, byte[] content) =>
        key.SignData(content, rule.Hash, DSASignatureFormat.Rfc3279DerSequence);
}
