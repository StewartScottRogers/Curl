namespace Curl.Tls;

/// <summary>An AEAD with a 12-byte nonce and a 16-byte tag, as TLS 1.2's AEAD records use it.</summary>
internal interface ITls12Aead : IDisposable
{
    /// <summary>Encrypts <paramref name="plaintext" /> and writes the tag over <paramref name="associatedData" /> and the ciphertext.</summary>
    void Encrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> plaintext, Span<byte> ciphertext, Span<byte> tag, ReadOnlySpan<byte> associatedData);

    /// <summary>Decrypts <paramref name="ciphertext" /> when <paramref name="tag" /> matches; returns <c>false</c> otherwise.</summary>
    bool TryDecrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> tag, Span<byte> plaintext, ReadOnlySpan<byte> associatedData);
}
