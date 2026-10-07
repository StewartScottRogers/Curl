using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// Arithmetic in GF(2^255 - 19), the field X25519 (RFC 7748) and Ed25519 (RFC 8032) work
/// in. An element is <see cref="LimbCount" /> signed 64-bit limbs of 51 bits each, least
/// significant first, held in a caller's <see cref="Span{T}" /> (usually a
/// <c>stackalloc</c>), so no operation allocates. A product of two limbs is formed whole
/// as a 128-bit integer, so a multiplication is 25 limb products and a squaring 15, held in
/// locals rather than in a buffer to zero. Every member is constant-time: no branch, loop
/// bound or index depends on an element's value, and conditional moves are masks. Outputs
/// may alias inputs.
/// </summary>
internal static class Field25519
{
    /// <summary>The number of limbs in one field element.</summary>
    public const int LimbCount = 5;

    /// <summary>The number of bytes in an element's little-endian encoding.</summary>
    public const int EncodedLength = 32;

    private const int LimbBits = 51;

    private const long LimbMask = (1L << LimbBits) - 1;

    /// <summary>Sets <paramref name="element" /> to the small value <paramref name="value" />.</summary>
    public static void SetSmall(Span<long> element, uint value)
    {
        element.Clear();
        element[0] = value;
    }

    /// <summary>
    /// Decodes a 32-byte little-endian value, ignoring its top bit (RFC 7748 section 5,
    /// <c>decodeUCoordinate</c>); a value of p or more is accepted and reduced as it is
    /// used.
    /// </summary>
    public static void Decode(Span<long> element, ReadOnlySpan<byte> encoded)
    {
        ulong word0 = BinaryPrimitives.ReadUInt64LittleEndian(encoded);
        ulong word1 = BinaryPrimitives.ReadUInt64LittleEndian(encoded[8..]);
        ulong word2 = BinaryPrimitives.ReadUInt64LittleEndian(encoded[16..]);
        ulong word3 = BinaryPrimitives.ReadUInt64LittleEndian(encoded[24..]);
        element[0] = (long)word0 & LimbMask;
        element[1] = (long)((word0 >> 51) | (word1 << 13)) & LimbMask;
        element[2] = (long)((word1 >> 38) | (word2 << 26)) & LimbMask;
        element[3] = (long)((word2 >> 25) | (word3 << 39)) & LimbMask;
        element[4] = (long)(word3 >> 12) & LimbMask;
    }

    /// <summary>
    /// Encodes <paramref name="element" /> as the 32-byte little-endian encoding of its
    /// canonical value, fully reduced modulo p.
    /// </summary>
    public static void Encode(Span<byte> encoded, ReadOnlySpan<long> element)
    {
        Span<long> reduced = stackalloc long[LimbCount];
        Span<long> subtracted = stackalloc long[LimbCount];
        try
        {
            element.CopyTo(reduced);
            Carry(reduced);
            Carry(reduced);
            Carry(reduced);
            SubtractPrimeIfNotBelow(reduced, subtracted);
            SubtractPrimeIfNotBelow(reduced, subtracted);
            ulong limb0 = (ulong)reduced[0];
            ulong limb1 = (ulong)reduced[1];
            ulong limb2 = (ulong)reduced[2];
            ulong limb3 = (ulong)reduced[3];
            ulong limb4 = (ulong)reduced[4];
            BinaryPrimitives.WriteUInt64LittleEndian(encoded, limb0 | (limb1 << 51));
            BinaryPrimitives.WriteUInt64LittleEndian(encoded[8..], (limb1 >> 13) | (limb2 << 38));
            BinaryPrimitives.WriteUInt64LittleEndian(encoded[16..], (limb2 >> 26) | (limb3 << 25));
            BinaryPrimitives.WriteUInt64LittleEndian(encoded[24..], (limb3 >> 39) | (limb4 << 12));
        }
        finally
        {
            Clear(reduced);
            Clear(subtracted);
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
    /// folding every product past 2^255 back in with 2^255 = 19 (mod p) and carrying.
    /// </summary>
    public static void Multiply(Span<long> result, ReadOnlySpan<long> left, ReadOnlySpan<long> right)
    {
        long f0 = left[0], f1 = left[1], f2 = left[2], f3 = left[3], f4 = left[4];
        long g0 = right[0], g1 = right[1], g2 = right[2], g3 = right[3], g4 = right[4];
        long g1Folded = 19 * g1, g2Folded = 19 * g2, g3Folded = 19 * g3, g4Folded = 19 * g4;
        Accumulator128 h0 = SumOfProducts(f0, g0, f1, g4Folded, f2, g3Folded, f3, g2Folded, f4, g1Folded);
        Accumulator128 h1 = SumOfProducts(f0, g1, f1, g0, f2, g4Folded, f3, g3Folded, f4, g2Folded);
        Accumulator128 h2 = SumOfProducts(f0, g2, f1, g1, f2, g0, f3, g4Folded, f4, g3Folded);
        Accumulator128 h3 = SumOfProducts(f0, g3, f1, g2, f2, g1, f3, g0, f4, g4Folded);
        Accumulator128 h4 = SumOfProducts(f0, g4, f1, g3, f2, g2, f3, g1, f4, g0);
        CarryProduct(result, h0, h1, h2, h3, h4);
    }

    /// <summary>
    /// Sets <paramref name="result" /> to <paramref name="value" /> squared, forming each
    /// cross product once and doubling it.
    /// </summary>
    public static void Square(Span<long> result, ReadOnlySpan<long> value)
    {
        long f0 = value[0], f1 = value[1], f2 = value[2], f3 = value[3], f4 = value[4];
        long f0Doubled = 2 * f0, f1Doubled = 2 * f1, f2Doubled = 2 * f2, f3Doubled = 2 * f3;
        long f3Folded = 19 * f3, f4Folded = 19 * f4;
        Accumulator128 h0 = SumOfProducts(f0, f0, f1Doubled, f4Folded, f2Doubled, f3Folded);
        Accumulator128 h1 = SumOfProducts(f0Doubled, f1, f2Doubled, f4Folded, f3, f3Folded);
        Accumulator128 h2 = SumOfProducts(f0Doubled, f2, f1, f1, f3Doubled, f4Folded);
        Accumulator128 h3 = SumOfProducts(f0Doubled, f3, f1Doubled, f2, f4, f4Folded);
        Accumulator128 h4 = SumOfProducts(f0Doubled, f4, f1Doubled, f3, f2, f2);
        CarryProduct(result, h0, h1, h2, h3, h4);
    }

    /// <summary>
    /// Sets <paramref name="result" /> to <paramref name="value" /> * <paramref name="small" />,
    /// a public constant such as the ladder's a24: one product per limb, then a carry.
    /// </summary>
    public static void MultiplySmall(Span<long> result, ReadOnlySpan<long> value, uint small)
    {
        CarryProduct(
            result,
            Product(value[0], small),
            Product(value[1], small),
            Product(value[2], small),
            Product(value[3], small),
            Product(value[4], small));
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

    /// <summary>The whole signed 128-bit product of two limbs.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Accumulator128 Product(long left, long right)
    {
        Accumulator128 sum = default;
        sum.MultiplyAdd(left, right);
        return sum;
    }

    /// <summary>The 128-bit sum of three limb products, <paramref name="a0" /> * <paramref name="b0" /> and so on.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Accumulator128 SumOfProducts(long a0, long b0, long a1, long b1, long a2, long b2)
    {
        Accumulator128 sum = default;
        sum.MultiplyAdd(a0, b0);
        sum.MultiplyAdd(a1, b1);
        sum.MultiplyAdd(a2, b2);
        return sum;
    }

    /// <summary>The 128-bit sum of five limb products, <paramref name="a0" /> * <paramref name="b0" /> and so on.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Accumulator128 SumOfProducts(long a0, long b0, long a1, long b1, long a2, long b2, long a3, long b3, long a4, long b4)
    {
        Accumulator128 sum = SumOfProducts(a0, b0, a1, b1, a2, b2);
        sum.MultiplyAdd(a3, b3);
        sum.MultiplyAdd(a4, b4);
        return sum;
    }

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
    /// Carries the five 128-bit column sums of a product into 51-bit limbs, folding the
    /// carry out of the top limb back into limb 0 with 2^255 = 19 (mod p).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CarryProduct(Span<long> result, Accumulator128 h0, Accumulator128 h1, Accumulator128 h2, Accumulator128 h3, Accumulator128 h4)
    {
        h1.Add(h0.ShiftRight(LimbBits));
        h2.Add(h1.ShiftRight(LimbBits));
        h3.Add(h2.ShiftRight(LimbBits));
        h4.Add(h3.ShiftRight(LimbBits));
        Accumulator128 low = Product(h4.ShiftRight(LimbBits), 19);
        low.Add((long)h0.Low & LimbMask);
        result[0] = (long)low.Low & LimbMask;
        result[1] = ((long)h1.Low & LimbMask) + low.ShiftRight(LimbBits);
        result[2] = (long)h2.Low & LimbMask;
        result[3] = (long)h3.Low & LimbMask;
        result[4] = (long)h4.Low & LimbMask;
    }

    /// <summary>
    /// Brings every limb into 0 to 2^51 - 1 plus a carry into the lowest limb, folding the
    /// carry out of the top limb back in with 2^255 = 19 (mod p).
    /// </summary>
    private static void Carry(Span<long> element)
    {
        for (int index = 0; index < LimbCount - 1; index++)
        {
            long carry = element[index] >> LimbBits;
            element[index] &= LimbMask;
            element[index + 1] += carry;
        }

        long topCarry = element[LimbCount - 1] >> LimbBits;
        element[LimbCount - 1] &= LimbMask;
        element[0] += 19 * topCarry;
    }

    /// <summary>
    /// Replaces a carried <paramref name="element" /> with element - p when that is not
    /// negative, choosing by mask; <paramref name="scratch" /> receives the difference.
    /// p's limbs are all 2^51 - 1 but the lowest, which is 2^51 - 19.
    /// </summary>
    private static void SubtractPrimeIfNotBelow(Span<long> element, Span<long> scratch)
    {
        scratch[0] = element[0] - (LimbMask - 18);
        for (int index = 1; index < LimbCount; index++)
        {
            scratch[index] = element[index] - LimbMask - ((scratch[index - 1] >> LimbBits) & 1);
            scratch[index - 1] &= LimbMask;
        }

        uint borrow = (uint)((scratch[LimbCount - 1] >> LimbBits) & 1);
        scratch[LimbCount - 1] &= LimbMask;
        ConditionalSwap(element, scratch, 1u - borrow);
    }
}
