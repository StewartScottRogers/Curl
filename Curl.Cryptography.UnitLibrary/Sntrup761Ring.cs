using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// sntrup761's arithmetic (NTRU Prime round 3, 2020, and its reference <c>kem.c</c>) in
/// the rings R/3 and R/q, R = Z[x]/(x^p - x - 1), p = 761, q = 4591: multiplication,
/// the constant-time reciprocals by divided-difference steps, rounding, the short and
/// small polynomials drawn from random bytes, and the core <see cref="Decrypt" />. Every
/// polynomial is a span of <see cref="P" /> coefficients held as <see cref="short" />:
/// -1 to 1 for R/3, -(q-1)/2 to (q-1)/2 for R/q.
/// </summary>
/// <remarks>
/// Constant-time: loop bounds are the public p and w, conditional swaps and selects are
/// masks, and every reduction goes through <see cref="Uint14Division" /> rather than the
/// divide instruction. Every temporary is zeroed before returning.
/// </remarks>
internal static class Sntrup761Ring
{
    /// <summary>The degree p of x^p - x - 1.</summary>
    public const int P = 761;

    /// <summary>The modulus q.</summary>
    public const int Q = 4591;

    /// <summary>The weight w: the number of nonzero coefficients of a short polynomial.</summary>
    public const int W = 286;

    private const int HalfQ = (Q - 1) / 2;

    /// <summary>Reduces <paramref name="value" /> to -1, 0 or 1 modulo 3 (<c>F3_freeze</c>).</summary>
    public static short FreezeModThree(int value) => (short)(Uint14Division.Remainder(value + 1, 3) - 1);

    /// <summary>Reduces <paramref name="value" /> to -(q-1)/2 to (q-1)/2 modulo q (<c>Fq_freeze</c>).</summary>
    public static short FreezeModQ(int value) => (short)(Uint14Division.Remainder(value + HalfQ, Q) - HalfQ);

    /// <summary>-1 when the low 16 bits of <paramref name="value" /> are nonzero, else 0 (<c>int16_nonzero_mask</c>).</summary>
    public static int NonzeroMask(int value) => -(int)((0u - (ushort)value) >> 31);

    /// <summary>-1 when <paramref name="value" /> is negative as a 16-bit integer, else 0 (<c>int16_negative_mask</c>).</summary>
    public static int NegativeMask(int value) => -((ushort)value >> 15);

    /// <summary><paramref name="product" /> = <paramref name="factor" /> times the small <paramref name="small" /> in R/q (<c>Rq_mult_small</c>).</summary>
    public static void MultiplyModQ(Span<short> product, ReadOnlySpan<short> factor, ReadOnlySpan<short> small)
    {
        Span<int> unreduced = stackalloc int[(2 * P) - 1];
        try
        {
            MultiplyUnreduced(unreduced, factor, small);
            for (int index = 0; index < P; index++)
            {
                product[index] = FreezeModQ(unreduced[index]);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(unreduced));
        }
    }

    /// <summary><paramref name="product" /> = <paramref name="left" /> times <paramref name="right" /> in R/3 (<c>R3_mult</c>).</summary>
    public static void MultiplyModThree(Span<short> product, ReadOnlySpan<short> left, ReadOnlySpan<short> right)
    {
        Span<int> unreduced = stackalloc int[(2 * P) - 1];
        try
        {
            MultiplyUnreduced(unreduced, left, right);
            for (int index = 0; index < P; index++)
            {
                product[index] = FreezeModThree(unreduced[index]);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(unreduced));
        }
    }

    /// <summary>
    /// Sets <paramref name="reciprocal" /> to 1 / <paramref name="small" /> in R/3 and returns
    /// 0, or returns -1 when <paramref name="small" /> is not invertible (<c>R3_recip</c>).
    /// </summary>
    public static int ReciprocalModThree(Span<short> reciprocal, ReadOnlySpan<short> small)
    {
        Span<short> state = stackalloc short[4 * (P + 1)];
        Span<short> f = state[..(P + 1)];
        Span<short> g = state.Slice(P + 1, P + 1);
        Span<short> v = state.Slice(2 * (P + 1), P + 1);
        Span<short> r = state.Slice(3 * (P + 1), P + 1);
        try
        {
            InitializeDivisionSteps(f, g, v, r, small, 1);
            int delta = 1;
            for (int loop = 0; loop < (2 * P) - 1; loop++)
            {
                delta = ShiftAndSwap(f, g, v, r, delta);
                int sign = -g[0] * f[0];
                for (int index = 0; index <= P; index++)
                {
                    g[index] = FreezeModThree(g[index] + (sign * f[index]));
                    r[index] = FreezeModThree(r[index] + (sign * v[index]));
                }

                DivideByX(g);
            }

            for (int index = 0; index < P; index++)
            {
                reciprocal[index] = (short)(f[0] * v[P - 1 - index]);
            }

            return NonzeroMask(delta);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(state));
        }
    }

    /// <summary>
    /// Sets <paramref name="reciprocal" /> to 1 / (3 <paramref name="small" />) in R/q and
    /// returns 0, or -1 when there is none (<c>Rq_recip3</c>). For a short polynomial there
    /// always is one, because x^p - x - 1 is irreducible modulo q.
    /// </summary>
    public static int ReciprocalOfThreeTimesModQ(Span<short> reciprocal, ReadOnlySpan<short> small)
    {
        Span<short> state = stackalloc short[4 * (P + 1)];
        Span<short> f = state[..(P + 1)];
        Span<short> g = state.Slice(P + 1, P + 1);
        Span<short> v = state.Slice(2 * (P + 1), P + 1);
        Span<short> r = state.Slice(3 * (P + 1), P + 1);
        try
        {
            InitializeDivisionSteps(f, g, v, r, small, ReciprocalModQ(3));
            int delta = 1;
            for (int loop = 0; loop < (2 * P) - 1; loop++)
            {
                delta = ShiftAndSwap(f, g, v, r, delta);
                int f0 = f[0];
                int g0 = g[0];
                for (int index = 0; index <= P; index++)
                {
                    g[index] = FreezeModQ((f0 * g[index]) - (g0 * f[index]));
                    r[index] = FreezeModQ((f0 * r[index]) - (g0 * v[index]));
                }

                DivideByX(g);
            }

            int scale = ReciprocalModQ(f[0]);
            for (int index = 0; index < P; index++)
            {
                reciprocal[index] = FreezeModQ(scale * v[P - 1 - index]);
            }

            return NonzeroMask(delta);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(state));
        }
    }

    /// <summary>Rounds each coefficient to the nearest multiple of 3 (<c>Round</c>).</summary>
    public static void Round(Span<short> rounded, ReadOnlySpan<short> polynomial)
    {
        for (int index = 0; index < P; index++)
        {
            rounded[index] = (short)(polynomial[index] - FreezeModThree(polynomial[index]));
        }
    }

    /// <summary>
    /// A small polynomial from 4p random bytes, each coefficient -1, 0 or 1 from one
    /// little-endian 32-bit word (<c>Small_random</c>).
    /// </summary>
    public static void SmallFromRandom(Span<short> small, ReadOnlySpan<byte> random)
    {
        for (int index = 0; index < P; index++)
        {
            uint word = BinaryPrimitives.ReadUInt32LittleEndian(random.Slice(4 * index, 4));
            small[index] = (short)((int)(((word & 0x3fffffffu) * 3) >> 30) - 1);
        }
    }

    /// <summary>
    /// A short polynomial - exactly w coefficients of -1 or 1, the rest 0 - from 4p random
    /// bytes by sorting tagged little-endian 32-bit words (<c>Short_random</c>,
    /// <c>Short_fromlist</c>).
    /// </summary>
    public static void ShortFromRandom(Span<short> shortPolynomial, ReadOnlySpan<byte> random)
    {
        Span<uint> list = stackalloc uint[P];
        try
        {
            for (int index = 0; index < P; index++)
            {
                uint word = BinaryPrimitives.ReadUInt32LittleEndian(random.Slice(4 * index, 4));
                list[index] = index < W ? word & 0xFFFFFFFEu : (word & 0xFFFFFFFDu) | 1u;
            }

            SortingNetwork.Sort(list);
            for (int index = 0; index < P; index++)
            {
                shortPolynomial[index] = (short)((int)(list[index] & 3) - 1);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(list));
        }
    }

    /// <summary>
    /// The core decryption (<c>Decrypt</c>): r = e / g in R/3 for e = 3 f c reduced mod 3,
    /// or the fixed weight-w polynomial (1, ..., 1, 0, ..., 0) when that result does not
    /// have weight w; the choice is a mask.
    /// </summary>
    public static void Decrypt(Span<short> result, ReadOnlySpan<short> ciphertext, ReadOnlySpan<short> f, ReadOnlySpan<short> gReciprocal)
    {
        Span<short> scratch = stackalloc short[2 * P];
        Span<short> product = scratch[..P];
        Span<short> reduced = scratch[P..];
        try
        {
            MultiplyModQ(product, ciphertext, f);
            for (int index = 0; index < P; index++)
            {
                reduced[index] = FreezeModThree(FreezeModQ(3 * product[index]));
            }

            MultiplyModThree(product, reduced, gReciprocal);
            int mask = WeightMask(product);
            for (int index = 0; index < P; index++)
            {
                int fallback = index < W ? 1 : 0;
                result[index] = (short)(((product[index] ^ fallback) & ~mask) ^ fallback);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(scratch));
        }
    }

    /// <summary>0 when <paramref name="small" /> has exactly w nonzero coefficients, else -1 (<c>Weightw_mask</c>).</summary>
    public static int WeightMask(ReadOnlySpan<short> small)
    {
        int weight = 0;
        for (int index = 0; index < P; index++)
        {
            weight += small[index] & 1;
        }

        return NonzeroMask(weight - W);
    }

    /// <summary>1 / <paramref name="value" /> mod q as value^(q-2), a fixed number of multiplications (<c>Fq_recip</c>).</summary>
    public static int ReciprocalModQ(int value)
    {
        int power = value;
        for (int exponent = 1; exponent < Q - 2; exponent++)
        {
            power = FreezeModQ(value * power);
        }

        return power;
    }

    /// <summary>
    /// The product of two polynomials with x^p folded back as x + 1, unreduced: the first
    /// p entries of <paramref name="unreduced" /> hold the result. Coefficients below 2^12
    /// keep every sum far inside an <see cref="int" />.
    /// </summary>
    private static void MultiplyUnreduced(Span<int> unreduced, ReadOnlySpan<short> left, ReadOnlySpan<short> right)
    {
        unreduced.Clear();
        for (int i = 0; i < P; i++)
        {
            int coefficient = left[i];
            for (int j = 0; j < P; j++)
            {
                unreduced[i + j] += coefficient * right[j];
            }
        }

        for (int index = (2 * P) - 2; index >= P; index--)
        {
            unreduced[index - P] += unreduced[index];
            unreduced[index - P + 1] += unreduced[index];
        }
    }

    /// <summary>
    /// f = x^p - x - 1 reversed, g = the input reversed, v = 0 and r = <paramref name="rStart" />:
    /// the starting state of both reciprocals.
    /// </summary>
    private static void InitializeDivisionSteps(
        Span<short> f,
        Span<short> g,
        Span<short> v,
        Span<short> r,
        ReadOnlySpan<short> input,
        int rStart)
    {
        v.Clear();
        r.Clear();
        r[0] = (short)rStart;
        f.Clear();
        f[0] = 1;
        f[P - 1] = -1;
        f[P] = -1;
        for (int index = 0; index < P; index++)
        {
            g[P - 1 - index] = input[index];
        }

        g[P] = 0;
    }

    /// <summary>
    /// The shared head of one division step: v = x v, then swap (f, v) with (g, r) and
    /// negate delta when delta > 0 and g(0) != 0; returns delta + 1. All by mask.
    /// </summary>
    private static int ShiftAndSwap(Span<short> f, Span<short> g, Span<short> v, Span<short> r, int delta)
    {
        for (int index = P; index > 0; index--)
        {
            v[index] = v[index - 1];
        }

        v[0] = 0;
        int swap = NegativeMask(-delta) & NonzeroMask(g[0]);
        delta ^= swap & (delta ^ -delta);
        for (int index = 0; index <= P; index++)
        {
            int difference = swap & (f[index] ^ g[index]);
            f[index] ^= (short)difference;
            g[index] ^= (short)difference;
            difference = swap & (v[index] ^ r[index]);
            v[index] ^= (short)difference;
            r[index] ^= (short)difference;
        }

        return delta + 1;
    }

    /// <summary>g = g / x: drops the constant coefficient, which the step made zero.</summary>
    private static void DivideByX(Span<short> g)
    {
        for (int index = 0; index < P; index++)
        {
            g[index] = g[index + 1];
        }

        g[P] = 0;
    }
}
