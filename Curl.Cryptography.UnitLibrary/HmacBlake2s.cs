using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// HMAC-BLAKE2s: RFC 2104's HMAC over unkeyed 32-byte <see cref="Blake2s" />, block size
/// <see cref="Blake2s.BlockSize" />. WireGuard's HMAC, and so its HKDF, is this
/// construction, distinct from BLAKE2s's own keyed mode.
/// </summary>
/// <remarks>
/// Constant-time: built on <see cref="Blake2s" />, with no branch on the key or the data
/// beyond the key's public length. The padded key and the inner digest are zeroed before
/// returning.
/// </remarks>
public static class HmacBlake2s
{
    /// <summary>The length in bytes of a MAC.</summary>
    public const int HashSize = Blake2s.HashSize;

    private const byte InnerPad = 0x36;

    private const byte OuterPad = 0x5C;

    /// <summary>
    /// Writes the HMAC-BLAKE2s of <paramref name="source" /> under <paramref name="key" />
    /// to <paramref name="destination" />. A key longer than a block is hashed first
    /// (RFC 2104 section 2).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="destination" /> is not <see cref="HashSize" /> bytes.</exception>
    public static void HashData(ReadOnlySpan<byte> key, ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (destination.Length != HashSize)
        {
            throw new ArgumentException($"HMAC-BLAKE2s writes {HashSize} bytes; the destination is {destination.Length}.", nameof(destination));
        }

        Span<byte> paddedKey = stackalloc byte[Blake2s.BlockSize];
        Span<byte> innerDigest = stackalloc byte[HashSize];
        using var hash = new Blake2s();
        try
        {
            paddedKey.Clear();
            if (key.Length > Blake2s.BlockSize)
            {
                Blake2s.HashData(key, paddedKey[..HashSize]);
            }
            else
            {
                key.CopyTo(paddedKey);
            }

            AppendPaddedKey(hash, paddedKey, InnerPad);
            hash.AppendData(source);
            hash.GetHashAndReset(innerDigest);
            AppendPaddedKey(hash, paddedKey, InnerPad ^ OuterPad);
            hash.AppendData(innerDigest);
            hash.GetHashAndReset(destination);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(paddedKey);
            CryptographicOperations.ZeroMemory(innerDigest);
        }
    }

    /// <summary>
    /// Exclusive-ors <paramref name="pad" /> into every byte of <paramref name="paddedKey" />
    /// in place and appends the result to <paramref name="hash" />.
    /// </summary>
    private static void AppendPaddedKey(Blake2s hash, Span<byte> paddedKey, int pad)
    {
        for (int index = 0; index < paddedKey.Length; index++)
        {
            paddedKey[index] ^= (byte)pad;
        }

        hash.AppendData(paddedKey);
    }
}
