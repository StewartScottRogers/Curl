using System.Buffers.Binary;
using System.Numerics;

namespace Curl.Zstandard;

/// <summary>
/// Hand-assembles literals sections for the decoder tests (RFC 8878 sections 3.1.1.3.1, 4.1
/// and 4.2): bitstreams, literals headers, Huffman tree descriptions with direct or
/// FSE-compressed weights, and Huffman streams coded from weights by RFC 8878's canonical
/// code assignment. It is an encoder only as far as the tests need one; real curl decoding
/// what it builds is what shows it is right.
/// </summary>
internal static class ZstandardTestLiterals
{
    public const int Raw = 0;

    public const int Rle = 1;

    public const int Compressed = 2;

    public const int Treeless = 3;

    /// <summary>
    /// A backward bitstream a decoder reads as <paramref name="readsInOrder" />: each value in
    /// its width in bits, the first read first. It is written in the reverse order and closed
    /// with the padding bit.
    /// </summary>
    public static byte[] BackwardStream(IEnumerable<(uint Value, int Bits)> readsInOrder)
    {
        var writer = new ForwardBitWriter();
        foreach (var (value, bits) in readsInOrder.Reverse())
        {
            writer.Write(value, bits);
        }

        writer.Write(1, 1);
        return writer.ToArray();
    }

    /// <summary>A raw or RLE literals header in its shortest form for <paramref name="size" />.</summary>
    public static byte[] UncompressedHeader(int type, int size) => size switch
    {
        < 32 => [(byte)((size << 3) | type)],
        < 4096 => [(byte)((size << 4) | 0x04 | type), (byte)(size >> 4)],
        _ => [(byte)((size << 4) | 0x0C | type), (byte)(size >> 4), (byte)(size >> 12)],
    };

    /// <summary>A compressed or treeless literals header of <paramref name="sizeFormat" /> (0 is one stream).</summary>
    public static byte[] HuffmanHeader(int type, int sizeFormat, int regeneratedSize, int compressedSize)
    {
        int[] widths = [10, 10, 14, 18];
        var width = widths[sizeFormat];
        var value = (ulong)(uint)(type | (sizeFormat << 2) | (regeneratedSize << 4)) | ((ulong)(uint)compressedSize << (4 + width));
        var length = Math.Max(3, sizeFormat + 2);
        var bytes = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        return bytes[..length];
    }

    /// <summary>A Huffman tree description of weights written directly, four bits each.</summary>
    public static byte[] DirectTreeDescription(params byte[] codedWeights)
    {
        var packed = new byte[(codedWeights.Length + 1) / 2];
        for (var index = 0; index < codedWeights.Length; index++)
        {
            packed[index / 2] |= (byte)((index & 1) == 0 ? codedWeights[index] << 4 : codedWeights[index]);
        }

        return [(byte)(127 + codedWeights.Length), .. packed];
    }

    /// <summary>
    /// A Huffman tree description of weights compressed with FSE under the distribution
    /// <paramref name="weightCounts" /> (indexed by weight, −1 for "less than 1"), whose
    /// magnitudes sum to <c>2^<paramref name="accuracyLog" /></c>.
    /// </summary>
    public static byte[] FseTreeDescription(byte[] codedWeights, short[] weightCounts, int accuracyLog)
    {
        byte[] description = [.. FseTableDescription(weightCounts, accuracyLog), .. FseWeightStream(codedWeights, weightCounts, accuracyLog)];
        return [(byte)description.Length, .. description];
    }

    /// <summary>
    /// An FSE table description (RFC 8878 section 4.1.1) of <paramref name="counts" />, each
    /// coded in the fewest bits the remaining probability allows, and zero counts after a
    /// zero as 2-bit repeat flags.
    /// </summary>
    public static byte[] FseTableDescription(short[] counts, int accuracyLog)
    {
        var writer = new ForwardBitWriter();
        writer.Write((uint)(accuracyLog - 5), 4);
        var remaining = 1 << accuracyLog;
        var symbol = 0;
        while (remaining > 0)
        {
            var count = counts[symbol++];
            WriteCount(writer, count + 1, remaining);
            remaining -= Math.Abs(count);
            if (count == 0)
            {
                var zeros = 0;
                while (symbol < counts.Length && counts[symbol] == 0)
                {
                    zeros++;
                    symbol++;
                }

                for (; zeros >= 3; zeros -= 3)
                {
                    writer.Write(3, 2);
                }

                writer.Write((uint)zeros, 2);
            }
        }

        return writer.ToArray();
    }

    /// <summary>
    /// Codes <paramref name="data" /> as one Huffman stream under the tree of
    /// <paramref name="codedWeights" /> and the last symbol's implied weight.
    /// </summary>
    public static byte[] HuffmanStream(byte[] codedWeights, ReadOnlySpan<byte> data)
    {
        var codes = HuffmanCodes(codedWeights);
        var reads = new List<(uint, int)>();
        foreach (var symbol in data)
        {
            reads.Add(codes[symbol]);
        }

        return BackwardStream(reads);
    }

    /// <summary>Four Huffman streams behind their jump table, splitting <paramref name="data" /> as RFC 8878 section 3.1.1.3.1.6 does.</summary>
    public static byte[] FourHuffmanStreams(byte[] codedWeights, byte[] data)
    {
        var segment = (data.Length + 3) / 4;
        var streams = new[]
        {
            HuffmanStream(codedWeights, data.AsSpan(0, segment)),
            HuffmanStream(codedWeights, data.AsSpan(segment, segment)),
            HuffmanStream(codedWeights, data.AsSpan(2 * segment, segment)),
            HuffmanStream(codedWeights, data.AsSpan(3 * segment)),
        };
        var jumpTable = new byte[6];
        for (var index = 0; index < 3; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(jumpTable.AsSpan(index * 2), (ushort)streams[index].Length);
        }

        return [.. jumpTable, .. streams.SelectMany(stream => stream)];
    }

    /// <summary>
    /// Each symbol's prefix code and length, RFC 8878 section 4.2.1.3: symbols sorted by
    /// weight then value, codes handed out in order from the lowest weight.
    /// </summary>
    public static (uint Code, int Length)[] HuffmanCodes(byte[] codedWeights)
    {
        var total = codedWeights.Sum(weight => weight == 0 ? 0 : 1 << (weight - 1));
        var maxBits = BitOperations.Log2((uint)total) + 1;
        byte[] weights = [.. codedWeights, (byte)(BitOperations.Log2((uint)((1 << maxBits) - total)) + 1)];
        var codes = new (uint, int)[256];
        uint code = 0;
        var length = maxBits;
        foreach (var symbol in Enumerable.Range(0, weights.Length).Where(symbol => weights[symbol] > 0).OrderBy(symbol => weights[symbol]))
        {
            var symbolLength = maxBits + 1 - weights[symbol];
            code >>= length - symbolLength;
            length = symbolLength;
            codes[symbol] = (code++, length);
        }

        return codes;
    }

    /// <summary>
    /// The FSE bitstream of <paramref name="codedWeights" />: two interleaved states, each
    /// chain of states found backwards from its last, ending where the update after the
    /// second-to-last weight overflows the stream.
    /// </summary>
    private static byte[] FseWeightStream(byte[] codedWeights, short[] weightCounts, int accuracyLog)
    {
        var table = ZstandardFseTable.Build(weightCounts, accuracyLog);
        var (numberOfBits, baselines) = StateTransitions(table, weightCounts, accuracyLog);
        var size = 1 << accuracyLog;
        var count = codedWeights.Length;
        var states = new int[count];
        states[count - 1] = Enumerable.Range(0, size).First(state => table.Symbol(state) == codedWeights[count - 1]);
        states[count - 2] = Enumerable.Range(0, size).First(state => table.Symbol(state) == codedWeights[count - 2] && numberOfBits[state] > 0);
        for (var index = count - 3; index >= 0; index--)
        {
            var next = states[index + 2];
            states[index] = Enumerable.Range(0, size).Single(state =>
                table.Symbol(state) == codedWeights[index] && baselines[state] <= next && next < baselines[state] + (1 << numberOfBits[state]));
        }

        var reads = new List<(uint, int)> { ((uint)states[0], accuracyLog), ((uint)states[1], accuracyLog) };
        for (var index = 0; index < count - 2; index++)
        {
            reads.Add(((uint)(states[index + 2] - baselines[states[index]]), numberOfBits[states[index]]));
        }

        return BackwardStream(reads);
    }

    /// <summary>Each state's bit count and baseline, worked out from the symbols the table spread (RFC 8878 section 4.1.1).</summary>
    private static (int[] NumberOfBits, int[] Baselines) StateTransitions(ZstandardFseTable table, short[] counts, int accuracyLog)
    {
        var size = 1 << accuracyLog;
        var next = counts.Select(count => (int)Math.Abs(count)).ToArray();
        var numberOfBits = new int[size];
        var baselines = new int[size];
        for (var state = 0; state < size; state++)
        {
            var value = next[table.Symbol(state)]++;
            numberOfBits[state] = accuracyLog - BitOperations.Log2((uint)value);
            baselines[state] = (value << numberOfBits[state]) - size;
        }

        return (numberOfBits, baselines);
    }

    /// <summary>The inverse of the decoder's count reading: <paramref name="value" /> is the count plus one.</summary>
    private static void WriteCount(ForwardBitWriter writer, int value, int remaining)
    {
        var width = BitOperations.Log2((uint)remaining + 1) + 1;
        var lowerMask = (1 << (width - 1)) - 1;
        var threshold = (1 << width) - 1 - (remaining + 1);
        if (value < threshold)
        {
            writer.Write((uint)value, width - 1);
        }
        else
        {
            writer.Write((uint)(value > lowerMask ? value + threshold : value), width);
        }
    }

    /// <summary>Writes bits lowest first, filling each byte from its lowest bit.</summary>
    private sealed class ForwardBitWriter
    {
        private readonly List<bool> bits = [];

        public void Write(uint value, int count)
        {
            for (var bit = 0; bit < count; bit++)
            {
                bits.Add(((value >> bit) & 1) != 0);
            }
        }

        public byte[] ToArray()
        {
            var bytes = new byte[(bits.Count + 7) / 8];
            for (var index = 0; index < bits.Count; index++)
            {
                bytes[index / 8] |= (byte)(bits[index] ? 1 << (index % 8) : 0);
            }

            return bytes;
        }
    }
}
