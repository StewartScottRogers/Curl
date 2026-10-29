namespace Curl.Extension.Kafka.Wire.Compression;

/// <summary>Checks the snappy decoder against vectors assembled by hand from google/snappy format_description.txt.</summary>
[TestClass]
public sealed class SnappyDecompressorTests
{
    private static readonly byte[] XerialHeader =
    [
        0x82, 0x53, 0x4E, 0x41, 0x50, 0x50, 0x59, 0x00, // magic
        0, 0, 0, 1, // version
        0, 0, 0, 1, // compatible version
    ];

    [TestMethod]
    public void Decompress_ShortLiteral_ReturnsTheBytes()
    {
        // length 3; literal tag (3-1)<<2 = 0x08; "abc"
        CollectionAssert.AreEqual("abc"u8.ToArray(), SnappyDecompressor.Decompress([0x03, 0x08, 0x61, 0x62, 0x63]));
    }

    [TestMethod]
    public void Decompress_EmptyRaw_ReturnsNothing()
    {
        Assert.AreEqual(0, SnappyDecompressor.Decompress([0x00]).Length);
    }

    [TestMethod]
    public void Decompress_LiteralWithOneLengthByte_ReturnsTheBytes()
    {
        byte[] text = new byte[100];
        Array.Fill(text, (byte)'x');
        // length 100; tag 60<<2 = 0xF0; length-1 = 99
        byte[] input = [100, 0xF0, 99, .. text];
        CollectionAssert.AreEqual(text, SnappyDecompressor.Decompress(input));
    }

    [TestMethod]
    public void Decompress_LiteralWithTwoThreeAndFourLengthBytes_ReturnsTheBytes()
    {
        byte[] text = new byte[300];
        Array.Fill(text, (byte)'y');
        // length 300 = varint AC 02; tag 61<<2 = 0xF4, 62<<2 = 0xF8, 63<<2 = 0xFC; length-1 = 299 = 0x012B
        CollectionAssert.AreEqual(text, SnappyDecompressor.Decompress([0xAC, 0x02, 0xF4, 0x2B, 0x01, .. text]));
        CollectionAssert.AreEqual(text, SnappyDecompressor.Decompress([0xAC, 0x02, 0xF8, 0x2B, 0x01, 0x00, .. text]));
        CollectionAssert.AreEqual(text, SnappyDecompressor.Decompress([0xAC, 0x02, 0xFC, 0x2B, 0x01, 0x00, 0x00, .. text]));
    }

    [TestMethod]
    public void Decompress_CopyWithOneByteOffset_RepeatsEarlierOutput()
    {
        // length 8; literal "abcd"; copy1 length 4 (tag = (4-4)<<2 | 1 = 0x01 | offset high bits 0), offset 4
        byte[] input = [0x08, 0x0C, 0x61, 0x62, 0x63, 0x64, 0x01, 0x04];
        CollectionAssert.AreEqual("abcdabcd"u8.ToArray(), SnappyDecompressor.Decompress(input));
    }

    [TestMethod]
    public void Decompress_CopyWithOneByteOffsetAndHighBits_UsesTheTagBitsAsOffsetHighBits()
    {
        byte[] text = new byte[300];
        for (int index = 0; index < text.Length; index++)
        {
            text[index] = (byte)index;
        }

        // length 304 = varint B0 02; literal 300 (tag 61<<2 = 0xF4, 299 = 0x012B); copy1 length 4, offset 300 = 0x12C: high bits 1 -> tag 0x21, low byte 0x2C
        byte[] expected = [.. text, .. text.AsSpan(0, 4)];
        CollectionAssert.AreEqual(expected, SnappyDecompressor.Decompress([0xB0, 0x02, 0xF4, 0x2B, 0x01, .. text, 0x21, 0x2C]));
    }

    [TestMethod]
    public void Decompress_CopyWithTwoByteOffset_RepeatsEarlierOutput()
    {
        // length 7; literal "abc"; copy2 length 4: tag (4-1)<<2 | 2 = 0x0E, offset 3 LE
        byte[] input = [0x07, 0x08, 0x61, 0x62, 0x63, 0x0E, 0x03, 0x00];
        CollectionAssert.AreEqual("abcabca"u8.ToArray(), SnappyDecompressor.Decompress(input));
    }

    [TestMethod]
    public void Decompress_CopyWithFourByteOffset_RepeatsEarlierOutput()
    {
        // length 7; literal "abc"; copy4 length 4: tag (4-1)<<2 | 3 = 0x0F, offset 3 LE
        byte[] input = [0x07, 0x08, 0x61, 0x62, 0x63, 0x0F, 0x03, 0x00, 0x00, 0x00];
        CollectionAssert.AreEqual("abcabca"u8.ToArray(), SnappyDecompressor.Decompress(input));
    }

    [TestMethod]
    public void Decompress_OverlappingCopy_RepeatsTheLastByte()
    {
        // length 9; literal "a"; copy1 length 8 (tag (8-4)<<2 | 1 = 0x11), offset 1
        byte[] input = [0x09, 0x00, 0x61, 0x11, 0x01];
        CollectionAssert.AreEqual("aaaaaaaaa"u8.ToArray(), SnappyDecompressor.Decompress(input));
    }

    [TestMethod]
    public void Decompress_XerialFramedChunks_ConcatenatesThem()
    {
        byte[] input =
        [
            .. XerialHeader,
            0, 0, 0, 5, 0x03, 0x08, 0x61, 0x62, 0x63, // chunk "abc"
            0, 0, 0, 4, 0x02, 0x04, 0x64, 0x65, // chunk "de"
        ];
        CollectionAssert.AreEqual("abcde"u8.ToArray(), SnappyDecompressor.Decompress(input));
    }

    [TestMethod]
    public void Decompress_XerialWithNoChunks_ReturnsNothing()
    {
        Assert.AreEqual(0, SnappyDecompressor.Decompress(XerialHeader).Length);
    }

    [TestMethod]
    public void Decompress_TruncatedXerialHeader_ThrowsFormatExceptionNamingSnappy()
    {
        AssertCorrupt(XerialHeader[..12]);
    }

    [TestMethod]
    public void Decompress_TruncatedXerialChunkLength_ThrowsFormatExceptionNamingSnappy()
    {
        AssertCorrupt([.. XerialHeader, 0, 0]);
    }

    [TestMethod]
    public void Decompress_XerialChunkLongerThanTheInput_ThrowsFormatExceptionNamingSnappy()
    {
        AssertCorrupt([.. XerialHeader, 0, 0, 0, 9, 0x03, 0x08]);
    }

    [TestMethod]
    public void Decompress_XerialNegativeChunkLength_ThrowsFormatExceptionNamingSnappy()
    {
        AssertCorrupt([.. XerialHeader, 0xFF, 0xFF, 0xFF, 0xFF]);
    }

    [TestMethod]
    public void Decompress_EmptyInput_ThrowsFormatExceptionNamingSnappy()
    {
        AssertCorrupt([]);
    }

    [TestMethod]
    public void Decompress_LengthVarintTooLong_ThrowsFormatExceptionNamingSnappy()
    {
        AssertCorrupt([0x80, 0x80, 0x80, 0x80, 0x80, 0x01]);
    }

    [TestMethod]
    public void Decompress_LengthVarintBeyondInt32_ThrowsFormatExceptionNamingSnappy()
    {
        AssertCorrupt([0xFF, 0xFF, 0xFF, 0xFF, 0x0F]);
    }

    [TestMethod]
    public void Decompress_DeclaredLengthFarBeyondWhatTheInputCanProduce_ThrowsFormatExceptionNamingSnappy()
    {
        AssertCorrupt([0x80, 0x80, 0x80, 0x08, 0x00, 0x61]);
    }

    [TestMethod]
    public void Decompress_TruncatedElementOffset_ThrowsFormatExceptionNamingSnappy()
    {
        AssertCorrupt([0x07, 0x08, 0x61, 0x62, 0x63, 0x0E, 0x03]);
    }

    [TestMethod]
    public void Decompress_TruncatedLiteral_ThrowsFormatExceptionNamingSnappy()
    {
        AssertCorrupt([0x03, 0x08, 0x61, 0x62]);
    }

    [TestMethod]
    public void Decompress_CopyOffsetOfZero_ThrowsFormatExceptionNamingSnappy()
    {
        AssertCorrupt([0x08, 0x0C, 0x61, 0x62, 0x63, 0x64, 0x01, 0x00]);
    }

    [TestMethod]
    public void Decompress_CopyOffsetBeforeTheStartOfTheOutput_ThrowsFormatExceptionNamingSnappy()
    {
        AssertCorrupt([0x08, 0x0C, 0x61, 0x62, 0x63, 0x64, 0x01, 0x05]);
    }

    [TestMethod]
    public void Decompress_LiteralLongerThanTheDeclaredLength_ThrowsFormatExceptionNamingSnappy()
    {
        AssertCorrupt([0x02, 0x08, 0x61, 0x62, 0x63]);
    }

    [TestMethod]
    public void Decompress_CopyLongerThanTheDeclaredLength_ThrowsFormatExceptionNamingSnappy()
    {
        AssertCorrupt([0x05, 0x0C, 0x61, 0x62, 0x63, 0x64, 0x01, 0x04]);
    }

    [TestMethod]
    public void Decompress_OutputShorterThanTheDeclaredLength_ThrowsFormatExceptionNamingSnappy()
    {
        AssertCorrupt([0x05, 0x08, 0x61, 0x62, 0x63]);
    }

    private static void AssertCorrupt(byte[] input)
    {
        FormatException exception = Assert.Throws<FormatException>(() => SnappyDecompressor.Decompress(input));
        StringAssert.Contains(exception.Message, "snappy");
    }
}
