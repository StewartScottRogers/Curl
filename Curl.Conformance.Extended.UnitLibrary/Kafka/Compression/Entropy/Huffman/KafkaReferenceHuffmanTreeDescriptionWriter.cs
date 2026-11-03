using Curl.Conformance.Extended.Kafka.Compression.Entropy.Fse;

namespace Curl.Conformance.Extended.Kafka.Compression.Entropy.Huffman;

/// <summary>
/// Writes a zstd Huffman tree description (RFC 8878 section 4.2.1): a header byte, then the explicit weights, either
/// directly (header 127 plus their number, two four-bit weights a byte, the first in the high half) or FSE-compressed
/// (header the compressed size, below 128: an FSE table description, then the weights on two interleaved states).
/// </summary>
public static class KafkaReferenceHuffmanTreeDescriptionWriter
{
    /// <summary>The most explicit weights a direct description holds.</summary>
    public const int MaximumDirectWeights = 128;

    /// <summary>The largest accuracy log FSE-compressed weights may use.</summary>
    public const int MaximumWeightsAccuracyLog = 6;

    private const int DirectHeaderBase = 127;

    private const int FirstDirectHeader = 128;

    /// <summary>Writes <paramref name="tree"/>'s explicit weights directly.</summary>
    /// <param name="tree">The tree.</param>
    /// <returns>The description's bytes.</returns>
    public static byte[] WriteDirect(KafkaReferenceHuffmanTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        IReadOnlyList<byte> weights = tree.ExplicitWeights;
        ArgumentOutOfRangeException.ThrowIfGreaterThan(weights.Count, MaximumDirectWeights, nameof(tree));
        byte[] description = new byte[1 + ((weights.Count + 1) / 2)];
        description[0] = (byte)(DirectHeaderBase + weights.Count);
        for (int index = 0; index < weights.Count; index++)
        {
            description[1 + (index / 2)] |= (byte)(index % 2 == 0 ? weights[index] << 4 : weights[index]);
        }

        return description;
    }

    /// <summary>Writes <paramref name="tree"/>'s explicit weights FSE-compressed, counts normalised from how often each weight occurs.</summary>
    /// <param name="tree">The tree; it needs two or more explicit weights.</param>
    /// <param name="accuracyLog">The FSE accuracy log, 5 or 6.</param>
    /// <returns>The description's bytes.</returns>
    public static byte[] WriteFseCompressed(KafkaReferenceHuffmanTree tree, int accuracyLog)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(accuracyLog, MaximumWeightsAccuracyLog);
        IReadOnlyList<byte> weights = tree.ExplicitWeights;
        int[] frequencies = new int[weights.Max() + 1];
        foreach (byte weight in weights)
        {
            frequencies[weight]++;
        }

        short[] counts = KafkaReferenceFseNormalizedCounts.FromFrequencies(frequencies, accuracyLog);
        byte[] compressed =
        [
            .. KafkaReferenceFseTableDescriptionWriter.Write(counts, accuracyLog),
            .. KafkaReferenceFseEncoder.Encode(new KafkaReferenceFseEncodingTable(counts, accuracyLog), weights, 2),
        ];
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(compressed.Length, FirstDirectHeader, nameof(tree));
        return [(byte)compressed.Length, .. compressed];
    }
}
