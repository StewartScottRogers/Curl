namespace Curl.Tls;

/// <summary>
/// An Ed448 key that signs with the <c>ed448</c> scheme (TLS 1.3, and TLS 1.2 by RFC 8422),
/// with an empty context, by <c>Curl.Cryptography</c>'s hand-built <see cref="Cryptography.Ed448" />.
/// </summary>
public sealed class Ed448TlsSigningKey : TlsSigningKey
{
    private readonly byte[] privateKey;

    /// <summary>Creates the key.</summary>
    /// <param name="privateKey">The 57-byte Ed448 private key (the seed).</param>
    /// <exception cref="ArgumentException">The key is not 57 bytes.</exception>
    public Ed448TlsSigningKey(byte[] privateKey)
    {
        ArgumentNullException.ThrowIfNull(privateKey);
        ArgumentOutOfRangeException.ThrowIfNotEqual(privateKey.Length, Cryptography.Ed448.PrivateKeySize, nameof(privateKey));
        this.privateKey = [.. privateKey];
    }

    private protected override bool Fits(TlsSignatureRule rule) => rule.Kind == TlsSignatureKind.Ed448;

    private protected override byte[] Sign(TlsSignatureRule rule, byte[] content)
    {
        byte[] signature = new byte[Cryptography.Ed448.SignatureSize];
        Cryptography.Ed448.Sign(privateKey, content, signature);
        return signature;
    }
}
