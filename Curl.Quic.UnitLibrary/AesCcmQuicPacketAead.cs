using Curl.Cryptography;

namespace Curl.Quic;

/// <summary>The hand-built <see cref="AeadAesCcm" /> with 16-byte tags, for <c>AEAD_AES_128_CCM</c>.</summary>
internal sealed class AesCcmQuicPacketAead(byte[] key) : IQuicPacketAead
{
    private readonly AeadAesCcm aead = new(key);

    public void Encrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> plaintext, Span<byte> ciphertext, Span<byte> tag, ReadOnlySpan<byte> associatedData) =>
        aead.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);

    public bool TryDecrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> tag, Span<byte> plaintext, ReadOnlySpan<byte> associatedData) =>
        aead.TryDecrypt(nonce, ciphertext, tag, plaintext, associatedData);

    public void Dispose() => aead.Dispose();
}
