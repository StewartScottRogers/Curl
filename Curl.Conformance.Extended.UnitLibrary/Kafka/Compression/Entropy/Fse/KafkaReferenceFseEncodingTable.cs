using System.Numerics;

namespace Curl.Conformance.Extended.Kafka.Compression.Entropy.Fse;

/// <summary>
/// A zstd FSE table (RFC 8878 section 4.1) built for encoding: the decoder's states, spread from the normalised counts
/// as section 4.1.1 lays them out, each with its symbol, the bits its next state reads and the baseline they are added
/// to. Encoding a symbol before a known next state picks the state of that symbol whose range holds the next state
/// and writes the offset into the range, which is what the decoder reads back. Also holds the predefined literals
/// length, match length and offset distributions (section 3.1.1.3.2.2).
/// </summary>
public sealed class KafkaReferenceFseEncodingTable
{
    private static readonly short[] LiteralsLengthCounts =
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

    private readonly short[] counts;

    private readonly byte[] symbols;

    private readonly int[] baselines;

    private readonly byte[] bitCounts;

    /// <summary>Builds the table for <paramref name="normalizedCounts"/>.</summary>
    /// <param name="normalizedCounts">One count per symbol, -1 for "less than one".</param>
    /// <param name="accuracyLog">Their accuracy log.</param>
    public KafkaReferenceFseEncodingTable(IReadOnlyList<short> normalizedCounts, int accuracyLog)
    {
        KafkaReferenceFseNormalizedCounts.CheckValid(normalizedCounts, accuracyLog);
        counts = [.. normalizedCounts];
        AccuracyLog = accuracyLog;
        int size = 1 << accuracyLog;
        symbols = new byte[size];
        int[] nextStateNumbers = new int[counts.Length];
        int lastCell = PlaceLowProbabilitySymbols(nextStateNumbers);
        SpreadSymbols(size, lastCell);
        baselines = new int[size];
        bitCounts = new byte[size];
        for (int state = 0; state < size; state++)
        {
            int nextStateNumber = nextStateNumbers[symbols[state]]++;
            int bits = accuracyLog - BitOperations.Log2((uint)nextStateNumber);
            bitCounts[state] = (byte)bits;
            baselines[state] = (nextStateNumber << bits) - size;
        }
    }

    /// <summary>The predefined literals length distribution (accuracy log 6).</summary>
    public static KafkaReferenceFseEncodingTable PredefinedLiteralsLength { get; } = new(LiteralsLengthCounts, 6);

    /// <summary>The predefined match length distribution (accuracy log 6).</summary>
    public static KafkaReferenceFseEncodingTable PredefinedMatchLength { get; } = new(MatchLengthCounts, 6);

    /// <summary>The predefined offset distribution (accuracy log 5).</summary>
    public static KafkaReferenceFseEncodingTable PredefinedOffset { get; } = new(OffsetCounts, 5);

    /// <summary>The bits a state takes; the table has two to the power of this many states.</summary>
    public int AccuracyLog { get; }

    /// <summary>The normalised counts the table was built from.</summary>
    public IReadOnlyList<short> NormalizedCounts => counts;

    /// <summary>The symbol <paramref name="state"/> decodes.</summary>
    /// <param name="state">A state, below two to the power of <see cref="AccuracyLog"/>.</param>
    /// <returns>Its symbol.</returns>
    public byte Symbol(int state) => symbols[state];

    /// <summary>The first state that decodes <paramref name="symbol"/>: the state a stream's last symbol starts from.</summary>
    /// <param name="symbol">The symbol.</param>
    /// <returns>The state.</returns>
    public int InitialState(byte symbol)
    {
        int state = Array.IndexOf(symbols, symbol);
        return state >= 0 ? state : throw new ArgumentException("FSE table has no state for symbol " + symbol, nameof(symbol));
    }

    /// <summary>
    /// Encodes <paramref name="symbol"/> ahead of <paramref name="nextState"/>: writes the bits the decoder reads to get
    /// from the symbol's state to the next one, and gives that state.
    /// </summary>
    /// <param name="nextState">The state of the symbol that follows.</param>
    /// <param name="symbol">The symbol.</param>
    /// <param name="writer">The stream the bits go to.</param>
    /// <returns>The state that decodes <paramref name="symbol"/>.</returns>
    public int Encode(int nextState, byte symbol, KafkaReferenceFseBackwardBitWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        for (int state = 0; state < symbols.Length; state++)
        {
            int offset = nextState - baselines[state];
            if (symbols[state] == symbol && offset >= 0 && offset < 1 << bitCounts[state])
            {
                writer.WriteBits(offset, bitCounts[state]);
                return state;
            }
        }

        throw new ArgumentException("FSE table has no state for symbol " + symbol + " before state " + nextState, nameof(symbol));
    }

    // Puts each "less than one" symbol in a cell from the end of the table down; gives the last cell left.
    private int PlaceLowProbabilitySymbols(int[] nextStateNumbers)
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

    private void SpreadSymbols(int size, int lastCell)
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
}
