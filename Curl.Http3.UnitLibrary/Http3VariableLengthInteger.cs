using System.Numerics;

namespace Curl.Http3;

/// <summary>
/// QUIC's variable-length integer encoding (RFC 9000 section 16), which HTTP/3 uses for
/// frame types and lengths, stream types, setting identifiers and values, and IDs.
/// </summary>
internal static class Http3VariableLengthInteger
{
    /// <summary>The largest value the encoding can carry, 2^62 - 1.</summary>
    public const long LargestValue = (1L << 62) - 1;

    /// <summary>
    /// Appends <paramref name="value" /> in the shortest encoding that holds it.
    /// </summary>
    /// <param name="output">Where the bytes go.</param>
    /// <param name="value">A value from 0 to <see cref="LargestValue" />.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value" /> is outside that range.</exception>
    public static void Write(List<byte> output, long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, LargestValue);
        var length = GetEncodedLength(value);
        var prefix = (byte)(BitOperations.Log2((uint)length) << 6);
        for (var shift = (length - 1) * 8; shift >= 0; shift -= 8)
        {
            output.Add((byte)(value >> shift));
        }

        output[^length] |= prefix;
    }

    /// <summary>
    /// Gets how many bytes an encoding is from its first byte: 1, 2, 4 or 8.
    /// </summary>
    /// <param name="firstByte">The encoding's first byte.</param>
    /// <returns>The encoding's length in bytes.</returns>
    public static int GetLengthFromFirstByte(byte firstByte) => 1 << (firstByte >> 6);

    /// <summary>
    /// Reads one value from <paramref name="input" /> at <paramref name="position" /> and
    /// moves <paramref name="position" /> past it.
    /// </summary>
    /// <param name="input">The bytes.</param>
    /// <param name="position">Where the value starts; afterwards, where it ended.</param>
    /// <param name="value">The value, when <paramref name="input" /> holds all of it.</param>
    /// <returns><see langword="false" /> when <paramref name="input" /> ends inside the value.</returns>
    public static bool TryRead(ReadOnlySpan<byte> input, ref int position, out long value)
    {
        value = 0;
        if (position >= input.Length)
        {
            return false;
        }

        var length = GetLengthFromFirstByte(input[position]);
        if (input.Length - position < length)
        {
            return false;
        }

        value = Decode(input.Slice(position, length));
        position += length;
        return true;
    }

    /// <summary>
    /// Decodes a whole encoding, first byte included.
    /// </summary>
    /// <param name="encoding">Exactly the encoding's bytes.</param>
    /// <returns>The value.</returns>
    public static long Decode(ReadOnlySpan<byte> encoding)
    {
        long value = encoding[0] & 0x3f;
        for (var index = 1; index < encoding.Length; index++)
        {
            value = (value << 8) | encoding[index];
        }

        return value;
    }

    private static int GetEncodedLength(long value) => value switch
    {
        < 1L << 6 => 1,
        < 1L << 14 => 2,
        < 1L << 30 => 4,
        _ => 8,
    };
}
