using System.Buffers;
using System.Text;
using static Curl.Zstandard.ZstandardTestFrames;

namespace Curl.Zstandard;

/// <summary>
/// Drives <see cref="ZstandardDecoder" /> over hand-assembled RFC 8878 frames of raw and
/// RLE blocks, skippable frames and content checksums, whole and a byte at a time, and
/// pins the <see cref="ZstandardDecodeError" /> each malformed frame fails with (BL-858).
/// </summary>
[TestClass]
public sealed class ZstandardDecoderTests
{
    /// <summary>
    /// "Hello, world" in a single-segment frame with a 1-byte <c>Frame_Content_Size</c>, one
    /// raw block and a <c>Content_Checksum</c>. curl 8.21.0 with libzstd 1.5.7 decodes it
    /// to "Hello, world" as a <c>Content-Encoding: zstd</c> body, and fails with exit 61
    /// when its last byte is 0x75 instead of 0x74 (measured with Record-CurlExchange.ps1).
    /// </summary>
    private const string MeasuredHelloWorldFrame = "28B52FFD240C61000048656C6C6F2C20776F726C64D7B42074";

    private static readonly byte[] HelloWorld = Encoding.ASCII.GetBytes("Hello, world");

    public static IEnumerable<object[]> ValidInputs =>
    [
        ["raw blocks", Concatenate(FrameHeader(0x00, OneKibibyteWindow), RawBlock(false, Ascii("Hello, ")), RawBlock(true, Ascii("world"))), HelloWorld, 1],
        ["RLE blocks", Concatenate(FrameHeader(0x00, OneKibibyteWindow), RleBlock(false, (byte)'a', 5), RleBlock(true, (byte)'b', 3)), Ascii("aaaaabbb"), 1],
        ["empty raw and RLE blocks", Concatenate(FrameHeader(0x00, OneKibibyteWindow), RawBlock(false, []), RleBlock(false, (byte)'q', 0), RawBlock(true, Ascii("ok"))), Ascii("ok"), 1],
        ["measured single segment with checksum", Convert.FromHexString(MeasuredHelloWorldFrame), HelloWorld, 1],
        ["no content size, with checksum", Concatenate(FrameHeader(0x04, OneKibibyteWindow), RawBlock(true, HelloWorld), Checksum(HelloWorld)), HelloWorld, 1],
        ["1-byte content size", Concatenate(FrameHeader(0x20, 12), RawBlock(true, HelloWorld)), HelloWorld, 1],
        ["2-byte content size", Concatenate(FrameHeader(0x40, OneKibibyteWindow, 44, 0), RleBlock(true, (byte)'x', 300)), Repeat((byte)'x', 300), 1],
        ["2-byte content size, single segment", Concatenate(FrameHeader(0x60, 44, 0), RleBlock(true, (byte)'x', 300)), Repeat((byte)'x', 300), 1],
        ["4-byte content size", Concatenate(FrameHeader(0x80, OneKibibyteWindow, 12, 0, 0, 0), RawBlock(true, HelloWorld)), HelloWorld, 1],
        ["8-byte content size", Concatenate(FrameHeader(0xC0, OneKibibyteWindow, 12, 0, 0, 0, 0, 0, 0, 0), RawBlock(true, HelloWorld)), HelloWorld, 1],
        ["zero dictionary IDs of 1, 2 and 4 bytes", Concatenate(
            FrameHeader(0x01, OneKibibyteWindow, 0), RawBlock(true, Ascii("a")),
            FrameHeader(0x02, OneKibibyteWindow, 0, 0), RawBlock(true, Ascii("b")),
            FrameHeader(0x03, OneKibibyteWindow, 0, 0, 0, 0), RawBlock(true, Ascii("c"))), Ascii("abc"), 3],
        ["1152-byte block in a window with mantissa 1", Concatenate(FrameHeader(0x00, 0x01), RleBlock(true, 9, 1152)), Repeat(9, 1152), 1],
        ["128 KiB block in a 256 KiB window", Concatenate(FrameHeader(0x00, 0x40), RleBlock(true, 7, 128 * 1024)), Repeat(7, 128 * 1024), 1],
        ["two frames around a skippable frame", Concatenate(
            FrameHeader(0x04, OneKibibyteWindow), RawBlock(true, Ascii("abc")), Checksum(Ascii("abc")),
            SkippableFrame(0xF, Ascii("xyz")),
            FrameHeader(0x20, 2), RleBlock(true, (byte)'d', 2)), Ascii("abcdd"), 3],
        ["empty skippable frame", SkippableFrame(0x0, []), Array.Empty<byte>(), 1],
    ];

    public static IEnumerable<object[]> InvalidInputs =>
    [
        ["unknown magic", new byte[] { 0x00, 0x01, 0x02, 0x03 }, ZstandardDecodeError.PrefixUnknown, ZstandardDecoder.DefaultMaxWindowLog],
        ["magic below the skippable range", new byte[] { 0x4F, 0x2A, 0x4D, 0x18 }, ZstandardDecodeError.PrefixUnknown, ZstandardDecoder.DefaultMaxWindowLog],
        ["magic above the skippable range", new byte[] { 0x60, 0x2A, 0x4D, 0x18 }, ZstandardDecodeError.PrefixUnknown, ZstandardDecoder.DefaultMaxWindowLog],
        ["reserved bit", Concatenate(FrameHeader(0x08, OneKibibyteWindow), RawBlock(true, [])), ZstandardDecodeError.FrameParameterUnsupported, ZstandardDecoder.DefaultMaxWindowLog],
        ["window above 2^10", Concatenate(FrameHeader(0x00, 0x08), RawBlock(true, [])), ZstandardDecodeError.FrameParameterWindowTooLarge, 10],
        ["single-segment content above 2^10", Concatenate(FrameHeader(0x60, 0x01, 0x03), RleBlock(true, 0, 1025)), ZstandardDecodeError.FrameParameterWindowTooLarge, 10],
        ["non-zero dictionary ID", Concatenate(FrameHeader(0x21, 5, 0), RawBlock(true, [])), ZstandardDecodeError.DictionaryWrong, ZstandardDecoder.DefaultMaxWindowLog],
        ["wrong checksum", Convert.FromHexString(MeasuredHelloWorldFrame[..^2] + "75"), ZstandardDecodeError.ChecksumWrong, ZstandardDecoder.DefaultMaxWindowLog],
        ["block larger than the window", Concatenate(FrameHeader(0x00, OneKibibyteWindow), BlockHeader(true, RleBlockType, 1025), [0]), ZstandardDecodeError.CorruptionDetected, ZstandardDecoder.DefaultMaxWindowLog],
        ["block larger than a window with mantissa 1", Concatenate(FrameHeader(0x00, 0x01), BlockHeader(true, RleBlockType, 1153), [0]), ZstandardDecodeError.CorruptionDetected, ZstandardDecoder.DefaultMaxWindowLog],
        ["block larger than 128 KiB", Concatenate(FrameHeader(0x00, 0x40), BlockHeader(true, RleBlockType, (128 * 1024) + 1), [0]), ZstandardDecodeError.CorruptionDetected, ZstandardDecoder.DefaultMaxWindowLog],
        ["more content than declared", Concatenate(FrameHeader(0x80, OneKibibyteWindow, 5, 0, 0, 0), RawBlock(true, Ascii("123456"))), ZstandardDecodeError.CorruptionDetected, ZstandardDecoder.DefaultMaxWindowLog],
        ["less content than declared", Concatenate(FrameHeader(0x80, OneKibibyteWindow, 5, 0, 0, 0), RawBlock(true, Ascii("1234"))), ZstandardDecodeError.CorruptionDetected, ZstandardDecoder.DefaultMaxWindowLog],
        ["reserved block type", Concatenate(FrameHeader(0x00, OneKibibyteWindow), BlockHeader(true, ReservedBlockType, 0)), ZstandardDecodeError.CorruptionDetected, ZstandardDecoder.DefaultMaxWindowLog],
        ["compressed block with a sequence and no Symbol_Compression_Modes", Concatenate(FrameHeader(0x00, OneKibibyteWindow), BlockHeader(true, CompressedBlockType, 2), [0, 1]), ZstandardDecodeError.CorruptionDetected, ZstandardDecoder.DefaultMaxWindowLog],
        ["compressed block larger than the window", Concatenate(FrameHeader(0x00, OneKibibyteWindow), BlockHeader(true, CompressedBlockType, 1025)), ZstandardDecodeError.CorruptionDetected, ZstandardDecoder.DefaultMaxWindowLog],
    ];

    [TestMethod]
    [DynamicData(nameof(ValidInputs))]
    public void TryDecompress_ValidFrames_WritesTheirContent(string name, byte[] source, byte[] expected, int frameCount)
    {
        var destination = new byte[expected.Length];

        var decoded = ZstandardDecoder.TryDecompress(source, destination, out var bytesWritten);

        Assert.IsTrue(decoded, name);
        Assert.AreEqual(expected.Length, bytesWritten, name);
        CollectionAssert.AreEqual(expected, destination, $"{name} ({frameCount} frames)");
    }

    [TestMethod]
    [DynamicData(nameof(ValidInputs))]
    public void Decompress_ValidFramesOneByteAtATime_WritesTheirContentAndIsDoneAtEachFrameEnd(string name, byte[] source, byte[] expected, int frameCount)
    {
        AssertDecodesInPieces(name, source, expected, frameCount, sourcePiece: 1, destinationPiece: 1);
    }

    [TestMethod]
    [DynamicData(nameof(ValidInputs))]
    public void Decompress_WholeSourceOneOutputByteAtATime_WritesTheirContentAndIsDoneAtEachFrameEnd(string name, byte[] source, byte[] expected, int frameCount)
    {
        AssertDecodesInPieces(name, source, expected, frameCount, sourcePiece: source.Length, destinationPiece: 1);
    }

    [TestMethod]
    [DynamicData(nameof(InvalidInputs))]
    public void Decompress_InvalidFrame_IsInvalidDataWithTheNamedError(string name, byte[] source, ZstandardDecodeError expected, int maxWindowLog)
    {
        var decoder = new ZstandardDecoder(maxWindowLog);

        var status = decoder.Decompress(source, new byte[200_000], out _, out _);

        Assert.AreEqual(OperationStatus.InvalidData, status, name);
        Assert.AreEqual(expected, decoder.LastError, name);
        Assert.IsFalse(ZstandardDecoder.TryDecompress(source, new byte[200_000], out _, maxWindowLog), name);
    }

    [TestMethod]
    public void Decompress_AfterInvalidData_StaysFailedAndConsumesNothing()
    {
        var decoder = new ZstandardDecoder();
        decoder.Decompress([0, 0, 0, 0], new byte[16], out _, out _);

        var status = decoder.Decompress(Convert.FromHexString(MeasuredHelloWorldFrame), new byte[16], out var consumed, out var written);

        Assert.AreEqual(OperationStatus.InvalidData, status);
        Assert.AreEqual(0, consumed);
        Assert.AreEqual(0, written);
        Assert.AreEqual(ZstandardDecodeError.PrefixUnknown, decoder.LastError);
    }

    [TestMethod]
    public void LastError_NewDecoder_IsNone()
    {
        Assert.AreEqual(ZstandardDecodeError.None, new ZstandardDecoder().LastError);
    }

    [TestMethod]
    public void Decompress_ConcatenatedFrames_StopsDoneAtTheEndOfEach()
    {
        var first = Convert.FromHexString(MeasuredHelloWorldFrame);
        var skippable = SkippableFrame(0x5, Ascii("skip me"));
        var decoder = new ZstandardDecoder();
        var destination = new byte[64];
        var source = Concatenate(first, skippable, first);

        var firstStatus = decoder.Decompress(source, destination, out var firstConsumed, out var firstWritten);
        var secondStatus = decoder.Decompress(source.AsSpan(firstConsumed), destination, out var secondConsumed, out var secondWritten);

        Assert.AreEqual(OperationStatus.Done, firstStatus);
        Assert.AreEqual(first.Length, firstConsumed);
        Assert.AreEqual(HelloWorld.Length, firstWritten);
        Assert.AreEqual(OperationStatus.Done, secondStatus);
        Assert.AreEqual(skippable.Length, secondConsumed);
        Assert.AreEqual(0, secondWritten);
    }

    [TestMethod]
    public void Decompress_RawBlockSourceAndDestinationRunOutTogether_NeedsMoreData()
    {
        var source = Concatenate(FrameHeader(0x00, OneKibibyteWindow), RawBlock(true, HelloWorld));
        var decoder = new ZstandardDecoder();
        var destination = new byte[5];

        var status = decoder.Decompress(source.AsSpan(0, source.Length - 7), destination, out var consumed, out var written);

        Assert.AreEqual(OperationStatus.NeedMoreData, status);
        Assert.AreEqual(source.Length - 7, consumed);
        Assert.AreEqual(5, written);
        CollectionAssert.AreEqual(Ascii("Hello"), destination);
    }

    [TestMethod]
    public void TryDecompress_TruncatedFrame_ReturnsFalse()
    {
        var source = Convert.FromHexString(MeasuredHelloWorldFrame)[..^1];

        Assert.IsFalse(ZstandardDecoder.TryDecompress(source, new byte[64], out _));
    }

    [TestMethod]
    public void TryDecompress_TruncatedSkippableFrame_ReturnsFalse()
    {
        var source = SkippableFrame(0x1, Ascii("xyz"))[..^1];

        Assert.IsFalse(ZstandardDecoder.TryDecompress(source, new byte[64], out _));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void TryDecompress_DestinationOneByteShort_ReturnsFalse(bool raw)
    {
        var block = raw ? RawBlock(true, HelloWorld) : RleBlock(true, (byte)'x', HelloWorld.Length);
        var source = Concatenate(FrameHeader(0x00, OneKibibyteWindow), block);

        Assert.IsFalse(ZstandardDecoder.TryDecompress(source, new byte[HelloWorld.Length - 1], out _));
    }

    [TestMethod]
    public void TryDecompress_EmptySource_WritesNothing()
    {
        Assert.IsTrue(ZstandardDecoder.TryDecompress([], [], out var bytesWritten));
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    public void TryDecompress_LargestMaxWindowLog_AcceptsA2GiBWindow()
    {
        var source = Concatenate(FrameHeader(0x00, 21 << 3), RawBlock(true, HelloWorld));

        Assert.IsTrue(ZstandardDecoder.TryDecompress(source, new byte[64], out var bytesWritten, ZstandardDecoder.MaximumMaxWindowLog));
        Assert.AreEqual(HelloWorld.Length, bytesWritten);
    }

    [TestMethod]
    [DataRow(ZstandardDecoder.MinimumMaxWindowLog - 1)]
    [DataRow(ZstandardDecoder.MaximumMaxWindowLog + 1)]
    public void Constructor_MaxWindowLogOutOfRange_Throws(int maxWindowLog)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ZstandardDecoder(maxWindowLog));
    }

    /// <summary>
    /// Feeds <paramref name="source" /> in pieces of at most <paramref name="sourcePiece" />
    /// bytes into destinations of <paramref name="destinationPiece" /> bytes, and checks the
    /// output and that one <see cref="OperationStatus.Done" /> came per frame.
    /// </summary>
    private static void AssertDecodesInPieces(string name, byte[] source, byte[] expected, int frameCount, int sourcePiece, int destinationPiece)
    {
        var decoder = new ZstandardDecoder();
        var output = new List<byte>();
        var destination = new byte[destinationPiece];
        var position = 0;
        var doneCount = 0;
        OperationStatus status;
        do
        {
            var piece = source.AsSpan(position, Math.Min(sourcePiece, source.Length - position));
            status = decoder.Decompress(piece, destination, out var consumed, out var written);
            position += consumed;
            output.AddRange(destination.AsSpan(0, written));
            doneCount += status == OperationStatus.Done ? 1 : 0;
        }
        while (status != OperationStatus.InvalidData && (position < source.Length || status == OperationStatus.DestinationTooSmall));

        Assert.AreEqual(OperationStatus.Done, status, name);
        Assert.AreEqual(frameCount, doneCount, name);
        CollectionAssert.AreEqual(expected, output, name);
    }

    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    private static byte[] Repeat(byte value, int count) => Enumerable.Repeat(value, count).ToArray();
}
