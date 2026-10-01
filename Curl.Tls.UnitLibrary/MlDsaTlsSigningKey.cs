namespace Curl.Tls;

/// <summary>
/// An ML-DSA-44, ML-DSA-65 or ML-DSA-87 key that signs with the TLS 1.3 <c>mldsa*</c> scheme
/// of its own parameter set: pure ML-DSA with an empty context, hedged, by
/// <c>Curl.Cryptography</c>'s hand-built <see cref="Cryptography.MlDsa" />.
/// </summary>
public sealed class MlDsaTlsSigningKey : TlsSigningKey
{
    private readonly Cryptography.MlDsa key;
    private readonly string keyOid;

    /// <summary>Creates the key.</summary>
    /// <param name="key">The ML-DSA key pair; its parameter set picks the one scheme it signs with.</param>
    public MlDsaTlsSigningKey(Cryptography.MlDsa key)
    {
        ArgumentNullException.ThrowIfNull(key);
        this.key = key;
        keyOid = key.ParameterSet switch
        {
            Cryptography.MlDsaParameterSet.MlDsa44 => TlsSignatureScheme.MlDsa44Oid,
            Cryptography.MlDsaParameterSet.MlDsa65 => TlsSignatureScheme.MlDsa65Oid,
            _ => TlsSignatureScheme.MlDsa87Oid,
        };
    }

    private protected override bool Fits(TlsSignatureRule rule) => rule.Kind == TlsSignatureKind.MlDsa && rule.KeyOid == keyOid;

    private protected override byte[] Sign(TlsSignatureRule rule, byte[] content)
    {
        byte[] signature = new byte[Cryptography.MlDsa.GetSignatureSize(key.ParameterSet)];
        key.SignData(content, [], signature);
        return signature;
    }
}
