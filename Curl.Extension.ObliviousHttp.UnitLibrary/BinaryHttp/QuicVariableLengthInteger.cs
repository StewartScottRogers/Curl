namespace Curl.Extension.ObliviousHttp.BinaryHttp;

/// <summary>
/// Writes the variable-length integers of RFC 9000 section 16 that Binary HTTP (RFC 9292
/// section 3) uses for every length, framing indicator and status code: the top two bits of
/// the first byte give the encoding's length (1, 2, 4 or 8 bytes) and the rest hold the value
/// in network byte order. <see cref="BinaryHttpReader" /> reads them.
/// </summary>
internal static class QuicVariableLengthInteger
{
    /// <summary>The largest value the encoding holds, 2^62 - 1.</summary>
    internal const long MaxValue = (1L << 62) - 1;

    /// <summary>Writes <paramref name="value" /> in the shortest encoding that holds it.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or above <see cref="MaxValue" />.</exception>
    internal static void Write(Stream destination, long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaxValue);
        int lengthExponent = value < 1 << 6 ? 0 : value < 1 << 14 ? 1 : value < 1 << 30 ? 2 : 3;
        int length = 1 << lengthExponent;
        ulong encoded = (ulong)value | ((ulong)lengthExponent << ((8 * length) - 2));
        for (int shift = 8 * (length - 1); shift >= 0; shift -= 8)
        {
            destination.WriteByte((byte)(encoded >> shift));
        }
    }
}
