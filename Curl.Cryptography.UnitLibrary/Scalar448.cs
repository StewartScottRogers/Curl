using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// Arithmetic on Ed448 scalars modulo the prime group order L = 2^446 -
/// 13818066809895115352007386748515426880336692474882178609894547503885 (RFC 8032
/// section 5.2). A scalar is 57 little-endian bytes. Reduction and multiplication run on
/// <see cref="MontgomeryModulus" />, so they are constant-time in the scalars.
/// </summary>
internal static class Scalar448
{
    /// <summary>The number of bytes in a scalar's encoding.</summary>
    public const int EncodedLength = 57;

    /// <summary>The number of bytes in the SHAKE256 output a scalar is reduced from.</summary>
    public const int WideLength = 114;

    /// <summary>The 32-bit limbs of a residue: L is below 2^448.</summary>
    private const int LimbCount = 14;

    /// <summary>L, little-endian.</summary>
    private static ReadOnlySpan<byte> Order =>
    [
        0xF3, 0x44, 0x58, 0xAB, 0x92, 0xC2, 0x78, 0x23, 0x55, 0x8F, 0xC5, 0x8D, 0x72, 0xC2, 0x6C, 0x21,
        0x90, 0x36, 0xD6, 0xAE, 0x49, 0xDB, 0x4E, 0xC4, 0xE9, 0x23, 0xCA, 0x7C, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x3F, 0x00,
    ];

    /// <summary>The arithmetic modulo L.</summary>
    private static readonly MontgomeryModulus Modulus = CreateModulus();

    /// <summary>
    /// Sets <paramref name="result" /> to the 114-byte little-endian integer
    /// <paramref name="wide" /> modulo L.
    /// </summary>
    public static void Reduce(Span<byte> result, ReadOnlySpan<byte> wide)
    {
        Span<uint> limbs = stackalloc uint[(WideLength + 3) / 4];
        Span<uint> residue = stackalloc uint[LimbCount];
        try
        {
            ToLimbs(wide, limbs);
            Modulus.Reduce(limbs, residue);
            FromLimbs(residue, result);
        }
        finally
        {
            ClearLimbs(limbs);
            ClearLimbs(residue);
        }
    }

    /// <summary>
    /// Sets <paramref name="result" /> to (<paramref name="left" /> *
    /// <paramref name="right" /> + <paramref name="addend" />) modulo L, the
    /// S = (r + k * s) mod L of RFC 8032 section 5.2.6. <paramref name="left" /> is below
    /// 2^448; <paramref name="right" /> and <paramref name="addend" /> are below L. The last
    /// byte of each is zero and is not read.
    /// </summary>
    public static void MultiplyAdd(
        Span<byte> result,
        ReadOnlySpan<byte> left,
        ReadOnlySpan<byte> right,
        ReadOnlySpan<byte> addend)
    {
        Span<uint> limbs = stackalloc uint[4 * (LimbCount + 2)];
        Span<uint> leftLimbs = limbs[..LimbCount];
        Span<uint> rightLimbs = limbs.Slice(LimbCount, LimbCount);
        Span<uint> addendLimbs = limbs.Slice(2 * LimbCount, LimbCount);
        Span<uint> scratch = limbs[(3 * LimbCount)..];
        try
        {
            ToLimbs(left[..(EncodedLength - 1)], leftLimbs);
            ToLimbs(right[..(EncodedLength - 1)], rightLimbs);
            ToLimbs(addend[..(EncodedLength - 1)], addendLimbs);
            Modulus.MultiplyModulo(leftLimbs, leftLimbs, rightLimbs);
            Modulus.Add(leftLimbs, leftLimbs, addendLimbs, scratch);
            FromLimbs(leftLimbs, result);
        }
        finally
        {
            ClearLimbs(limbs);
        }
    }

    /// <summary>
    /// Returns whether the 57-byte little-endian integer <paramref name="scalar" /> is
    /// below L, the canonical-S check of RFC 8032 section 5.2.7. Reads public data only,
    /// so it stops at the first byte that differs.
    /// </summary>
    public static bool IsBelowOrder(ReadOnlySpan<byte> scalar)
    {
        for (int index = EncodedLength - 1; index >= 0; index--)
        {
            if (scalar[index] != Order[index])
            {
                return scalar[index] < Order[index];
            }
        }

        return false;
    }

    /// <summary>Reads little-endian bytes into 32-bit limbs, zero-extending.</summary>
    private static void ToLimbs(ReadOnlySpan<byte> littleEndian, Span<uint> limbs)
    {
        limbs.Clear();
        for (int index = 0; index < littleEndian.Length; index++)
        {
            limbs[index >> 2] |= (uint)littleEndian[index] << (8 * (index & 3));
        }
    }

    /// <summary>Writes 32-bit limbs as a 57-byte little-endian scalar.</summary>
    private static void FromLimbs(ReadOnlySpan<uint> limbs, Span<byte> littleEndian)
    {
        littleEndian[^1] = 0;
        for (int index = 0; index < EncodedLength - 1; index++)
        {
            littleEndian[index] = (byte)(limbs[index >> 2] >> (8 * (index & 3)));
        }
    }

    private static MontgomeryModulus CreateModulus()
    {
        byte[] bigEndian = Order[..(EncodedLength - 1)].ToArray();
        Array.Reverse(bigEndian);
        return new MontgomeryModulus(bigEndian);
    }

    private static void ClearLimbs(Span<uint> limbs) =>
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(limbs));
}
