using System.Buffers.Binary;

namespace Curl.Cryptography;

/// <summary>
/// ML-DSA's samplers (FIPS 204 section 7.3): <see cref="SampleNtt" /> for the matrix A,
/// <see cref="SampleBounded" /> for the secrets s1 and s2, <see cref="SampleMask" /> for
/// the mask y, and <see cref="SampleInBall" /> for the challenge c. Every coefficient is
/// written reduced mod q.
/// </summary>
/// <remarks>
/// <see cref="SampleNtt" /> reads only the public seed rho. <see cref="SampleMask" /> is
/// constant-time. <see cref="SampleBounded" /> and <see cref="SampleInBall" /> reject
/// bytes of a secret SHAKE256 stream as FIPS 204 specifies: they branch only on whether a
/// byte (or half-byte) is rejected, which says how many were read and that the rejected
/// ones were out of range, never what the kept values are. The values kept are computed
/// without a branch, and <see cref="SampleInBall" /> moves them with a masked pass over
/// the whole polynomial, so no memory address depends on a secret index.
/// </remarks>
internal static class MlDsaSampling
{
    private const int Shake128BlockSize = 168;
    private const int Shake256BlockSize = 136;
    private const int SignBitsSize = 8;

    /// <summary>
    /// Samples the NTT-domain polynomial A[<paramref name="row" />][<paramref name="column" />]
    /// from SHAKE128 of rho || column || row by rejection (FIPS 204 algorithms 30 and 14).
    /// </summary>
    public static void SampleNtt(ReadOnlySpan<byte> rho, int row, int column, Span<int> polynomial)
    {
        using Shake xof = Shake.Create128();
        xof.AppendData(rho);
        xof.AppendData([(byte)column, (byte)row]);
        Span<byte> block = stackalloc byte[Shake128BlockSize];
        int count = 0;
        while (count < MlDsaPolynomial.Degree)
        {
            xof.Read(block);
            for (int offset = 0; offset < Shake128BlockSize && count < MlDsaPolynomial.Degree; offset += 3)
            {
                int candidate = block[offset] | (block[offset + 1] << 8) | ((block[offset + 2] & 0x7F) << 16);
                if (candidate < MlDsaPolynomial.Modulus)
                {
                    polynomial[count++] = candidate;
                }
            }
        }
    }

    /// <summary>
    /// Samples a polynomial with coefficients in [-eta, eta] from SHAKE256 of the 64-byte
    /// <paramref name="seed" /> rho' || <paramref name="index" /> as two little-endian bytes
    /// (FIPS 204 algorithms 31 and 15).
    /// </summary>
    public static void SampleBounded(ReadOnlySpan<byte> seed, int index, int eta, Span<int> polynomial)
    {
        using Shake xof = Shake.Create256();
        xof.AppendData(seed);
        xof.AppendData([(byte)index, (byte)(index >> 8)]);
        Span<byte> block = stackalloc byte[Shake256BlockSize];
        int limit = eta == 2 ? 15 : 9;
        int count = 0;
        try
        {
            while (count < MlDsaPolynomial.Degree)
            {
                xof.Read(block);
                for (int offset = 0; offset < Shake256BlockSize; offset++)
                {
                    count = AcceptHalfByte(polynomial, count, block[offset] & 0x0F, eta, limit);
                    count = AcceptHalfByte(polynomial, count, block[offset] >> 4, eta, limit);
                }
            }
        }
        finally
        {
            block.Clear();
        }
    }

    /// <summary>
    /// Samples the mask polynomial with coefficients in (-gamma1, gamma1] from SHAKE256 of
    /// the 64-byte <paramref name="seed" /> rho'' || <paramref name="index" /> as two
    /// little-endian bytes (FIPS 204 algorithm 34, one polynomial of ExpandMask).
    /// </summary>
    public static void SampleMask(ReadOnlySpan<byte> seed, int index, int gamma1Bits, Span<int> polynomial)
    {
        int bits = gamma1Bits + 1;
        Span<byte> bytes = stackalloc byte[32 * bits];
        try
        {
            using (Shake xof = Shake.Create256())
            {
                xof.AppendData(seed);
                xof.AppendData([(byte)index, (byte)(index >> 8)]);
                xof.Read(bytes);
            }

            MlDsaEncoding.UnpackCentered(bytes, 1 << gamma1Bits, bits, polynomial);
        }
        finally
        {
            bytes.Clear();
        }
    }

    /// <summary>
    /// Samples the challenge polynomial, <paramref name="tau" /> coefficients of plus or
    /// minus one and the rest zero, from SHAKE256 of <paramref name="commitmentHash" />
    /// (FIPS 204 algorithm 29).
    /// </summary>
    public static void SampleInBall(ReadOnlySpan<byte> commitmentHash, int tau, Span<int> polynomial)
    {
        using Shake xof = Shake.Create256();
        xof.AppendData(commitmentHash);
        Span<byte> signBits = stackalloc byte[SignBitsSize];
        Span<byte> candidate = stackalloc byte[1];
        xof.Read(signBits);
        ulong signs = BinaryPrimitives.ReadUInt64LittleEndian(signBits);
        polynomial.Clear();
        for (int position = MlDsaPolynomial.Degree - tau; position < MlDsaPolynomial.Degree; position++)
        {
            do
            {
                xof.Read(candidate);
            }
            while (candidate[0] > position);

            uint sign = (uint)(signs >> (position + tau - MlDsaPolynomial.Degree)) & 1u;
            MoveAndSet(polynomial, position, candidate[0], sign);
        }

        signs = 0;
        signBits.Clear();
    }

    // c[position] = c[chosen]; c[chosen] = (-1)^sign, as masked reads and writes of every
    // coefficient up to position, so the chosen index never becomes an address.
    private static void MoveAndSet(Span<int> polynomial, int position, int chosen, uint sign)
    {
        uint moved = 0;
        for (int index = 0; index <= position; index++)
        {
            moved |= (uint)polynomial[index] & ConstantTime.EqualMask((uint)index, (uint)chosen);
        }

        polynomial[position] = (int)moved;
        uint value = ConstantTime.Select(ConstantTime.MaskFromBit(sign), MlDsaPolynomial.Modulus - 1, 1);
        for (int index = 0; index <= position; index++)
        {
            uint mask = ConstantTime.EqualMask((uint)index, (uint)chosen);
            polynomial[index] = (int)ConstantTime.Select(mask, value, (uint)polynomial[index]);
        }
    }

    // FIPS 204 algorithm 15, CoeffFromHalfByte: eta - (b mod 5) for eta = 2 and b < 15,
    // eta - b for eta = 4 and b < 9, b mod 5 computed as b - 5 floor(205 b / 1024).
    private static int AcceptHalfByte(Span<int> polynomial, int count, int halfByte, int eta, int limit)
    {
        if (count < MlDsaPolynomial.Degree && halfByte < limit)
        {
            int reduced = eta == 2 ? halfByte - (5 * ((halfByte * 205) >> 10)) : halfByte;
            polynomial[count] = MlDsaPolynomial.FromSigned(eta - reduced);
            return count + 1;
        }

        return count;
    }
}
