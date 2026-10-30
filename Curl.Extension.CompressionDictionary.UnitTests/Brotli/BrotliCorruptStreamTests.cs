using Curl.Extension.CompressionDictionary.Brotli;

namespace Curl.Extension.CompressionDictionary.UnitTests.Brotli;

[TestClass]
public sealed class BrotliCorruptStreamTests
{
    private static void AssertCorrupt(BrotliBitWriter writer, byte[]? prefixDictionary = null) =>
        Assert.ThrowsExactly<BrotliFormatException>(() => BrotliDictionaryDecoder.Decode(writer.ToArray(), prefixDictionary ?? []));

    // RFC 7932 section 9.1: WBITS 0, ISLAST, ISLASTEMPTY.
    [TestMethod]
    public void Decode_EmptyLastMetaBlock_GivesNothing() => Assert.IsEmpty(BrotliDictionaryDecoder.Decode([0x06]));

    [TestMethod]
    [DataRow(new byte[0])]
    [DataRow(new byte[] { 0x0E })]
    [DataRow(new byte[] { 0x06, 0x00 })]
    public void Decode_TruncatedOrBadPaddingOrTrailingBytes_Throws(byte[] stream) =>
        Assert.ThrowsExactly<BrotliFormatException>(() => BrotliDictionaryDecoder.Decode(stream));

    // RFC 7932 section 9.1: WBITS 17 is 1000000; 1000100 is reserved.
    [TestMethod]
    public void Decode_Window17_Decodes() =>
        Assert.IsEmpty(BrotliDictionaryDecoder.Decode(new BrotliBitWriter().Bits(1, 1).Bits(0, 6).Bits(3, 2).ToArray()));

    [TestMethod]
    public void Decode_ReservedWindowCode_Throws() => AssertCorrupt(new BrotliBitWriter().Bits(1, 1).Bits(0, 3).Bits(1, 3).Bits(3, 2));

    // RFC 7932 section 9.2: metadata blocks are skipped.
    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(1, 1)]
    public void Decode_MetadataBlock_IsSkipped(int byteCount, int skipped)
    {
        var writer = new BrotliBitWriter().Bits(0, 1).Bits(0, 1).Bits(3, 2).Bits(0, 1).Bits(byteCount, 2).Bits(0, byteCount * 8);

        var output = BrotliDictionaryDecoder.Decode(writer.AlignedBytes(new byte[skipped]).Bits(3, 2).ToArray());

        Assert.IsEmpty(output);
    }

    [TestMethod]
    [DataRow(1, 0, 1, 0)]
    [DataRow(0, 1, 1, 0)]
    [DataRow(0, 0, 2, 5)]
    public void Decode_BadMetadataBlock_Throws(int isLast, int reserved, int byteCount, int length) =>
        AssertCorrupt(new BrotliBitWriter().Bits(0, 1).Bits(isLast, 1).Bits(0, isLast).Bits(3, 2).Bits(reserved, 1).Bits(byteCount, 2).Bits(length, byteCount * 8).Bits(0, 16));

    [TestMethod]
    public void Decode_MetaBlockLengthWithZeroLastNibble_Throws() =>
        AssertCorrupt(new BrotliBitWriter().Bits(0, 1).Bits(0, 1).Bits(1, 2).Bits(5, 20).Bits(0, 8));

    // RFC 7932 section 9.2: an uncompressed meta-block's bytes follow at the next byte boundary.
    [TestMethod]
    public void Decode_UncompressedMetaBlock_GivesItsBytes()
    {
        var writer = new BrotliBitWriter().Bits(0, 1).Bits(0, 1).Bits(0, 2).Bits(2, 16).Bits(1, 1).AlignedBytes(1, 2, 3).Bits(3, 2);

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, BrotliDictionaryDecoder.Decode(writer.ToArray()));
    }

    [TestMethod]
    public void Decode_UncompressedMetaBlockCutShort_Throws() =>
        AssertCorrupt(new BrotliBitWriter().Bits(0, 1).Bits(0, 1).Bits(0, 2).Bits(2, 16).Bits(1, 1).AlignedBytes(1, 2));

    // RFC 7932 section 8: command 129 copies 3 bytes, too short for a dictionary word.
    [TestMethod]
    public void Decode_StaticDictionaryCopyOfThreeBytes_Throws() =>
        AssertCorrupt(BrotliBitWriter.LastCompressedMetaBlock(8, 129, 16).Bits(0, 1));

    // Distance symbol 46 has 16 extra bits; distance 131069 gives transform 128, past the 121 there are.
    [TestMethod]
    public void Decode_StaticDictionaryTransformPast120_Throws() =>
        AssertCorrupt(BrotliBitWriter.LastCompressedMetaBlock(8, 130, 46).Bits(0, 16));

    // RFC 7932 section 9.3: command 16 inserts two literals into a one-byte meta-block.
    [TestMethod]
    public void Decode_InsertPastMetaBlockEnd_Throws() => AssertCorrupt(BrotliBitWriter.LastCompressedMetaBlock(1, 16, 16));

    // Command 144 inserts two literals, then copies two bytes from distance 1 into one byte of room.
    [TestMethod]
    public void Decode_CopyPastMetaBlockEnd_Throws() => AssertCorrupt(BrotliBitWriter.LastCompressedMetaBlock(3, 144, 16).Bits(0, 1));

    [TestMethod]
    public void Decode_CopyWithinMetaBlock_RepeatsBytes() =>
        CollectionAssert.AreEqual("aaaa"u8.ToArray(), BrotliDictionaryDecoder.Decode(BrotliBitWriter.LastCompressedMetaBlock(4, 144, 16).Bits(0, 1).ToArray()));

    // Command 130 copies the 4-byte word "time" into a one-byte meta-block.
    [TestMethod]
    public void Decode_StaticWordPastMetaBlockEnd_Throws() => AssertCorrupt(BrotliBitWriter.LastCompressedMetaBlock(1, 130, 16).Bits(0, 1));

    // RFC 9842 section 4: distance 1 reaches the one-byte prefix dictionary, but a 3-byte copy runs past its end.
    [TestMethod]
    public void Decode_CopyPastPrefixDictionaryEnd_Throws() =>
        AssertCorrupt(BrotliBitWriter.LastCompressedMetaBlock(8, 129, 16).Bits(0, 1), [0x41]);

    // RFC 7932 section 4: after distance 1, distance code 4 (last distance less one) gives 0.
    [TestMethod]
    public void Decode_RingDistanceBelowOne_Throws() =>
        AssertCorrupt(BrotliBitWriter.LastCompressedMetaBlock(10, 144, 4, 16).Bits(1, 1).Bits(0, 1).Bits(0, 1));
}
