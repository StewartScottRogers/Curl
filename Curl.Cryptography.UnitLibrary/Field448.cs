using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// Arithmetic in GF(2^448 - 2^224 - 1), the field X448 (RFC 7748) and Ed448 (RFC 8032)
/// work in. An element is <see cref="LimbCount" /> signed 64-bit limbs of 56 bits each,
/// least significant first, held in a caller's <see cref="Span{T}" /> (usually a
/// <c>stackalloc</c>), so no operation allocates. A product of two limbs is formed whole
/// as a 128-bit integer, so a multiplication is 64 limb products and a squaring 36, held in
/// locals rather than in a buffer to zero. The
/// prime's shape makes reduction two additions per column: 2^448 = 2^224 + 1 (mod p), and
/// 2^224 is limb <see cref="MiddleLimb" />. Every member is constant-time: no branch,
/// loop bound or index depends on an element's value, and conditional moves are masks.
/// Outputs may alias inputs.
/// </summary>
internal static class Field448
{
    /// <summary>The number of limbs in one field element.</summary>
    public const int LimbCount = 8;

    /// <summary>The number of bytes in an element's little-endian encoding.</summary>
    public const int EncodedLength = 56;

    /// <summary>The limb that holds 2^224, where a carry out of 2^448 folds in beside limb 0.</summary>
    private const int MiddleLimb = 4;

    private const int LimbBits = 56;

    private const int LimbBytes = 7;

    private const long LimbMask = (1L << LimbBits) - 1;

    /// <summary>Sets <paramref name="element" /> to the small value <paramref name="value" />.</summary>
    public static void SetSmall(Span<long> element, uint value)
    {
        element.Clear();
        element[0] = value;
    }

    /// <summary>
    /// Decodes a 56-byte little-endian value, every bit of it (RFC 7748 section 5 masks no
    /// bit for X448); a value of p or more is accepted and reduced as it is used.
    /// </summary>
    public static void Decode(Span<long> element, ReadOnlySpan<byte> encoded)
    {
        for (int index = 0; index < LimbCount; index++)
        {
            long limb = 0;
            for (int offset = 0; offset < LimbBytes; offset++)
            {
                limb |= (long)encoded[(LimbBytes * index) + offset] << (8 * offset);
            }

            element[index] = limb;
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
            element.CopyTo(reduced);
            Carry(reduced);
            Carry(reduced);
            Carry(reduced);
            Carry(reduced);
            SubtractPrimeIfNotBelow(reduced, subtracted);
            for (int index = 0; index < LimbCount; index++)
            {
                for (int offset = 0; offset < LimbBytes; offset++)
                {
                    encoded[(LimbBytes * index) + offset] = (byte)(reduced[index] >> (8 * offset));
                }
            }
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
    /// folding the high half back in with 2^448 = 2^224 + 1 (mod p) and carrying.
    /// </summary>
    public static void Multiply(Span<long> result, ReadOnlySpan<long> left, ReadOnlySpan<long> right)
    {
        FoldAndCarry(
            result,
            Column(left, right, 0),
            Column(left, right, 1),
            Column(left, right, 2),
            Column(left, right, 3),
            Column(left, right, 4),
            Column(left, right, 5),
            Column(left, right, 6),
            Column(left, right, 7),
            Column(left, right, 8),
            Column(left, right, 9),
            Column(left, right, 10),
            Column(left, right, 11),
            Column(left, right, 12),
            Column(left, right, 13),
            Column(left, right, 14));
    }

    /// <summary>
    /// Sets <paramref name="result" /> to <paramref name="value" /> squared, forming each
    /// cross product once and doubling it.
    /// </summary>
    public static void Square(Span<long> result, ReadOnlySpan<long> value)
    {
        FoldAndCarry(
            result,
            SquareColumn(value, 0),
            SquareColumn(value, 1),
            SquareColumn(value, 2),
            SquareColumn(value, 3),
            SquareColumn(value, 4),
            SquareColumn(value, 5),
            SquareColumn(value, 6),
            SquareColumn(value, 7),
            SquareColumn(value, 8),
            SquareColumn(value, 9),
            SquareColumn(value, 10),
            SquareColumn(value, 11),
            SquareColumn(value, 12),
            SquareColumn(value, 13),
            SquareColumn(value, 14));
    }

    /// <summary>
    /// Sets <paramref name="result" /> to <paramref name="value" /> * <paramref name="small" />,
    /// a public constant such as the ladder's a24: one product per limb, then a carry.
    /// </summary>
    public static void MultiplySmall(Span<long> result, ReadOnlySpan<long> value, uint small)
    {
        CarryColumns(
            result,
            Product(value[0], small),
            Product(value[1], small),
            Product(value[2], small),
            Product(value[3], small),
            Product(value[4], small),
            Product(value[5], small),
            Product(value[6], small),
            Product(value[7], small));
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

    /// <summary>The whole signed 128-bit product of two limbs.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Accumulator128 Product(long left, long right)
    {
        Accumulator128 sum = default;
        sum.MultiplyAdd(left, right);
        return sum;
    }

    /// <summary>
    /// Column <paramref name="column" /> of the schoolbook product: the sum of every limb
    /// product whose indices add up to it. The loop bounds depend only on the public column
    /// number.
    /// </summary>
    private static Accumulator128 Column(ReadOnlySpan<long> left, ReadOnlySpan<long> right, int column)
    {
        Accumulator128 sum = default;
        int last = Math.Min(column, LimbCount - 1);
        for (int index = column - last; index <= last; index++)
        {
            sum.MultiplyAdd(left[index], right[column - index]);
        }

        return sum;
    }

    /// <summary>
    /// Column <paramref name="column" /> of a square: each cross product below the diagonal
    /// once, doubled, plus the diagonal limb's square when the public column number is even.
    /// </summary>
    private static Accumulator128 SquareColumn(ReadOnlySpan<long> value, int column)
    {
        Accumulator128 sum = default;
        for (int index = column - Math.Min(column, LimbCount - 1); 2 * index < column; index++)
        {
            sum.MultiplyAdd(value[index], value[column - index]);
        }

        sum.Add(sum);
        if ((column & 1) == 0)
        {
            long diagonal = value[column >> 1];
            sum.MultiplyAdd(diagonal, diagonal);
        }

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
    /// Folds the 15 column sums of a product into <paramref name="result" /> with
    /// 2^448 = 2^224 + 1 (mod p): column 8 + k adds into columns k and k + 4, from the top
    /// column down so a column folded into the high half is folded again; then carries.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FoldAndCarry(
        Span<long> result,
        Accumulator128 c0,
        Accumulator128 c1,
        Accumulator128 c2,
        Accumulator128 c3,
        Accumulator128 c4,
        Accumulator128 c5,
        Accumulator128 c6,
        Accumulator128 c7,
        Accumulator128 c8,
        Accumulator128 c9,
        Accumulator128 c10,
        Accumulator128 c11,
        Accumulator128 c12,
        Accumulator128 c13,
        Accumulator128 c14)
    {
        c6.Add(c14);
        c10.Add(c14);
        c5.Add(c13);
        c9.Add(c13);
        c4.Add(c12);
        c8.Add(c12);
        c3.Add(c11);
        c7.Add(c11);
        c2.Add(c10);
        c6.Add(c10);
        c1.Add(c9);
        c5.Add(c9);
        c0.Add(c8);
        c4.Add(c8);
        CarryColumns(result, c0, c1, c2, c3, c4, c5, c6, c7);
    }

    /// <summary>
    /// Carries eight 128-bit column sums into 56-bit limbs, folding the carry out of the top
    /// limb into limbs 0 and <see cref="MiddleLimb" /> with 2^448 = 2^224 + 1 (mod p).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CarryColumns(
        Span<long> result,
        Accumulator128 c0,
        Accumulator128 c1,
        Accumulator128 c2,
        Accumulator128 c3,
        Accumulator128 c4,
        Accumulator128 c5,
        Accumulator128 c6,
        Accumulator128 c7)
    {
        c1.Add(c0.ShiftRight(LimbBits));
        c2.Add(c1.ShiftRight(LimbBits));
        c3.Add(c2.ShiftRight(LimbBits));
        c4.Add(c3.ShiftRight(LimbBits));
        c5.Add(c4.ShiftRight(LimbBits));
        c6.Add(c5.ShiftRight(LimbBits));
        c7.Add(c6.ShiftRight(LimbBits));
        long topCarry = c7.ShiftRight(LimbBits);
        long limb0 = ((long)c0.Low & LimbMask) + topCarry;
        long limb4 = ((long)c4.Low & LimbMask) + topCarry;
        result[0] = limb0 & LimbMask;
        result[1] = ((long)c1.Low & LimbMask) + (limb0 >> LimbBits);
        result[2] = (long)c2.Low & LimbMask;
        result[3] = (long)c3.Low & LimbMask;
        result[4] = limb4 & LimbMask;
        result[5] = ((long)c5.Low & LimbMask) + (limb4 >> LimbBits);
        result[6] = (long)c6.Low & LimbMask;
        result[7] = (long)c7.Low & LimbMask;
    }

    /// <summary>
    /// Brings every limb into 0 to 2^56 - 1 plus a carry into limbs 0 and
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
    /// <paramref name="scratch" /> receives the difference. p's limbs are all 2^56 - 1 but
    /// limb <see cref="MiddleLimb" />, which is 2^56 - 2.
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
