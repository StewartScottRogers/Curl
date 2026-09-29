using System.Numerics;

namespace Curl.Zstandard;

/// <summary>
/// An FSE decoding table (RFC 8878 section 4.1): for each state, the symbol it decodes and
/// how the next state is read. The Huffman weights of a literals section use one, and the
/// sequences section uses three.
/// </summary>
internal sealed class ZstandardFseTable
{
    /// <summary>RFC 8878 section 4.1.1: <c>Accuracy_Log</c> is its 4-bit field plus 5.</summary>
    private const int MinimumAccuracyLog = 5;

    private readonly byte[] symbols;

    private readonly byte[] numberOfBits;

    private readonly int[] baselines;

    private ZstandardFseTable(int accuracyLog, byte[] symbols, byte[] numberOfBits, int[] baselines)
    {
        AccuracyLog = accuracyLog;
        this.symbols = symbols;
        this.numberOfBits = numberOfBits;
        this.baselines = baselines;
    }

    /// <summary>The table has <c>2^AccuracyLog</c> states.</summary>
    public int AccuracyLog { get; }

    /// <summary>
    /// Reads an FSE table description from the start of <paramref name="source" /> and
    /// builds its table; <see langword="null" /> when the description is corrupt, uses an
    /// <c>Accuracy_Log</c> above <paramref name="maxAccuracyLog" /> or a symbol above
    /// <paramref name="maxSymbol" />, or runs past the end of <paramref name="source" />.
    /// </summary>
    public static ZstandardFseTable? Read(ReadOnlySpan<byte> source, int maxAccuracyLog, int maxSymbol, out int bytesRead)
    {
        var bits = new ZstandardForwardBitReader(source);
        var accuracyLog = (int)bits.ReadBits(4) + MinimumAccuracyLog;
        var counts = accuracyLog > maxAccuracyLog ? null : ReadNormalizedCounts(ref bits, accuracyLog, maxSymbol);
        bytesRead = bits.BytesConsumed;
        return counts is null || bits.IsOverrun ? null : Build(counts, accuracyLog);
    }

    /// <summary>
    /// Builds the table for a distribution whose <paramref name="normalizedCounts" /> (−1 for
    /// a "less than 1" probability) have magnitudes summing to <c>2^<paramref name="accuracyLog" /></c>.
    /// </summary>
    public static ZstandardFseTable Build(ReadOnlySpan<short> normalizedCounts, int accuracyLog)
    {
        var size = 1 << accuracyLog;
        var symbols = new byte[size];
        var nextStates = new int[normalizedCounts.Length];
        var highThreshold = size;
        for (var symbol = 0; symbol < normalizedCounts.Length; symbol++)
        {
            if (normalizedCounts[symbol] == -1)
            {
                symbols[--highThreshold] = (byte)symbol;
                nextStates[symbol] = 1;
            }
        }

        SpreadSymbols(normalizedCounts, symbols, nextStates, highThreshold);
        var numberOfBits = new byte[size];
        var baselines = new int[size];
        for (var state = 0; state < size; state++)
        {
            var next = nextStates[symbols[state]]++;
            numberOfBits[state] = (byte)(accuracyLog - BitOperations.Log2((uint)next));
            baselines[state] = (next << numberOfBits[state]) - size;
        }

        return new ZstandardFseTable(accuracyLog, symbols, numberOfBits, baselines);
    }

    /// <summary>The symbol <paramref name="state" /> decodes.</summary>
    public byte Symbol(int state) => symbols[state];

    /// <summary>Reads the first state from <paramref name="bits" />.</summary>
    public int ReadInitialState(ref ZstandardBackwardBitReader bits) => (int)bits.ReadBits(AccuracyLog);

    /// <summary>Reads the state that follows <paramref name="state" /> from <paramref name="bits" />.</summary>
    public int ReadNextState(int state, ref ZstandardBackwardBitReader bits) => baselines[state] + (int)bits.ReadBits(numberOfBits[state]);

    /// <summary>
    /// RFC 8878 section 4.1.1: reads each symbol's normalized count until the probabilities
    /// add up; <see langword="null" /> when they do not add up exactly by
    /// <paramref name="maxSymbol" />.
    /// </summary>
    private static short[]? ReadNormalizedCounts(ref ZstandardForwardBitReader bits, int accuracyLog, int maxSymbol)
    {
        var counts = new short[maxSymbol + 1];
        var remaining = 1 << accuracyLog;
        var symbol = 0;
        while (remaining > 0 && symbol <= maxSymbol)
        {
            var count = ReadCount(ref bits, remaining);
            counts[symbol++] = (short)count;
            remaining -= Math.Abs(count);
            symbol += count == 0 ? ReadZeroRepeat(ref bits) : 0;
        }

        return remaining == 0 ? counts : null;
    }

    /// <summary>
    /// Reads one count, coded in just enough bits for any count up to the
    /// <paramref name="remaining" /> probability, plus one for "less than 1".
    /// </summary>
    private static int ReadCount(ref ZstandardForwardBitReader bits, int remaining)
    {
        var width = BitOperations.Log2((uint)remaining + 1) + 1;
        var lowerMask = (1 << (width - 1)) - 1;
        var threshold = (1 << width) - 1 - (remaining + 1);
        var value = (int)bits.PeekBits(width);
        if ((value & lowerMask) < threshold)
        {
            bits.SkipBits(width - 1);
            return (value & lowerMask) - 1;
        }

        bits.SkipBits(width);
        return (value > lowerMask ? value - threshold : value) - 1;
    }

    /// <summary>After a zero count: how many more symbols have a zero count, in 2-bit pieces while each is 3.</summary>
    private static int ReadZeroRepeat(ref ZstandardForwardBitReader bits)
    {
        var total = 0;
        uint repeat;
        do
        {
            repeat = bits.ReadBits(2);
            total += (int)repeat;
        }
        while (repeat == 3);

        return total;
    }

    /// <summary>Spreads each symbol over as many states as its count, stepping over the "less than 1" states at the top.</summary>
    private static void SpreadSymbols(ReadOnlySpan<short> normalizedCounts, byte[] symbols, int[] nextStates, int highThreshold)
    {
        var size = symbols.Length;
        var step = (size >> 1) + (size >> 3) + 3;
        var mask = size - 1;
        var position = 0;
        for (var symbol = 0; symbol < normalizedCounts.Length; symbol++)
        {
            for (var occurrence = 0; occurrence < normalizedCounts[symbol]; occurrence++)
            {
                symbols[position] = (byte)symbol;
                do
                {
                    position = (position + step) & mask;
                }
                while (position >= highThreshold);
            }

            nextStates[symbol] = Math.Max(nextStates[symbol], (int)normalizedCounts[symbol]);
        }
    }
}
