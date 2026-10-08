using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// Arithmetic in GF(2^448 - 2^224 - 1), the field X448 (RFC 7748) and Ed448 (RFC 8032)
/// work in. An element is <see cref="LimbCount" /> signed 64-bit limbs of 28 bits each,
/// least significant first, held in a caller's <see cref="Span{T}" /> (usually a
/// <c>stackalloc</c>), so no operation allocates. Every limb product and every column sum
/// fits in 64 bits, so a multiplication is 256 plain 64-bit products and a squaring 136,
/// with no 128-bit arithmetic (BL-1645). <see cref="Add" /> and <see cref="Subtract" />
/// carry their result, so a product's inputs are always carried elements, and its
/// folded column sums stay below 2^61, at most 26 limb products of below 2^56
/// each. The prime's shape makes reduction additions: 2^448 = 2^224 + 1
/// (mod p), and 2^224 is limb <see cref="MiddleLimb" />. Every member is constant-time:
/// no branch, loop bound or index depends on an element's value, and conditional moves
/// are masks. Outputs may alias inputs.
/// </summary>
internal static class Field448
{
    /// <summary>The number of limbs in one field element.</summary>
    public const int LimbCount = 16;

    /// <summary>The number of bytes in an element's little-endian encoding.</summary>
    public const int EncodedLength = 56;

    /// <summary>The limb that holds 2^224, where a carry out of 2^448 folds in beside limb 0.</summary>
    private const int MiddleLimb = 8;

    private const int LimbBits = 28;

    /// <summary>The bytes of a pair of limbs, 56 bits.</summary>
    private const int PairBytes = 7;

    private const long LimbMask = (1L << LimbBits) - 1;

    /// <summary>Sets <paramref name="element" /> to the small value <paramref name="value" />.</summary>
    public static void SetSmall(Span<long> element, uint value)
    {
        element.Clear();
        element[0] = value & LimbMask;
        element[1] = value >> LimbBits;
    }

    /// <summary>
    /// Decodes a 56-byte little-endian value, every bit of it (RFC 7748 section 5 masks no
    /// bit for X448); a value of p or more is accepted and reduced as it is used.
    /// </summary>
    public static void Decode(Span<long> element, ReadOnlySpan<byte> encoded)
    {
        for (int pair = 0; pair < LimbCount / 2; pair++)
        {
            long bits = 0;
            for (int offset = 0; offset < PairBytes; offset++)
            {
                bits |= (long)encoded[(PairBytes * pair) + offset] << (8 * offset);
            }

            element[2 * pair] = bits & LimbMask;
            element[(2 * pair) + 1] = bits >> LimbBits;
        }
    }

    /// <summary>
    /// Encodes <paramref name="element" /> as the 56-byte little-endian encoding of its
    /// canonical value, fully reduced modulo p.
    /// </summary>
    public static void Encode(Span<byte> encoded, ReadOnlySpan<long> element)
    {
        Span<long> reduced = stackalloc long[LimbCount];
        Span<long> subtracted = stackalloc long[LimbCount];
        try
        {
            // Adding 4p first makes a sum or difference with negative limbs positive, so
            // the carries below never fold a negative top carry back in.
            for (int index = 0; index < LimbCount; index++)
            {
                reduced[index] = element[index] + (4 * (LimbMask - (index == MiddleLimb ? 1 : 0)));
            }

            Carry(reduced);
            Carry(reduced);
            Carry(reduced);
            Carry(reduced);
            SubtractPrimeIfNotBelow(reduced, subtracted);
            for (int pair = 0; pair < LimbCount / 2; pair++)
            {
                long bits = reduced[2 * pair] | (reduced[(2 * pair) + 1] << LimbBits);
                for (int offset = 0; offset < PairBytes; offset++)
                {
                    encoded[(PairBytes * pair) + offset] = (byte)(bits >> (8 * offset));
                }
            }
        }
        finally
        {
            Clear(reduced);
            Clear(subtracted);
        }
    }

    /// <summary>Sets <paramref name="result" /> to <paramref name="left" /> + <paramref name="right" />, carried.</summary>
    public static void Add(Span<long> result, ReadOnlySpan<long> left, ReadOnlySpan<long> right)
    {
        for (int index = 0; index < LimbCount; index++)
        {
            result[index] = left[index] + right[index];
        }
        Carry(result);
    }

    /// <summary>Sets <paramref name="result" /> to <paramref name="left" /> - <paramref name="right" />, carried.</summary>
    public static void Subtract(Span<long> result, ReadOnlySpan<long> left, ReadOnlySpan<long> right)
    {
        for (int index = 0; index < LimbCount; index++)
        {
            result[index] = left[index] - right[index];
        }
        Carry(result);
    }

    /// <summary>
    /// Sets <paramref name="result" /> to <paramref name="left" /> * <paramref name="right" />,
    /// folding the high half back in with 2^448 = 2^224 + 1 (mod p) and carrying.
    /// </summary>
    public static void Multiply(Span<long> result, ReadOnlySpan<long> left, ReadOnlySpan<long> right)
    {
        long f0 = left[0], f1 = left[1], f2 = left[2], f3 = left[3], f4 = left[4], f5 = left[5], f6 = left[6], f7 = left[7];
        long f8 = left[8], f9 = left[9], f10 = left[10], f11 = left[11], f12 = left[12], f13 = left[13], f14 = left[14], f15 = left[15];
        long g0 = right[0], g1 = right[1], g2 = right[2], g3 = right[3], g4 = right[4], g5 = right[5], g6 = right[6], g7 = right[7];
        long g8 = right[8], g9 = right[9], g10 = right[10], g11 = right[11], g12 = right[12], g13 = right[13], g14 = right[14], g15 = right[15];
        long c30 = (f15 * g15);
        long c29 = (f14 * g15) + (f15 * g14);
        long c28 = (f13 * g15) + (f14 * g14) + (f15 * g13);
        long c27 = (f12 * g15) + (f13 * g14) + (f14 * g13) + (f15 * g12);
        long c26 = (f11 * g15) + (f12 * g14) + (f13 * g13) + (f14 * g12) + (f15 * g11);
        long c25 = (f10 * g15) + (f11 * g14) + (f12 * g13) + (f13 * g12) + (f14 * g11) + (f15 * g10);
        long c24 = (f9 * g15) + (f10 * g14) + (f11 * g13) + (f12 * g12) + (f13 * g11) + (f14 * g10) + (f15 * g9);
        long c23 = (f8 * g15) + (f9 * g14) + (f10 * g13) + (f11 * g12) + (f12 * g11) + (f13 * g10) + (f14 * g9) + (f15 * g8);
        long c22 = (f7 * g15) + (f8 * g14) + (f9 * g13) + (f10 * g12) + (f11 * g11) + (f12 * g10) + (f13 * g9) + (f14 * g8) + (f15 * g7);
        long c21 = (f6 * g15) + (f7 * g14) + (f8 * g13) + (f9 * g12) + (f10 * g11) + (f11 * g10) + (f12 * g9) + (f13 * g8) + (f14 * g7) + (f15 * g6);
        long c20 = (f5 * g15) + (f6 * g14) + (f7 * g13) + (f8 * g12) + (f9 * g11) + (f10 * g10) + (f11 * g9) + (f12 * g8) + (f13 * g7) + (f14 * g6) + (f15 * g5);
        long c19 = (f4 * g15) + (f5 * g14) + (f6 * g13) + (f7 * g12) + (f8 * g11) + (f9 * g10) + (f10 * g9) + (f11 * g8) + (f12 * g7) + (f13 * g6) + (f14 * g5) + (f15 * g4);
        long c18 = (f3 * g15) + (f4 * g14) + (f5 * g13) + (f6 * g12) + (f7 * g11) + (f8 * g10) + (f9 * g9) + (f10 * g8) + (f11 * g7) + (f12 * g6) + (f13 * g5) + (f14 * g4) + (f15 * g3);
        long c17 = (f2 * g15) + (f3 * g14) + (f4 * g13) + (f5 * g12) + (f6 * g11) + (f7 * g10) + (f8 * g9) + (f9 * g8) + (f10 * g7) + (f11 * g6) + (f12 * g5) + (f13 * g4) + (f14 * g3) + (f15 * g2);
        long c16 = (f1 * g15) + (f2 * g14) + (f3 * g13) + (f4 * g12) + (f5 * g11) + (f6 * g10) + (f7 * g9) + (f8 * g8) + (f9 * g7) + (f10 * g6) + (f11 * g5) + (f12 * g4) + (f13 * g3) + (f14 * g2) + (f15 * g1);
        long c15 = (f0 * g15) + (f1 * g14) + (f2 * g13) + (f3 * g12) + (f4 * g11) + (f5 * g10) + (f6 * g9) + (f7 * g8) + (f8 * g7) + (f9 * g6) + (f10 * g5) + (f11 * g4) + (f12 * g3) + (f13 * g2) + (f14 * g1) + (f15 * g0);
        long c14 = (f0 * g14) + (f1 * g13) + (f2 * g12) + (f3 * g11) + (f4 * g10) + (f5 * g9) + (f6 * g8) + (f7 * g7) + (f8 * g6) + (f9 * g5) + (f10 * g4) + (f11 * g3) + (f12 * g2) + (f13 * g1) + (f14 * g0);
        long c13 = (f0 * g13) + (f1 * g12) + (f2 * g11) + (f3 * g10) + (f4 * g9) + (f5 * g8) + (f6 * g7) + (f7 * g6) + (f8 * g5) + (f9 * g4) + (f10 * g3) + (f11 * g2) + (f12 * g1) + (f13 * g0);
        long c12 = (f0 * g12) + (f1 * g11) + (f2 * g10) + (f3 * g9) + (f4 * g8) + (f5 * g7) + (f6 * g6) + (f7 * g5) + (f8 * g4) + (f9 * g3) + (f10 * g2) + (f11 * g1) + (f12 * g0);
        long c11 = (f0 * g11) + (f1 * g10) + (f2 * g9) + (f3 * g8) + (f4 * g7) + (f5 * g6) + (f6 * g5) + (f7 * g4) + (f8 * g3) + (f9 * g2) + (f10 * g1) + (f11 * g0);
        long c10 = (f0 * g10) + (f1 * g9) + (f2 * g8) + (f3 * g7) + (f4 * g6) + (f5 * g5) + (f6 * g4) + (f7 * g3) + (f8 * g2) + (f9 * g1) + (f10 * g0);
        long c9 = (f0 * g9) + (f1 * g8) + (f2 * g7) + (f3 * g6) + (f4 * g5) + (f5 * g4) + (f6 * g3) + (f7 * g2) + (f8 * g1) + (f9 * g0);
        long c8 = (f0 * g8) + (f1 * g7) + (f2 * g6) + (f3 * g5) + (f4 * g4) + (f5 * g3) + (f6 * g2) + (f7 * g1) + (f8 * g0);
        long c7 = (f0 * g7) + (f1 * g6) + (f2 * g5) + (f3 * g4) + (f4 * g3) + (f5 * g2) + (f6 * g1) + (f7 * g0);
        long c6 = (f0 * g6) + (f1 * g5) + (f2 * g4) + (f3 * g3) + (f4 * g2) + (f5 * g1) + (f6 * g0);
        long c5 = (f0 * g5) + (f1 * g4) + (f2 * g3) + (f3 * g2) + (f4 * g1) + (f5 * g0);
        long c4 = (f0 * g4) + (f1 * g3) + (f2 * g2) + (f3 * g1) + (f4 * g0);
        long c3 = (f0 * g3) + (f1 * g2) + (f2 * g1) + (f3 * g0);
        long c2 = (f0 * g2) + (f1 * g1) + (f2 * g0);
        long c1 = (f0 * g1) + (f1 * g0);
        long c0 = (f0 * g0);
        FoldAndCarry(result, c0, c1, c2, c3, c4, c5, c6, c7, c8, c9, c10, c11, c12, c13, c14, c15, c16, c17, c18, c19, c20, c21, c22, c23, c24, c25, c26, c27, c28, c29, c30);
    }

    /// <summary>
    /// Sets <paramref name="result" /> to <paramref name="value" /> squared, forming each
    /// cross product once and doubling it.
    /// </summary>
    public static void Square(Span<long> result, ReadOnlySpan<long> value)
    {
        long f0 = value[0], f1 = value[1], f2 = value[2], f3 = value[3], f4 = value[4], f5 = value[5], f6 = value[6], f7 = value[7];
        long f8 = value[8], f9 = value[9], f10 = value[10], f11 = value[11], f12 = value[12], f13 = value[13], f14 = value[14], f15 = value[15];
        long f0Doubled = 2 * f0, f1Doubled = 2 * f1, f2Doubled = 2 * f2, f3Doubled = 2 * f3, f4Doubled = 2 * f4, f5Doubled = 2 * f5, f6Doubled = 2 * f6;
        long f7Doubled = 2 * f7, f8Doubled = 2 * f8, f9Doubled = 2 * f9, f10Doubled = 2 * f10, f11Doubled = 2 * f11, f12Doubled = 2 * f12;
        long f13Doubled = 2 * f13, f14Doubled = 2 * f14;
        long c30 = (f15 * f15);
        long c29 = (f14Doubled * f15);
        long c28 = (f13Doubled * f15) + (f14 * f14);
        long c27 = (f12Doubled * f15) + (f13Doubled * f14);
        long c26 = (f11Doubled * f15) + (f12Doubled * f14) + (f13 * f13);
        long c25 = (f10Doubled * f15) + (f11Doubled * f14) + (f12Doubled * f13);
        long c24 = (f9Doubled * f15) + (f10Doubled * f14) + (f11Doubled * f13) + (f12 * f12);
        long c23 = (f8Doubled * f15) + (f9Doubled * f14) + (f10Doubled * f13) + (f11Doubled * f12);
        long c22 = (f7Doubled * f15) + (f8Doubled * f14) + (f9Doubled * f13) + (f10Doubled * f12) + (f11 * f11);
        long c21 = (f6Doubled * f15) + (f7Doubled * f14) + (f8Doubled * f13) + (f9Doubled * f12) + (f10Doubled * f11);
        long c20 = (f5Doubled * f15) + (f6Doubled * f14) + (f7Doubled * f13) + (f8Doubled * f12) + (f9Doubled * f11) + (f10 * f10);
        long c19 = (f4Doubled * f15) + (f5Doubled * f14) + (f6Doubled * f13) + (f7Doubled * f12) + (f8Doubled * f11) + (f9Doubled * f10);
        long c18 = (f3Doubled * f15) + (f4Doubled * f14) + (f5Doubled * f13) + (f6Doubled * f12) + (f7Doubled * f11) + (f8Doubled * f10) + (f9 * f9);
        long c17 = (f2Doubled * f15) + (f3Doubled * f14) + (f4Doubled * f13) + (f5Doubled * f12) + (f6Doubled * f11) + (f7Doubled * f10) + (f8Doubled * f9);
        long c16 = (f1Doubled * f15) + (f2Doubled * f14) + (f3Doubled * f13) + (f4Doubled * f12) + (f5Doubled * f11) + (f6Doubled * f10) + (f7Doubled * f9) + (f8 * f8);
        long c15 = (f0Doubled * f15) + (f1Doubled * f14) + (f2Doubled * f13) + (f3Doubled * f12) + (f4Doubled * f11) + (f5Doubled * f10) + (f6Doubled * f9) + (f7Doubled * f8);
        long c14 = (f0Doubled * f14) + (f1Doubled * f13) + (f2Doubled * f12) + (f3Doubled * f11) + (f4Doubled * f10) + (f5Doubled * f9) + (f6Doubled * f8) + (f7 * f7);
        long c13 = (f0Doubled * f13) + (f1Doubled * f12) + (f2Doubled * f11) + (f3Doubled * f10) + (f4Doubled * f9) + (f5Doubled * f8) + (f6Doubled * f7);
        long c12 = (f0Doubled * f12) + (f1Doubled * f11) + (f2Doubled * f10) + (f3Doubled * f9) + (f4Doubled * f8) + (f5Doubled * f7) + (f6 * f6);
        long c11 = (f0Doubled * f11) + (f1Doubled * f10) + (f2Doubled * f9) + (f3Doubled * f8) + (f4Doubled * f7) + (f5Doubled * f6);
        long c10 = (f0Doubled * f10) + (f1Doubled * f9) + (f2Doubled * f8) + (f3Doubled * f7) + (f4Doubled * f6) + (f5 * f5);
        long c9 = (f0Doubled * f9) + (f1Doubled * f8) + (f2Doubled * f7) + (f3Doubled * f6) + (f4Doubled * f5);
        long c8 = (f0Doubled * f8) + (f1Doubled * f7) + (f2Doubled * f6) + (f3Doubled * f5) + (f4 * f4);
        long c7 = (f0Doubled * f7) + (f1Doubled * f6) + (f2Doubled * f5) + (f3Doubled * f4);
        long c6 = (f0Doubled * f6) + (f1Doubled * f5) + (f2Doubled * f4) + (f3 * f3);
        long c5 = (f0Doubled * f5) + (f1Doubled * f4) + (f2Doubled * f3);
        long c4 = (f0Doubled * f4) + (f1Doubled * f3) + (f2 * f2);
        long c3 = (f0Doubled * f3) + (f1Doubled * f2);
        long c2 = (f0Doubled * f2) + (f1 * f1);
        long c1 = (f0Doubled * f1);
        long c0 = (f0 * f0);
        FoldAndCarry(result, c0, c1, c2, c3, c4, c5, c6, c7, c8, c9, c10, c11, c12, c13, c14, c15, c16, c17, c18, c19, c20, c21, c22, c23, c24, c25, c26, c27, c28, c29, c30);
    }

    /// <summary>
    /// Sets <paramref name="result" /> to <paramref name="value" /> * <paramref name="small" />,
    /// a public constant such as the ladder's a24: one product per limb, then a carry.
    /// </summary>
    public static void MultiplySmall(Span<long> result, ReadOnlySpan<long> value, uint small)
    {
        for (int index = 0; index < LimbCount; index++)
        {
            result[index] = value[index] * small;
        }

        CarryProduct(result);
    }

    /// <summary>
    /// Sets <paramref name="result" /> to the inverse of <paramref name="value" />, computed
    /// as value^(p - 2) by a fixed addition chain; zero maps to zero. p - 2 = 2^448 - 2^224
    /// - 3 is, from the top, 223 one bits, a zero, 222 one bits, a zero and a one.
    /// </summary>
    public static void Invert(Span<long> result, ReadOnlySpan<long> value)
    {
        Span<long> scratch = stackalloc long[7 * LimbCount];
        Span<long> ones3 = scratch[..LimbCount];
        Span<long> ones6 = scratch.Slice(LimbCount, LimbCount);
        Span<long> ones24 = scratch.Slice(2 * LimbCount, LimbCount);
        Span<long> ones30 = scratch.Slice(3 * LimbCount, LimbCount);
        Span<long> ones96 = scratch.Slice(4 * LimbCount, LimbCount);
        Span<long> ones222 = scratch.Slice(5 * LimbCount, LimbCount);
        Span<long> power = scratch.Slice(6 * LimbCount, LimbCount);
        try
        {
            // onesN is value^(2^N - 1), an exponent of N one bits.
            Square(power, value);
            Multiply(power, power, value);
            Square(ones3, power);
            Multiply(ones3, ones3, value);
            AppendOnes(ones6, ones3, ones3, 3);
            AppendOnes(power, ones6, ones6, 6);
            AppendOnes(ones24, power, power, 12);
            AppendOnes(ones30, ones24, ones6, 6);
            AppendOnes(power, ones24, ones24, 24);
            AppendOnes(ones96, power, power, 48);
            AppendOnes(power, ones96, ones96, 96);
            AppendOnes(ones222, power, ones30, 30);
            AppendOnes(power, ones222, value, 1);
            Square(power, power);
            AppendOnes(power, power, ones222, 222);
            SquareRepeatedly(power, power, 2);
            Multiply(result, power, value);
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
    /// public constant (the square-root exponent of RFC 8032 section 5.2.3), never a
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
    /// Sets <paramref name="result" /> to <paramref name="value" />^(2^<paramref name="count" />)
    /// * <paramref name="ones" />: the exponent of <paramref name="value" /> shifted left by
    /// <paramref name="count" /> bits, with <paramref name="ones" />' exponent of that many
    /// one bits beneath it.
    /// </summary>
    private static void AppendOnes(Span<long> result, ReadOnlySpan<long> value, ReadOnlySpan<long> ones, int count)
    {
        SquareRepeatedly(result, value, count);
        Multiply(result, result, ones);
    }

    /// <summary>
    /// Folds the 31 column sums of a product into 16 with 2^448 = 2^224 + 1 (mod p), then
    /// carries: column 16 + k (k below 8) adds into columns k and 8 + k, and column 24 + k,
    /// whose fold into column 8 + k is folded again, adds into column k once and column
    /// 8 + k twice.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FoldAndCarry(
        Span<long> result,
        long c0, long c1, long c2, long c3, long c4, long c5, long c6, long c7,
        long c8, long c9, long c10, long c11, long c12, long c13, long c14, long c15,
        long c16, long c17, long c18, long c19, long c20, long c21, long c22, long c23,
        long c24, long c25, long c26, long c27, long c28, long c29, long c30)
    {
        result[0] = c0 + c16 + c24;
        result[1] = c1 + c17 + c25;
        result[2] = c2 + c18 + c26;
        result[3] = c3 + c19 + c27;
        result[4] = c4 + c20 + c28;
        result[5] = c5 + c21 + c29;
        result[6] = c6 + c22 + c30;
        result[7] = c7 + c23;
        result[8] = c8 + c16 + (2 * c24);
        result[9] = c9 + c17 + (2 * c25);
        result[10] = c10 + c18 + (2 * c26);
        result[11] = c11 + c19 + (2 * c27);
        result[12] = c12 + c20 + (2 * c28);
        result[13] = c13 + c21 + (2 * c29);
        result[14] = c14 + c22 + (2 * c30);
        result[15] = c15 + c23;
        CarryProduct(result);
    }

    /// <summary>
    /// Carries <paramref name="element" />'s 16 column sums, each below 2^63 in magnitude,
    /// into 28-bit limbs in place, folding the carry out of the top limb into limbs 0 and
    /// <see cref="MiddleLimb" /> with 2^448 = 2^224 + 1 (mod p), and carrying those two once
    /// more.
    /// </summary>
    private static void CarryProduct(Span<long> element)
    {
        for (int index = 0; index < LimbCount - 1; index++)
        {
            element[index + 1] += element[index] >> LimbBits;
            element[index] &= LimbMask;
        }

        long topCarry = element[LimbCount - 1] >> LimbBits;
        element[LimbCount - 1] &= LimbMask;
        long limb0 = element[0] + topCarry;
        long middle = element[MiddleLimb] + topCarry;
        element[0] = limb0 & LimbMask;
        element[1] += limb0 >> LimbBits;
        element[MiddleLimb] = middle & LimbMask;
        element[MiddleLimb + 1] += middle >> LimbBits;
    }

    /// <summary>
    /// Brings every limb into 0 to 2^28 - 1 plus a carry into limbs 0 and
    /// <see cref="MiddleLimb" />, folding the carry out of the top limb back in with
    /// 2^448 = 2^224 + 1 (mod p).
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
        element[0] += topCarry;
        element[MiddleLimb] += topCarry;
    }

    /// <summary>
    /// Replaces a carried <paramref name="element" /> (a value below 2^448, so below 2p)
    /// with element - p when that is not negative, choosing by mask;
    /// <paramref name="scratch" /> receives the difference. p's limbs are all 2^28 - 1 but
    /// limb <see cref="MiddleLimb" />, which is 2^28 - 2.
    /// </summary>
    private static void SubtractPrimeIfNotBelow(Span<long> element, Span<long> scratch)
    {
        long borrow = 0;
        for (int index = 0; index < LimbCount; index++)
        {
            long primeLimb = LimbMask - (index == MiddleLimb ? 1 : 0);
            scratch[index] = element[index] - primeLimb - borrow;
            borrow = (scratch[index] >> LimbBits) & 1;
            scratch[index] &= LimbMask;
        }

        ConditionalSwap(element, scratch, 1u - (uint)borrow);
    }
}
