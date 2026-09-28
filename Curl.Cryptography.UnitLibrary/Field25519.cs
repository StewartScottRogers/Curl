using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// Arithmetic in GF(2^255 - 19), the field X25519 (RFC 7748) and Ed25519 (RFC 8032) work
/// in. An element is <see cref="LimbCount" /> signed 64-bit limbs of 16 bits each, least
/// significant first, held in a caller's <see cref="Span{T}" /> (usually a
/// <c>stackalloc</c>), so no operation allocates. Every member is constant-time: loop
/// bounds and indexes depend only on the limb count, and conditional moves are masks.
/// Outputs may alias inputs.
/// </summary>
internal static class Field25519
{
    /// <summary>The number of limbs in one field element.</summary>
    public const int LimbCount = 16;

    /// <summary>The number of bytes in an element's little-endian encoding.</summary>
    public const int EncodedLength = 32;

    /// <summary>Sets <paramref name="element" /> to the small value <paramref name="value" />.</summary>
    public static void SetSmall(Span<long> element, ushort value)
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
        for (int index = 0; index < LimbCount; index++)
        {
            element[index] = encoded[2 * index] | ((long)encoded[(2 * index) + 1] << 8);
        }

        element[LimbCount - 1] &= 0x7FFF;
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
            for (int index = 0; index < LimbCount; index++)
            {
                encoded[2 * index] = (byte)reduced[index];
                encoded[(2 * index) + 1] = (byte)(reduced[index] >> 8);
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
    /// folding the high half back in with 2^256 = 38 (mod p) and carrying twice.
    /// </summary>
    public static void Multiply(Span<long> result, ReadOnlySpan<long> left, ReadOnlySpan<long> right)
    {
        Span<long> product = stackalloc long[(2 * LimbCount) - 1];
        try
        {
            product.Clear();
            for (int leftIndex = 0; leftIndex < LimbCount; leftIndex++)
            {
                long leftLimb = left[leftIndex];
                Span<long> row = product.Slice(leftIndex, LimbCount);
                for (int rightIndex = 0; rightIndex < LimbCount; rightIndex++)
                {
                    row[rightIndex] += leftLimb * right[rightIndex];
                }
            }

            Reduce(result, product);
        }
        finally
        {
            Clear(product);
        }
    }

    /// <summary>
    /// Sets <paramref name="result" /> to <paramref name="value" /> squared, forming each
    /// cross product once and doubling it.
    /// </summary>
    public static void Square(Span<long> result, ReadOnlySpan<long> value)
    {
        Span<long> product = stackalloc long[(2 * LimbCount) - 1];
        try
        {
            product.Clear();
            for (int leftIndex = 0; leftIndex < LimbCount; leftIndex++)
            {
                long leftLimb = value[leftIndex];
                product[2 * leftIndex] += leftLimb * leftLimb;
                long doubled = 2 * leftLimb;
                for (int rightIndex = leftIndex + 1; rightIndex < LimbCount; rightIndex++)
                {
                    product[leftIndex + rightIndex] += doubled * value[rightIndex];
                }
            }

            Reduce(result, product);
        }
        finally
        {
            Clear(product);
        }
    }

    /// <summary>
    /// Sets <paramref name="result" /> to the inverse of <paramref name="value" />, computed
    /// as value^(p - 2) by a fixed square-and-multiply chain; zero maps to zero.
    /// </summary>
    public static void Invert(Span<long> result, ReadOnlySpan<long> value)
    {
        Span<long> power = stackalloc long[LimbCount];
        try
        {
            value.CopyTo(power);
            // p - 2 = 2^255 - 21: every bit from 253 down to 0 is set except bits 2 and 4.
            for (int bit = 253; bit >= 0; bit--)
            {
                Square(power, power);
                if (bit != 2 && bit != 4)
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

    /// <summary>
    /// Folds a 31-limb product into <paramref name="result" /> with 2^256 = 38 (mod p) and
    /// carries twice.
    /// </summary>
    private static void Reduce(Span<long> result, Span<long> product)
    {
        for (int index = 0; index < LimbCount - 1; index++)
        {
            product[index] += 38 * product[index + LimbCount];
        }

        product[..LimbCount].CopyTo(result);
        Carry(result);
        Carry(result);
    }

    /// <summary>
    /// Brings every limb into 0 to 2^16 - 1 plus a carry into the lowest limb, folding the
    /// carry out of the top limb back in with 2^256 = 38 (mod p).
    /// </summary>
    private static void Carry(Span<long> element)
    {
        for (int index = 0; index < LimbCount; index++)
        {
            long carry = element[index] >> 16;
            element[index] -= carry << 16;
            int next = (index + 1) % LimbCount;
            element[next] += carry * (next == 0 ? 38 : 1);
        }
    }

    /// <summary>
    /// Replaces a carried <paramref name="element" /> with element - p when that is not
    /// negative, choosing by mask; <paramref name="scratch" /> receives the difference.
    /// </summary>
    private static void SubtractPrimeIfNotBelow(Span<long> element, Span<long> scratch)
    {
        scratch[0] = element[0] - 0xFFED;
        for (int index = 1; index < LimbCount - 1; index++)
        {
            scratch[index] = element[index] - 0xFFFF - ((scratch[index - 1] >> 16) & 1);
            scratch[index - 1] &= 0xFFFF;
        }

        scratch[LimbCount - 1] = element[LimbCount - 1] - 0x7FFF - ((scratch[LimbCount - 2] >> 16) & 1);
        uint borrow = (uint)((scratch[LimbCount - 1] >> 16) & 1);
        scratch[LimbCount - 2] &= 0xFFFF;
        ConditionalSwap(element, scratch, 1u - borrow);
    }
}
