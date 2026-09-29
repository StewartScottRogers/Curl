using Curl.Cryptography;

namespace Curl.Tls;

/// <summary>The hand-built <see cref="AeadChaCha20Poly1305" />, for the RFC 7905 suites.</summary>
internal sealed class ChaCha20Poly1305Tls12Aead(byte[] key) : ITls12Aead
{
    private readonly AeadChaCha20Poly1305 aead = new(key);

    public void Encrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> plaintext, Span<byte> ciphertext, Span<byte> tag, ReadOnlySpan<byte> associatedData) =>
        aead.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);

    public bool TryDecrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> tag, Span<byte> plaintext, ReadOnlySpan<byte> associatedData) =>
        aead.TryDecrypt(nonce, ciphertext, tag, plaintext, associatedData);

    public void Dispose() => aead.Dispose();
}
