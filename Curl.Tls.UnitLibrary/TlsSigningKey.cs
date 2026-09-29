namespace Curl.Tls;

/// <summary>
/// A private key that signs a TLS 1.3 CertificateVerify: the client certificate's key, or
/// in tests the in-memory server's.
/// </summary>
public abstract class TlsSigningKey
{
    /// <summary>Returns whether this key can sign with <paramref name="scheme" />.</summary>
    /// <param name="scheme">The signature scheme code point.</param>
    /// <returns><see langword="true" /> when the scheme fits the key's type, curve and padding.</returns>
    public bool CanSign(ushort scheme) => TlsSignatureScheme.FindRule(scheme) is { } rule && Fits(rule);

    /// <summary>Signs <paramref name="content" /> with <paramref name="scheme" />.</summary>
    /// <param name="scheme">A scheme <see cref="CanSign" /> accepts.</param>
    /// <param name="content">The content to sign.</param>
    /// <returns>The signature.</returns>
    /// <exception cref="ArgumentException">The key cannot sign with the scheme.</exception>
    public byte[] Sign(ushort scheme, byte[] content)
    {
        TlsSignatureRule? rule = TlsSignatureScheme.FindRule(scheme);
        return rule is not null && Fits(rule)
            ? Sign(rule, content)
            : throw new ArgumentException($"This key cannot sign with signature scheme 0x{scheme:x4}.", nameof(scheme));
    }

    private protected abstract bool Fits(TlsSignatureRule rule);

    private protected abstract byte[] Sign(TlsSignatureRule rule, byte[] content);
}
