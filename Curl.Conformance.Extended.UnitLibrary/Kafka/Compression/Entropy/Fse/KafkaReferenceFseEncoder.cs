namespace Curl.Conformance.Extended.Kafka.Compression.Entropy.Fse;

/// <summary>
/// Encodes a run of symbols with one FSE table into a backward bit stream (RFC 8878 section 4.1) as zstd's decoder
/// reads it: one state, or two interleaved states (symbol <c>i</c> on state <c>i % 2</c>, as Huffman weights use,
/// section 4.2.1.2). The decoder reads the initial states, first state first, then after each symbol the bits that
/// update its state; the last symbol of each state is where that state started, so it reads nothing after it.
/// </summary>
public static class KafkaReferenceFseEncoder
{
    /// <summary>Encodes <paramref name="symbols"/> on <paramref name="stateCount"/> interleaved states.</summary>
    /// <param name="table">The table.</param>
    /// <param name="symbols">The symbols, at least <paramref name="stateCount"/> of them.</param>
    /// <param name="stateCount">1 or 2.</param>
    /// <returns>The bit stream, marker bit included.</returns>
    public static byte[] Encode(KafkaReferenceFseEncodingTable table, IReadOnlyList<byte> symbols, int stateCount)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(symbols);
        ArgumentOutOfRangeException.ThrowIfLessThan(stateCount, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(stateCount, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(symbols.Count, stateCount, nameof(symbols));
        KafkaReferenceFseBackwardBitWriter writer = new();
        int[] states = new int[stateCount];
        for (int index = symbols.Count - stateCount; index < symbols.Count; index++)
        {
            states[index % stateCount] = table.InitialState(symbols[index]);
        }

        for (int index = symbols.Count - stateCount - 1; index >= 0; index--)
        {
            states[index % stateCount] = table.Encode(states[index % stateCount], symbols[index], writer);
        }

        for (int state = stateCount - 1; state >= 0; state--)
        {
            writer.WriteBits(states[state], table.AccuracyLog);
        }

        return writer.ToArray();
    }
}
