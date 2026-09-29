using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// HMAC (RFC 2104) with SHA-1, SHA-256 or SHA-384 over <c>header || data[..dataLength]</c>
/// where <c>dataLength</c> is secret, hashing the same number of compression-function blocks
/// whatever it is: the Lucky Thirteen countermeasure for MAC-then-encrypt CBC records
/// (OpenSSL's <c>ssl3_cbc_digest_record</c>). The MAC is the one
/// <see cref="HMAC" /> gives over the same bytes.
/// </summary>
/// <remarks>
/// Constant-time in the key, the header and data bytes, and <c>dataLength</c>: running time
/// depends only on the key's length, the header's length, <c>data.Length</c> and
/// <c>minimumDataLength</c>. The padded key and the inner hash are zeroed after use.
/// </remarks>
public static class FixedBlockHmac
{
    private const byte InnerPadByte = 0x36;

    private const byte OuterPadByte = 0x5c;

    /// <summary>
    /// Writes the HMAC of <c><paramref name="header" /> || <paramref name="data" />[..<paramref name="dataLength" />]</c>
    /// under <paramref name="key" /> to <paramref name="destination" />.
    /// </summary>
    /// <param name="hash">SHA1, SHA256 or SHA384.</param>
    /// <param name="key">The MAC key.</param>
    /// <param name="header">Bytes authenticated before the data; may be secret, its length is not.</param>
    /// <param name="data">The data at its longest.</param>
    /// <param name="dataLength">The secret length of the data authenticated, from <paramref name="minimumDataLength" /> to <c>data.Length</c>.</param>
    /// <param name="minimumDataLength">The public least <paramref name="dataLength" /> can be; the closer to <c>data.Length</c>, the fewer bytes are built with masks.</param>
    /// <param name="destination">Receives the MAC; exactly the hash's length.</param>
    /// <exception cref="ArgumentException"><paramref name="hash" /> is not SHA1, SHA256 or SHA384, or <paramref name="destination" /> is not the hash's length.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minimumDataLength" /> or <paramref name="dataLength" /> is outside its range.</exception>
    public static void Compute(HashAlgorithmName hash, ReadOnlySpan<byte> key, ReadOnlySpan<byte> header, ReadOnlySpan<byte> data, int dataLength, int minimumDataLength, Span<byte> destination)
    {
        if (hash == HashAlgorithmName.SHA1)
        {
            Compute<Sha1>(key, header, data, dataLength, minimumDataLength, destination);
        }
        else if (hash == HashAlgorithmName.SHA256)
        {
            Compute<Sha256>(key, header, data, dataLength, minimumDataLength, destination);
        }
        else if (hash == HashAlgorithmName.SHA384)
        {
            Compute<Sha384>(key, header, data, dataLength, minimumDataLength, destination);
        }
        else
        {
            throw new ArgumentException($"A fixed-block HMAC is built on SHA1, SHA256 or SHA384, not {hash.Name}.", nameof(hash));
        }
    }

    private static void Compute<TCompression>(ReadOnlySpan<byte> key, ReadOnlySpan<byte> header, ReadOnlySpan<byte> data, int dataLength, int minimumDataLength, Span<byte> destination)
        where TCompression : struct, IBigEndianCompressionFunction
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimumDataLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minimumDataLength, data.Length);
        if ((uint)(dataLength - minimumDataLength) > (uint)(data.Length - minimumDataLength))
        {
            throw new ArgumentOutOfRangeException(nameof(dataLength), "The data length must lie between the minimum and the data's length.");
        }

        if (destination.Length != TCompression.HashSize)
        {
            throw new ArgumentException($"This HMAC needs a {TCompression.HashSize}-byte destination; this is {destination.Length}.", nameof(destination));
        }

        Span<byte> pad = stackalloc byte[TCompression.BlockSize];
        Span<byte> innerHash = stackalloc byte[TCompression.HashSize];
        try
        {
            WritePaddedKey<TCompression>(key, pad);
            Xor(pad, InnerPadByte);
            FixedBlockMerkleDamgard<TCompression>.Hash(pad, header, data, dataLength, minimumDataLength, innerHash);
            Xor(pad, InnerPadByte ^ OuterPadByte);
            FixedBlockMerkleDamgard<TCompression>.Hash(pad, [], innerHash, innerHash.Length, innerHash.Length, destination);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pad);
            CryptographicOperations.ZeroMemory(innerHash);
        }
    }

    /// <summary>Writes the key, or its hash when it is longer than a block, zero-padded to a block.</summary>
    private static void WritePaddedKey<TCompression>(ReadOnlySpan<byte> key, Span<byte> pad)
        where TCompression : struct, IBigEndianCompressionFunction
    {
        if (key.Length > pad.Length)
        {
            FixedBlockMerkleDamgard<TCompression>.Hash([], [], key, key.Length, key.Length, pad[..TCompression.HashSize]);
        }
        else
        {
            key.CopyTo(pad);
        }
    }

    private static void Xor(Span<byte> pad, int value)
    {
        for (int index = 0; index < pad.Length; index++)
        {
            pad[index] ^= (byte)value;
        }
    }
}
