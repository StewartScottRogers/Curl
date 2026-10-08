using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// Arithmetic modulo an odd modulus in Montgomery form, on fixed-width 32-bit limbs
/// stored least significant first: Montgomery multiplication with a masked final
/// subtraction, run on 64-bit limbs read in place when the limb count is even and by 32-bit
/// coarsely integrated operand scanning (CIOS) when it is odd, modular addition,
/// subtraction and reduction, and a fixed-window exponentiation whose table look-up reads
/// every entry, which runs on 64-bit limbs with its own multiplication and squaring.
/// <see cref="FiniteFieldDiffieHellman" /> runs on it with a public prime, and
/// <see cref="RsaCrtPrivateKey" /> with the secret primes of an RSA key, and the brainpool
/// curves' field and group order (<see cref="BrainpoolDomainParameters" />) with public primes.
/// </summary>
/// <remarks>
/// Constant-time in the operands, the exponent and the modulus's value: every loop bound
/// is the limb count or a byte length, both public, and a secret only ever becomes a mask.
/// The set-up derives R mod m and R^2 mod m by doubling 1 with masked modular additions,
/// so it never divides by the modulus either; the 64-bit limbs' constants are those doubled
/// on. Carries on 64-bit limbs are unsigned comparisons taken as values, which compile to
/// a flag set, never a branch (ADR-0429). <see cref="Clear" /> zeroes the modulus and
/// its derived values when the modulus is a secret.
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

    // The same modulus on 64-bit limbs for Exponentiate and an even-limbed Multiply, whose Montgomery radix is
    // R' = 2^(64 * ceil(LimbCount / 2)): R' mod m, R'^2 mod m, and -m^-1 mod 2^64.
    private readonly ulong[] wideModulus;
    private readonly ulong[] wideOne;
    private readonly ulong[] wideRSquared;
    private readonly ulong wideNegativeInverse;

    /// <summary>Prepares the arithmetic modulo the odd big-endian <paramref name="modulusBigEndian" />.</summary>
    /// <param name="modulusBigEndian">An odd modulus greater than 1; leading zero bytes only widen the limbs.</param>
    public MontgomeryModulus(ReadOnlySpan<byte> modulusBigEndian)
    {
        LimbCount = (modulusBigEndian.Length + 3) / 4;
        modulus = new uint[LimbCount];
        ToLimbs(modulusBigEndian, modulus);
        one = new uint[LimbCount];
        rSquared = new uint[LimbCount];
        uint[] scratch = new uint[LimbCount + 2];
        one[0] = 1;
        DoubleRepeatedly(one, 32 * LimbCount, scratch);
        one.CopyTo(rSquared, 0);
        DoubleRepeatedly(rSquared, 32 * LimbCount, scratch);
        negativeInverse = ComputeNegativeInverse(modulus[0]);

        // R' is R times 2^32 when the limb count is odd, and R itself when it is even.
        int wideLimbCount = (LimbCount + 1) / 2;
        int extraBits = 32 * ((2 * wideLimbCount) - LimbCount);
        uint[] wideValue = new uint[LimbCount];
        wideModulus = ToWideLimbs(modulus, wideLimbCount);
        one.CopyTo(wideValue, 0);
        DoubleRepeatedly(wideValue, extraBits, scratch);
        wideOne = ToWideLimbs(wideValue, wideLimbCount);
        rSquared.CopyTo(wideValue, 0);
        DoubleRepeatedly(wideValue, 2 * extraBits, scratch);
        wideRSquared = ToWideLimbs(wideValue, wideLimbCount);
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(wideValue.AsSpan()));
        wideNegativeInverse = ComputeWideNegativeInverse(wideModulus[0]);
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
    /// Returns the big-endian <paramref name="prime" /> minus 2, its length kept: the exponent
    /// that inverts modulo a prime by Fermat's little theorem. The borrow runs through every
    /// byte, so the time depends only on the length.
    /// </summary>
    public static byte[] MinusTwo(ReadOnlySpan<byte> prime)
    {
        byte[] result = prime.ToArray();
        int borrow = 2;
        for (int index = result.Length - 1; index >= 0; index--)
        {
            int difference = result[index] - borrow;
            result[index] = (byte)difference;
            borrow = (difference >> 8) & 1;
        }

        return result;
    }

    /// <summary>
    /// Computes <paramref name="baseValue" />^<paramref name="exponent" /> modulo the modulus
    /// into <paramref name="result" />, all as limbs in ordinary (not Montgomery) form.
    /// </summary>
    /// <param name="baseValue">The base, already reduced below the modulus.</param>
    /// <param name="exponent">The big-endian exponent; only its length shapes the running time.</param>
    /// <param name="result">Receives the power; may be the same span as <paramref name="baseValue" />.</param>
    public void Exponentiate(ReadOnlySpan<uint> baseValue, ReadOnlySpan<byte> exponent, Span<uint> result) =>
        Exponentiate(baseValue, exponent, result, null);

    /// <summary>
    /// <see cref="Exponentiate(ReadOnlySpan{uint}, ReadOnlySpan{byte}, Span{uint})" />, writing
    /// each step it takes to <paramref name="operations" /> when one is given - <c>square</c>,
    /// <c>select</c> (the masked table look-up) and <c>multiply</c> - so a test can see that
    /// the sequence depends only on the exponent's length, never on its bits.
    /// </summary>
    /// <param name="baseValue">The base, already reduced below the modulus.</param>
    /// <param name="exponent">The big-endian exponent; only its length shapes the running time.</param>
    /// <param name="result">Receives the power; may be the same span as <paramref name="baseValue" />.</param>
    /// <param name="operations">Receives the steps taken, or <see langword="null" /> to record none.</param>
    /// <remarks>
    /// The work runs on 64-bit limbs (<see cref="Math.BigMul(ulong, ulong, out ulong)" />),
    /// a quarter of the inner-loop steps of 32-bit limbs, in its own Montgomery radix, so
    /// the base and the result stay in ordinary form at this method's edges. Every window
    /// squares four times, reads all 16 table entries and multiplies once, by base^0 when
    /// the window is zero, and every carry and borrow is computed with bitwise arithmetic.
    /// </remarks>
    internal void Exponentiate(ReadOnlySpan<uint> baseValue, ReadOnlySpan<byte> exponent, Span<uint> result, ICollection<string>? operations)
    {
        int m = wideModulus.Length;
        ulong[] work = new ulong[((TableSize + 2) * m) + (2 * m) + 1];
        Span<ulong> table = work.AsSpan(0, TableSize * m);
        Span<ulong> accumulator = work.AsSpan(TableSize * m, m);
        Span<ulong> selected = work.AsSpan((TableSize + 1) * m, m);
        Span<ulong> scratch = work.AsSpan((TableSize + 2) * m);
        try
        {
            CopyToWideLimbs(baseValue[..LimbCount], selected);
            FillWideTable(selected, table, scratch);
            wideOne.CopyTo(accumulator);
            foreach (byte exponentByte in exponent)
            {
                ApplyWideWindow(accumulator, table, (uint)exponentByte >> WindowBits, selected, scratch, operations);
                ApplyWideWindow(accumulator, table, exponentByte & 0xFu, selected, scratch, operations);
            }

            selected.Clear();
            selected[0] = 1;
            MultiplyWide(accumulator, accumulator, selected, scratch);
            for (int index = 0; index < LimbCount; index++)
            {
                result[index] = (uint)(accumulator[index >> 1] >> (32 * (index & 1)));
            }
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
    /// <remarks>
    /// With an even <see cref="LimbCount" /> the 64-bit limbs' radix R' equals R, so the
    /// 32-bit limbs are read in place as 64-bit ones (little-endian, as every .NET target
    /// is) and <see cref="MultiplyWide" /> does the work in a quarter of the inner-loop
    /// steps; an odd count keeps the 32-bit loop. The choice rests on the limb count alone,
    /// which is public, and neither loop has a bound or index that depends on a value.
    /// </remarks>
    public void Multiply(Span<uint> result, ReadOnlySpan<uint> left, ReadOnlySpan<uint> right, Span<uint> scratch)
    {
        int n = LimbCount;
        if ((n & 1) == 0)
        {
            MultiplyWide(
                MemoryMarshal.Cast<uint, ulong>(result[..n]),
                MemoryMarshal.Cast<uint, ulong>(left[..n]),
                MemoryMarshal.Cast<uint, ulong>(right[..n]),
                MemoryMarshal.Cast<uint, ulong>(scratch[..(n + 2)]));
            return;
        }

        MultiplyNarrow(result, left, right, scratch);
    }

    /// <summary>The 32-bit CIOS loop of <see cref="Multiply" />, for an odd <see cref="LimbCount" />.</summary>
    private void MultiplyNarrow(Span<uint> result, ReadOnlySpan<uint> left, ReadOnlySpan<uint> right, Span<uint> scratch)
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
    /// Sets <paramref name="result" /> to <paramref name="value" /> * R modulo the modulus:
    /// the value moved into Montgomery form, a Montgomery multiplication by R^2.
    /// <paramref name="result" /> may alias <paramref name="value" />.
    /// </summary>
    /// <param name="value">The value in ordinary form, below the modulus.</param>
    /// <param name="result">Receives the value in Montgomery form.</param>
    /// <param name="scratch">At least <see cref="LimbCount" /> + 2 limbs of working space.</param>
    public void ToMontgomeryForm(ReadOnlySpan<uint> value, Span<uint> result, Span<uint> scratch) =>
        Multiply(result, value, rSquared, scratch);

    /// <summary>
    /// Sets <paramref name="result" /> to <paramref name="left" /> * <paramref name="right" />
    /// modulo the modulus, in ordinary form: a Montgomery multiplication, then one by R^2.
    /// <paramref name="result" /> may alias either operand.
    /// </summary>
    /// <param name="result">Receives the product.</param>
    /// <param name="left">The first operand, below R = 2^(32 * <see cref="LimbCount" />).</param>
    /// <param name="right">The second operand, below the modulus.</param>
    public void MultiplyModulo(Span<uint> result, ReadOnlySpan<uint> left, ReadOnlySpan<uint> right)
    {
        uint[] scratch = new uint[LimbCount + 2];
        try
        {
            Multiply(result, left, right, scratch);
            Multiply(result, result, rSquared, scratch);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(scratch.AsSpan()));
        }
    }

    /// <summary>
    /// Sets <paramref name="result" /> to <paramref name="value" /> modulo the modulus, for a
    /// value of any number of limbs: Horner's rule over chunks of <see cref="LimbCount" />
    /// limbs, each chunk moved into Montgomery form and the running sum multiplied by R.
    /// </summary>
    /// <param name="value">The little-endian limbs to reduce.</param>
    /// <param name="result">Receives the residue, <see cref="LimbCount" /> limbs.</param>
    public void Reduce(ReadOnlySpan<uint> value, Span<uint> result)
    {
        int n = LimbCount;
        uint[] work = new uint[(4 * n) + 2];
        Span<uint> sum = work.AsSpan(0, n);
        Span<uint> chunk = work.AsSpan(n, n);
        Span<uint> term = work.AsSpan(2 * n, n);
        Span<uint> scratch = work.AsSpan(3 * n);
        try
        {
            for (int start = (value.Length - 1) / n * n; start >= 0; start -= n)
            {
                chunk.Clear();
                value.Slice(start, Math.Min(n, value.Length - start)).CopyTo(chunk);
                Multiply(sum, sum, rSquared, scratch);
                Multiply(term, chunk, rSquared, scratch);
                Add(sum, sum, term, scratch);
            }

            term.Clear();
            term[0] = 1;
            Multiply(result, sum, term, scratch);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(work.AsSpan()));
        }
    }

    /// <summary>
    /// Sets <paramref name="result" /> to <paramref name="left" /> - <paramref name="right" />
    /// modulo the modulus, adding the modulus back by mask when the subtraction borrowed.
    /// <paramref name="result" /> may alias either operand.
    /// </summary>
    /// <param name="result">Receives the difference.</param>
    /// <param name="left">The minuend, below the modulus.</param>
    /// <param name="right">The subtrahend, below the modulus.</param>
    public void Subtract(Span<uint> result, ReadOnlySpan<uint> left, ReadOnlySpan<uint> right)
    {
        int n = LimbCount;
        ulong borrow = 0;
        for (int index = 0; index < n; index++)
        {
            ulong difference = (ulong)left[index] - right[index] - borrow;
            result[index] = (uint)difference;
            borrow = difference >> 63;
        }

        uint addBack = 0u - (uint)borrow;
        ulong carry = 0;
        for (int index = 0; index < n; index++)
        {
            ulong sum = result[index] + (ulong)(modulus[index] & addBack) + carry;
            result[index] = (uint)sum;
            carry = sum >> 32;
        }
    }

    /// <summary>
    /// Sets <paramref name="result" /> to <paramref name="left" /> + <paramref name="right" />
    /// modulo the modulus, both below it, subtracting the modulus by mask. <paramref name="result" />
    /// may alias either operand.
    /// </summary>
    /// <param name="result">Receives the sum.</param>
    /// <param name="left">The first addend, below the modulus.</param>
    /// <param name="right">The second addend, below the modulus.</param>
    /// <param name="scratch">At least <see cref="LimbCount" /> + 2 limbs of working space.</param>
    public void Add(Span<uint> result, ReadOnlySpan<uint> left, ReadOnlySpan<uint> right, Span<uint> scratch)
    {
        int n = LimbCount;
        Span<uint> total = scratch[..(n + 2)];
        ulong carry = 0;
        for (int index = 0; index < n; index++)
        {
            ulong sum = left[index] + (ulong)right[index] + carry;
            total[index] = (uint)sum;
            carry = sum >> 32;
        }

        total[n] = (uint)carry;
        total[n + 1] = 0;
        SubtractModulusIfNotBelow(result, total);
    }

    /// <summary>Returns whether <paramref name="value" />, <see cref="LimbCount" /> limbs, is below the modulus: whether subtracting the modulus borrows.</summary>
    /// <param name="value">The value to compare.</param>
    /// <returns><see langword="true" /> when the value is below the modulus.</returns>
    public bool IsBelowModulus(ReadOnlySpan<uint> value)
    {
        ulong borrow = 0;
        for (int index = 0; index < LimbCount; index++)
        {
            borrow = ((ulong)value[index] - modulus[index] - borrow) >> 63;
        }

        return borrow != 0;
    }

    /// <summary>Zeroes the modulus and the values derived from it, for a modulus that is a secret.</summary>
    public void Clear()
    {
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(modulus.AsSpan()));
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(rSquared.AsSpan()));
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(one.AsSpan()));
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(wideModulus.AsSpan()));
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(wideRSquared.AsSpan()));
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(wideOne.AsSpan()));
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

    /// <summary>
    /// Returns -m^-1 modulo 2^64 for the odd lowest 64-bit limb <paramref name="lowestLimb" />,
    /// by Newton's iteration (1, 2, 4, ... 64 correct bits).
    /// </summary>
    private static ulong ComputeWideNegativeInverse(ulong lowestLimb)
    {
        ulong inverse = 1;
        for (int step = 0; step < 6; step++)
        {
            inverse *= 2ul - (lowestLimb * inverse);
        }

        return 0ul - inverse;
    }

    /// <summary>Returns a new array of <paramref name="wideLimbCount" /> 64-bit limbs holding the 32-bit <paramref name="limbs" />.</summary>
    private static ulong[] ToWideLimbs(ReadOnlySpan<uint> limbs, int wideLimbCount)
    {
        ulong[] wide = new ulong[wideLimbCount];
        CopyToWideLimbs(limbs, wide);
        return wide;
    }

    /// <summary>Packs the 32-bit <paramref name="limbs" />, least significant first, into the 64-bit <paramref name="wide" />, zero-extending.</summary>
    private static void CopyToWideLimbs(ReadOnlySpan<uint> limbs, Span<ulong> wide)
    {
        wide.Clear();
        for (int index = 0; index < limbs.Length; index++)
        {
            wide[index >> 1] |= (ulong)limbs[index] << (32 * (index & 1));
        }
    }

    /// <summary>
    /// The carry out of adding a value to <paramref name="left" /> that gave <paramref name="sum" />, 0 or 1:
    /// an unsigned comparison as a value, which compiles to <c>clt.un</c> and then to a flag
    /// set (<c>setb</c> on x64, <c>cset</c> on Arm64), never a branch.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong CarryOut(ulong left, ulong sum) =>
        Unsafe.BitCast<bool, byte>(sum < left);

    /// <summary>The borrow out of <paramref name="left" /> - <paramref name="right" />, 0 or 1, the same branch-free comparison as <see cref="CarryOut" />.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong BorrowOut(ulong left, ulong right) =>
        Unsafe.BitCast<bool, byte>(left < right);

    /// <summary>
    /// Returns the high half of <paramref name="left" /> * <paramref name="right" /> +
    /// <paramref name="first" /> + <paramref name="second" />, which never exceeds 128 bits,
    /// and writes the low half to <paramref name="low" />.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong MultiplyAdd(ulong left, ulong right, ulong first, ulong second, out ulong low)
    {
        ulong high = Math.BigMul(left, right, out ulong productLow);
        ulong withFirst = productLow + first;
        ulong withSecond = withFirst + second;
        low = withSecond;
        return high + CarryOut(productLow, withFirst) + CarryOut(withFirst, withSecond);
    }

    /// <summary>Sets <paramref name="destination" /> to the one entry of <paramref name="table" /> <paramref name="window" /> names, reading every entry.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void SelectWideEntry(ReadOnlySpan<ulong> table, uint window, Span<ulong> destination)
    {
        int m = destination.Length;
        destination.Clear();
        for (int entry = 0; entry < TableSize; entry++)
        {
            ulong mask = 0ul - (((ulong)((uint)entry ^ window) - 1) >> 63);
            ReadOnlySpan<ulong> candidate = table.Slice(entry * m, m);
            for (int limb = 0; limb < m; limb++)
            {
                destination[limb] |= candidate[limb] & mask;
            }
        }
    }

    /// <summary>Doubles <paramref name="value" />, below the modulus, <paramref name="times" /> times modulo the modulus.</summary>
    private void DoubleRepeatedly(Span<uint> value, int times, Span<uint> scratch)
    {
        for (int step = 0; step < times; step++)
        {
            Add(value, value, value, scratch);
        }
    }

    /// <summary>Fills the table with base^0 to base^15 in the 64-bit limbs' Montgomery form.</summary>
    private void FillWideTable(ReadOnlySpan<ulong> baseValue, Span<ulong> table, Span<ulong> scratch)
    {
        int m = wideModulus.Length;
        wideOne.CopyTo(table[..m]);
        Span<ulong> first = table.Slice(m, m);
        MultiplyWide(first, baseValue, wideRSquared, scratch);
        for (int entry = 2; entry < TableSize; entry++)
        {
            MultiplyWide(table.Slice(entry * m, m), table.Slice((entry - 1) * m, m), first, scratch);
        }
    }

    /// <summary>Squares the accumulator four times, then multiplies in table entry <paramref name="window" />, even when it is base^0.</summary>
    private void ApplyWideWindow(Span<ulong> accumulator, ReadOnlySpan<ulong> table, uint window, Span<ulong> selected, Span<ulong> scratch, ICollection<string>? operations)
    {
        for (int square = 0; square < WindowBits; square++)
        {
            SquareWide(accumulator, accumulator, scratch);
            operations?.Add("square");
        }

        SelectWideEntry(table, window, selected);
        operations?.Add("select");
        MultiplyWide(accumulator, accumulator, selected, scratch);
        operations?.Add("multiply");
    }

    /// <summary>
    /// Montgomery multiplication on 64-bit limbs, finely integrated (one pass per limb of
    /// <paramref name="right" /> both multiplies and reduces): <paramref name="result" /> =
    /// <paramref name="left" /> * <paramref name="right" /> * R'^-1 modulo the modulus, for
    /// operands below it. <paramref name="result" /> may alias either operand.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void MultiplyWide(Span<ulong> result, ReadOnlySpan<ulong> left, ReadOnlySpan<ulong> right, Span<ulong> scratch)
    {
        int m = wideModulus.Length;
        Span<ulong> total = scratch[..(m + 1)];
        total.Clear();
        left = left[..m];
        right = right[..m];

        // The inner loop reads by reference past one bounds check per span above: it is
        // where the exponentiation spends its time.
        ref ulong leftLimbs = ref MemoryMarshal.GetReference(left);
        ref ulong modulusLimbs = ref MemoryMarshal.GetArrayDataReference(wideModulus);
        ref ulong totalLimbs = ref MemoryMarshal.GetReference(total);
        for (int index = 0; index < m; index++)
        {
            ulong factorOfRight = right[index];
            ulong productCarry = MultiplyAdd(leftLimbs, factorOfRight, totalLimbs, 0, out ulong low);
            ulong factor = low * wideNegativeInverse;
            ulong reductionCarry = MultiplyAdd(factor, modulusLimbs, low, 0, out _);
            for (int limb = 1; limb < m; limb++)
            {
                productCarry = MultiplyAdd(Unsafe.Add(ref leftLimbs, limb), factorOfRight, Unsafe.Add(ref totalLimbs, limb), productCarry, out low);
                reductionCarry = MultiplyAdd(factor, Unsafe.Add(ref modulusLimbs, limb), low, reductionCarry, out Unsafe.Add(ref totalLimbs, limb - 1));
            }

            ulong carries = productCarry + reductionCarry;
            ulong top = carries + total[m];
            total[m - 1] = top;
            total[m] = CarryOut(productCarry, carries) + CarryOut(carries, top);
        }

        SubtractWideModulusIfNotBelow(result, total);
    }

    /// <summary>
    /// Montgomery squaring on 64-bit limbs, separated operand scanning: the full square,
    /// each cross product computed once and doubled, then reduced limb by limb.
    /// <paramref name="result" /> = <paramref name="value" />^2 * R'^-1 modulo the modulus,
    /// for a value below it; <paramref name="result" /> may alias <paramref name="value" />.
    /// <paramref name="scratch" /> holds at least 2 * m + 1 limbs.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void SquareWide(Span<ulong> result, ReadOnlySpan<ulong> value, Span<ulong> scratch)
    {
        int m = wideModulus.Length;
        Span<ulong> total = scratch[..((2 * m) + 1)];
        total.Clear();
        value = value[..m];
        ref ulong valueLimbs = ref MemoryMarshal.GetReference(value);
        ref ulong totalLimbs = ref MemoryMarshal.GetReference(total);
        for (int row = 0; row < m; row++)
        {
            ulong rowLimb = value[row];
            ulong carry = 0;
            for (int column = row + 1; column < m; column++)
            {
                ref ulong target = ref Unsafe.Add(ref totalLimbs, row + column);
                carry = MultiplyAdd(rowLimb, Unsafe.Add(ref valueLimbs, column), target, carry, out target);
            }

            total[row + m] = carry;
        }

        DoubleAndAddDiagonal(total, value);
        ReduceWide(total);
        SubtractWideModulusIfNotBelow(result, total.Slice(m, m + 1));
    }

    /// <summary>Doubles the cross products in <paramref name="total" />, then adds each value limb's square on the diagonal.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void DoubleAndAddDiagonal(Span<ulong> total, ReadOnlySpan<ulong> value)
    {
        ulong shiftedOut = 0;
        for (int index = 0; index < 2 * value.Length; index++)
        {
            ulong limb = total[index];
            total[index] = (limb << 1) | shiftedOut;
            shiftedOut = limb >> 63;
        }

        ulong carry = 0;
        for (int index = 0; index < value.Length; index++)
        {
            ulong high = MultiplyAdd(value[index], value[index], total[2 * index], carry, out total[2 * index]);
            ulong sum = total[(2 * index) + 1] + high;
            carry = CarryOut(high, sum);
            total[(2 * index) + 1] = sum;
        }
    }

    /// <summary>
    /// Montgomery reduction of the 2 * m + 1 limbs of <paramref name="total" />, below
    /// R' times the modulus: adds the multiple of the modulus that clears each low limb in
    /// turn, leaving total / R' in limbs m to 2 * m.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void ReduceWide(Span<ulong> total)
    {
        int m = wideModulus.Length;
        ref ulong modulusLimbs = ref MemoryMarshal.GetArrayDataReference(wideModulus);
        ref ulong totalLimbs = ref MemoryMarshal.GetReference(total);
        ulong pending = 0;
        for (int index = 0; index < m; index++)
        {
            ulong factor = total[index] * wideNegativeInverse;
            ulong carry = 0;
            for (int limb = 0; limb < m; limb++)
            {
                ref ulong target = ref Unsafe.Add(ref totalLimbs, index + limb);
                carry = MultiplyAdd(factor, Unsafe.Add(ref modulusLimbs, limb), target, carry, out target);
            }

            ulong withCarry = total[index + m] + carry;
            ulong withPending = withCarry + pending;
            pending = CarryOut(carry, withCarry) + CarryOut(withCarry, withPending);
            total[index + m] = withPending;
        }

        total[2 * m] = pending;
    }

    /// <summary>
    /// Writes total - modulus to <paramref name="result" />, then keeps total instead, by
    /// mask, when the subtraction borrowed out of total's top limb.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void SubtractWideModulusIfNotBelow(Span<ulong> result, ReadOnlySpan<ulong> total)
    {
        ReadOnlySpan<ulong> wide = wideModulus;
        int m = wide.Length;
        ulong borrow = 0;
        for (int index = 0; index < m; index++)
        {
            ulong difference = total[index] - wide[index];
            ulong withBorrow = difference - borrow;
            borrow = BorrowOut(total[index], wide[index]) | BorrowOut(difference, borrow);
            result[index] = withBorrow;
        }

        ulong keepTotal = 0ul - BorrowOut(total[m], borrow);
        for (int index = 0; index < m; index++)
        {
            result[index] = (total[index] & keepTotal) | (result[index] & ~keepTotal);
        }
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
