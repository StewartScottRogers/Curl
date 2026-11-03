using Curl.Conformance.Extended.Kafka.Compression.Entropy.Huffman;

namespace Curl.Conformance.Extended.UnitTests.Kafka.Compression.Entropy.Literals;

[TestClass]
public sealed class KafkaReferenceHuffmanTreeDescriptionWriterTests
{
    [TestMethod]
    public void WriteDirect_ThreeExplicitWeights_PacksTwoAByteHighHalfFirst()
    {
        byte[] description = KafkaReferenceHuffmanTreeDescriptionWriter.WriteDirect(new KafkaReferenceHuffmanTree([1, 1, 2, 3]));

        CollectionAssert.AreEqual(new byte[] { 130, 0x11, 0x20 }, description);
    }

    [TestMethod]
    public void WriteDirect_MoreThan128ExplicitWeights_Throws() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            KafkaReferenceHuffmanTreeDescriptionWriter.WriteDirect(new KafkaReferenceHuffmanTree([.. Enumerable.Repeat((byte)1, 256)])));

    [TestMethod]
    public void WriteDirect_Null_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => KafkaReferenceHuffmanTreeDescriptionWriter.WriteDirect(null!));

    [TestMethod]
    public void WriteFseCompressed_BananaTree_HeaderIsTheCompressedSize()
    {
        byte[] description = KafkaReferenceHuffmanTreeDescriptionWriter.WriteFseCompressed(KafkaZstdHuffmanLiteralsTests.BananaTree(), 6);

        Assert.AreEqual(description.Length - 1, description[0]);
    }

    [TestMethod]
    public void WriteFseCompressed_AccuracyLogAboveSix_Throws() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            KafkaReferenceHuffmanTreeDescriptionWriter.WriteFseCompressed(KafkaZstdHuffmanLiteralsTests.BananaTree(), 7));

    [TestMethod]
    public void WriteFseCompressed_OneExplicitWeight_ThrowsAsTwoStatesNeedTwo() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            KafkaReferenceHuffmanTreeDescriptionWriter.WriteFseCompressed(new KafkaReferenceHuffmanTree([1, 1]), 6));

    [TestMethod]
    public void WriteFseCompressed_Null_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => KafkaReferenceHuffmanTreeDescriptionWriter.WriteFseCompressed(null!, 6));
}
