using System.Numerics;

namespace Curl.Extension.Protocol.Kafka.Wire.Compression.Zstd;

/// <summary>
/// Reads a zstd bit stream backward (RFC 8878 section 4.1): the last byte's highest set bit is an end marker, the bits
/// below it are read first, and the first bit read is the most significant bit of a value. Reading past the start of the
/// stream gives zero bits and sets <see cref="Overflowed"/>, which the caller checks, as FSE and Huffman decoding end
/// by running out of bits.
/// </summary>
internal sealed class ZstdBackwardBitReader
{
    private readonly byte[] _bytes;

    /// <summary>Reads <paramref name="stream"/>, which must end in a byte with an end marker bit.</summary>
    internal ZstdBackwardBitReader(ReadOnlySpan<byte> stream)
    {
        if (stream.IsEmpty || stream[^1] == 0)
        {
            throw new FormatException("zstd: bit stream has no end marker");
        }

        _bytes = stream.ToArray();
        RemainingBits = ((stream.Length - 1) * 8) + BitOperations.Log2(stream[^1]);
    }

    /// <summary>The bits not yet read; negative once reading has gone past the start of the stream.</summary>
    internal int RemainingBits { get; private set; }

    /// <summary>Whether more bits were read than the stream holds.</summary>
    internal bool Overflowed => RemainingBits < 0;

    /// <summary>Gives the next <paramref name="count"/> bits (at most 32) without reading them; bits before the start are zero.</summary>
    internal uint PeekBits(int count)
    {
        uint value = 0;
        for (int offset = 1; offset <= count; offset++)
        {
            int index = RemainingBits - offset;
            value <<= 1;
            if (index >= 0 && ((_bytes[index >> 3] >> (index & 7)) & 1) != 0)
            {
                value |= 1;
            }
        }

        return value;
    }

    /// <summary>Reads <paramref name="count"/> bits without giving them.</summary>
    internal void SkipBits(int count) => RemainingBits -= count;

    /// <summary>Reads and gives the next <paramref name="count"/> bits (at most 32).</summary>
    internal uint ReadBits(int count)
    {
        uint value = PeekBits(count);
        SkipBits(count);
        return value;
    }
}
