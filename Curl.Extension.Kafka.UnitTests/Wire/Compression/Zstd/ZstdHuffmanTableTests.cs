namespace Curl.Extension.Kafka.Wire.Compression.Zstd;

/// <summary>Checks Huffman table decoding against RFC 8878 section 4.2.</summary>
[TestClass]
public sealed class ZstdHuffmanTableTests
{
    private static readonly byte[] FseWeightsTable = [0x10, 0x88, 0x1F];

    [TestMethod]
    public void Read_DirectWeights_InfersTheLastWeightAndAssignsCodesFromTheLowestWeight()
    {
        // Header 129 = 2 weights, 0x11 = weights 1, 1; the sum 2 is completed by weight 2 for symbol 2.
        ZstdHuffmanTable table = ZstdHuffmanTable.Read([0x81, 0x11], out int bytesRead);

        Assert.AreEqual(2, bytesRead);
        Assert.AreEqual(2, table.TableLog);
        // Codes: symbol 0 = 00, symbol 1 = 01, symbol 2 = 1. Stream 1 00 01 under the marker bit 5.
        ZstdBackwardBitReader reader = new([0x31]);
        Assert.AreEqual(2, table.DecodeSymbol(reader));
        Assert.AreEqual(0, table.DecodeSymbol(reader));
        Assert.AreEqual(1, table.DecodeSymbol(reader));
        Assert.AreEqual(0, reader.RemainingBits);
    }

    [TestMethod]
    public void Read_CompressedWeightsEndingOnTheSecondState_DecodesThreeWeights()
    {
        // The FSE block (3 bytes of table, 2 of stream) gives weights 2, 1, 1; the inferred last weight is 3.
        ZstdHuffmanTable table = ZstdHuffmanTable.Read([0x05, .. FseWeightsTable, 0xC1, 0x08], out int bytesRead);

        Assert.AreEqual(6, bytesRead);
        Assert.AreEqual(3, table.TableLog);
        // Codes: symbol 0 = 01, symbol 1 = 000, symbol 2 = 001, symbol 3 = 1. Stream 1 01 000 001 under the marker.
        ZstdBackwardBitReader reader = new([0x41, 0x03]);
        Assert.AreEqual(3, table.DecodeSymbol(reader));
        Assert.AreEqual(0, table.DecodeSymbol(reader));
        Assert.AreEqual(1, table.DecodeSymbol(reader));
        Assert.AreEqual(2, table.DecodeSymbol(reader));
        Assert.AreEqual(0, reader.RemainingBits);
    }

    [TestMethod]
    public void Read_CompressedWeightsEndingOnTheFirstState_DecodesTwoWeights()
    {
        // Weights 1, 2; the inferred last weight is 1.
        ZstdHuffmanTable table = ZstdHuffmanTable.Read([0x05, .. FseWeightsTable, 0x03, 0x04], out int bytesRead);

        Assert.AreEqual(6, bytesRead);
        Assert.AreEqual(2, table.TableLog);
    }

    [TestMethod]
    public void Read_NoBytes_ThrowsFormatExceptionNamingZstd() => AssertRejected([]);

    [TestMethod]
    public void Read_DirectWeightsTruncated_ThrowsFormatExceptionNamingZstd() => AssertRejected([0x81]);

    [TestMethod]
    public void Read_CompressedSizeZero_ThrowsFormatExceptionNamingZstd() => AssertRejected([0x00, 0x10]);

    [TestMethod]
    public void Read_CompressedBlockTruncated_ThrowsFormatExceptionNamingZstd() => AssertRejected([0x05, 0x10]);

    [TestMethod]
    public void Read_CompressedStreamTooShortForTheStates_ThrowsFormatExceptionNamingZstd() =>
        AssertRejected([0x04, .. FseWeightsTable, 0x01]);

    [TestMethod]
    public void Read_CompressedWeightsNeverEnd_ThrowsFormatExceptionNamingZstd() =>
        AssertRejected([0x06, 0x10, 0xF8, 0x01, 0x00, 0x00, 0x01]);

    [TestMethod]
    public void Read_WeightsAllZero_ThrowsFormatExceptionNamingZstd() => AssertRejected([0x81, 0x00]);

    [TestMethod]
    public void Read_WeightsNotCompletingAPowerOfTwo_ThrowsFormatExceptionNamingZstd() => AssertRejected([0x81, 0x31]);

    [TestMethod]
    public void Read_WeightsNeedingMoreThanElevenBits_ThrowsFormatExceptionNamingZstd() => AssertRejected([0x80, 0xC0]);

    private static void AssertRejected(byte[] source)
    {
        FormatException exception = Assert.ThrowsExactly<FormatException>(() => ZstdHuffmanTable.Read(source, out _));

        StringAssert.Contains(exception.Message, "zstd");
    }
}
