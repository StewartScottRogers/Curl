using System.Numerics;

namespace Curl.Extension.Kafka.Wire.Compression.Zstd;

/// <summary>
/// A zstd Huffman decoding table (RFC 8878 section 4.2): weights read from a header, either directly as four-bit values
/// or FSE-compressed, with the last weight implied by completing a power of two. Every code is looked up by peeking
/// <see cref="TableLog"/> bits of a backward bit stream.
/// </summary>
internal sealed class ZstdHuffmanTable
{
    private const int MaximumBits = 11;

    private const int MaximumWeight = 11;

    private const int MaximumFseAccuracyLog = 6;

    private const int FirstDirectHeader = 128;

    private const int MaximumExplicitWeights = 255;

    private readonly byte[] _symbols;

    private readonly byte[] _bitCounts;

    private ZstdHuffmanTable(int tableLog, byte[] symbols, byte[] bitCounts)
    {
        TableLog = tableLog;
        _symbols = symbols;
        _bitCounts = bitCounts;
    }

    /// <summary>The longest code in bits; the table has two to the power of this many entries.</summary>
    internal int TableLog { get; }

    /// <summary>
    /// Reads a Huffman tree description from the start of <paramref name="source"/>: a header byte at 128 or more gives
    /// that minus 127 four-bit weights, a smaller one is the size of an FSE-compressed weights block.
    /// <paramref name="bytesRead"/> is the header and weights' bytes.
    /// </summary>
    internal static ZstdHuffmanTable Read(ReadOnlySpan<byte> source, out int bytesRead)
    {
        if (source.IsEmpty)
        {
            throw new FormatException("zstd: Huffman table description is missing");
        }

        int header = source[0];
        byte[] weights = header >= FirstDirectHeader
            ? ReadDirectWeights(source, header - (FirstDirectHeader - 1), out bytesRead)
            : ReadCompressedWeights(source, header, out bytesRead);
        return FromWeights(weights);
    }

    /// <summary>
    /// Builds the table from <paramref name="explicitWeights"/> for symbols 0 up, adding the last symbol whose weight
    /// completes the sum of two to the power of weight minus one to a power of two.
    /// </summary>
    internal static ZstdHuffmanTable FromWeights(ReadOnlySpan<byte> explicitWeights)
    {
        byte[] weights = CompleteWeights(explicitWeights, out int tableLog);
        byte[] symbols = new byte[1 << tableLog];
        byte[] bitCounts = new byte[1 << tableLog];
        int cell = 0;
        for (int weight = 1; weight <= tableLog; weight++)
        {
            for (int symbol = 0; symbol < weights.Length; symbol++)
            {
                if (weights[symbol] != weight)
                {
                    continue;
                }

                for (int placed = 0; placed < 1 << (weight - 1); placed++)
                {
                    symbols[cell] = (byte)symbol;
                    bitCounts[cell++] = (byte)(tableLog + 1 - weight);
                }
            }
        }

        return new ZstdHuffmanTable(tableLog, symbols, bitCounts);
    }

    /// <summary>Reads one code from <paramref name="reader"/>; the caller checks <see cref="ZstdBackwardBitReader.Overflowed"/>.</summary>
    internal byte DecodeSymbol(ZstdBackwardBitReader reader)
    {
        int index = (int)reader.PeekBits(TableLog);
        reader.SkipBits(_bitCounts[index]);
        return _symbols[index];
    }

    private static byte[] CompleteWeights(ReadOnlySpan<byte> explicitWeights, out int tableLog)
    {
        int sum = 0;
        foreach (byte weight in explicitWeights)
        {
            sum += weight == 0 ? 0 : 1 << (weight - 1);
        }

        if (sum == 0)
        {
            throw new FormatException("zstd: Huffman weights are all zero");
        }

        tableLog = BitOperations.Log2((uint)sum) + 1;
        int missing = (1 << tableLog) - sum;
        if (tableLog > MaximumBits || !BitOperations.IsPow2(missing))
        {
            throw new FormatException("zstd: Huffman weights do not complete a power of two of at most " + (1 << MaximumBits));
        }

        return [.. explicitWeights, (byte)(BitOperations.Log2((uint)missing) + 1)];
    }

    private static byte[] ReadDirectWeights(ReadOnlySpan<byte> source, int count, out int bytesRead)
    {
        bytesRead = 1 + ((count + 1) / 2);
        if (source.Length < bytesRead)
        {
            throw new FormatException("zstd: Huffman weights are truncated");
        }

        byte[] weights = new byte[count];
        for (int index = 0; index < count; index++)
        {
            byte pair = source[1 + (index / 2)];
            weights[index] = (byte)(index % 2 == 0 ? pair >> 4 : pair & 0x0F);
        }

        return weights;
    }

    private static byte[] ReadCompressedWeights(ReadOnlySpan<byte> source, int size, out int bytesRead)
    {
        bytesRead = 1 + size;
        if (size == 0 || source.Length < bytesRead)
        {
            throw new FormatException("zstd: Huffman compressed weights are empty or truncated");
        }

        return DecodeWeightStates(source.Slice(1, size));
    }

    private static byte[] DecodeWeightStates(ReadOnlySpan<byte> compressed)
    {
        ZstdFiniteStateEntropyTable table = ZstdFiniteStateEntropyTable.Read(compressed, MaximumFseAccuracyLog, MaximumWeight, out int tableBytes);
        ZstdBackwardBitReader reader = new(compressed[tableBytes..]);
        int first = table.ReadInitialState(reader);
        int second = table.ReadInitialState(reader);
        if (reader.Overflowed)
        {
            throw new FormatException("zstd: Huffman compressed weights are truncated");
        }

        List<byte> weights = [];
        while (true)
        {
            AddWeight(weights, table.Symbol(first));
            first = table.NextState(first, reader);
            if (reader.Overflowed)
            {
                AddWeight(weights, table.Symbol(second));
                return [.. weights];
            }

            AddWeight(weights, table.Symbol(second));
            second = table.NextState(second, reader);
            if (reader.Overflowed)
            {
                AddWeight(weights, table.Symbol(first));
                return [.. weights];
            }
        }
    }

    private static void AddWeight(List<byte> weights, byte weight)
    {
        if (weights.Count == MaximumExplicitWeights)
        {
            throw new FormatException("zstd: Huffman compressed weights name more than " + MaximumExplicitWeights + " symbols");
        }

        weights.Add(weight);
    }
}
