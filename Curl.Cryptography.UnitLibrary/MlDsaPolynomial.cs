namespace Curl.Cryptography;

/// <summary>
/// ML-DSA's arithmetic on polynomials of 256 coefficients modulo q = 8380417 (FIPS 204
/// sections 7.4 and 7.5): reduction, the NTT and its inverse, multiplication in the NTT
/// domain, the rounding functions Power2Round, Decompose, HighBits and LowBits, the hints,
/// and the infinity-norm check. Coefficients are <see cref="int" />s kept in [0, q); the
/// centred representative is taken only where a function needs it.
/// </summary>
/// <remarks>
/// Constant-time in every coefficient: reduction multiplies by a reciprocal and corrects
/// with a mask instead of dividing, the rounding functions and the norm check are
/// arithmetic on masks, and no branch or index depends on a coefficient.
/// <see cref="UseHint" /> is the one exception: only verification calls it, on public data.
/// </remarks>
internal static class MlDsaPolynomial
{
    /// <summary>n, the number of coefficients.</summary>
    public const int Degree = 256;

    /// <summary>q, the modulus.</summary>
    public const int Modulus = 8380417;

    private const int HalfModulusRoundedDown = (Modulus - 1) / 2;

    // floor((2^64 - 1) / q), which is floor(2^64 / q) because q is odd: the high 64 bits
    // of a product with it are floor(x / q) or one less, for every 64-bit x.
    private const ulong ModulusReciprocal = ulong.MaxValue / Modulus;
    private const int Generator = 1753;
    private const int InverseNttScale = 8347681; // 256^-1 mod q
    private const int Gamma2Of44 = (Modulus - 1) / 88;
    private const int Power2RoundHalf = 1 << (MlDsaParameters.DroppedBits - 1);

    // zeta^BitRev8(i) (FIPS 204 appendix B), zeta = 1753.
    private static readonly int[] Zetas = ComputeZetas();

    /// <summary>Returns <paramref name="value" /> mod q without dividing.</summary>
    public static int Reduce(ulong value)
    {
        ulong quotient = Math.BigMul(value, ModulusReciprocal, out _);
        return SubtractModulusIfAtLeast((int)(value - (quotient * Modulus)));
    }

    /// <summary>Returns (<paramref name="left" /> + <paramref name="right" />) mod q for two reduced values.</summary>
    public static int Add(int left, int right) => SubtractModulusIfAtLeast(left + right);

    /// <summary>Returns (<paramref name="left" /> - <paramref name="right" />) mod q for two reduced values.</summary>
    public static int Subtract(int left, int right) => SubtractModulusIfAtLeast(left - right + Modulus);

    /// <summary>Returns (<paramref name="left" /> * <paramref name="right" />) mod q for two reduced values.</summary>
    public static int Multiply(int left, int right) => Reduce((ulong)left * (ulong)right);

    /// <summary>Returns the reduced value of <paramref name="value" />, which lies in (-q, q).</summary>
    public static int FromSigned(int value) => value + (Modulus & (value >> 31));

    /// <summary>Returns the representative of the reduced <paramref name="value" /> in [-(q - 1) / 2, (q - 1) / 2].</summary>
    public static int ToCentered(int value) =>
        value - (int)((uint)Modulus & ConstantTime.LessThanMask(HalfModulusRoundedDown, (uint)value));

    /// <summary>Replaces <paramref name="polynomial" /> with its NTT (FIPS 204 algorithm 41).</summary>
    public static void Ntt(Span<int> polynomial)
    {
        int zetaIndex = 0;
        for (int length = 128; length >= 1; length /= 2)
        {
            for (int start = 0; start < Degree; start += 2 * length)
            {
                int zeta = Zetas[++zetaIndex];
                for (int index = start; index < start + length; index++)
                {
                    int product = Multiply(zeta, polynomial[index + length]);
                    polynomial[index + length] = Subtract(polynomial[index], product);
                    polynomial[index] = Add(polynomial[index], product);
                }
            }
        }
    }

    /// <summary>Replaces <paramref name="polynomial" /> with its inverse NTT (FIPS 204 algorithm 42).</summary>
    public static void InverseNtt(Span<int> polynomial)
    {
        int zetaIndex = Degree;
        for (int length = 1; length < Degree; length *= 2)
        {
            for (int start = 0; start < Degree; start += 2 * length)
            {
                int zeta = Modulus - Zetas[--zetaIndex];
                for (int index = start; index < start + length; index++)
                {
                    int first = polynomial[index];
                    polynomial[index] = Add(first, polynomial[index + length]);
                    polynomial[index + length] = Multiply(zeta, Subtract(first, polynomial[index + length]));
                }
            }
        }

        for (int index = 0; index < Degree; index++)
        {
            polynomial[index] = Multiply(polynomial[index], InverseNttScale);
        }
    }

    /// <summary>Replaces each polynomial of <paramref name="vector" /> with its NTT.</summary>
    public static void NttEach(Span<int> vector)
    {
        for (int offset = 0; offset < vector.Length; offset += Degree)
        {
            Ntt(vector.Slice(offset, Degree));
        }
    }

    /// <summary>Replaces each polynomial of <paramref name="vector" /> with its inverse NTT.</summary>
    public static void InverseNttEach(Span<int> vector)
    {
        for (int offset = 0; offset < vector.Length; offset += Degree)
        {
            InverseNtt(vector.Slice(offset, Degree));
        }
    }

    /// <summary>
    /// Adds the NTT-domain product of <paramref name="left" /> and <paramref name="right" />
    /// to <paramref name="accumulator" /> coefficient by coefficient (FIPS 204 algorithm 45).
    /// </summary>
    public static void MultiplyNttsAndAdd(Span<int> accumulator, ReadOnlySpan<int> left, ReadOnlySpan<int> right)
    {
        for (int index = 0; index < Degree; index++)
        {
            accumulator[index] = Add(accumulator[index], Multiply(left[index], right[index]));
        }
    }

    /// <summary>Adds <paramref name="addend" /> to <paramref name="polynomial" /> coefficient by coefficient.</summary>
    public static void AddTo(Span<int> polynomial, ReadOnlySpan<int> addend)
    {
        for (int index = 0; index < polynomial.Length; index++)
        {
            polynomial[index] = Add(polynomial[index], addend[index]);
        }
    }

    /// <summary>Subtracts <paramref name="subtrahend" /> from <paramref name="polynomial" /> coefficient by coefficient.</summary>
    public static void SubtractFrom(Span<int> polynomial, ReadOnlySpan<int> subtrahend)
    {
        for (int index = 0; index < polynomial.Length; index++)
        {
            polynomial[index] = Subtract(polynomial[index], subtrahend[index]);
        }
    }

    /// <summary>
    /// Splits the reduced <paramref name="value" /> r into r1 2^d + r0 with r0 in
    /// (-2^(d-1), 2^(d-1)] (FIPS 204 algorithm 35), returning r1 and writing r0 reduced mod q.
    /// </summary>
    public static int Power2Round(int value, out int low)
    {
        int high = (value + Power2RoundHalf - 1) >> MlDsaParameters.DroppedBits;
        low = FromSigned(value - (high << MlDsaParameters.DroppedBits));
        return high;
    }

    /// <summary>
    /// Splits the reduced <paramref name="value" /> r into r1 2 gamma2 + r0 with r0 centred
    /// (FIPS 204 algorithm 36), returning r1 and writing r0 as a signed value; the case
    /// r - r0 = q - 1 gives r1 = 0 and r0 - 1, as the standard requires, without a branch.
    /// </summary>
    public static int Decompose(int value, int gamma2, out int low)
    {
        int high = (value + 127) >> 7;
        if (gamma2 == Gamma2Of44)
        {
            high = ((high * 11275) + (1 << 23)) >> 24;
            high ^= ((43 - high) >> 31) & high;
        }
        else
        {
            high = ((high * 1025) + (1 << 21)) >> 22;
            high &= 15;
        }

        low = value - (high * 2 * gamma2);
        low -= ((HalfModulusRoundedDown - low) >> 31) & Modulus;
        return high;
    }

    /// <summary>Returns r1 of <see cref="Decompose" /> (FIPS 204 algorithm 37).</summary>
    public static int HighBits(int value, int gamma2) => Decompose(value, gamma2, out _);

    /// <summary>
    /// Returns 1 when adding <paramref name="addend" /> to <paramref name="value" /> changes
    /// its high bits and 0 otherwise (FIPS 204 algorithm 39, MakeHint(z, r) with z the
    /// addend and r the value), without a branch.
    /// </summary>
    public static int MakeHint(int addend, int value, int gamma2)
    {
        uint before = (uint)HighBits(value, gamma2);
        uint after = (uint)HighBits(Add(value, addend), gamma2);
        return (int)(~ConstantTime.EqualMask(before, after) & 1u);
    }

    /// <summary>
    /// Returns the high bits of <paramref name="value" /> corrected by the hint bit
    /// <paramref name="hint" /> (FIPS 204 algorithm 40). Verification's, on public data.
    /// </summary>
    public static int UseHint(int hint, int value, int gamma2)
    {
        int modulus = (Modulus - 1) / (2 * gamma2);
        int high = Decompose(value, gamma2, out int low);
        int step = low > 0 ? hint : -hint;
        return (high + step + modulus) % modulus;
    }

    /// <summary>
    /// Returns whether any coefficient of <paramref name="polynomials" />, taken centred,
    /// has an absolute value of <paramref name="bound" /> or more: the infinity-norm check,
    /// reading every coefficient whatever it holds.
    /// </summary>
    public static bool ExceedsBound(ReadOnlySpan<int> polynomials, int bound)
    {
        uint exceeds = 0;
        foreach (int value in polynomials)
        {
            exceeds |= ~ConstantTime.LessThanMask((uint)Absolute(ToCentered(value)), (uint)bound);
        }

        return exceeds != 0;
    }

    /// <summary>
    /// Returns whether any coefficient's r0 of <see cref="Decompose" /> has an absolute value
    /// of <paramref name="bound" /> or more, reading every coefficient whatever it holds.
    /// </summary>
    public static bool LowBitsExceedBound(ReadOnlySpan<int> polynomials, int gamma2, int bound)
    {
        uint exceeds = 0;
        foreach (int value in polynomials)
        {
            Decompose(value, gamma2, out int low);
            exceeds |= ~ConstantTime.LessThanMask((uint)Absolute(low), (uint)bound);
        }

        return exceeds != 0;
    }

    private static int Absolute(int value)
    {
        int sign = value >> 31;
        return (value ^ sign) - sign;
    }

    private static int SubtractModulusIfAtLeast(int value)
    {
        int lowered = value - Modulus;
        return lowered + (Modulus & (lowered >> 31));
    }

    private static int[] ComputeZetas()
    {
        int[] zetas = new int[Degree];
        for (int index = 0; index < Degree; index++)
        {
            int exponent = ReverseEightBits(index);
            long power = 1;
            for (int step = 0; step < exponent; step++)
            {
                power = power * Generator % Modulus;
            }

            zetas[index] = (int)power;
        }

        return zetas;
    }

    private static int ReverseEightBits(int value)
    {
        int reversed = 0;
        for (int bit = 0; bit < 8; bit++)
        {
            reversed |= ((value >> bit) & 1) << (7 - bit);
        }

        return reversed;
    }
}
