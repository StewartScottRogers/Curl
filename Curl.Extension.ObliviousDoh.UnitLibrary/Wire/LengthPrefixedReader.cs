using System.Buffers.Binary;

namespace Curl.Extension.ObliviousDoh.Wire;

/// <summary>
/// Reads the TLS presentation language fields RFC 9230 sections 5 and 6.1 are written in: one octet, a two-octet
/// big-endian integer, or an <c>opaque &lt;0..2^16-1&gt;</c> vector, its two-octet length first.
/// </summary>
internal ref struct LengthPrefixedReader
{
    private ReadOnlySpan<byte> remaining;

    /// <summary>Initializes a new instance of the <see cref="LengthPrefixedReader" /> struct over <paramref name="bytes" />.</summary>
    /// <param name="bytes">The bytes to read.</param>
    internal LengthPrefixedReader(ReadOnlySpan<byte> bytes) => remaining = bytes;

    /// <summary>Gets a value indicating whether every byte has been read.</summary>
    internal readonly bool IsEmpty => remaining.IsEmpty;

    /// <summary>Reads one octet.</summary>
    /// <param name="value">The octet.</param>
    /// <returns><see langword="false" /> when no byte is left.</returns>
    internal bool TryReadByte(out byte value)
    {
        value = remaining.IsEmpty ? (byte)0 : remaining[0];
        return TrySkip(1);
    }

    /// <summary>Reads a two-octet big-endian integer.</summary>
    /// <param name="value">The integer.</param>
    /// <returns><see langword="false" /> when fewer than two bytes are left.</returns>
    internal bool TryReadUInt16(out ushort value)
    {
        value = remaining.Length < 2 ? (ushort)0 : BinaryPrimitives.ReadUInt16BigEndian(remaining);
        return TrySkip(2);
    }

    /// <summary>Reads a vector: a two-octet length, then that many bytes.</summary>
    /// <param name="value">The vector's bytes, without the length.</param>
    /// <returns><see langword="false" /> when the length or the bytes it counts are not all there.</returns>
    internal bool TryReadVector(out ReadOnlySpan<byte> value)
    {
        value = [];
        if (!TryReadUInt16(out ushort length) || remaining.Length < length)
        {
            return false;
        }

        value = remaining[..length];
        return TrySkip(length);
    }

    private bool TrySkip(int count)
    {
        if (remaining.Length < count)
        {
            return false;
        }

        remaining = remaining[count..];
        return true;
    }
}
