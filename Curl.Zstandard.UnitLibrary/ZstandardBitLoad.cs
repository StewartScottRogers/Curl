using System.Buffers.Binary;

namespace Curl.Zstandard;

/// <summary>Loads the little-endian bytes the bit readers extract their bits from.</summary>
internal static class ZstandardBitLoad
{
    /// <summary>
    /// Returns the eight bytes of <paramref name="source" /> from <paramref name="index" /> as a
    /// little-endian integer, with every byte past the end of <paramref name="source" /> read as 0.
    /// </summary>
    public static ulong LittleEndian64(ReadOnlySpan<byte> source, int index)
    {
        if (source.Length - index >= sizeof(ulong))
        {
            return BinaryPrimitives.ReadUInt64LittleEndian(source[index..]);
        }

        ulong value = 0;
        for (var position = source.Length - 1; position >= index; position--)
        {
            value = (value << 8) | source[position];
        }

        return value;
    }

    /// <summary>Returns the low <paramref name="count" /> bits of <paramref name="value" />.</summary>
    public static uint LowBits(ulong value, int count) => (uint)(value & ((1UL << count) - 1));
}
