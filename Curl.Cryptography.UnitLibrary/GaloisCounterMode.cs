using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// Galois/Counter Mode (NIST SP 800-38D) over any block cipher with 16-byte blocks: a
/// 96-bit nonce, so the pre-counter block J0 is the nonce then <c>00000001</c>; counter
/// mode from inc32(J0); and a 16-byte tag, E(J0) XOR GHASH over the associated data, the
/// ciphertext and both bit lengths. <see cref="AeadAriaGcm" /> runs it over
/// <see cref="Aria" />.
/// </summary>
/// <remarks>
/// GHASH is constant-time: each GF(2^128) multiplication runs all 128 bits with masks and
/// no table. The received tag is compared with
/// <see cref="CryptographicOperations.FixedTimeEquals" />. The hash subkey H is zeroed by
/// <see cref="Dispose" />; the block cipher stays the caller's to dispose.
/// </remarks>
internal sealed class GaloisCounterMode : IDisposable
{
    /// <summary>The length in bytes of a nonce.</summary>
    internal const int NonceSize = 12;

    /// <summary>The length in bytes of a tag.</summary>
    internal const int TagSize = 16;

    private const int BlockSize = 16;

    // NIST SP 800-38D section 6.3, R = 11100001 || 0^120.
    private static readonly UInt128 Reduction = new(0xE100000000000000UL, 0);

    private readonly IBlockCipher cipher;

    private UInt128 hashSubkey;

    /// <summary>Computes the hash subkey H, the encryption of the all-zero block, with <paramref name="cipher" />.</summary>
    internal GaloisCounterMode(IBlockCipher cipher)
    {
        this.cipher = cipher;
        Span<byte> block = stackalloc byte[BlockSize];
        try
        {
            block.Clear();
            cipher.EncryptBlock(block, block);
            hashSubkey = BinaryPrimitives.ReadUInt128BigEndian(block);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(block);
        }
    }

    /// <summary>
    /// Encrypts <paramref name="plaintext" /> into <paramref name="ciphertext" />, which may
    /// be <paramref name="plaintext" /> itself, and writes the tag into
    /// <paramref name="tag" />. Lengths are the caller's to check.
    /// </summary>
    internal void Encrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> plaintext, Span<byte> ciphertext, Span<byte> tag, ReadOnlySpan<byte> associatedData)
    {
        ApplyKeyStream(nonce, plaintext, ciphertext);
        ComputeTag(nonce, associatedData, ciphertext, tag);
    }

    /// <summary>
    /// Checks <paramref name="tag" /> in fixed time and, only when it matches, decrypts
    /// <paramref name="ciphertext" /> into <paramref name="plaintext" />, which may be
    /// <paramref name="ciphertext" /> itself.
    /// </summary>
    /// <returns><c>false</c>, with <paramref name="plaintext" /> all zero, when the tag does not match.</returns>
    internal bool TryDecrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> tag, Span<byte> plaintext, ReadOnlySpan<byte> associatedData)
    {
        Span<byte> expectedTag = stackalloc byte[TagSize];
        try
        {
            ComputeTag(nonce, associatedData, ciphertext, expectedTag);
            if (!CryptographicOperations.FixedTimeEquals(expectedTag, tag))
            {
                CryptographicOperations.ZeroMemory(plaintext);
                return false;
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expectedTag);
        }

        ApplyKeyStream(nonce, ciphertext, plaintext);
        return true;
    }

    /// <summary>Zeroes the hash subkey.</summary>
    public void Dispose() => hashSubkey = UInt128.Zero;

    /// <summary>
    /// NIST SP 800-38D algorithm 1: the product of <paramref name="x" /> and
    /// <paramref name="y" /> in GF(2^128), bit 0 the most significant, with no branch or
    /// index on either.
    /// </summary>
    internal static UInt128 Multiply(UInt128 x, UInt128 y)
    {
        UInt128 product = UInt128.Zero;
        UInt128 shifted = y;
        for (int bit = 127; bit >= 0; bit--)
        {
            product ^= shifted & MaskFromLowBit(x >> bit);
            shifted = (shifted >> 1) ^ (Reduction & MaskFromLowBit(shifted));
        }

        return product;
    }

    private static UInt128 MaskFromLowBit(UInt128 value)
    {
        ulong mask = 0UL - ((ulong)value & 1UL);
        return new UInt128(mask, mask);
    }

    /// <summary>GHASH over <paramref name="data" />, its last block zero-padded, continuing from <paramref name="state" />.</summary>
    private UInt128 Absorb(UInt128 state, ReadOnlySpan<byte> data)
    {
        Span<byte> block = stackalloc byte[BlockSize];
        try
        {
            for (int offset = 0; offset < data.Length; offset += BlockSize)
            {
                block.Clear();
                data[offset..Math.Min(offset + BlockSize, data.Length)].CopyTo(block);
                state = Multiply(state ^ BinaryPrimitives.ReadUInt128BigEndian(block), hashSubkey);
            }

            return state;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(block);
        }
    }

    /// <summary>NIST SP 800-38D algorithm 4 steps 5 and 6: GHASH, then XOR with E(J0).</summary>
    private void ComputeTag(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> associatedData, ReadOnlySpan<byte> ciphertext, Span<byte> tag)
    {
        Span<byte> block = stackalloc byte[BlockSize];
        try
        {
            UInt128 state = Absorb(UInt128.Zero, associatedData);
            state = Absorb(state, ciphertext);
            UInt128 lengths = new((ulong)associatedData.Length * 8, (ulong)ciphertext.Length * 8);
            state = Multiply(state ^ lengths, hashSubkey);
            WriteCounterBlock(nonce, 1, block);
            cipher.EncryptBlock(block, block);
            BinaryPrimitives.WriteUInt128BigEndian(tag, state ^ BinaryPrimitives.ReadUInt128BigEndian(block));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(block);
        }
    }

    /// <summary>NIST SP 800-38D algorithm 3, GCTR from inc32(J0): the counter is the last 32 bits and wraps.</summary>
    private void ApplyKeyStream(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> source, Span<byte> destination)
    {
        Span<byte> keyStream = stackalloc byte[BlockSize];
        try
        {
            uint counter = 2;
            for (int offset = 0; offset < source.Length; offset += BlockSize, counter++)
            {
                WriteCounterBlock(nonce, counter, keyStream);
                cipher.EncryptBlock(keyStream, keyStream);
                int length = Math.Min(BlockSize, source.Length - offset);
                for (int index = 0; index < length; index++)
                {
                    destination[offset + index] = (byte)(source[offset + index] ^ keyStream[index]);
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyStream);
        }
    }

    private static void WriteCounterBlock(ReadOnlySpan<byte> nonce, uint counter, Span<byte> block)
    {
        nonce.CopyTo(block);
        BinaryPrimitives.WriteUInt32BigEndian(block[NonceSize..], counter);
    }
}
