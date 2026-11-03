using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Kafka.Compression;
using Curl.Conformance.Extended.Kafka.Compression.Entropy.Huffman;
using static Curl.Conformance.Extended.UnitTests.Kafka.Compression.KafkaCompressionCases;
using static Curl.Conformance.Extended.UnitTests.Kafka.KafkaCases;

namespace Curl.Conformance.Extended.UnitTests.Kafka.Compression.Entropy.Literals;

/// <summary>
/// Fetch of zstd frames whose compressed blocks carry Huffman-coded literals (RFC 8878 section 3.1.1.3.1): a tree
/// described by direct and by FSE-compressed weights, one stream and four, and treeless literals reusing the tree of
/// the frame's previous block. Each block has no sequences, so it decodes to its literals.
/// </summary>
[TestClass]
public sealed class KafkaZstdHuffmanLiteralsTests
{
    private const string OneStream = "Kafka:RFC8878-3.1.1.3.1-2";

    private const string FourStreams = "Kafka:RFC8878-3.1.1.3.1-3";

    private const string FseCompressedWeights = "Kafka:RFC8878-3.1.1.3.1-4";

    private const string Treeless = "Kafka:RFC8878-3.1.1.3.1-5";

    private const string TreelessWithNoTree = "Kafka:RFC8878-3.1.1.3.1-6";

    /// <summary>' ' and 'd' weight 1, 'b' 2, 'n' 3 (implied), 'a' 4: 16 cells, codes of 1 to 4 bits.</summary>
    internal static KafkaReferenceHuffmanTree BananaTree()
    {
        byte[] weights = new byte['n' + 1];
        (weights[' '], weights['a'], weights['b'], weights['d'], weights['n']) = (1, 4, 2, 1, 3);
        return new KafkaReferenceHuffmanTree(weights);
    }

    /// <summary>A frame of a raw block before <paramref name="value"/>, <paramref name="middle"/> for it, and a raw block after it.</summary>
    private static byte[] AroundValue(byte[] records, byte[] value, Func<KafkaReferenceZstdFrameWriter, KafkaReferenceZstdFrameWriter> middle)
    {
        int start = records.AsSpan().IndexOf(value);
        return middle(new KafkaReferenceZstdFrameWriter().Raw(records[..start])).Raw(records[(start + value.Length)..]).ToArray();
    }

    private static byte[] Block(byte[] literalsSection) => KafkaReferenceLiteralsSectionWriter.BlockOfLiteralsOnly(literalsSection);

    [TestMethod]
    [SpecRequirement(OneStream)]
    public Task Fetch_HuffmanLiteralsInOneStreamWithDirectWeights_WritesTheRecord() =>
        Known.RunCaseAsync([OneStream], async () =>
        {
            KafkaReferenceHuffmanTree tree = BananaTree();
            byte[] value = Ascii("a banana bandana");
            byte[] block = Block(KafkaReferenceLiteralsSectionWriter.Compressed(KafkaReferenceHuffmanTreeDescriptionWriter.WriteDirect(tree), tree, value, fourStreams: false));

            AssertDelivered(await FetchAsync(KafkaCompressedRecordBatch.Zstd, records => AroundValue(records, value, frame => frame.Compressed(block, value)), value), value);
        });

    [TestMethod]
    [SpecRequirement(FourStreams)]
    public Task Fetch_HuffmanLiteralsInFourStreams_WritesTheRecord() =>
        Known.RunCaseAsync([FourStreams], async () =>
        {
            KafkaReferenceHuffmanTree tree = BananaTree();
            byte[] value = Ascii("a banana and a bandana and a bad nan");
            byte[] block = Block(KafkaReferenceLiteralsSectionWriter.Compressed(KafkaReferenceHuffmanTreeDescriptionWriter.WriteDirect(tree), tree, value, fourStreams: true));

            AssertDelivered(await FetchAsync(KafkaCompressedRecordBatch.Zstd, records => AroundValue(records, value, frame => frame.Compressed(block, value)), value), value);
        });

    [TestMethod]
    [SpecRequirement(FseCompressedWeights)]
    public Task Fetch_HuffmanLiteralsWithFseCompressedWeights_WritesTheRecord() =>
        Known.RunCaseAsync([FseCompressedWeights], async () =>
        {
            KafkaReferenceHuffmanTree tree = BananaTree();
            byte[] value = Ascii("banana bandana");
            byte[] block = Block(KafkaReferenceLiteralsSectionWriter.Compressed(KafkaReferenceHuffmanTreeDescriptionWriter.WriteFseCompressed(tree, 6), tree, value, fourStreams: false));

            AssertDelivered(await FetchAsync(KafkaCompressedRecordBatch.Zstd, records => AroundValue(records, value, frame => frame.Compressed(block, value)), value), value);
        });

    [TestMethod]
    [SpecRequirement(Treeless)]
    public Task Fetch_TreelessLiteralsReusingThePreviousBlocksTree_WritesTheRecord() =>
        Known.RunCaseAsync([Treeless], async () =>
        {
            KafkaReferenceHuffmanTree tree = BananaTree();
            byte[] first = Ascii("a banana ");
            byte[] second = Ascii("and a bad bandana");
            byte[] value = [.. first, .. second];
            byte[] described = Block(KafkaReferenceLiteralsSectionWriter.Compressed(KafkaReferenceHuffmanTreeDescriptionWriter.WriteDirect(tree), tree, first, fourStreams: false));
            byte[] treeless = Block(KafkaReferenceLiteralsSectionWriter.Treeless(tree, second, fourStreams: true));

            AssertDelivered(await FetchAsync(KafkaCompressedRecordBatch.Zstd, records => AroundValue(records, value, frame => frame.Compressed(described, first).Compressed(treeless, second)), value), value);
        });

    [TestMethod]
    [SpecRequirement(TreelessWithNoTree)]
    public Task Fetch_TreelessLiteralsWithNoEarlierTree_FailsWithWeirdServerReply() =>
        Known.RunCaseAsync([TreelessWithNoTree], async () =>
        {
            byte[] value = Ascii("no tree yet");
            byte[] block = Block(KafkaReferenceLiteralsSectionWriter.Treeless(BananaTree(), Ascii("banana"), fourStreams: false));

            AssertCorrupt(await FetchAsync(KafkaCompressedRecordBatch.Zstd, records => AroundValue(records, value, frame => frame.Compressed(block, Ascii("banana"))), value), "zstd");
        });
}
