using Curl.Cryptography;

namespace Curl.Tls;

/// <summary>The hand-built <see cref="AeadAriaGcm" />, for the RFC 6209 GCM suites.</summary>
internal sealed class AriaGcmTls12Aead(byte[] key) : ITls12Aead
{
    private readonly AeadAriaGcm aead = new(key);

    public void Encrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> plaintext, Span<byte> ciphertext, Span<byte> tag, ReadOnlySpan<byte> associatedData) =>
        aead.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);

    public bool TryDecrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> tag, Span<byte> plaintext, ReadOnlySpan<byte> associatedData) =>
        aead.TryDecrypt(nonce, ciphertext, tag, plaintext, associatedData);

    public void Dispose() => aead.Dispose();
}
