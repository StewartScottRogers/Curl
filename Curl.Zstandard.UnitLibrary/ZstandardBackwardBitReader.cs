using System.Numerics;

namespace Curl.Zstandard;

/// <summary>
/// Reads a Zstandard backward bitstream (RFC 8878 section 4.1): the Huffman streams and
/// the FSE-coded streams are written forwards and read from their last byte back, starting
/// below the highest set bit of that byte, which is padding. Reading past the start of the
/// stream gives zero bits and leaves <see cref="BitsRemaining" /> negative, which is how an
/// FSE stream signals its end.
/// </summary>
internal ref struct ZstandardBackwardBitReader
{
    private readonly ReadOnlySpan<byte> stream;

    private ZstandardBackwardBitReader(ReadOnlySpan<byte> stream)
    {
        this.stream = stream;
        BitsRemaining = ((stream.Length - 1) * 8) + BitOperations.Log2(stream[^1]);
    }

    /// <summary>The bits still unread; negative once more bits were read than the stream holds.</summary>
    public int BitsRemaining { get; private set; }

    /// <summary>Whether more bits were read than the stream holds.</summary>
    public readonly bool IsOverflowed => BitsRemaining < 0;

    /// <summary>
    /// Starts reading <paramref name="stream" />; <see langword="false" /> when it is empty or
    /// its last byte is 0, so it has no padding bit, which RFC 8878 makes corruption.
    /// </summary>
    public static bool TryCreate(ReadOnlySpan<byte> stream, out ZstandardBackwardBitReader reader)
    {
        reader = default;
        if (stream.IsEmpty || stream[^1] == 0)
        {
            return false;
        }

        reader = new ZstandardBackwardBitReader(stream);
        return true;
    }

    /// <summary>Reads the next <paramref name="count" /> bits (at most 32), the first-read bit highest.</summary>
    public uint ReadBits(int count)
    {
        BitsRemaining -= count;
        var start = BitsRemaining;
        if (start >= 0)
        {
            return ZstandardBitLoad.LowBits(ZstandardBitLoad.LittleEndian64(stream, start >> 3) >> (start & 7), count);
        }

        return -start >= count ? 0 : ZstandardBitLoad.LowBits(ZstandardBitLoad.LittleEndian64(stream, 0) << -start, count);
    }
}
