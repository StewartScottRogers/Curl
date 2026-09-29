namespace Curl.Cryptography;

/// <summary>
/// ML-KEM's arithmetic on polynomials of 256 coefficients modulo q = 3329 (FIPS 203
/// section 4): reduction, the NTT and its inverse, multiplication in the NTT domain,
/// compression, byte encoding, and the two samplers. Coefficients are <see cref="int" />s
/// kept in [0, q).
/// </summary>
/// <remarks>
/// Constant-time in every coefficient: reduction multiplies by a reciprocal and corrects
/// with a mask instead of dividing, and no branch or index depends on a coefficient.
/// <see cref="SampleNtt" /> is the one exception, as FIPS 203 allows: it rejects samples
/// of the public seed rho, so its running time depends only on public data.
/// </remarks>
internal static class MlKemPolynomial
{
    /// <summary>n, the number of coefficients.</summary>
    public const int Degree = 256;

    /// <summary>q, the modulus.</summary>
    public const int Modulus = 3329;

    // floor(2^32 / q): a product with it, shifted down 32, is floor(x / q) or one less.
    private const ulong ModulusReciprocal = 1290167;
    private const int Generator = 17;
    private const int HalfModulusRoundedDown = Modulus / 2;
    private const int InverseNttScale = 3303; // 128^-1 mod q
    private const int XofBlockSize = 168;

    // zeta^BitRev7(i) (FIPS 203 appendix A) and zeta^(2 BitRev7(i) + 1), zeta = 17.
    private static readonly int[] Zetas = ComputePowers(bitReversed => bitReversed);
    private static readonly int[] Gammas = ComputePowers(bitReversed => (2 * bitReversed) + 1);

    /// <summary>Returns floor(<paramref name="value" /> / q) without dividing.</summary>
    public static uint DivideByModulus(uint value)
    {
        uint quotient = (uint)((value * ModulusReciprocal) >> 32);
        uint remainder = value - (quotient * Modulus);
        return quotient + (~ConstantTime.LessThanMask(remainder, Modulus) & 1u);
    }

    /// <summary>Returns <paramref name="value" /> mod q without dividing.</summary>
    public static int Reduce(uint value) => (int)(value - (DivideByModulus(value) * Modulus));

    /// <summary>Returns (<paramref name="left" /> + <paramref name="right" />) mod q for two reduced values.</summary>
    public static int Add(int left, int right) => SubtractModulusIfAtLeast(left + right);

    /// <summary>Returns (<paramref name="left" /> - <paramref name="right" />) mod q for two reduced values.</summary>
    public static int Subtract(int left, int right) => SubtractModulusIfAtLeast(left - right + Modulus);

    /// <summary>Returns (<paramref name="left" /> * <paramref name="right" />) mod q for two reduced values.</summary>
    public static int Multiply(int left, int right) => Reduce((uint)(left * right));

    /// <summary>Replaces <paramref name="polynomial" /> with its NTT (FIPS 203 algorithm 9).</summary>
    public static void Ntt(Span<int> polynomial)
    {
        int zetaIndex = 1;
        for (int length = 128; length >= 2; length /= 2)
        {
            for (int start = 0; start < Degree; start += 2 * length)
            {
                int zeta = Zetas[zetaIndex++];
                for (int index = start; index < start + length; index++)
                {
                    int product = Multiply(zeta, polynomial[index + length]);
                    polynomial[index + length] = Subtract(polynomial[index], product);
                    polynomial[index] = Add(polynomial[index], product);
                }
            }
        }
    }

    /// <summary>Replaces <paramref name="polynomial" /> with its inverse NTT (FIPS 203 algorithm 10).</summary>
    public static void InverseNtt(Span<int> polynomial)
    {
        int zetaIndex = 127;
        for (int length = 2; length <= 128; length *= 2)
        {
            for (int start = 0; start < Degree; start += 2 * length)
            {
                int zeta = Zetas[zetaIndex--];
                for (int index = start; index < start + length; index++)
                {
                    int first = polynomial[index];
                    polynomial[index] = Add(first, polynomial[index + length]);
                    polynomial[index + length] = Multiply(zeta, Subtract(polynomial[index + length], first));
                }
            }
        }

        for (int index = 0; index < Degree; index++)
        {
            polynomial[index] = Multiply(polynomial[index], InverseNttScale);
        }
    }

    /// <summary>
    /// Adds the NTT-domain product of <paramref name="left" /> and <paramref name="right" />
    /// to <paramref name="accumulator" /> (FIPS 203 algorithms 11 and 12).
    /// </summary>
    public static void MultiplyNttsAndAdd(Span<int> accumulator, ReadOnlySpan<int> left, ReadOnlySpan<int> right)
    {
        for (int pair = 0; pair < Degree / 2; pair++)
        {
            int a0 = left[2 * pair];
            int a1 = left[(2 * pair) + 1];
            int b0 = right[2 * pair];
            int b1 = right[(2 * pair) + 1];
            int c0 = Reduce((uint)((a0 * b0) + (Multiply(a1, b1) * Gammas[pair])));
            int c1 = Reduce((uint)((a0 * b1) + (a1 * b0)));
            accumulator[2 * pair] = Add(accumulator[2 * pair], c0);
            accumulator[(2 * pair) + 1] = Add(accumulator[(2 * pair) + 1], c1);
        }
    }

    /// <summary>Adds <paramref name="addend" /> to <paramref name="polynomial" /> coefficient by coefficient.</summary>
    public static void AddTo(Span<int> polynomial, ReadOnlySpan<int> addend)
    {
        for (int index = 0; index < Degree; index++)
        {
            polynomial[index] = Add(polynomial[index], addend[index]);
        }
    }

    /// <summary>Replaces each coefficient x with round(2^d x / q) mod 2^d, <paramref name="bits" /> = d (FIPS 203 equation 4.7).</summary>
    public static void Compress(Span<int> polynomial, int bits)
    {
        uint mask = (1u << bits) - 1;
        for (int index = 0; index < Degree; index++)
        {
            uint scaled = ((uint)polynomial[index] << bits) + HalfModulusRoundedDown;
            polynomial[index] = (int)(DivideByModulus(scaled) & mask);
        }
    }

    /// <summary>Replaces each coefficient y with round(q y / 2^d), <paramref name="bits" /> = d (FIPS 203 equation 4.8).</summary>
    public static void Decompress(Span<int> polynomial, int bits)
    {
        for (int index = 0; index < Degree; index++)
        {
            polynomial[index] = ((polynomial[index] * Modulus) + (1 << (bits - 1))) >> bits;
        }
    }

    /// <summary>
    /// Writes the low <paramref name="bits" /> bits of each coefficient, little-endian, to
    /// the 32 * <paramref name="bits" /> bytes of <paramref name="destination" /> (FIPS 203
    /// algorithm 5).
    /// </summary>
    public static void Encode(ReadOnlySpan<int> polynomial, int bits, Span<byte> destination)
    {
        ulong buffer = 0;
        int buffered = 0;
        int output = 0;
        for (int index = 0; index < Degree; index++)
        {
            buffer |= (ulong)(uint)polynomial[index] << buffered;
            buffered += bits;
            while (buffered >= 8)
            {
                destination[output++] = (byte)buffer;
                buffer >>= 8;
                buffered -= 8;
            }
        }
    }

    /// <summary>
    /// Reads 256 coefficients of <paramref name="bits" /> bits each from
    /// <paramref name="source" /> (FIPS 203 algorithm 6); a 12-bit value is reduced mod q.
    /// </summary>
    public static void Decode(ReadOnlySpan<byte> source, int bits, Span<int> polynomial)
    {
        ulong buffer = 0;
        int buffered = 0;
        int input = 0;
        ulong mask = (1ul << bits) - 1;
        for (int index = 0; index < Degree; index++)
        {
            while (buffered < bits)
            {
                buffer |= (ulong)source[input++] << buffered;
                buffered += 8;
            }

            polynomial[index] = SubtractModulusIfAtLeast((int)(buffer & mask));
            buffer >>= bits;
            buffered -= bits;
        }
    }

    /// <summary>
    /// Returns whether every 12-bit value packed in <paramref name="encoded" /> is below q:
    /// FIPS 203 section 7.2's modulus check of an encapsulation key, which is public.
    /// </summary>
    public static bool AreAllBelowModulus(ReadOnlySpan<byte> encoded)
    {
        for (int offset = 0; offset < encoded.Length; offset += 3)
        {
            int first = encoded[offset] | ((encoded[offset + 1] & 0x0F) << 8);
            int second = (encoded[offset + 1] >> 4) | (encoded[offset + 2] << 4);
            if (first >= Modulus || second >= Modulus)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Samples a polynomial in the NTT domain from SHAKE128 of the 34-byte
    /// <paramref name="seed" /> rho || j || i by rejection (FIPS 203 algorithm 7).
    /// </summary>
    public static void SampleNtt(ReadOnlySpan<byte> seed, Span<int> polynomial)
    {
        using Shake xof = Shake.Create128();
        xof.AppendData(seed);
        Span<byte> block = stackalloc byte[XofBlockSize];
        int count = 0;
        while (count < Degree)
        {
            xof.Read(block);
            for (int offset = 0; offset < XofBlockSize && count < Degree; offset += 3)
            {
                int first = block[offset] | ((block[offset + 1] & 0x0F) << 8);
                int second = (block[offset + 1] >> 4) | (block[offset + 2] << 4);
                count = AcceptBelowModulus(polynomial, count, first);
                count = AcceptBelowModulus(polynomial, count, second);
            }
        }
    }

    /// <summary>
    /// Samples a polynomial from the centred binomial distribution of width
    /// <paramref name="eta" /> over the 64 * eta bytes of <paramref name="bytes" /> (FIPS 203
    /// algorithm 8).
    /// </summary>
    public static void SampleCenteredBinomial(ReadOnlySpan<byte> bytes, int eta, Span<int> polynomial)
    {
        for (int index = 0; index < Degree; index++)
        {
            int first = 0;
            int second = 0;
            int start = 2 * index * eta;
            for (int bit = 0; bit < eta; bit++)
            {
                first += ReadBit(bytes, start + bit);
                second += ReadBit(bytes, start + eta + bit);
            }

            polynomial[index] = SubtractModulusIfAtLeast(first - second + Modulus);
        }
    }

    private static int AcceptBelowModulus(Span<int> polynomial, int count, int candidate)
    {
        if (count < Degree && candidate < Modulus)
        {
            polynomial[count] = candidate;
            return count + 1;
        }

        return count;
    }

    private static int ReadBit(ReadOnlySpan<byte> bytes, int position) => (bytes[position >> 3] >> (position & 7)) & 1;

    private static int SubtractModulusIfAtLeast(int value)
    {
        int lowered = value - Modulus;
        return lowered + (Modulus & (lowered >> 31));
    }

    private static int[] ComputePowers(Func<int, int> exponentOfBitReversed)
    {
        int[] powers = new int[Degree / 2];
        for (int index = 0; index < powers.Length; index++)
        {
            int exponent = exponentOfBitReversed(ReverseSevenBits(index));
            int power = 1;
            for (int step = 0; step < exponent; step++)
            {
                power = power * Generator % Modulus;
            }

            powers[index] = power;
        }

        return powers;
    }

    private static int ReverseSevenBits(int value)
    {
        int reversed = 0;
        for (int bit = 0; bit < 7; bit++)
        {
            reversed |= ((value >> bit) & 1) << (6 - bit);
        }

        return reversed;
    }
}
