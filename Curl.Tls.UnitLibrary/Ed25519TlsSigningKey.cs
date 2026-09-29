namespace Curl.Tls;

/// <summary>An Ed25519 key that signs with the <c>ed25519</c> scheme.</summary>
public sealed class Ed25519TlsSigningKey : TlsSigningKey
{
    private readonly byte[] privateKey;

    /// <summary>Creates the key.</summary>
    /// <param name="privateKey">The 32-byte Ed25519 private key (the seed).</param>
    /// <exception cref="ArgumentException">The key is not 32 bytes.</exception>
    public Ed25519TlsSigningKey(byte[] privateKey)
    {
        ArgumentNullException.ThrowIfNull(privateKey);
        ArgumentOutOfRangeException.ThrowIfNotEqual(privateKey.Length, Cryptography.Ed25519.PrivateKeySize, nameof(privateKey));
        this.privateKey = [.. privateKey];
    }

    private protected override bool Fits(TlsSignatureRule rule) => rule.Kind == TlsSignatureKind.Ed25519;

    private protected override byte[] Sign(TlsSignatureRule rule, byte[] content)
    {
        byte[] signature = new byte[Cryptography.Ed25519.SignatureSize];
        Cryptography.Ed25519.Sign(privateKey, content, signature);
        return signature;
    }
}
