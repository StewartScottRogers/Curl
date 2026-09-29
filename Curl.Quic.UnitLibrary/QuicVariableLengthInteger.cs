using System.Numerics;

namespace Curl.Quic;

/// <summary>
/// QUIC's variable-length integer encoding (RFC 9000 section 16): the two most significant
/// bits of the first byte give the length, 1, 2, 4 or 8 bytes, and the remaining bits hold
/// the value in network byte order, up to 2^62 - 1.
/// </summary>
public static class QuicVariableLengthInteger
{
    /// <summary>The largest value the encoding can carry, 2^62 - 1.</summary>
    public const ulong MaximumValue = (1UL << 62) - 1;

    /// <summary>
    /// Gets the length of the shortest encoding of a value.
    /// </summary>
    /// <param name="value">The value, at most <see cref="MaximumValue" />.</param>
    /// <returns>1, 2, 4 or 8.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value" /> is above <see cref="MaximumValue" />.</exception>
    public static int GetEncodedLength(ulong value)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaximumValue);
        return value switch
        {
            <= 0x3f => 1,
            <= 0x3fff => 2,
            <= 0x3fff_ffff => 4,
            _ => 8,
        };
    }

    /// <summary>
    /// Writes a value in its shortest encoding.
    /// </summary>
    /// <param name="value">The value, at most <see cref="MaximumValue" />.</param>
    /// <param name="destination">Where to write it.</param>
    /// <returns>The number of bytes written.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value" /> is above <see cref="MaximumValue" />.</exception>
    /// <exception cref="ArgumentException"><paramref name="destination" /> is too short.</exception>
    public static int Write(ulong value, Span<byte> destination) => Write(value, GetEncodedLength(value), destination);

    /// <summary>
    /// Writes a value in an encoding of a chosen length, which may be longer than the
    /// shortest: RFC 9000 section 16 allows it, and RFC 9001 Appendix A.2 writes a packet's
    /// Length field in two bytes.
    /// </summary>
    /// <param name="value">The value, at most <see cref="MaximumValue" />.</param>
    /// <param name="encodedLength">1, 2, 4 or 8, and no shorter than the value needs.</param>
    /// <param name="destination">Where to write it.</param>
    /// <returns><paramref name="encodedLength" />.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value" /> is above <see cref="MaximumValue" />, or <paramref name="encodedLength" /> is not 1, 2, 4 or 8, or is too short for it.</exception>
    /// <exception cref="ArgumentException"><paramref name="destination" /> is too short.</exception>
    public static int Write(ulong value, int encodedLength, Span<byte> destination)
    {
        if (encodedLength > 8 || !BitOperations.IsPow2(encodedLength) || encodedLength < GetEncodedLength(value))
        {
            throw new ArgumentOutOfRangeException(nameof(encodedLength), encodedLength, $"The value {value} cannot be encoded in {encodedLength} bytes.");
        }

        if (destination.Length < encodedLength)
        {
            throw new ArgumentException($"A {encodedLength}-byte variable-length integer does not fit in {destination.Length} bytes.", nameof(destination));
        }

        var encoded = value | (ulong)BitOperations.Log2((uint)encodedLength) << (8 * encodedLength - 2);
        for (var index = 0; index < encodedLength; index++)
        {
            destination[index] = (byte)(encoded >> (8 * (encodedLength - 1 - index)));
        }

        return encodedLength;
    }

    /// <summary>
    /// Reads a value from the start of a buffer. Any of the four lengths is accepted for
    /// any value, as RFC 9000 section 16 requires.
    /// </summary>
    /// <param name="source">The bytes to read from.</param>
    /// <param name="value">The value read, or 0.</param>
    /// <param name="bytesRead">The length of its encoding, or 0.</param>
    /// <returns><see langword="false" /> when <paramref name="source" /> ends before the encoding does.</returns>
    public static bool TryRead(ReadOnlySpan<byte> source, out ulong value, out int bytesRead)
    {
        value = 0;
        bytesRead = 0;
        if (source.IsEmpty || source.Length < 1 << (source[0] >> 6))
        {
            return false;
        }

        bytesRead = 1 << (source[0] >> 6);
        value = source[0] & 0x3fUL;
        foreach (var next in source[1..bytesRead])
        {
            value = value << 8 | next;
        }

        return true;
    }
}
