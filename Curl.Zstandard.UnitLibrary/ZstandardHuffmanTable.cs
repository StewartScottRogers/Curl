using System.Numerics;

namespace Curl.Zstandard;

/// <summary>
/// A Huffman decoding table for literals (RFC 8878 section 4.2): read from a Huffman tree
/// description, indexed by the next <see cref="MaxNumberOfBits" /> bits of a stream.
/// </summary>
internal sealed class ZstandardHuffmanTable
{
    /// <summary>RFC 8878 section 4.2.1: no Huffman code is longer than 11 bits.</summary>
    private const int MaxCodeLength = 11;

    /// <summary>RFC 8878 section 4.2.1.2: FSE-compressed weights use an <c>Accuracy_Log</c> of at most 6.</summary>
    private const int MaxWeightAccuracyLog = 6;

    /// <summary>A weight is at most <see cref="MaxCodeLength" />.</summary>
    private const int MaxWeight = MaxCodeLength;

    /// <summary>At most 255 weights are coded; the 256th symbol's weight is implied.</summary>
    private const int MaxCodedWeights = 255;

    /// <summary>A <c>headerByte</c> of 128 or more codes <c>headerByte - 127</c> weights directly, four bits each.</summary>
    private const int DirectWeightsHeader = 128;

    private readonly byte[] symbols;

    private readonly byte[] numberOfBits;

    private ZstandardHuffmanTable(int maxNumberOfBits, byte[] symbols, byte[] numberOfBits)
    {
        MaxNumberOfBits = maxNumberOfBits;
        this.symbols = symbols;
        this.numberOfBits = numberOfBits;
    }

    /// <summary>The longest code, in bits: the table has <c>2^MaxNumberOfBits</c> entries.</summary>
    public int MaxNumberOfBits { get; }

    /// <summary>
    /// Reads a Huffman tree description (RFC 8878 section 4.2.1) from the start of
    /// <paramref name="source" />; <see langword="null" /> when it runs past the end of
    /// <paramref name="source" /> or its weights do not form a complete tree of codes at most
    /// 11 bits long.
    /// </summary>
    public static ZstandardHuffmanTable? Read(ReadOnlySpan<byte> source, out int bytesRead)
    {
        bytesRead = 0;
        if (source.IsEmpty)
        {
            return null;
        }

        var header = source[0];
        bytesRead = 1 + (header < DirectWeightsHeader ? header : (header - DirectWeightsHeader + 2) / 2);
        if (bytesRead > source.Length)
        {
            return null;
        }

        var description = source[1..bytesRead];
        var weights = header < DirectWeightsHeader ? ReadFseWeights(description) : ReadDirectWeights(description, header - DirectWeightsHeader + 1);
        return weights is null ? null : FromWeights(weights);
    }

    /// <summary>
    /// Decodes one Huffman stream (RFC 8878 section 4.2.2) into all of
    /// <paramref name="destination" />; <see langword="false" /> when the stream has no
    /// padding bit or does not end exactly where the last symbol does.
    /// </summary>
    public bool TryDecodeStream(ReadOnlySpan<byte> stream, Span<byte> destination)
    {
        if (!ZstandardBackwardBitReader.TryCreate(stream, out var bits))
        {
            return false;
        }

        var mask = (1 << MaxNumberOfBits) - 1;
        var state = (int)bits.ReadBits(MaxNumberOfBits);
        for (var index = 0; index < destination.Length; index++)
        {
            destination[index] = symbols[state];
            var length = numberOfBits[state];
            state = ((state << length) & mask) | (int)bits.ReadBits(length);
        }

        return bits.BitsRemaining == -MaxNumberOfBits;
    }

    /// <summary>Weights packed two to a byte, the first in the high nibble.</summary>
    private static byte[] ReadDirectWeights(ReadOnlySpan<byte> packed, int count)
    {
        var weights = new byte[count];
        for (var index = 0; index < count; index++)
        {
            weights[index] = (byte)((index & 1) == 0 ? packed[index >> 1] >> 4 : packed[index >> 1] & 0x0F);
        }

        return weights;
    }

    /// <summary>
    /// Weights coded with FSE (RFC 8878 section 4.2.1.2): a table description, then one
    /// backward stream read by two interleaved states until it overflows.
    /// </summary>
    private static byte[]? ReadFseWeights(ReadOnlySpan<byte> description)
    {
        var table = ZstandardFseTable.Read(description, MaxWeightAccuracyLog, MaxWeight, out var tableLength);
        if (table is null || !ZstandardBackwardBitReader.TryCreate(description[tableLength..], out var bits))
        {
            return null;
        }

        var weights = new byte[MaxCodedWeights];
        var count = 0;
        var current = table.ReadInitialState(ref bits);
        var other = table.ReadInitialState(ref bits);
        while (count < MaxCodedWeights - 1)
        {
            weights[count++] = table.Symbol(current);
            current = table.ReadNextState(current, ref bits);
            if (bits.IsOverflowed)
            {
                weights[count++] = table.Symbol(other);
                return weights[..count];
            }

            (current, other) = (other, current);
        }

        return null;
    }

    /// <summary>
    /// Builds the table from the coded weights (RFC 8878 section 4.2.1.3), completing them
    /// with the last symbol's implied weight; <see langword="null" /> when no weight is set,
    /// the codes would be longer than 11 bits, no weight completes the tree, or the longest
    /// codes are not paired.
    /// </summary>
    private static ZstandardHuffmanTable? FromWeights(byte[] weights)
    {
        var total = weights.Sum(weight => (1 << weight) >> 1);
        if (total == 0)
        {
            return null;
        }

        var maxNumberOfBits = BitOperations.Log2((uint)total) + 1;
        var leftover = (1 << maxNumberOfBits) - total;
        if (maxNumberOfBits > MaxCodeLength || !BitOperations.IsPow2(leftover))
        {
            return null;
        }

        byte[] allWeights = [.. weights, (byte)(BitOperations.Log2((uint)leftover) + 1)];
        return HasPairedLongestCodes(allWeights) ? Build(allWeights, maxNumberOfBits) : null;
    }

    /// <summary>
    /// Whether at least two symbols have weight 1, the longest codes, as libzstd's
    /// <c>HUF_readStats</c> requires (it refuses any other tree as <c>corruption_detected</c>).
    /// Their number is always even here: the weights of a complete tree sum to a power of 2
    /// of at least 2, and every other weight adds an even amount.
    /// </summary>
    private static bool HasPairedLongestCodes(byte[] weights) => weights.Count(weight => weight == 1) >= 2;

    /// <summary>
    /// Gives each symbol the table entries its code prefixes: the longest codes take the
    /// lowest entries, and within one length the lower symbol comes first.
    /// </summary>
    private static ZstandardHuffmanTable Build(byte[] weights, int maxNumberOfBits)
    {
        var rankStarts = new int[maxNumberOfBits + 1];
        foreach (var weight in weights)
        {
            rankStarts[weight] += (1 << weight) >> 1;
        }

        var start = 0;
        for (var weight = 1; weight <= maxNumberOfBits; weight++)
        {
            (rankStarts[weight], start) = (start, start + rankStarts[weight]);
        }

        var size = 1 << maxNumberOfBits;
        var symbols = new byte[size];
        var numberOfBits = new byte[size];
        for (var symbol = 0; symbol < weights.Length; symbol++)
        {
            var weight = weights[symbol];
            if (weight > 0)
            {
                var span = 1 << (weight - 1);
                symbols.AsSpan(rankStarts[weight], span).Fill((byte)symbol);
                numberOfBits.AsSpan(rankStarts[weight], span).Fill((byte)(maxNumberOfBits + 1 - weight));
                rankStarts[weight] += span;
            }
        }

        return new ZstandardHuffmanTable(maxNumberOfBits, symbols, numberOfBits);
    }
}
