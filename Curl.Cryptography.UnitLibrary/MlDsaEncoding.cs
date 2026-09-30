namespace Curl.Cryptography;

/// <summary>
/// ML-DSA's byte encodings (FIPS 204 section 7.1 and 7.2): SimpleBitPack and BitPack of a
/// polynomial's coefficients, their inverses, and the hint's HintBitPack and HintBitUnpack.
/// Bits are packed least significant first, as IntegerToBits and BitsToBytes lay them out.
/// </summary>
/// <remarks>
/// The packing of coefficients is constant-time: its running time depends only on the
/// number of bits per coefficient. The hint encodings branch on the hint, which is public:
/// signing encodes a hint only once it is part of the signature it returns.
/// </remarks>
internal static class MlDsaEncoding
{
    /// <summary>
    /// Writes the low <paramref name="bits" /> bits of each of the 256 coefficients of
    /// <paramref name="polynomial" /> to the 32 * <paramref name="bits" /> bytes of
    /// <paramref name="destination" /> (FIPS 204 algorithm 16, SimpleBitPack).
    /// </summary>
    public static void Pack(ReadOnlySpan<int> polynomial, int bits, Span<byte> destination)
    {
        ulong buffer = 0;
        int buffered = 0;
        int output = 0;
        for (int index = 0; index < MlDsaPolynomial.Degree; index++)
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
    /// <paramref name="source" /> (FIPS 204 algorithm 18, SimpleBitUnpack).
    /// </summary>
    public static void Unpack(ReadOnlySpan<byte> source, int bits, Span<int> polynomial)
    {
        ulong buffer = 0;
        int buffered = 0;
        int input = 0;
        ulong mask = (1ul << bits) - 1;
        for (int index = 0; index < MlDsaPolynomial.Degree; index++)
        {
            while (buffered < bits)
            {
                buffer |= (ulong)source[input++] << buffered;
                buffered += 8;
            }

            polynomial[index] = (int)(buffer & mask);
            buffer >>= bits;
            buffered -= bits;
        }
    }

    /// <summary>
    /// Writes <paramref name="upper" /> - w for each reduced coefficient w of
    /// <paramref name="polynomial" />, taken centred, in <paramref name="bits" /> bits
    /// (FIPS 204 algorithm 17, BitPack(w, a, b) with b the upper bound). The polynomial is
    /// left unchanged.
    /// </summary>
    public static void PackCentered(ReadOnlySpan<int> polynomial, int upper, int bits, Span<byte> destination)
    {
        Span<int> shifted = stackalloc int[MlDsaPolynomial.Degree];
        try
        {
            for (int index = 0; index < MlDsaPolynomial.Degree; index++)
            {
                shifted[index] = upper - MlDsaPolynomial.ToCentered(polynomial[index]);
            }

            Pack(shifted, bits, destination);
        }
        finally
        {
            shifted.Clear();
        }
    }

    /// <summary>
    /// Reads 256 values v of <paramref name="bits" /> bits and writes
    /// <paramref name="upper" /> - v, reduced mod q, as each coefficient (FIPS 204
    /// algorithm 19, BitUnpack(v, a, b) with b the upper bound).
    /// </summary>
    public static void UnpackCentered(ReadOnlySpan<byte> source, int upper, int bits, Span<int> polynomial)
    {
        Unpack(source, bits, polynomial);
        for (int index = 0; index < MlDsaPolynomial.Degree; index++)
        {
            polynomial[index] = MlDsaPolynomial.FromSigned(upper - polynomial[index]);
        }
    }

    /// <summary>
    /// Writes the positions of the ones in each polynomial of <paramref name="hint" />, then
    /// each polynomial's running count, to the omega + k bytes of
    /// <paramref name="destination" /> (FIPS 204 algorithm 20, HintBitPack). The hint holds
    /// at most omega ones.
    /// </summary>
    public static void PackHint(ReadOnlySpan<int> hint, int omega, Span<byte> destination)
    {
        destination.Clear();
        int count = 0;
        int rows = hint.Length / MlDsaPolynomial.Degree;
        for (int row = 0; row < rows; row++)
        {
            for (int index = 0; index < MlDsaPolynomial.Degree; index++)
            {
                if (hint[(row * MlDsaPolynomial.Degree) + index] != 0)
                {
                    destination[count++] = (byte)index;
                }
            }

            destination[omega + row] = (byte)count;
        }
    }

    /// <summary>
    /// Reads a hint packed by <see cref="PackHint" /> into <paramref name="hint" />, whose
    /// length gives k (FIPS 204 algorithm 21, HintBitUnpack).
    /// </summary>
    /// <returns>
    /// <c>false</c> when the encoding is malformed: a count that falls or passes omega,
    /// positions not strictly increasing within a polynomial, or a nonzero byte after the
    /// last position; otherwise <c>true</c>.
    /// </returns>
    public static bool TryUnpackHint(ReadOnlySpan<byte> source, int omega, Span<int> hint)
    {
        hint.Clear();
        int index = 0;
        int rows = hint.Length / MlDsaPolynomial.Degree;
        for (int row = 0; row < rows; row++)
        {
            int end = source[omega + row];
            if (end < index || end > omega)
            {
                return false;
            }

            if (!TryReadPositions(source, index, end, hint.Slice(row * MlDsaPolynomial.Degree, MlDsaPolynomial.Degree)))
            {
                return false;
            }

            index = end;
        }

        return ConstantTime.IsAllZero(source[index..omega]);
    }

    private static bool TryReadPositions(ReadOnlySpan<byte> source, int first, int end, Span<int> polynomial)
    {
        for (int index = first; index < end; index++)
        {
            if (index > first && source[index - 1] >= source[index])
            {
                return false;
            }

            polynomial[source[index]] = 1;
        }

        return true;
    }
}
