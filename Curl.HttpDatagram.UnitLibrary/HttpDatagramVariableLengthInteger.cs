namespace Curl.HttpDatagram;

/// <summary>
/// Writes and reads the variable-length integers of RFC 9000 section 16, which every
/// RFC 9297 capsule type, capsule length and quarter stream ID uses.
/// </summary>
public static class HttpDatagramVariableLengthInteger
{
    /// <summary>
    /// The largest value a variable-length integer holds, 2^62 - 1.
    /// </summary>
    public const ulong MaximumValue = (1UL << 62) - 1;

    /// <summary>
    /// Returns how many bytes the shortest encoding of <paramref name="value" /> takes.
    /// </summary>
    /// <param name="value">The value to encode.</param>
    /// <returns>1, 2, 4 or 8.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value is above 2^62 - 1.</exception>
    public static int GetEncodedLength(ulong value)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaximumValue);
        return value switch
        {
            < 1UL << 6 => 1,
            < 1UL << 14 => 2,
            < 1UL << 30 => 4,
            _ => 8,
        };
    }

    /// <summary>
    /// Writes the shortest encoding of <paramref name="value" /> at the start of
    /// <paramref name="destination" />.
    /// </summary>
    /// <param name="value">The value to encode.</param>
    /// <param name="destination">Where to write; at least <see cref="GetEncodedLength" /> bytes.</param>
    /// <returns>How many bytes were written.</returns>
    public static int Write(ulong value, Span<byte> destination)
    {
        var length = GetEncodedLength(value);
        var lengthBits = (ulong)System.Numerics.BitOperations.Log2((uint)length);
        var encoded = value | (lengthBits << ((length * 8) - 2));
        for (var index = length - 1; index >= 0; index--)
        {
            destination[index] = (byte)encoded;
            encoded >>= 8;
        }

        return length;
    }

    /// <summary>
    /// Reads one variable-length integer from the start of <paramref name="source" />.
    /// </summary>
    /// <param name="source">The bytes to read from.</param>
    /// <param name="value">The value read, or 0 when the source holds too few bytes.</param>
    /// <param name="consumed">How many bytes the integer took, or 0 when incomplete.</param>
    /// <returns><see langword="true" /> when the source held the whole integer.</returns>
    public static bool TryRead(ReadOnlySpan<byte> source, out ulong value, out int consumed)
    {
        value = 0;
        consumed = 0;
        if (source.IsEmpty)
        {
            return false;
        }

        var length = 1 << (source[0] >> 6);
        if (source.Length < length)
        {
            return false;
        }

        var result = (ulong)(source[0] & 0x3F);
        for (var index = 1; index < length; index++)
        {
            result = (result << 8) | source[index];
        }

        value = result;
        consumed = length;
        return true;
    }
}
