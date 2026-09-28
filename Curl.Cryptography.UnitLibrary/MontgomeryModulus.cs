using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// Arithmetic modulo a public odd modulus in Montgomery form, on fixed-width 32-bit limbs
/// stored least significant first: multiplication by coarsely integrated operand scanning
/// (CIOS) with a masked final subtraction, and a fixed-window exponentiation whose table
/// look-up reads every entry. <see cref="FiniteFieldDiffieHellman" /> runs on it.
/// </summary>
/// <remarks>
/// Constant-time in the operands and the exponent: every loop bound is the limb count or
/// the exponent's byte length, both public, and a secret only ever becomes a mask. The
/// modulus itself is public, so its set-up uses <see cref="BigInteger" />.
/// </remarks>
internal sealed class MontgomeryModulus
{
    /// <summary>The bits an exponent window covers; the table holds 2^4 = 16 powers.</summary>
    private const int WindowBits = 4;

    /// <summary>The number of precomputed powers, base^0 to base^15.</summary>
    private const int TableSize = 1 << WindowBits;

    private readonly uint[] modulus;
    private readonly uint[] rSquared;
    private readonly uint[] one;
    private readonly uint negativeInverse;

    /// <summary>Prepares the arithmetic modulo the odd big-endian <paramref name="modulusBigEndian" />.</summary>
    /// <param name="modulusBigEndian">An odd modulus greater than 1, without leading zero bytes.</param>
    public MontgomeryModulus(ReadOnlySpan<byte> modulusBigEndian)
    {
        LimbCount = (modulusBigEndian.Length + 3) / 4;
        modulus = new uint[LimbCount];
        ToLimbs(modulusBigEndian, modulus);
        var value = new BigInteger(modulusBigEndian, isUnsigned: true, isBigEndian: true);
        BigInteger r = BigInteger.One << (32 * LimbCount);
        rSquared = FromBigInteger(r * r % value);
        one = FromBigInteger(r % value);
        negativeInverse = ComputeNegativeInverse(modulus[0]);
    }

    /// <summary>The number of 32-bit limbs a residue occupies.</summary>
    public int LimbCount { get; }

    /// <summary>
    /// Converts a big-endian unsigned integer to little-endian limbs, zero-extending into
    /// <paramref name="limbs" />. The loop runs over the byte count, which is public.
    /// </summary>
    public static void ToLimbs(ReadOnlySpan<byte> bigEndian, Span<uint> limbs)
    {
        limbs.Clear();
        for (int index = 0; index < bigEndian.Length; index++)
        {
            int bytePosition = bigEndian.Length - 1 - index;
            limbs[bytePosition >> 2] |= (uint)bigEndian[index] << (8 * (bytePosition & 3));
        }
    }

    /// <summary>
    /// Writes little-endian <paramref name="limbs" /> to <paramref name="bigEndian" /> as a
    /// big-endian integer of exactly its length, keeping leading zero bytes.
    /// </summary>
    public static void FromLimbs(ReadOnlySpan<uint> limbs, Span<byte> bigEndian)
    {
        for (int index = 0; index < bigEndian.Length; index++)
        {
            int bytePosition = bigEndian.Length - 1 - index;
            bigEndian[index] = (byte)(limbs[bytePosition >> 2] >> (8 * (bytePosition & 3)));
        }
    }

    /// <summary>
    /// Computes <paramref name="baseValue" />^<paramref name="exponent" /> modulo the modulus
    /// into <paramref name="result" />, all as limbs in ordinary (not Montgomery) form.
    /// </summary>
    /// <param name="baseValue">The base, already reduced below the modulus.</param>
    /// <param name="exponent">The big-endian exponent; only its length shapes the running time.</param>
    /// <param name="result">Receives the power; may be the same span as <paramref name="baseValue" />.</param>
    public void Exponentiate(ReadOnlySpan<uint> baseValue, ReadOnlySpan<byte> exponent, Span<uint> result)
    {
        int n = LimbCount;
        uint[] work = new uint[((TableSize + 3) * n) + n + 2];
        Span<uint> table = work.AsSpan(0, TableSize * n);
        Span<uint> accumulator = work.AsSpan(TableSize * n, n);
        Span<uint> selected = work.AsSpan((TableSize + 1) * n, n);
        Span<uint> unit = work.AsSpan((TableSize + 2) * n, n);
        Span<uint> scratch = work.AsSpan((TableSize + 3) * n);
        try
        {
            FillTable(baseValue, table, scratch);
            one.CopyTo(accumulator);
            foreach (byte exponentByte in exponent)
            {
                ApplyWindow(accumulator, table, (uint)exponentByte >> WindowBits, selected, scratch);
                ApplyWindow(accumulator, table, exponentByte & 0xFu, selected, scratch);
            }

            unit[0] = 1;
            Multiply(result, accumulator, unit, scratch);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(work.AsSpan()));
        }
    }

    /// <summary>
    /// Montgomery multiplication: <paramref name="result" /> = <paramref name="left" /> *
    /// <paramref name="right" /> * R^-1 modulo the modulus, for operands below the modulus.
    /// <paramref name="result" /> may alias either operand.
    /// </summary>
    /// <param name="result">Receives the product in Montgomery form.</param>
    /// <param name="left">The first operand.</param>
    /// <param name="right">The second operand.</param>
    /// <param name="scratch">At least <see cref="LimbCount" /> + 2 limbs of working space.</param>
    public void Multiply(Span<uint> result, ReadOnlySpan<uint> left, ReadOnlySpan<uint> right, Span<uint> scratch)
    {
        int n = LimbCount;
        Span<uint> total = scratch[..(n + 2)];
        total.Clear();
        for (int index = 0; index < n; index++)
        {
            AddProduct(total, left, right[index]);
            ReduceOneLimb(total);
        }

        SubtractModulusIfNotBelow(result, total);
    }

    /// <summary>
    /// Returns -m^-1 modulo 2^32 for the odd lowest limb <paramref name="lowestLimb" />, by
    /// Newton's iteration, which doubles the correct low bits each step (1, 2, 4, ... 32).
    /// </summary>
    private static uint ComputeNegativeInverse(uint lowestLimb)
    {
        uint inverse = 1;
        for (int step = 0; step < 5; step++)
        {
            inverse *= 2u - (lowestLimb * inverse);
        }

        return 0u - inverse;
    }

    /// <summary>Sets <paramref name="destination" /> to the one entry of <paramref name="table" /> <paramref name="window" /> names, reading every entry.</summary>
    private static void SelectEntry(ReadOnlySpan<uint> table, uint window, Span<uint> destination)
    {
        int n = destination.Length;
        destination.Clear();
        for (int entry = 0; entry < TableSize; entry++)
        {
            uint mask = (uint)(((ulong)((uint)entry ^ window) - 1) >> 32);
            ReadOnlySpan<uint> candidate = table.Slice(entry * n, n);
            for (int limb = 0; limb < n; limb++)
            {
                destination[limb] |= candidate[limb] & mask;
            }
        }
    }

    private uint[] FromBigInteger(BigInteger value)
    {
        uint[] limbs = new uint[LimbCount];
        byte[] bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
        ToLimbs(bytes, limbs);
        return limbs;
    }

    /// <summary>Fills the table with base^0 to base^15 in Montgomery form.</summary>
    private void FillTable(ReadOnlySpan<uint> baseValue, Span<uint> table, Span<uint> scratch)
    {
        int n = LimbCount;
        one.CopyTo(table[..n]);
        Span<uint> first = table.Slice(n, n);
        Multiply(first, baseValue, rSquared, scratch);
        for (int entry = 2; entry < TableSize; entry++)
        {
            Multiply(table.Slice(entry * n, n), table.Slice((entry - 1) * n, n), first, scratch);
        }
    }

    /// <summary>Squares the accumulator four times, then multiplies in table entry <paramref name="window" />, even when it is base^0.</summary>
    private void ApplyWindow(Span<uint> accumulator, ReadOnlySpan<uint> table, uint window, Span<uint> selected, Span<uint> scratch)
    {
        for (int square = 0; square < WindowBits; square++)
        {
            Multiply(accumulator, accumulator, accumulator, scratch);
        }

        SelectEntry(table, window, selected);
        Multiply(accumulator, accumulator, selected, scratch);
    }

    /// <summary>total += left * factor, over n + 2 limbs.</summary>
    private void AddProduct(Span<uint> total, ReadOnlySpan<uint> left, uint factor)
    {
        int n = LimbCount;
        ulong carry = 0;
        for (int index = 0; index < n; index++)
        {
            ulong sum = total[index] + ((ulong)left[index] * factor) + carry;
            total[index] = (uint)sum;
            carry = sum >> 32;
        }

        ulong top = total[n] + carry;
        total[n] = (uint)top;
        total[n + 1] = (uint)(top >> 32);
    }

    /// <summary>Adds the multiple of the modulus that clears the lowest limb, then shifts total right by one limb.</summary>
    private void ReduceOneLimb(Span<uint> total)
    {
        int n = LimbCount;
        uint factor = total[0] * negativeInverse;
        ulong carry = (total[0] + ((ulong)factor * modulus[0])) >> 32;
        for (int index = 1; index < n; index++)
        {
            ulong sum = total[index] + ((ulong)factor * modulus[index]) + carry;
            total[index - 1] = (uint)sum;
            carry = sum >> 32;
        }

        ulong top = total[n] + carry;
        total[n - 1] = (uint)top;
        total[n] = total[n + 1] + (uint)(top >> 32);
        total[n + 1] = 0;
    }

    /// <summary>
    /// Writes total - modulus to <paramref name="result" />, then keeps total instead, by
    /// mask, when the subtraction borrowed out of total's top limb (total was below the modulus).
    /// </summary>
    private void SubtractModulusIfNotBelow(Span<uint> result, ReadOnlySpan<uint> total)
    {
        int n = LimbCount;
        ulong borrow = 0;
        for (int index = 0; index < n; index++)
        {
            ulong difference = (ulong)total[index] - modulus[index] - borrow;
            result[index] = (uint)difference;
            borrow = difference >> 63;
        }

        uint keepTotal = 0u - (uint)(((ulong)total[n] - borrow) >> 63);
        for (int index = 0; index < n; index++)
        {
            result[index] = ConstantTime.Select(keepTotal, total[index], result[index]);
        }
    }
}
