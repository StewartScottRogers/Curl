using System.Buffers.Binary;

namespace Curl.Conformance.Extended.Kafka.Compression.Entropy.Huffman;

/// <summary>
/// Writes the Huffman-coded literals section of a zstd compressed block (RFC 8878 section 3.1.1.3.1): a three-byte
/// header (the block type, the size format, the regenerated and compressed sizes, each below 1024), then for a
/// Compressed_Literals_Block the tree description, and the literals in one stream or in four behind a six-byte jump
/// table. A Treeless_Literals_Block has no tree description: it reuses the previous Huffman tree of the frame.
/// </summary>
public static class KafkaReferenceLiteralsSectionWriter
{
    /// <summary>The largest regenerated or compressed size a three-byte header holds.</summary>
    public const int MaximumThreeByteHeaderSize = 1023;

    private const int CompressedLiteralsBlock = 2;

    private const int TreelessLiteralsBlock = 3;

    private const int OneStreamSizeFormat = 0;

    private const int FourStreamsSizeFormat = 1;

    private const int StreamCount = 4;

    /// <summary>A Compressed_Literals_Block: <paramref name="treeDescription"/>, then <paramref name="literals"/> coded with <paramref name="tree"/>.</summary>
    /// <param name="treeDescription">The tree's description, as <see cref="KafkaReferenceHuffmanTreeDescriptionWriter"/> writes it.</param>
    /// <param name="tree">The tree the description describes.</param>
    /// <param name="literals">The literals.</param>
    /// <param name="fourStreams">Whether the literals go in four streams rather than one.</param>
    /// <returns>The section's bytes.</returns>
    public static byte[] Compressed(byte[] treeDescription, KafkaReferenceHuffmanTree tree, byte[] literals, bool fourStreams)
    {
        ArgumentNullException.ThrowIfNull(treeDescription);
        return Section(CompressedLiteralsBlock, treeDescription, tree, literals, fourStreams);
    }

    /// <summary>A Treeless_Literals_Block: <paramref name="literals"/> coded with <paramref name="tree"/>, the frame's previous tree.</summary>
    /// <param name="tree">The previous Huffman tree of the frame.</param>
    /// <param name="literals">The literals.</param>
    /// <param name="fourStreams">Whether the literals go in four streams rather than one.</param>
    /// <returns>The section's bytes.</returns>
    public static byte[] Treeless(KafkaReferenceHuffmanTree tree, byte[] literals, bool fourStreams) =>
        Section(TreelessLiteralsBlock, [], tree, literals, fourStreams);

    /// <summary>A compressed block's content of <paramref name="literalsSection"/> and a sequences section of no sequences, so it decodes to the literals.</summary>
    /// <param name="literalsSection">The literals section.</param>
    /// <returns>The block's content.</returns>
    public static byte[] BlockOfLiteralsOnly(byte[] literalsSection)
    {
        ArgumentNullException.ThrowIfNull(literalsSection);
        return [.. literalsSection, 0];
    }

    private static byte[] Section(int type, byte[] treeDescription, KafkaReferenceHuffmanTree tree, byte[] literals, bool fourStreams)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(literals);
        byte[] streams = fourStreams ? FourStreams(tree, literals) : tree.Encode(literals);
        int compressedSize = treeDescription.Length + streams.Length;
        ArgumentOutOfRangeException.ThrowIfGreaterThan(literals.Length, MaximumThreeByteHeaderSize, nameof(literals));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(compressedSize, MaximumThreeByteHeaderSize, nameof(literals));
        int header = type | ((fourStreams ? FourStreamsSizeFormat : OneStreamSizeFormat) << 2) | (literals.Length << 4) | (compressedSize << 14);
        return [(byte)header, (byte)(header >> 8), (byte)(header >> 16), .. treeDescription, .. streams];
    }

    // The jump table (the sizes of the first three streams, two bytes each) and the four streams; the first three
    // each code a quarter of the literals rounded up, the fourth the rest.
    private static byte[] FourStreams(KafkaReferenceHuffmanTree tree, byte[] literals)
    {
        int share = (literals.Length + StreamCount - 1) / StreamCount;
        List<byte> jumpTable = [];
        List<byte> streams = [];
        for (int stream = 0; stream < StreamCount; stream++)
        {
            int start = Math.Min(stream * share, literals.Length);
            int end = stream == StreamCount - 1 ? literals.Length : Math.Min(start + share, literals.Length);
            byte[] encoded = tree.Encode(literals[start..end]);
            streams.AddRange(encoded);
            if (stream < StreamCount - 1)
            {
                byte[] size = new byte[sizeof(ushort)];
                BinaryPrimitives.WriteUInt16LittleEndian(size, (ushort)encoded.Length);
                jumpTable.AddRange(size);
            }
        }

        return [.. jumpTable, .. streams];
    }
}
