using System.Security.Cryptography;

namespace Curl.Quic;

/// <summary>The BCL's <see cref="AesGcm" /> with 16-byte tags, for <c>AEAD_AES_128_GCM</c> and <c>AEAD_AES_256_GCM</c>.</summary>
internal sealed class AesGcmQuicPacketAead(byte[] key) : IQuicPacketAead
{
    private readonly AesGcm aesGcm = new(key, QuicPacketProtection.TagLength);

    public void Encrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> plaintext, Span<byte> ciphertext, Span<byte> tag, ReadOnlySpan<byte> associatedData) =>
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);

    public bool TryDecrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> tag, Span<byte> plaintext, ReadOnlySpan<byte> associatedData)
    {
        try
        {
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);
            return true;
        }
        catch (AuthenticationTagMismatchException)
        {
            return false;
        }
    }

    public void Dispose() => aesGcm.Dispose();
}
