namespace Curl.Kerberos;

/// <summary>
/// RFC 3961 section 5.1's n-fold: stretches or shrinks a byte string to any length by
/// repeating it, rotated right 13 bits more each time, to the least common multiple of
/// the two lengths, and adding the output-sized pieces with ones'-complement addition.
/// The simplified profile uses it to turn a key usage constant into a cipher block.
/// </summary>
internal static class KerberosNFold
{
    /// <summary>Writes the n-fold of <paramref name="input" /> to <paramref name="output" />, n being its length in bits.</summary>
    /// <remarks>A port of MIT Kerberos' <c>krb5int_nfold</c>, which walks the rotated copies bit by bit.</remarks>
    public static void Fold(ReadOnlySpan<byte> input, Span<byte> output)
    {
        int inputLength = input.Length;
        int inputBits = inputLength << 3;
        int leastCommonMultiple = inputLength / GreatestCommonDivisor(inputLength, output.Length) * output.Length;
        output.Clear();
        int carry = 0;
        for (int index = leastCommonMultiple - 1; index >= 0; index--)
        {
            // The input bit that lands in the most significant bit of byte `index` of the
            // repeated, rotated string.
            int mostSignificantBit = (inputBits - 1 + ((inputBits + 13) * (index / inputLength)) + ((inputLength - (index % inputLength)) << 3)) % inputBits;
            int byteIndex = mostSignificantBit >> 3;
            int pair = (input[(inputLength - 1 - byteIndex) % inputLength] << 8) | input[(inputLength - byteIndex) % inputLength];
            carry += ((pair >> ((mostSignificantBit & 7) + 1)) & 0xff) + output[index % output.Length];
            output[index % output.Length] = (byte)carry;
            carry >>= 8;
        }

        // Ones'-complement addition wraps the last carry around to the least significant byte.
        for (int index = output.Length - 1; index >= 0; index--)
        {
            carry += output[index];
            output[index] = (byte)carry;
            carry >>= 8;
        }
    }

    private static int GreatestCommonDivisor(int left, int right)
    {
        while (right != 0)
        {
            (left, right) = (right, left % right);
        }

        return left;
    }
}
