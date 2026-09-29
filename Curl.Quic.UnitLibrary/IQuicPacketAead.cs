namespace Curl.Quic;

/// <summary>An AEAD with a 12-byte nonce and a 16-byte tag, as QUIC packet protection uses it (RFC 9001 section 5.3).</summary>
internal interface IQuicPacketAead : IDisposable
{
    /// <summary>Encrypts <paramref name="plaintext" /> and writes the tag over <paramref name="associatedData" /> and the ciphertext.</summary>
    void Encrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> plaintext, Span<byte> ciphertext, Span<byte> tag, ReadOnlySpan<byte> associatedData);

    /// <summary>Decrypts <paramref name="ciphertext" /> when <paramref name="tag" /> matches; returns <c>false</c> otherwise.</summary>
    bool TryDecrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> tag, Span<byte> plaintext, ReadOnlySpan<byte> associatedData);
}
