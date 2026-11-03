using Curl.Conformance.Extended.Kafka.Compression.Entropy.Huffman;

namespace Curl.Conformance.Extended.UnitTests.Kafka.Compression.Entropy.Literals;

[TestClass]
public sealed class KafkaReferenceHuffmanTreeTests
{
    [TestMethod]
    public void Constructor_TrailingZeroWeights_AreDroppedAndTheLastNonZeroIsImplied()
    {
        KafkaReferenceHuffmanTree tree = new([1, 1, 0, 0]);

        CollectionAssert.AreEqual(new byte[] { 1 }, tree.ExplicitWeights.ToArray());
        Assert.AreEqual(1, tree.TableLog);
    }

    [TestMethod]
    public void Constructor_Null_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaReferenceHuffmanTree(null!));

    [TestMethod]
    public void Constructor_WeightAboveEleven_Throws() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new KafkaReferenceHuffmanTree([12, 12]));

    [TestMethod]
    [DataRow(new byte[] { 0, 0 })]
    [DataRow(new byte[] { 0, 1 })]
    [DataRow(new byte[] { 1, 1, 1 })]
    [DataRow(new byte[] { 11, 11, 11, 11 })]
    public void Constructor_NotTwoSymbolsOrCellsNotAPowerOfTwoUpTo2048_Throws(byte[] weights) =>
        Assert.ThrowsExactly<ArgumentException>(() => new KafkaReferenceHuffmanTree(weights));

    [TestMethod]
    public void Encode_ThreeSymbols_WritesCanonicalCodesLastLiteralFirst()
    {
        // Cells: symbol 0 at 0 (code 00), symbol 1 at 1 (code 01), symbol 2 at 2 and 3 (code 1).
        KafkaReferenceHuffmanTree tree = new([1, 1, 2]);

        byte[] stream = tree.Encode([0, 1, 2]);

        Assert.AreEqual(2, tree.TableLog);
        CollectionAssert.AreEqual(new byte[] { 0x23 }, stream);
    }

    [TestMethod]
    [DataRow((byte)1)]
    [DataRow((byte)9)]
    public void Encode_LiteralWithNoCode_Throws(byte literal) =>
        Assert.ThrowsExactly<ArgumentException>(() => new KafkaReferenceHuffmanTree([1, 0, 1, 2]).Encode([literal]));

    [TestMethod]
    public void Encode_Null_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaReferenceHuffmanTree([1, 1]).Encode(null!));
}
