using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// Arithmetic in GF(2^255 - 19), the field X25519 (RFC 7748) and Ed25519 (RFC 8032) work
/// in. An element is <see cref="LimbCount" /> signed 64-bit limbs in radix 2^25.5 - 26
/// bits in even limbs and 25 in odd ones, least significant first (ref10's layout) - held
/// in a caller's <see cref="Span{T}" /> (usually a <c>stackalloc</c>), so no operation
/// allocates. Every limb product and every column sum fits in 64 bits, so a multiplication
/// is 100 plain 64-bit products and a squaring 55, with no 128-bit arithmetic (BL-1645).
/// A multiplication's inputs may be sums or differences of up to three carried elements
/// (Edwards25519's point addition uses that much); column sums then stay below 2^62.
/// Every member is constant-time: no branch, loop bound or index depends on an element's
/// value, and conditional moves are masks. Outputs may alias inputs.
/// </summary>
internal static class Field25519
{
    /// <summary>The number of limbs in one field element.</summary>
    public const int LimbCount = 10;

    /// <summary>The number of bytes in an element's little-endian encoding.</summary>
    public const int EncodedLength = 32;

    private const int EvenLimbBits = 26;

    private const int OddLimbBits = 25;

    private const long EvenLimbMask = (1L << EvenLimbBits) - 1;

    private const long OddLimbMask = (1L << OddLimbBits) - 1;

    private const int EncodedWords = EncodedLength / sizeof(ulong);

    /// <summary>Sets <paramref name="element" /> to the small value <paramref name="value" />.</summary>
    public static void SetSmall(Span<long> element, uint value)
    {
        element.Clear();
        element[0] = value & EvenLimbMask;
        element[1] = value >> EvenLimbBits;
    }

    /// <summary>
    /// Decodes a 32-byte little-endian value, ignoring its top bit (RFC 7748 section 5,
    /// <c>decodeUCoordinate</c>); a value of p or more is accepted and reduced as it is
    /// used.
    /// </summary>
    public static void Decode(Span<long> element, ReadOnlySpan<byte> encoded)
    {
        Span<ulong> words = stackalloc ulong[EncodedWords + 1];
        try
        {
            for (int index = 0; index < EncodedWords; index++)
            {
                words[index] = BinaryPrimitives.ReadUInt64LittleEndian(encoded[(sizeof(ulong) * index)..]);
            }

            words[EncodedWords] = 0;
            for (int index = 0; index < LimbCount; index++)
            {
                int start = LimbStart(index);
                int shift = start & 63;
                ulong bits = (words[start >> 6] >> shift) | ((words[(start >> 6) + 1] << (63 - shift)) << 1);
                element[index] = (long)bits & LimbMask(index);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(words));
        }
    }

    /// <summary>
    /// Encodes <paramref name="element" /> as the 32-byte little-endian encoding of its
    /// canonical value, fully reduced modulo p.
    /// </summary>
    public static void Encode(Span<byte> encoded, ReadOnlySpan<long> element)
    {
        Span<long> reduced = stackalloc long[LimbCount];
        Span<long> subtracted = stackalloc long[LimbCount];
        Span<ulong> words = stackalloc ulong[EncodedWords + 1];
        try
        {
            element.CopyTo(reduced);
            Carry(reduced);
            Carry(reduced);
            Carry(reduced);
            SubtractPrimeIfNotBelow(reduced, subtracted);
            SubtractPrimeIfNotBelow(reduced, subtracted);
            words.Clear();
            for (int index = 0; index < LimbCount; index++)
            {
                int start = LimbStart(index);
                int shift = start & 63;
                ulong limb = (ulong)reduced[index];
                words[start >> 6] |= limb << shift;
                words[(start >> 6) + 1] |= (limb >> (63 - shift)) >> 1;
            }

            for (int index = 0; index < EncodedWords; index++)
            {
                BinaryPrimitives.WriteUInt64LittleEndian(encoded[(sizeof(ulong) * index)..], words[index]);
            }
        }
        finally
        {
            Clear(reduced);
            Clear(subtracted);
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(words));
        }
    }

    /// <summary>Sets <paramref name="result" /> to <paramref name="left" /> + <paramref name="right" />.</summary>
    public static void Add(Span<long> result, ReadOnlySpan<long> left, ReadOnlySpan<long> right)
    {
        for (int index = 0; index < LimbCount; index++)
        {
            result[index] = left[index] + right[index];
        }
    }

    /// <summary>Sets <paramref name="result" /> to <paramref name="left" /> - <paramref name="right" />.</summary>
    public static void Subtract(Span<long> result, ReadOnlySpan<long> left, ReadOnlySpan<long> right)
    {
        for (int index = 0; index < LimbCount; index++)
        {
            result[index] = left[index] - right[index];
        }
    }

    /// <summary>
    /// Sets <paramref name="result" /> to <paramref name="left" /> * <paramref name="right" />,
    /// folding every product past 2^255 back in with 2^255 = 19 (mod p) and carrying. A
    /// product of two odd limbs lands half a bit above its column, so it is doubled.
    /// </summary>
    public static void Multiply(Span<long> result, ReadOnlySpan<long> left, ReadOnlySpan<long> right)
    {
        long f0 = left[0], f1 = left[1], f2 = left[2], f3 = left[3], f4 = left[4];
        long f5 = left[5], f6 = left[6], f7 = left[7], f8 = left[8], f9 = left[9];
        long g0 = right[0], g1 = right[1], g2 = right[2], g3 = right[3], g4 = right[4];
        long g5 = right[5], g6 = right[6], g7 = right[7], g8 = right[8], g9 = right[9];
        long f1Doubled = 2 * f1, f3Doubled = 2 * f3, f5Doubled = 2 * f5, f7Doubled = 2 * f7, f9Doubled = 2 * f9;
        long g1Folded = 19 * g1, g2Folded = 19 * g2, g3Folded = 19 * g3, g4Folded = 19 * g4, g5Folded = 19 * g5;
        long g6Folded = 19 * g6, g7Folded = 19 * g7, g8Folded = 19 * g8, g9Folded = 19 * g9;
        long h0 = (f0 * g0) + (f1Doubled * g9Folded) + (f2 * g8Folded) + (f3Doubled * g7Folded) + (f4 * g6Folded) + (f5Doubled * g5Folded) + (f6 * g4Folded) + (f7Doubled * g3Folded) + (f8 * g2Folded) + (f9Doubled * g1Folded);
        long h1 = (f0 * g1) + (f1 * g0) + (f2 * g9Folded) + (f3 * g8Folded) + (f4 * g7Folded) + (f5 * g6Folded) + (f6 * g5Folded) + (f7 * g4Folded) + (f8 * g3Folded) + (f9 * g2Folded);
        long h2 = (f0 * g2) + (f1Doubled * g1) + (f2 * g0) + (f3Doubled * g9Folded) + (f4 * g8Folded) + (f5Doubled * g7Folded) + (f6 * g6Folded) + (f7Doubled * g5Folded) + (f8 * g4Folded) + (f9Doubled * g3Folded);
        long h3 = (f0 * g3) + (f1 * g2) + (f2 * g1) + (f3 * g0) + (f4 * g9Folded) + (f5 * g8Folded) + (f6 * g7Folded) + (f7 * g6Folded) + (f8 * g5Folded) + (f9 * g4Folded);
        long h4 = (f0 * g4) + (f1Doubled * g3) + (f2 * g2) + (f3Doubled * g1) + (f4 * g0) + (f5Doubled * g9Folded) + (f6 * g8Folded) + (f7Doubled * g7Folded) + (f8 * g6Folded) + (f9Doubled * g5Folded);
        long h5 = (f0 * g5) + (f1 * g4) + (f2 * g3) + (f3 * g2) + (f4 * g1) + (f5 * g0) + (f6 * g9Folded) + (f7 * g8Folded) + (f8 * g7Folded) + (f9 * g6Folded);
        long h6 = (f0 * g6) + (f1Doubled * g5) + (f2 * g4) + (f3Doubled * g3) + (f4 * g2) + (f5Doubled * g1) + (f6 * g0) + (f7Doubled * g9Folded) + (f8 * g8Folded) + (f9Doubled * g7Folded);
        long h7 = (f0 * g7) + (f1 * g6) + (f2 * g5) + (f3 * g4) + (f4 * g3) + (f5 * g2) + (f6 * g1) + (f7 * g0) + (f8 * g9Folded) + (f9 * g8Folded);
        long h8 = (f0 * g8) + (f1Doubled * g7) + (f2 * g6) + (f3Doubled * g5) + (f4 * g4) + (f5Doubled * g3) + (f6 * g2) + (f7Doubled * g1) + (f8 * g0) + (f9Doubled * g9Folded);
        long h9 = (f0 * g9) + (f1 * g8) + (f2 * g7) + (f3 * g6) + (f4 * g5) + (f5 * g4) + (f6 * g3) + (f7 * g2) + (f8 * g1) + (f9 * g0);
        CarryProduct(result, h0, h1, h2, h3, h4, h5, h6, h7, h8, h9);
    }

    /// <summary>
    /// Sets <paramref name="result" /> to <paramref name="value" /> squared, forming each
    /// cross product once and doubling it.
    /// </summary>
    public static void Square(Span<long> result, ReadOnlySpan<long> value)
    {
        long f0 = value[0], f1 = value[1], f2 = value[2], f3 = value[3], f4 = value[4];
        long f5 = value[5], f6 = value[6], f7 = value[7], f8 = value[8], f9 = value[9];
        long f0Doubled = 2 * f0, f1Doubled = 2 * f1, f2Doubled = 2 * f2, f3Doubled = 2 * f3, f4Doubled = 2 * f4;
        long f5Doubled = 2 * f5, f6Doubled = 2 * f6, f7Doubled = 2 * f7, f8Doubled = 2 * f8, f9Doubled = 2 * f9;
        long f5Folded = 19 * f5, f6Folded = 19 * f6, f7Folded = 19 * f7, f8Folded = 19 * f8, f9Folded = 19 * f9;
        long f7FoldedDoubled = 38 * f7, f9FoldedDoubled = 38 * f9;
        long h0 = (f0 * f0) + (f1Doubled * f9FoldedDoubled) + (f2Doubled * f8Folded) + (f3Doubled * f7FoldedDoubled) + (f4Doubled * f6Folded) + (f5Doubled * f5Folded);
        long h1 = (f0Doubled * f1) + (f2Doubled * f9Folded) + (f3Doubled * f8Folded) + (f4Doubled * f7Folded) + (f5Doubled * f6Folded);
        long h2 = (f0Doubled * f2) + (f1Doubled * f1) + (f3Doubled * f9FoldedDoubled) + (f4Doubled * f8Folded) + (f5Doubled * f7FoldedDoubled) + (f6 * f6Folded);
        long h3 = (f0Doubled * f3) + (f1Doubled * f2) + (f4Doubled * f9Folded) + (f5Doubled * f8Folded) + (f6Doubled * f7Folded);
        long h4 = (f0Doubled * f4) + (f1Doubled * f3Doubled) + (f2 * f2) + (f5Doubled * f9FoldedDoubled) + (f6Doubled * f8Folded) + (f7Doubled * f7Folded);
        long h5 = (f0Doubled * f5) + (f1Doubled * f4) + (f2Doubled * f3) + (f6Doubled * f9Folded) + (f7Doubled * f8Folded);
        long h6 = (f0Doubled * f6) + (f1Doubled * f5Doubled) + (f2Doubled * f4) + (f3Doubled * f3) + (f7Doubled * f9FoldedDoubled) + (f8 * f8Folded);
        long h7 = (f0Doubled * f7) + (f1Doubled * f6) + (f2Doubled * f5) + (f3Doubled * f4) + (f8Doubled * f9Folded);
        long h8 = (f0Doubled * f8) + (f1Doubled * f7Doubled) + (f2Doubled * f6) + (f3Doubled * f5Doubled) + (f4 * f4) + (f9Doubled * f9Folded);
        long h9 = (f0Doubled * f9) + (f1Doubled * f8) + (f2Doubled * f7) + (f3Doubled * f6) + (f4Doubled * f5);
        CarryProduct(result, h0, h1, h2, h3, h4, h5, h6, h7, h8, h9);
    }

    /// <summary>
    /// Sets <paramref name="result" /> to <paramref name="value" /> * <paramref name="small" />,
    /// a public constant such as the ladder's a24: one product per limb, then a carry.
    /// </summary>
    public static void MultiplySmall(Span<long> result, ReadOnlySpan<long> value, uint small)
    {
        CarryProduct(
            result,
            value[0] * small,
            value[1] * small,
            value[2] * small,
            value[3] * small,
            value[4] * small,
            value[5] * small,
            value[6] * small,
            value[7] * small,
            value[8] * small,
            value[9] * small);
    }

    /// <summary>
    /// Sets <paramref name="result" /> to the inverse of <paramref name="value" />, computed
    /// as value^(p - 2) = value^(2^255 - 21) by a fixed addition chain of 254 squarings and
    /// 11 multiplications; zero maps to zero.
    /// </summary>
    public static void Invert(Span<long> result, ReadOnlySpan<long> value)
    {
        Span<long> scratch = stackalloc long[7 * LimbCount];
        Span<long> power2 = scratch[..LimbCount];
        Span<long> power11 = scratch.Slice(LimbCount, LimbCount);
        Span<long> ones5 = scratch.Slice(2 * LimbCount, LimbCount);
        Span<long> ones10 = scratch.Slice(3 * LimbCount, LimbCount);
        Span<long> ones50 = scratch.Slice(4 * LimbCount, LimbCount);
        Span<long> ones100 = scratch.Slice(5 * LimbCount, LimbCount);
        Span<long> power = scratch.Slice(6 * LimbCount, LimbCount);
        try
        {
            // onesN is value^(2^N - 1), an exponent of N one bits.
            Square(power2, value);
            SquareRepeatedly(power, power2, 2);
            Multiply(power, power, value);
            Multiply(power11, power, power2);
            Square(ones5, power11);
            Multiply(ones5, ones5, power);
            SquareRepeatedly(ones10, ones5, 5);
            Multiply(ones10, ones10, ones5);
            SquareRepeatedly(power, ones10, 10);
            Multiply(power, power, ones10);
            SquareRepeatedly(ones50, power, 20);
            Multiply(ones50, ones50, power);
            SquareRepeatedly(ones50, ones50, 10);
            Multiply(ones50, ones50, ones10);
            SquareRepeatedly(ones100, ones50, 50);
            Multiply(ones100, ones100, ones50);
            SquareRepeatedly(power, ones100, 100);
            Multiply(power, power, ones100);
            SquareRepeatedly(power, power, 50);
            Multiply(power, power, ones50);
            SquareRepeatedly(power, power, 5);
            Multiply(result, power, power11);
        }
        finally
        {
            Clear(scratch);
        }
    }

    /// <summary>Sets <paramref name="result" /> to -<paramref name="value" />.</summary>
    public static void Negate(Span<long> result, ReadOnlySpan<long> value)
    {
        for (int index = 0; index < LimbCount; index++)
        {
            result[index] = -value[index];
        }
    }

    /// <summary>
    /// Sets <paramref name="result" /> to <paramref name="value" /> raised to
    /// <paramref name="exponent" />, a little-endian integer, by square-and-multiply from
    /// its top bit. The chain branches on the exponent's bits, so the exponent must be a
    /// public constant (the square-root exponents of RFC 8032 section 5.1.3), never a
    /// secret; the value may be anything.
    /// </summary>
    public static void PowerByPublicExponent(Span<long> result, ReadOnlySpan<long> value, ReadOnlySpan<byte> exponent)
    {
        Span<long> power = stackalloc long[LimbCount];
        try
        {
            SetSmall(power, 1);
            for (int bit = (8 * exponent.Length) - 1; bit >= 0; bit--)
            {
                Square(power, power);
                if (((exponent[bit >> 3] >> (bit & 7)) & 1) != 0)
                {
                    Multiply(power, power, value);
                }
            }

            power.CopyTo(result);
        }
        finally
        {
            Clear(power);
        }
    }

    /// <summary>
    /// Returns whether <paramref name="left" /> and <paramref name="right" /> are the same
    /// field element, comparing their canonical encodings in fixed time.
    /// </summary>
    public static bool AreEqual(ReadOnlySpan<long> left, ReadOnlySpan<long> right)
    {
        Span<byte> leftEncoding = stackalloc byte[EncodedLength];
        Span<byte> rightEncoding = stackalloc byte[EncodedLength];
        try
        {
            Encode(leftEncoding, left);
            Encode(rightEncoding, right);
            return CryptographicOperations.FixedTimeEquals(leftEncoding, rightEncoding);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(leftEncoding);
            CryptographicOperations.ZeroMemory(rightEncoding);
        }
    }

    /// <summary>
    /// Returns the lowest bit of the canonical value of <paramref name="element" />
    /// (RFC 8032's "x mod 2", the sign of an x-coordinate), without a branch.
    /// </summary>
    public static uint Parity(ReadOnlySpan<long> element)
    {
        Span<byte> encoding = stackalloc byte[EncodedLength];
        try
        {
            Encode(encoding, element);
            return encoding[0] & 1u;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encoding);
        }
    }

    /// <summary>
    /// Swaps <paramref name="left" /> and <paramref name="right" /> when the lowest bit of
    /// <paramref name="bit" /> is <c>1</c>, by masking, touching every limb either way.
    /// </summary>
    public static void ConditionalSwap(Span<long> left, Span<long> right, uint bit)
    {
        long mask = -(long)(bit & 1u);
        for (int index = 0; index < LimbCount; index++)
        {
            long difference = mask & (left[index] ^ right[index]);
            left[index] ^= difference;
            right[index] ^= difference;
        }
    }

    /// <summary>Zeroes an element that held secret material.</summary>
    public static void Clear(Span<long> element) =>
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(element));

    /// <summary>The bit of the encoding where limb <paramref name="index" /> starts, ceil(25.5 * index).</summary>
    private static int LimbStart(int index) => ((51 * index) + 1) >> 1;

    /// <summary>The mask of limb <paramref name="index" />'s width: 26 bits when it is even, 25 when it is odd.</summary>
    private static long LimbMask(int index) => EvenLimbMask >> (index & 1);

    /// <summary>Squares <paramref name="value" /> <paramref name="count" /> times, a public count, into <paramref name="result" />.</summary>
    private static void SquareRepeatedly(Span<long> result, ReadOnlySpan<long> value, int count)
    {
        Square(result, value);
        for (int round = 1; round < count; round++)
        {
            Square(result, result);
        }
    }

    /// <summary>
    /// Carries the ten 64-bit column sums of a product into 26- and 25-bit limbs, folding
    /// the carry out of the top limb back into limb 0 with 2^255 = 19 (mod p).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CarryProduct(Span<long> result, long h0, long h1, long h2, long h3, long h4, long h5, long h6, long h7, long h8, long h9)
    {
        h1 += h0 >> EvenLimbBits;
        h2 += h1 >> OddLimbBits;
        h3 += h2 >> EvenLimbBits;
        h4 += h3 >> OddLimbBits;
        h5 += h4 >> EvenLimbBits;
        h6 += h5 >> OddLimbBits;
        h7 += h6 >> EvenLimbBits;
        h8 += h7 >> OddLimbBits;
        h9 += h8 >> EvenLimbBits;
        long low = (h0 & EvenLimbMask) + (19 * (h9 >> OddLimbBits));
        result[9] = h9 & OddLimbMask;
        result[8] = h8 & EvenLimbMask;
        result[7] = h7 & OddLimbMask;
        result[6] = h6 & EvenLimbMask;
        result[5] = h5 & OddLimbMask;
        result[4] = h4 & EvenLimbMask;
        result[3] = h3 & OddLimbMask;
        result[2] = h2 & EvenLimbMask;
        result[1] = (h1 & OddLimbMask) + (low >> EvenLimbBits);
        result[0] = low & EvenLimbMask;
    }

    /// <summary>
    /// Brings every limb into its width plus a carry into the lowest limb, folding the
    /// carry out of the top limb back in with 2^255 = 19 (mod p).
    /// </summary>
    private static void Carry(Span<long> element)
    {
        for (int index = 0; index < LimbCount - 1; index++)
        {
            int bits = EvenLimbBits - (index & 1);
            long carry = element[index] >> bits;
            element[index] &= LimbMask(index);
            element[index + 1] += carry;
        }

        long topCarry = element[LimbCount - 1] >> OddLimbBits;
        element[LimbCount - 1] &= OddLimbMask;
        element[0] += 19 * topCarry;
    }

    /// <summary>
    /// Replaces a carried <paramref name="element" /> with element - p when that is not
    /// negative, choosing by mask; <paramref name="scratch" /> receives the difference.
    /// p's limbs are all ones in their width but the lowest, which is 2^26 - 19.
    /// </summary>
    private static void SubtractPrimeIfNotBelow(Span<long> element, Span<long> scratch)
    {
        scratch[0] = element[0] - (EvenLimbMask - 18);
        for (int index = 1; index < LimbCount; index++)
        {
            int previousBits = EvenLimbBits - ((index - 1) & 1);
            scratch[index] = element[index] - LimbMask(index) - ((scratch[index - 1] >> previousBits) & 1);
            scratch[index - 1] &= LimbMask(index - 1);
        }

        uint borrow = (uint)((scratch[LimbCount - 1] >> OddLimbBits) & 1);
        scratch[LimbCount - 1] &= OddLimbMask;
        ConditionalSwap(element, scratch, 1u - borrow);
    }
}
