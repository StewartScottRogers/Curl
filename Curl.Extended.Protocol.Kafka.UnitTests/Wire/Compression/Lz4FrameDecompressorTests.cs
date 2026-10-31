namespace Curl.Extended.Protocol.Kafka.Wire.Compression;

/// <summary>Checks the lz4 frame decoder against vectors assembled by hand from the lz4 frame and block format specifications.</summary>
[TestClass]
public sealed class Lz4FrameDecompressorTests
{
    private static readonly byte[] Magic = [0x04, 0x22, 0x4D, 0x18];

    private static readonly byte[] EndMark = [0, 0, 0, 0];

    // FLG (version 01, independent blocks by default), BD (64 KB max block), optional fields, header checksum (not verified)
    private static byte[] Header(byte flags = 0x60, params byte[] optional) => [.. Magic, flags, 0x40, .. optional, 0x00];

    private static byte[] Block(params byte[] bytes) => [.. BitConverter.GetBytes(bytes.Length), .. bytes];

    private static byte[] StoredBlock(params byte[] bytes) => [.. BitConverter.GetBytes(bytes.Length | int.MinValue), .. bytes];

    private static byte[] Frame(params byte[][] parts) => [.. parts.SelectMany(part => part)];

    private static byte[] OptionalFields() => [1, 0, 0, 0, 0, 0, 0, 0, 9, 9, 9, 9]; // 8-byte content size, 4-byte dictionary id

    [TestMethod]
    public void Decompress_EmptyFrame_ReturnsNothing()
    {
        Assert.AreEqual(0, Lz4FrameDecompressor.Decompress(Frame(Header(), EndMark)).Length);
    }

    [TestMethod]
    public void Decompress_StoredBlock_ReturnsTheBytes()
    {
        CollectionAssert.AreEqual("abc"u8.ToArray(), Lz4FrameDecompressor.Decompress(Frame(Header(), StoredBlock(0x61, 0x62, 0x63), EndMark)));
    }

    [TestMethod]
    public void Decompress_LiteralOnlyBlock_ReturnsTheBytes()
    {
        // token: 3 literals, no match; "abc"
        CollectionAssert.AreEqual("abc"u8.ToArray(), Lz4FrameDecompressor.Decompress(Frame(Header(), Block(0x30, 0x61, 0x62, 0x63), EndMark)));
    }

    [TestMethod]
    public void Decompress_MatchWithinBlock_RepeatsEarlierOutput()
    {
        // token: 4 literals, match nibble 2 (length 6); "abcd"; offset 4; then token 0x10 "e" as the last sequence
        byte[] block = [0x42, 0x61, 0x62, 0x63, 0x64, 0x04, 0x00, 0x10, 0x65];
        CollectionAssert.AreEqual("abcdabcdabe"u8.ToArray(), Lz4FrameDecompressor.Decompress(Frame(Header(), Block(block), EndMark)));
    }

    [TestMethod]
    public void Decompress_OverlappingMatch_RepeatsTheByte()
    {
        // token: 1 literal, match nibble 5 (length 9); "a"; offset 1; last sequence "b"
        byte[] block = [0x15, 0x61, 0x01, 0x00, 0x10, 0x62];
        CollectionAssert.AreEqual("aaaaaaaaaab"u8.ToArray(), Lz4FrameDecompressor.Decompress(Frame(Header(), Block(block), EndMark)));
    }

    [TestMethod]
    public void Decompress_LongLiteralAndMatchLengths_ReadTheExtensionBytes()
    {
        byte[] text = new byte[300];
        for (int index = 0; index < text.Length; index++)
        {
            text[index] = (byte)(index * 7 % 251);
        }

        // token 0xFF: literal 15 + 255 + 30 = 300, match 15 + 255 + 1 + 4 = 275; offset 300 = 0x012C; last sequence one literal 'z'
        byte[] block = [0xFF, 255, 30, .. text, 0x2C, 0x01, 255, 1, 0x10, 0x7A];
        byte[] expected = [.. text, .. Enumerable.Range(0, 275).Select(index => text[index % 300]), 0x7A];
        CollectionAssert.AreEqual(expected, Lz4FrameDecompressor.Decompress(Frame(Header(), Block(block), EndMark)));
    }

    [TestMethod]
    public void Decompress_MatchReachingIntoAnEarlierBlock_RepeatsEarlierOutput()
    {
        // second block: 0 literals, match nibble 0 (length 4), offset 4 into the first block; then a last sequence of one literal
        byte[] second = [0x00, 0x04, 0x00, 0x10, 0x65];
        CollectionAssert.AreEqual(
            "abcdabcde"u8.ToArray(),
            Lz4FrameDecompressor.Decompress(Frame(Header(), StoredBlock(0x61, 0x62, 0x63, 0x64), Block(second), EndMark)));
    }

    [TestMethod]
    public void Decompress_OptionalFieldsPresent_AreSkipped()
    {
        // FLG 0x7D: version 01, block independence, block checksum, content size, content checksum, dictionary id
        byte[] frame = Frame(Header(0x7D, OptionalFields()), StoredBlock(0x61), [0xAA, 0xBB, 0xCC, 0xDD], EndMark, [0x11, 0x22, 0x33, 0x44]);
        CollectionAssert.AreEqual("a"u8.ToArray(), Lz4FrameDecompressor.Decompress(frame));
    }

    [TestMethod]
    public void Decompress_WrongMagicOrEmptyInput_ThrowsNamingLz4()
    {
        AssertCorrupt([]);
        AssertCorrupt([0x04, 0x22, 0x4D, 0x19, 0x60, 0x40, 0x00, 0, 0, 0, 0]);
    }

    [TestMethod]
    public void Decompress_UnsupportedVersion_ThrowsNamingLz4()
    {
        AssertCorrupt(Frame(Header(0x20), EndMark));
    }

    [TestMethod]
    public void Decompress_TruncatedAnywhere_ThrowsNamingLz4()
    {
        byte[] frame = Frame(Header(0x7D, OptionalFields()), StoredBlock(0x61), [0xAA, 0xBB, 0xCC, 0xDD], EndMark, [0x11, 0x22, 0x33, 0x44]);
        for (int length = 4; length < frame.Length; length++)
        {
            AssertCorrupt(frame[..length]);
        }
    }

    [TestMethod]
    public void Decompress_BlockLongerThanInput_ThrowsNamingLz4()
    {
        AssertCorrupt(Frame(Header(), BitConverter.GetBytes(10), [0x30, 0x61]));
    }

    [TestMethod]
    public void Decompress_MatchOffsetZeroOrBeforeTheOutput_ThrowsNamingLz4()
    {
        AssertCorrupt(Frame(Header(), Block(0x10, 0x61, 0x00, 0x00, 0x10, 0x62), EndMark));
        AssertCorrupt(Frame(Header(), Block(0x10, 0x61, 0x02, 0x00, 0x10, 0x62), EndMark));
    }

    [TestMethod]
    public void Decompress_TruncatedInsideABlock_ThrowsNamingLz4()
    {
        AssertCorrupt(Frame(Header(), Block(0x30, 0x61), EndMark)); // literals cut short
        AssertCorrupt(Frame(Header(), Block(0x10, 0x61, 0x01), EndMark)); // offset cut short
        AssertCorrupt(Frame(Header(), Block(0x1F, 0x61, 0x01, 0x00), EndMark)); // match length extension missing
    }

    [TestMethod]
    public void Decompress_LiteralLengthLargerThanTheBlock_ThrowsNamingLz4()
    {
        // literal length 15 + 255 * 4 = 1035 in a 6-byte block
        AssertCorrupt(Frame(Header(), Block(0xF0, 255, 255, 255, 255, 0x00), EndMark));
    }

    private static void AssertCorrupt(byte[] input)
    {
        FormatException exception = Assert.ThrowsExactly<FormatException>(() => Lz4FrameDecompressor.Decompress(input));
        StringAssert.Contains(exception.Message, "lz4");
    }
}
