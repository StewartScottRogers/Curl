using System.Numerics;
using Curl.Conformance.Extended.Kafka.Compression.Entropy.Fse;

namespace Curl.Conformance.Extended.Kafka.Compression.Entropy.Huffman;

/// <summary>
/// A zstd Huffman tree (RFC 8878 section 4.2) given by one weight per symbol, 0 for a symbol that never occurs. A
/// symbol of weight <c>w</c> takes two to the power of <c>w - 1</c> of the table's cells, which add up to two to the
/// power of <see cref="TableLog"/>, and its code is <c>TableLog + 1 - w</c> bits long. Codes are laid out as the
/// decoder fills its table (section 4.2.1.3): weight 1 first, symbols in order within a weight. Encodes literals into
/// a backward bit stream (section 4.2.2), the first literal read first.
/// </summary>
public sealed class KafkaReferenceHuffmanTree
{
    /// <summary>The largest weight, and the longest code in bits, zstd allows.</summary>
    public const int MaximumWeight = 11;

    private readonly byte[] weights;

    private readonly int[] codes;

    private readonly byte[] codeLengths;

    /// <summary>Builds the tree for <paramref name="symbolWeights"/>.</summary>
    /// <param name="symbolWeights">
    /// One weight per symbol, indexed by symbol; trailing zeros are dropped, and the last symbol of non-zero weight is
    /// the one a tree description leaves implied. The cells must add up to a power of two.
    /// </param>
    public KafkaReferenceHuffmanTree(IReadOnlyList<byte> symbolWeights)
    {
        ArgumentNullException.ThrowIfNull(symbolWeights);
        int length = symbolWeights.Count;
        while (length > 0 && symbolWeights[length - 1] == 0)
        {
            length--;
        }

        weights = [.. symbolWeights.Take(length)];
        TableLog = CheckWeights(weights);
        codes = new int[weights.Length];
        codeLengths = new byte[weights.Length];
        int cell = 0;
        for (int weight = 1; weight <= TableLog; weight++)
        {
            for (int symbol = 0; symbol < weights.Length; symbol++)
            {
                cell = weights[symbol] == weight ? AssignCode(symbol, weight, cell) : cell;
            }
        }
    }

    /// <summary>The longest code in bits; the cells add up to two to the power of this.</summary>
    public int TableLog { get; }

    /// <summary>The weights a tree description carries: every symbol's but the last, whose weight is implied.</summary>
    public IReadOnlyList<byte> ExplicitWeights => weights[..^1];

    /// <summary>Encodes <paramref name="literals"/> into one Huffman stream, marker bit included.</summary>
    /// <param name="literals">The literals, each a symbol of non-zero weight.</param>
    /// <returns>The stream.</returns>
    public byte[] Encode(IReadOnlyList<byte> literals)
    {
        ArgumentNullException.ThrowIfNull(literals);
        KafkaReferenceFseBackwardBitWriter writer = new();
        for (int index = literals.Count - 1; index >= 0; index--)
        {
            byte literal = literals[index];
            if (literal >= weights.Length || weights[literal] == 0)
            {
                throw new ArgumentException("Huffman tree has no code for literal " + literal, nameof(literals));
            }

            writer.WriteBits(codes[literal], codeLengths[literal]);
        }

        return writer.ToArray();
    }

    // Gives the table log the weights make, or throws unless they are 0 to 11, two or more are non-zero and their cells add up to a power of two.
    private static int CheckWeights(byte[] weights)
    {
        int cells = weights.Sum(CellsOf);
        int present = weights.Count(weight => weight != 0);
        if (present < 2 || !BitOperations.IsPow2(cells) || cells > 1 << MaximumWeight)
        {
            throw new ArgumentException("Huffman weights need two or more symbols whose cells add up to a power of two of at most " + (1 << MaximumWeight) + ", not " + cells + " over " + present, nameof(weights));
        }

        return BitOperations.Log2((uint)cells);
    }

    // The cells a symbol of this weight takes; throws above the maximum weight.
    private static int CellsOf(byte weight)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(weight, (byte)MaximumWeight);
        return weight == 0 ? 0 : 1 << (weight - 1);
    }

    // The symbol's cells start at cell; its code is their common prefix. Gives the cell after them.
    private int AssignCode(int symbol, int weight, int cell)
    {
        codes[symbol] = cell >> (weight - 1);
        codeLengths[symbol] = (byte)(TableLog + 1 - weight);
        return cell + (1 << (weight - 1));
    }
}
