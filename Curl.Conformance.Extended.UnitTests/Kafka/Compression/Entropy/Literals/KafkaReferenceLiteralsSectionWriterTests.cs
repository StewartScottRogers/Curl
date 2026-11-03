using Curl.Conformance.Extended.Kafka.Compression.Entropy.Huffman;

namespace Curl.Conformance.Extended.UnitTests.Kafka.Compression.Entropy.Literals;

[TestClass]
public sealed class KafkaReferenceLiteralsSectionWriterTests
{
    private static readonly KafkaReferenceHuffmanTree Tree = new([1, 1, 2]);

    [TestMethod]
    public void Compressed_OneStream_WritesHeaderTreeAndStream()
    {
        byte[] section = KafkaReferenceLiteralsSectionWriter.Compressed([129, 0x11], Tree, [0, 1, 2], fourStreams: false);

        // Type 2, size format 0, 3 literals, compressed size 3 (two description bytes and one stream byte).
        CollectionAssert.AreEqual(new byte[] { 0x32, 0xC0, 0x00, 129, 0x11, 0x23 }, section);
    }

    [TestMethod]
    public void Treeless_FourStreams_WritesHeaderJumpTableAndQuarterStreams()
    {
        byte[] literals = [0, 1, 2, 2, 2];

        byte[] section = KafkaReferenceLiteralsSectionWriter.Treeless(Tree, literals, fourStreams: true);

        byte[][] streams = [Tree.Encode([0, 1]), Tree.Encode([2, 2]), Tree.Encode([2]), Tree.Encode([])];
        int compressedSize = 6 + streams.Sum(stream => stream.Length);
        int header = 3 | (1 << 2) | (5 << 4) | (compressedSize << 14);
        byte[] expected =
        [
            (byte)header, (byte)(header >> 8), (byte)(header >> 16),
            (byte)streams[0].Length, 0, (byte)streams[1].Length, 0, (byte)streams[2].Length, 0,
            .. streams.SelectMany(stream => stream),
        ];
        CollectionAssert.AreEqual(expected, section);
    }

    [TestMethod]
    public void Compressed_MoreThan1023Literals_Throws() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => KafkaReferenceLiteralsSectionWriter.Compressed([], Tree, new byte[1024], fourStreams: false));

    [TestMethod]
    public void Compressed_CompressedSizeAbove1023_Throws() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => KafkaReferenceLiteralsSectionWriter.Compressed(new byte[1024], Tree, [0], fourStreams: false));

    [TestMethod]
    public void Compressed_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => KafkaReferenceLiteralsSectionWriter.Compressed(null!, Tree, [0], fourStreams: false));
        Assert.ThrowsExactly<ArgumentNullException>(() => KafkaReferenceLiteralsSectionWriter.Treeless(null!, [0], fourStreams: false));
        Assert.ThrowsExactly<ArgumentNullException>(() => KafkaReferenceLiteralsSectionWriter.Treeless(Tree, null!, fourStreams: false));
    }

    [TestMethod]
    public void BlockOfLiteralsOnly_Section_EndsWithZeroSequences() =>
        CollectionAssert.AreEqual(new byte[] { 7, 8, 0 }, KafkaReferenceLiteralsSectionWriter.BlockOfLiteralsOnly([7, 8]));

    [TestMethod]
    public void BlockOfLiteralsOnly_Null_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => KafkaReferenceLiteralsSectionWriter.BlockOfLiteralsOnly(null!));
}
