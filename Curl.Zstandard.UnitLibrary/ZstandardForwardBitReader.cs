namespace Curl.Zstandard;

/// <summary>
/// Reads bits forwards, lowest bit of the first byte first, as an FSE table description
/// is written (RFC 8878 section 4.1.1). Reading past the end gives zero bits and sets
/// <see cref="IsOverrun" />.
/// </summary>
internal ref struct ZstandardForwardBitReader(ReadOnlySpan<byte> source)
{
    private readonly ReadOnlySpan<byte> source = source;

    private int position;

    /// <summary>The whole bytes the bits read so far occupy.</summary>
    public readonly int BytesConsumed => (position + 7) >> 3;

    /// <summary>Whether more bits were read than the source holds.</summary>
    public readonly bool IsOverrun => position > source.Length * 8;

    /// <summary>Returns the next <paramref name="count" /> bits (at most 32) without consuming them.</summary>
    public readonly uint PeekBits(int count) =>
        ZstandardBitLoad.LowBits(ZstandardBitLoad.LittleEndian64(source, position >> 3) >> (position & 7), count);

    /// <summary>Consumes <paramref name="count" /> bits.</summary>
    public void SkipBits(int count) => position += count;

    /// <summary>Reads the next <paramref name="count" /> bits (at most 32).</summary>
    public uint ReadBits(int count)
    {
        var value = PeekBits(count);
        position += count;
        return value;
    }
}
