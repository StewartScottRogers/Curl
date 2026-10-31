using System.Numerics;

namespace Curl.Extension.Protocol.Kafka.Wire.Compression.Zstd;

/// <summary>
/// A zstd Finite State Entropy decoding table (RFC 8878 section 4.1): one entry per state giving the symbol, the bits
/// the next state reads and the baseline they are added to. Built from normalised counts read from the stream, from a
/// predefined distribution (section 3.1.1.3.2.2) or as a one-symbol RLE table.
/// </summary>
internal sealed class ZstdFiniteStateEntropyTable
{
    private const int MinimumAccuracyLog = 5;

    private const int AccuracyLogFieldBits = 4;

    private const int ZeroRunFieldBits = 2;

    private const int ZeroRunContinues = 3;

    private static readonly short[] LiteralLengthCounts =
    [
        4, 3, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 3, 2, 1, 1, 1, 1, 1, -1, -1, -1, -1,
    ];

    private static readonly short[] MatchLengthCounts =
    [
        1, 4, 3, 2, 2, 2, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, -1, -1, -1, -1, -1, -1, -1,
    ];

    private static readonly short[] OffsetCounts =
    [
        1, 1, 1, 1, 1, 1, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, -1, -1, -1, -1, -1,
    ];

    private readonly byte[] _symbols;

    private readonly int[] _baselines;

    private readonly byte[] _bitCounts;

    private ZstdFiniteStateEntropyTable(int accuracyLog, byte[] symbols, int[] baselines, byte[] bitCounts)
    {
        AccuracyLog = accuracyLog;
        _symbols = symbols;
        _baselines = baselines;
        _bitCounts = bitCounts;
    }

    /// <summary>The predefined literal length table (accuracy log 6).</summary>
    internal static ZstdFiniteStateEntropyTable PredefinedLiteralLength { get; } = FromNormalizedCounts(LiteralLengthCounts, 6);

    /// <summary>The predefined match length table (accuracy log 6).</summary>
    internal static ZstdFiniteStateEntropyTable PredefinedMatchLength { get; } = FromNormalizedCounts(MatchLengthCounts, 6);

    /// <summary>The predefined offset table (accuracy log 5).</summary>
    internal static ZstdFiniteStateEntropyTable PredefinedOffset { get; } = FromNormalizedCounts(OffsetCounts, 5);

    /// <summary>The bits that read the initial state; the table has two to the power of this many states.</summary>
    internal int AccuracyLog { get; }

    /// <summary>Gives the table that always decodes <paramref name="symbol"/> and reads no bits.</summary>
    internal static ZstdFiniteStateEntropyTable Rle(byte symbol) => new(0, [symbol], [0], [0]);

    /// <summary>
    /// Reads a table description from the start of <paramref name="source"/>: the accuracy log, then the normalised
    /// counts (-1 is "less than one") as variable-width fields, with runs of zero counts coded in two-bit steps.
    /// <paramref name="bytesRead"/> is the whole bytes the description takes.
    /// </summary>
    internal static ZstdFiniteStateEntropyTable Read(ReadOnlySpan<byte> source, int maxAccuracyLog, int maxSymbol, out int bytesRead)
    {
        int bitPosition = 0;
        int accuracyLog = MinimumAccuracyLog + PeekForward(source, bitPosition, AccuracyLogFieldBits);
        Advance(source, ref bitPosition, AccuracyLogFieldBits);
        if (accuracyLog > maxAccuracyLog)
        {
            throw new FormatException("zstd: FSE accuracy log " + accuracyLog + " is above " + maxAccuracyLog);
        }

        int threshold = 1 << accuracyLog;
        int remaining = threshold + 1;
        int bitCount = accuracyLog + 1;
        List<short> counts = [];
        bool previousWasZero = false;
        while (remaining > 1 && counts.Count <= maxSymbol)
        {
            if (previousWasZero)
            {
                SkipZeroRun(source, ref bitPosition, counts, maxSymbol);
            }

            int count = ReadCount(source, ref bitPosition, bitCount, threshold, remaining) - 1;
            counts.Add((short)count);
            remaining -= Math.Abs(count);
            previousWasZero = count == 0;
            while (remaining < threshold)
            {
                bitCount--;
                threshold >>= 1;
            }
        }

        bytesRead = (bitPosition + 7) / 8;
        return FromNormalizedCounts([.. counts], accuracyLog);
    }

    /// <summary>
    /// Builds the decoding table for <paramref name="counts"/> (one per symbol, -1 for "less than one"), which must add up
    /// to two to the power of <paramref name="accuracyLog"/> with each -1 counting as one.
    /// </summary>
    internal static ZstdFiniteStateEntropyTable FromNormalizedCounts(short[] counts, int accuracyLog)
    {
        int size = 1 << accuracyLog;
        int total = SumCounts(counts);
        if (total != size)
        {
            throw new FormatException("zstd: FSE counts add up to " + total + ", not " + size);
        }

        byte[] symbols = new byte[size];
        int[] nextStateNumbers = new int[counts.Length];
        int lastCell = PlaceLowProbabilitySymbols(counts, symbols, nextStateNumbers);
        SpreadSymbols(counts, symbols, size, lastCell);
        int[] baselines = new int[size];
        byte[] bitCounts = new byte[size];
        for (int state = 0; state < size; state++)
        {
            int nextStateNumber = nextStateNumbers[symbols[state]]++;
            int bits = accuracyLog - BitOperations.Log2((uint)nextStateNumber);
            bitCounts[state] = (byte)bits;
            baselines[state] = (nextStateNumber << bits) - size;
        }

        return new ZstdFiniteStateEntropyTable(accuracyLog, symbols, baselines, bitCounts);
    }

    /// <summary>Gives the symbol that <paramref name="state"/> decodes.</summary>
    internal byte Symbol(int state) => _symbols[state];

    /// <summary>Reads the initial state, <see cref="AccuracyLog"/> bits.</summary>
    internal int ReadInitialState(ZstdBackwardBitReader reader) => (int)reader.ReadBits(AccuracyLog);

    /// <summary>Gives the state after <paramref name="state"/>: its baseline plus the bits it asks for.</summary>
    internal int NextState(int state, ZstdBackwardBitReader reader) => _baselines[state] + (int)reader.ReadBits(_bitCounts[state]);

    private static int SumCounts(short[] counts)
    {
        int total = 0;
        foreach (short count in counts)
        {
            total += count == -1 ? 1 : count;
        }

        return total;
    }

    /// <summary>Puts each "less than one" symbol in a cell from the end of the table down; gives the last cell left.</summary>
    private static int PlaceLowProbabilitySymbols(short[] counts, byte[] symbols, int[] nextStateNumbers)
    {
        int lastCell = symbols.Length - 1;
        for (int symbol = 0; symbol < counts.Length; symbol++)
        {
            if (counts[symbol] == -1)
            {
                symbols[lastCell--] = (byte)symbol;
                nextStateNumbers[symbol] = 1;
            }
            else
            {
                nextStateNumbers[symbol] = counts[symbol];
            }
        }

        return lastCell;
    }

    private static void SpreadSymbols(short[] counts, byte[] symbols, int size, int lastCell)
    {
        int step = (size >> 1) + (size >> 3) + 3;
        int mask = size - 1;
        int position = 0;
        for (int symbol = 0; symbol < counts.Length; symbol++)
        {
            for (int placed = 0; placed < counts[symbol]; placed++)
            {
                symbols[position] = (byte)symbol;
                do
                {
                    position = (position + step) & mask;
                }
                while (position > lastCell);
            }
        }
    }

    private static int ReadCount(ReadOnlySpan<byte> source, ref int bitPosition, int bitCount, int threshold, int remaining)
    {
        int maximum = (2 * threshold) - 1 - remaining;
        int value = PeekForward(source, bitPosition, bitCount);
        int low = value & (threshold - 1);
        if (low < maximum)
        {
            Advance(source, ref bitPosition, bitCount - 1);
            return low;
        }

        Advance(source, ref bitPosition, bitCount);
        return value >= threshold ? value - maximum : value;
    }

    private static void SkipZeroRun(ReadOnlySpan<byte> source, ref int bitPosition, List<short> counts, int maxSymbol)
    {
        int step;
        do
        {
            step = PeekForward(source, bitPosition, ZeroRunFieldBits);
            Advance(source, ref bitPosition, ZeroRunFieldBits);
            for (int zero = 0; zero < step; zero++)
            {
                counts.Add(0);
            }
        }
        while (step == ZeroRunContinues);

        if (counts.Count > maxSymbol)
        {
            throw new FormatException("zstd: FSE counts name more than " + (maxSymbol + 1) + " symbols");
        }
    }

    private static int PeekForward(ReadOnlySpan<byte> source, int bitPosition, int count)
    {
        int value = 0;
        for (int offset = 0; offset < count; offset++)
        {
            int index = bitPosition + offset;
            int bit = (index >> 3) < source.Length ? (source[index >> 3] >> (index & 7)) & 1 : 0;
            value |= bit << offset;
        }

        return value;
    }

    private static void Advance(ReadOnlySpan<byte> source, ref int bitPosition, int count)
    {
        bitPosition += count;
        if (bitPosition > source.Length * 8)
        {
            throw new FormatException("zstd: FSE table description is truncated");
        }
    }
}
