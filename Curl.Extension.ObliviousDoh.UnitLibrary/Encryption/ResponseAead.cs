using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Extension.ObliviousDoh.Encryption;

/// <summary>Opens a response sealed with the config's AEAD under the key and nonce RFC 9230 section 6.4 derives.</summary>
internal static class ResponseAead
{
    /// <summary>The nonce length <c>Nn</c> of every AEAD <see cref="Hpke" /> supports.</summary>
    internal const int NonceSize = 12;

    /// <summary>The tag length of every AEAD <see cref="Hpke" /> supports.</summary>
    internal const int TagSize = HpkeContext.TagSize;

    /// <summary>Gives the key length <c>Nk</c> of <paramref name="aead" />.</summary>
    /// <param name="aead">The AEAD.</param>
    /// <returns>16 for AES-128-GCM, 32 for the others.</returns>
    internal static int KeySize(HpkeAead aead) => aead == HpkeAead.Aes128Gcm ? 16 : 32;

    /// <summary>Opens <paramref name="ciphertext" /> (sealed text, then tag) into <paramref name="plaintext" />.</summary>
    /// <param name="aead">The AEAD.</param>
    /// <param name="key">The key.</param>
    /// <param name="nonce">The nonce.</param>
    /// <param name="associatedData">The associated data.</param>
    /// <param name="ciphertext">The sealed text and its tag.</param>
    /// <param name="plaintext">Receives the opened text; tag length shorter than <paramref name="ciphertext" />.</param>
    /// <returns><see langword="false" /> when the tag does not match.</returns>
    internal static bool TryOpen(HpkeAead aead, ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> associatedData, ReadOnlySpan<byte> ciphertext, Span<byte> plaintext)
    {
        ReadOnlySpan<byte> sealedText = ciphertext[..^TagSize];
        ReadOnlySpan<byte> tag = ciphertext[^TagSize..];
        if (aead == HpkeAead.ChaCha20Poly1305)
        {
            using AeadChaCha20Poly1305 chaCha20Poly1305 = new(key);
            return chaCha20Poly1305.TryDecrypt(nonce, sealedText, tag, plaintext, associatedData);
        }

        using AesGcm aesGcm = new(key, TagSize);
        try
        {
            aesGcm.Decrypt(nonce, sealedText, tag, plaintext, associatedData);
            return true;
        }
        catch (AuthenticationTagMismatchException)
        {
            return false;
        }
    }
}
