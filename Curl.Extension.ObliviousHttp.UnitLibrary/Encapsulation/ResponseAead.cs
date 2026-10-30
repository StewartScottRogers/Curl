using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Extension.ObliviousHttp.Encapsulation;

/// <summary>
/// The AEAD an encapsulated response is sealed with (RFC 9458 section 4.4): the request's HPKE
/// AEAD, keyed and nonced from the HPKE exporter rather than by HPKE's own key schedule.
/// </summary>
internal static class ResponseAead
{
    /// <summary>Nn, the nonce size of every supported AEAD.</summary>
    internal const int NonceSize = 12;

    /// <summary>The tag size of every supported AEAD.</summary>
    internal const int TagSize = HpkeContext.TagSize;

    /// <summary>Nk, 16 bytes for AES-128-GCM and 32 for AES-256-GCM and ChaCha20Poly1305.</summary>
    internal static int KeySize(HpkeAead aead) => aead == HpkeAead.Aes128Gcm ? 16 : 32;

    /// <summary>Opens <paramref name="ciphertext" /> (sealed text then tag) with empty associated data into <paramref name="plaintext" />.</summary>
    internal static bool TryOpen(HpkeAead aead, ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext, Span<byte> plaintext)
    {
        ReadOnlySpan<byte> sealedText = ciphertext[..^TagSize];
        ReadOnlySpan<byte> tag = ciphertext[^TagSize..];
        if (aead == HpkeAead.ChaCha20Poly1305)
        {
            using AeadChaCha20Poly1305 chaCha20Poly1305 = new(key);
            return chaCha20Poly1305.TryDecrypt(nonce, sealedText, tag, plaintext, []);
        }

        using AesGcm aesGcm = new(key, TagSize);
        try
        {
            aesGcm.Decrypt(nonce, sealedText, tag, plaintext);
            return true;
        }
        catch (AuthenticationTagMismatchException)
        {
            return false;
        }
    }
}
