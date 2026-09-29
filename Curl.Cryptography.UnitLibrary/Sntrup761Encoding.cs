using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// sntrup761's byte encodings (NTRU Prime round 3, 2020, section 3.2 and the reference
/// <c>Encode.c</c>, <c>Decode.c</c> and <c>kem.c</c>): the mixed-radix <see cref="Encode" />
/// and <see cref="Decode" /> of a list of values each below its own modulus, and on them the
/// public key (<see cref="EncodeRq" />), the rounded ciphertext (<see cref="EncodeRounded" />)
/// and the two-bit small polynomial (<see cref="EncodeSmall" />). Which bytes are written or
/// read depends only on the moduli, never on the values, so encoding a secret is
/// constant-time.
/// </summary>
internal static class Sntrup761Encoding
{
    /// <summary>The bytes of a small polynomial, (p + 3) / 4.</summary>
    public const int SmallSize = (Sntrup761Ring.P + 3) / 4;

    /// <summary>The bytes of a polynomial mod q, the public key.</summary>
    public const int RqSize = 1158;

    /// <summary>The bytes of a rounded polynomial, the ciphertext before its confirmation hash.</summary>
    public const int RoundedSize = 1007;

    private const int HalfQ = (Sntrup761Ring.Q - 1) / 2;

    private const ushort RoundedModulus = (Sntrup761Ring.Q + 2) / 3;

    /// <summary>
    /// Writes <paramref name="values" />, each below the matching entry of
    /// <paramref name="moduli" /> (every modulus below 16384), and returns the bytes written.
    /// </summary>
    public static int Encode(Span<byte> output, ReadOnlySpan<ushort> values, ReadOnlySpan<ushort> moduli)
    {
        if (values.Length == 1)
        {
            return EncodeLast(output, values[0], moduli[0]);
        }

        int half = (values.Length + 1) / 2;
        Span<ushort> merged = stackalloc ushort[2 * half];
        Span<ushort> mergedValues = merged[..half];
        Span<ushort> mergedModuli = merged[half..];
        try
        {
            int written = EncodePairs(output, values, moduli, mergedValues, mergedModuli);
            return written + Encode(output[written..], mergedValues, mergedModuli);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(merged));
        }
    }

    /// <summary>
    /// Reads what <see cref="Encode" /> wrote for <paramref name="moduli" /> into
    /// <paramref name="output" />. Any input decodes: each value is reduced below its modulus.
    /// </summary>
    public static void Decode(Span<ushort> output, ReadOnlySpan<byte> input, ReadOnlySpan<ushort> moduli)
    {
        if (moduli.Length == 1)
        {
            output[0] = DecodeLast(input, moduli[0]);
            return;
        }

        int half = (moduli.Length + 1) / 2;
        int pairs = moduli.Length / 2;
        Span<ushort> merged = stackalloc ushort[(2 * half) + pairs];
        Span<uint> bottomScale = stackalloc uint[pairs];
        Span<ushort> mergedValues = merged[..half];
        Span<ushort> mergedModuli = merged.Slice(half, half);
        Span<ushort> bottomValues = merged[(2 * half)..];
        try
        {
            int consumed = SplitPairs(input, moduli, mergedModuli, bottomValues, bottomScale);
            Decode(mergedValues, input[consumed..], mergedModuli);
            CombinePairs(output, mergedValues, moduli, bottomValues, bottomScale);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(merged));
        }
    }

    /// <summary>Encodes a polynomial mod q, coefficients -(q-1)/2 to (q-1)/2, into <see cref="RqSize" /> bytes.</summary>
    public static void EncodeRq(Span<byte> output, ReadOnlySpan<short> polynomial)
    {
        Span<ushort> values = stackalloc ushort[Sntrup761Ring.P];
        Span<ushort> moduli = stackalloc ushort[Sntrup761Ring.P];
        for (int index = 0; index < Sntrup761Ring.P; index++)
        {
            values[index] = (ushort)(polynomial[index] + HalfQ);
        }

        moduli.Fill(Sntrup761Ring.Q);
        Encode(output, values, moduli);
    }

    /// <summary>Decodes <see cref="RqSize" /> bytes into a polynomial mod q.</summary>
    public static void DecodeRq(Span<short> polynomial, ReadOnlySpan<byte> input)
    {
        Span<ushort> values = stackalloc ushort[Sntrup761Ring.P];
        Span<ushort> moduli = stackalloc ushort[Sntrup761Ring.P];
        moduli.Fill(Sntrup761Ring.Q);
        Decode(values, input, moduli);
        for (int index = 0; index < Sntrup761Ring.P; index++)
        {
            polynomial[index] = (short)(values[index] - HalfQ);
        }
    }

    /// <summary>Encodes a rounded polynomial (every coefficient a multiple of 3) into <see cref="RoundedSize" /> bytes.</summary>
    public static void EncodeRounded(Span<byte> output, ReadOnlySpan<short> polynomial)
    {
        Span<ushort> values = stackalloc ushort[Sntrup761Ring.P];
        Span<ushort> moduli = stackalloc ushort[Sntrup761Ring.P];
        try
        {
            for (int index = 0; index < Sntrup761Ring.P; index++)
            {
                values[index] = (ushort)(((polynomial[index] + HalfQ) * 10923) >> 15);
            }

            moduli.Fill(RoundedModulus);
            Encode(output, values, moduli);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(values));
        }
    }

    /// <summary>Decodes <see cref="RoundedSize" /> bytes into a rounded polynomial.</summary>
    public static void DecodeRounded(Span<short> polynomial, ReadOnlySpan<byte> input)
    {
        Span<ushort> values = stackalloc ushort[Sntrup761Ring.P];
        Span<ushort> moduli = stackalloc ushort[Sntrup761Ring.P];
        moduli.Fill(RoundedModulus);
        Decode(values, input, moduli);
        for (int index = 0; index < Sntrup761Ring.P; index++)
        {
            polynomial[index] = (short)((values[index] * 3) - HalfQ);
        }
    }

    /// <summary>Encodes a small polynomial, coefficients -1 to 1, two bits each, into <see cref="SmallSize" /> bytes.</summary>
    public static void EncodeSmall(Span<byte> output, ReadOnlySpan<short> polynomial)
    {
        for (int index = 0; index < Sntrup761Ring.P / 4; index++)
        {
            int packed = polynomial[4 * index] + 1;
            packed += (polynomial[(4 * index) + 1] + 1) << 2;
            packed += (polynomial[(4 * index) + 2] + 1) << 4;
            packed += (polynomial[(4 * index) + 3] + 1) << 6;
            output[index] = (byte)packed;
        }

        output[SmallSize - 1] = (byte)(polynomial[Sntrup761Ring.P - 1] + 1);
    }

    /// <summary>Decodes <see cref="SmallSize" /> bytes into a small polynomial.</summary>
    public static void DecodeSmall(Span<short> polynomial, ReadOnlySpan<byte> input)
    {
        for (int index = 0; index < Sntrup761Ring.P / 4; index++)
        {
            int packed = input[index];
            polynomial[4 * index] = (short)((packed & 3) - 1);
            polynomial[(4 * index) + 1] = (short)(((packed >> 2) & 3) - 1);
            polynomial[(4 * index) + 2] = (short)(((packed >> 4) & 3) - 1);
            polynomial[(4 * index) + 3] = (short)(((packed >> 6) & 3) - 1);
        }

        polynomial[Sntrup761Ring.P - 1] = (short)((input[SmallSize - 1] & 3) - 1);
    }

    /// <summary>The last value: little-endian bytes until the modulus is used up.</summary>
    private static int EncodeLast(Span<byte> output, uint value, uint modulus)
    {
        int written = 0;
        while (modulus > 1)
        {
            output[written++] = (byte)value;
            value >>= 8;
            modulus = (modulus + 255) >> 8;
        }

        return written;
    }

    /// <summary>
    /// Merges each pair of values into one below the product of their moduli, writing its
    /// low bytes until that product is below 16384; an odd last value passes through.
    /// </summary>
    private static int EncodePairs(
        Span<byte> output,
        ReadOnlySpan<ushort> values,
        ReadOnlySpan<ushort> moduli,
        Span<ushort> mergedValues,
        Span<ushort> mergedModuli)
    {
        int written = 0;
        for (int index = 0; index + 1 < values.Length; index += 2)
        {
            uint value = values[index] + (values[index + 1] * (uint)moduli[index]);
            uint modulus = moduli[index + 1] * (uint)moduli[index];
            while (modulus >= 16384)
            {
                output[written++] = (byte)value;
                value >>= 8;
                modulus = (modulus + 255) >> 8;
            }

            mergedValues[index / 2] = (ushort)value;
            mergedModuli[index / 2] = (ushort)modulus;
        }

        if (values.Length % 2 == 1)
        {
            mergedValues[^1] = values[^1];
            mergedModuli[^1] = moduli[^1];
        }

        return written;
    }

    /// <summary>The last value: one byte, two bytes, or none for a modulus of 1.</summary>
    private static ushort DecodeLast(ReadOnlySpan<byte> input, ushort modulus)
    {
        if (modulus == 1)
        {
            return 0;
        }

        if (modulus <= 256)
        {
            return Uint14Division.Remainder((uint)input[0], modulus);
        }

        return Uint14Division.Remainder(input[0] + ((uint)input[1] << 8), modulus);
    }

    /// <summary>
    /// For each pair, reads the low bytes <see cref="Encode" /> wrote and computes the
    /// merged modulus; returns the bytes read.
    /// </summary>
    private static int SplitPairs(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<ushort> moduli,
        Span<ushort> mergedModuli,
        Span<ushort> bottomValues,
        Span<uint> bottomScale)
    {
        int consumed = 0;
        for (int pair = 0; pair < bottomValues.Length; pair++)
        {
            uint modulus = moduli[2 * pair] * (uint)moduli[(2 * pair) + 1];
            int bytes = LowBytesOfMergedModulus(modulus);
            bottomScale[pair] = 1u << (8 * bytes);
            bottomValues[pair] = ReadLittleEndian(input.Slice(consumed, bytes));
            mergedModuli[pair] = (ushort)ShrinkModulus(modulus, bytes);
            consumed += bytes;
        }

        if (moduli.Length % 2 == 1)
        {
            mergedModuli[^1] = moduli[^1];
        }

        return consumed;
    }

    /// <summary>How many low bytes <see cref="Encode" /> wrote for a pair whose moduli multiply to <paramref name="modulus" />.</summary>
    private static int LowBytesOfMergedModulus(uint modulus)
    {
        if (modulus > 256 * 16383)
        {
            return 2;
        }

        return modulus >= 16384 ? 1 : 0;
    }

    /// <summary>Divides a modulus by 256, rounding up, once per byte written.</summary>
    private static uint ShrinkModulus(uint modulus, int bytes)
    {
        for (int step = 0; step < bytes; step++)
        {
            modulus = (modulus + 255) >> 8;
        }

        return modulus;
    }

    /// <summary>Zero, one or two bytes as a little-endian value.</summary>
    private static ushort ReadLittleEndian(ReadOnlySpan<byte> bytes)
    {
        uint value = 0;
        for (int index = bytes.Length - 1; index >= 0; index--)
        {
            value = (value << 8) | bytes[index];
        }

        return (ushort)value;
    }

    /// <summary>Splits each merged value back into its pair; an odd last value passes through.</summary>
    private static void CombinePairs(
        Span<ushort> output,
        ReadOnlySpan<ushort> mergedValues,
        ReadOnlySpan<ushort> moduli,
        ReadOnlySpan<ushort> bottomValues,
        ReadOnlySpan<uint> bottomScale)
    {
        for (int pair = 0; pair < bottomValues.Length; pair++)
        {
            uint value = bottomValues[pair] + (bottomScale[pair] * mergedValues[pair]);
            output[2 * pair] = Uint14Division.DivideWithRemainder(value, moduli[2 * pair], out uint high);
            output[(2 * pair) + 1] = Uint14Division.Remainder(high, moduli[(2 * pair) + 1]);
        }

        if (moduli.Length % 2 == 1)
        {
            output[^1] = mergedValues[^1];
        }
    }
}
