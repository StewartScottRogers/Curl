using System.Buffers;
using System.Text;
using Curl.Testing;
using static Curl.Zstandard.ZstandardTestFrames;
using static Curl.Zstandard.ZstandardTestLiterals;

namespace Curl.Zstandard;

/// <summary>
/// Drives <see cref="ZstandardDecoder" /> over hand-assembled compressed blocks with zero
/// sequences - raw, RLE, Huffman-coded and treeless literals, one and four streams, direct
/// and FSE-compressed weights - whole and a byte at a time, and pins the
/// <see cref="ZstandardDecodeError" /> each malformed literals section fails with (BL-859).
/// </summary>
[TestClass]
public sealed class ZstandardLiteralsDecoderTests
{
    public TestContext TestContext { get; set; } = null!;

    /// <summary>A <c>Window_Descriptor</c> of 0x30: a 64 KiB window.</summary>
    private const byte SixtyFourKibibyteWindow = 0x30;

    /// <summary>
    /// "abcdefabacadaeafbbccddeeffedcbaabcdef" as one Huffman stream whose tree has
    /// FSE-compressed weights, as <see cref="ZstandardTestLiterals" /> builds it. curl 8.21.0
    /// with libzstd 1.5.7 decodes it to that text as a <c>Content-Encoding: zstd</c> body
    /// (measured with Record-CurlExchange.ps1, like every frame of <see cref="ValidInputs" />).
    /// </summary>
    private const string MeasuredFseWeightsFrame = "28B52FFD0000ED00005242060AB0A7018436616666D60A07C432101F0190E84550B203620100";

    /// <summary>
    /// A compressed block of <c>Block_Size</c> 0, a raw block of "ok", and a last compressed
    /// block of <c>Block_Size</c> 0. libzstd's streaming decoder treats a 0-byte block of any
    /// type as empty, so curl 8.21.0 prints "ok" and exits 0 (measured; ADR-0192).
    /// </summary>
    private const string MeasuredEmptyCompressedBlocksFrame = "28B52FFD00000400001000006F6B050000";

    /// <summary>The Huffman tree of RFC 8878 section 4.2.1: weights 4, 3, 2, 0, 1 and the implied 1 of literal 5.</summary>
    private static readonly byte[] RfcWeights = [4, 3, 2, 0, 1];

    /// <summary>Literals 'a' to 'f' with weights 3, 3, 2, 1, 1 and the implied 3 of 'f'.</summary>
    private static readonly byte[] LetterWeights = [.. new byte[97], 3, 3, 2, 1, 1];

    /// <summary>The weights of <see cref="LetterWeights" /> are 0 to 3, spread over 32 states; weight 4 is "less than 1".</summary>
    private static readonly short[] LetterWeightCounts = [26, 2, 1, 2, -1];

    private static readonly byte[] Letters = Ascii("abcdefabacadaeafbbccddeeffedcbaabcdef");

    private static readonly byte[] RfcLiterals = [0, 0, 1, 2, 4, 5, 0, 1, 0, 0, 2, 5, 4, 1, 0];

    public static IEnumerable<object[]> ValidInputs =>
    [
        ["raw literals, 1-byte header", Frame(CompressedBlock(true, RawLiterals(Ascii("Hello, world")))), Ascii("Hello, world"), 1],
        ["raw literals, 2-byte header", Frame(CompressedBlock(true, RawLiterals(Pattern(300)))), Pattern(300), 1],
        ["raw literals, 3-byte header", FrameIn(SixtyFourKibibyteWindow, CompressedBlock(true, RawLiterals(Pattern(5000)))), Pattern(5000), 1],
        ["empty raw literals", Frame(CompressedBlock(true, RawLiterals([]))), Array.Empty<byte>(), 1],
        ["RLE literals, 1-byte header", Frame(CompressedBlock(true, RleLiterals((byte)'z', 31))), Repeat((byte)'z', 31), 1],
        ["RLE literals, 2-byte header", Frame(CompressedBlock(true, RleLiterals((byte)'y', 1000))), Repeat((byte)'y', 1000), 1],
        ["RLE literals, 3-byte header", FrameIn(SixtyFourKibibyteWindow, CompressedBlock(true, RleLiterals((byte)'x', 20000))), Repeat((byte)'x', 20000), 1],
        ["Huffman literals, one stream, RFC 8878 tree", Frame(CompressedBlock(true, HuffmanLiterals(DirectTreeDescription(RfcWeights), RfcWeights, RfcLiterals, 0))), RfcLiterals, 1],
        ["Huffman literals, one stream, letters", Frame(CompressedBlock(true, HuffmanLiterals(DirectTreeDescription(LetterWeights), LetterWeights, Letters, 0))), Letters, 1],
        ["Huffman literals, four streams, 10-bit sizes", Frame(CompressedBlock(true, HuffmanLiterals(DirectTreeDescription(LetterWeights), LetterWeights, Letters, 1))), Letters, 1],
        ["Huffman literals, four streams, 14-bit sizes", Frame(CompressedBlock(true, HuffmanLiterals(DirectTreeDescription(LetterWeights), LetterWeights, Letters, 2))), Letters, 1],
        ["Huffman literals, four streams, 18-bit sizes", FrameIn(SixtyFourKibibyteWindow, CompressedBlock(true, HuffmanLiterals(DirectTreeDescription(LetterWeights), LetterWeights, ManyLetters, 3))), ManyLetters, 1],
        ["Huffman literals, six literals in four streams", Frame(CompressedBlock(true, HuffmanLiterals(DirectTreeDescription(LetterWeights), LetterWeights, Ascii("fedcba"), 1))), Ascii("fedcba"), 1],
        ["Huffman literals, FSE-compressed weights", Frame(CompressedBlock(true, FseLetterLiterals(Letters, 0))), Letters, 1],
        ["treeless literals reuse the previous block's tree", Frame(
            CompressedBlock(false, HuffmanLiterals(DirectTreeDescription(LetterWeights), LetterWeights, Letters, 0)),
            CompressedBlock(false, RawLiterals(Ascii("|"))),
            RawBlock(false, Ascii("|")),
            CompressedBlock(false, TreelessLiterals(LetterWeights, Ascii("cafe"), 0)),
            CompressedBlock(true, TreelessLiterals(LetterWeights, Ascii("facebeadfade"), 1))), Concatenate(Letters, Ascii("||cafefacebeadfade")), 1],
        ["a new frame has its own tree", Concatenate(
            Frame(CompressedBlock(true, HuffmanLiterals(DirectTreeDescription(LetterWeights), LetterWeights, Letters, 0))),
            Frame(CompressedBlock(true, HuffmanLiterals(DirectTreeDescription(RfcWeights), RfcWeights, RfcLiterals, 0)))), Concatenate(Letters, RfcLiterals), 2],
        ["Huffman literals, FSE-compressed weights with a count of 28", Frame(CompressedBlock(true, HuffmanLiterals(FseTreeDescription(LetterWeights, [28, 2, 1, 1], 5), LetterWeights, Letters, 1))), Letters, 1],
        ["measured FSE-compressed weights frame", Convert.FromHexString(MeasuredFseWeightsFrame), Letters, 1],
        ["measured empty compressed blocks", Convert.FromHexString(MeasuredEmptyCompressedBlocksFrame), Ascii("ok"), 1],
        ["2-byte zero Number_of_Sequences", Frame(CompressedBlockWithSequences(true, RawLiterals(Ascii("ok")), 0x80, 0x00)), Ascii("ok"), 1],
        ["declared content size, with checksum", Concatenate(FrameHeader(0x84, OneKibibyteWindow, 12, 0, 0, 0), CompressedBlock(true, RawLiterals(Ascii("Hello, world"))), Checksum(Ascii("Hello, world"))), Ascii("Hello, world"), 1],
    ];

    public static IEnumerable<object[]> InvalidInputs =>
    [
        ["treeless literals with no previous tree", Frame(CompressedBlock(true, TreelessLiterals(LetterWeights, Letters, 0))), ZstandardDecodeError.CorruptionDetected],
        ["treeless literals after a tree from the previous frame", Concatenate(
            Frame(CompressedBlock(true, HuffmanLiterals(DirectTreeDescription(LetterWeights), LetterWeights, Letters, 0))),
            Frame(CompressedBlock(true, TreelessLiterals(LetterWeights, Letters, 0)))), ZstandardDecodeError.CorruptionDetected],
        ["Huffman stream without a padding bit", Frame(CompressedBlock(true, HuffmanSection(Compressed, 0, Letters.Length, [.. DirectTreeDescription(LetterWeights), .. HuffmanStream(LetterWeights, Letters), 0]))), ZstandardDecodeError.CorruptionDetected],
        ["Huffman stream with bits left over", Frame(CompressedBlock(true, HuffmanSection(Compressed, 0, Letters.Length, [.. DirectTreeDescription(LetterWeights), 0xFF, .. HuffmanStream(LetterWeights, Letters)]))), ZstandardDecodeError.CorruptionDetected],
        ["Huffman stream shorter than its literals", Frame(CompressedBlock(true, HuffmanSection(Compressed, 0, Letters.Length + 4, [.. DirectTreeDescription(LetterWeights), .. HuffmanStream(LetterWeights, Letters)]))), ZstandardDecodeError.CorruptionDetected],
        ["empty Huffman stream", Frame(CompressedBlock(true, HuffmanSection(Compressed, 0, 1, DirectTreeDescription(LetterWeights)))), ZstandardDecodeError.CorruptionDetected],
        ["weights that leave a gap in the tree", Frame(CompressedBlock(true, HuffmanSection(Compressed, 0, 1, [.. DirectTreeDescription(2, 2, 1), 0x01]))), ZstandardDecodeError.CorruptionDetected],
        ["weights with no pair of longest codes", Frame(CompressedBlock(true, HuffmanSection(Compressed, 0, 1, [0x80, 0x20, 0x01]))), ZstandardDecodeError.CorruptionDetected],
        ["weights that are all zero", Frame(CompressedBlock(true, HuffmanSection(Compressed, 0, 1, [.. DirectTreeDescription(0, 0), 0x01]))), ZstandardDecodeError.CorruptionDetected],
        ["weights deeper than 11 bits", Frame(CompressedBlock(true, HuffmanSection(Compressed, 0, 1, [.. DirectTreeDescription(12), 0x01]))), ZstandardDecodeError.CorruptionDetected],
        ["direct weights past the literals", Frame(CompressedBlock(true, HuffmanSection(Compressed, 0, 1, [.. DirectTreeDescription(LetterWeights)[..10]]))), ZstandardDecodeError.CorruptionDetected],
        ["no tree description", Frame(CompressedBlock(true, HuffmanSection(Compressed, 0, 1, []))), ZstandardDecodeError.CorruptionDetected],
        ["FSE weights past the literals", Frame(CompressedBlock(true, HuffmanSection(Compressed, 0, 1, [100, 0x00, 0x01]))), ZstandardDecodeError.CorruptionDetected],
        ["FSE weights with Accuracy_Log 7", Frame(CompressedBlock(true, HuffmanSection(Compressed, 0, 1, [.. WithHeaderLength([.. FseTableDescription([100, 28], 7), 0x81]), 0x01]))), ZstandardDecodeError.CorruptionDetected],
        ["FSE weights with no weight stream", Frame(CompressedBlock(true, HuffmanSection(Compressed, 0, 1, [.. WithHeaderLength(FseTableDescription(LetterWeightCounts, 5)), 0x01]))), ZstandardDecodeError.CorruptionDetected],
        ["FSE weights, more than 255", Frame(CompressedBlock(true, HuffmanSection(Compressed, 0, 1, [.. FseTreeDescription([.. new byte[300], 1], [31, 1], 5), 0x01]))), ZstandardDecodeError.CorruptionDetected],
        ["raw literals past the block", Frame(CompressedBlock(true, [.. UncompressedHeader(Raw, 20), .. Ascii("Hello, world")])), ZstandardDecodeError.CorruptionDetected],
        ["RLE literals past the block", Frame(CompressedBlockWithSequences(true, UncompressedHeader(Rle, 20))), ZstandardDecodeError.CorruptionDetected],
        ["Huffman literals past the block", Frame(CompressedBlock(true, HuffmanHeader(Compressed, 0, 40, 30))), ZstandardDecodeError.CorruptionDetected],
        ["literals larger than the window", Frame(CompressedBlock(true, RleLiterals((byte)'r', 1025))), ZstandardDecodeError.CorruptionDetected],
        ["literals larger than the declared content", Concatenate(FrameHeader(0x80, OneKibibyteWindow, 5, 0, 0, 0), CompressedBlock(true, RawLiterals(Ascii("123456")))), ZstandardDecodeError.CorruptionDetected],
        ["literals header cut short", Frame(CompressedBlockWithSequences(true, HuffmanHeader(Compressed, 3, 40, 30)[..4])), ZstandardDecodeError.CorruptionDetected],
        ["five literals in four streams", Frame(CompressedBlock(true, HuffmanSection(Compressed, 1, 5, [.. DirectTreeDescription(LetterWeights), 1, 0, 1, 0, 1, 0, 0x01, 0x01, 0x01, 0x01]))), ZstandardDecodeError.LiteralsHeaderWrong],
        ["four streams without a jump table", Frame(CompressedBlock(true, HuffmanSection(Compressed, 1, 8, [.. DirectTreeDescription(LetterWeights), 1, 0, 1, 0, 1]))), ZstandardDecodeError.CorruptionDetected],
        ["four streams longer than the literals", Frame(CompressedBlock(true, HuffmanSection(Compressed, 1, 8, [.. DirectTreeDescription(LetterWeights), 9, 0, 1, 0, 1, 0, 0x01]))), ZstandardDecodeError.CorruptionDetected],
        ["four streams, one of them corrupt", Frame(CompressedBlock(true, HuffmanSection(Compressed, 1, 8, [.. DirectTreeDescription(LetterWeights), 1, 0, 1, 0, 1, 0, 0x01, 0x01, 0x01, 0x01]))), ZstandardDecodeError.CorruptionDetected],
        ["four streams, the last without a padding bit", Frame(CompressedBlock(true, HuffmanSection(Compressed, 1, Letters.Length, [.. DirectTreeDescription(LetterWeights), .. FourHuffmanStreams(LetterWeights, Letters), 0]))), ZstandardDecodeError.CorruptionDetected],
        ["no sequences section", Frame(CompressedBlockWithSequences(true, RawLiterals(Ascii("ok")))), ZstandardDecodeError.CorruptionDetected],
        ["a byte after zero sequences", Frame(CompressedBlockWithSequences(true, RawLiterals(Ascii("ok")), 0x00, 0x00)), ZstandardDecodeError.CorruptionDetected],
        ["a byte after the 2-byte zero sequences", Frame(CompressedBlockWithSequences(true, RawLiterals(Ascii("ok")), 0x80, 0x00, 0x00)), ZstandardDecodeError.CorruptionDetected],
        ["one sequence and no bitstream", Frame(CompressedBlockWithSequences(true, RawLiterals(Ascii("ok")), 0x01, 0x00)), ZstandardDecodeError.CorruptionDetected],
        ["256 sequences and no bitstream", Frame(CompressedBlockWithSequences(true, RawLiterals(Ascii("ok")), 0x81, 0x00, 0x00)), ZstandardDecodeError.CorruptionDetected],
        ["one sequence in the 2-byte form and no bitstream", Frame(CompressedBlockWithSequences(true, RawLiterals(Ascii("ok")), 0x80, 0x01, 0x00)), ZstandardDecodeError.CorruptionDetected],
        ["a 2-byte Number_of_Sequences cut short", Frame(CompressedBlockWithSequences(true, RawLiterals(Ascii("ok")), 0x80)), ZstandardDecodeError.CorruptionDetected],
    ];

    private static byte[] ManyLetters => [.. Enumerable.Range(0, 20000).Select(index => Letters[(index * 7) % Letters.Length])];

    [TestMethod]
    [DynamicData(nameof(ValidInputs))]
    public void TryDecompress_LiteralsOnlyBlocks_WritesTheirLiterals(string name, byte[] source, byte[] expected, int frameCount)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.ArrangeSource(name, source);
        var destination = new byte[expected.Length];

        var decoded = ZstandardDecoder.TryDecompress(source, destination, out var bytesWritten);

        diagnostics.Act("decoded", decoded);
        diagnostics.Act("bytes written", bytesWritten);
        diagnostics.ActOutput(expected, destination.AsSpan(0, Math.Min(bytesWritten, destination.Length)));
        diagnostics.Assert("bytes written", expected.Length, bytesWritten);
        Assert.IsTrue(decoded, name);
        Assert.AreEqual(expected.Length, bytesWritten, name);
        CollectionAssert.AreEqual(expected, destination, $"{name} ({frameCount} frames)");
    }

    [TestMethod]
    [DynamicData(nameof(ValidInputs))]
    public void Decompress_LiteralsOnlyBlocksOneByteAtATime_GivesWhatTryDecompressGives(string name, byte[] source, byte[] expected, int frameCount)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.ArrangeSource(name, source);
        diagnostics.Arrange("frames expected", frameCount);
        var decoder = new ZstandardDecoder();
        var output = new List<byte>();
        var destination = new byte[1];
        var position = 0;
        var doneCount = 0;
        OperationStatus status;
        do
        {
            status = decoder.Decompress(source.AsSpan(position, Math.Min(1, source.Length - position)), destination, out var consumed, out var written);
            position += consumed;
            output.AddRange(destination.AsSpan(0, written));
            doneCount += status == OperationStatus.Done ? 1 : 0;
        }
        while (status != OperationStatus.InvalidData && (position < source.Length || status == OperationStatus.DestinationTooSmall));

        var whole = new byte[expected.Length];
        var wholeDecoded = ZstandardDecoder.TryDecompress(source, whole, out _);
        diagnostics.Act("last status", status);
        diagnostics.Act("error raised", decoder.LastError);
        diagnostics.Act("Done statuses", doneCount);
        diagnostics.Act("TryDecompress decoded", wholeDecoded);
        diagnostics.ActOutput(whole, output.ToArray());
        diagnostics.Assert("last status", OperationStatus.Done, status);
        diagnostics.Assert("Done statuses", frameCount, doneCount);
        Assert.IsTrue(wholeDecoded, name);
        Assert.AreEqual(OperationStatus.Done, status, name);
        Assert.AreEqual(frameCount, doneCount, name);
        CollectionAssert.AreEqual(whole, output, name);
    }

    [TestMethod]
    [DynamicData(nameof(InvalidInputs))]
    public void Decompress_InvalidLiteralsSection_IsInvalidDataWithTheNamedError(string name, byte[] source, ZstandardDecodeError expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.ArrangeSource(name, source);
        var decoder = new ZstandardDecoder();

        OperationStatus status;
        var position = 0;
        do
        {
            status = decoder.Decompress(source.AsSpan(position), new byte[200_000], out var consumed, out _);
            position += consumed;
        }
        while (status == OperationStatus.Done);

        diagnostics.Act("status", status);
        diagnostics.Act("error raised", decoder.LastError);
        diagnostics.Act("consumed before the failure", position);
        diagnostics.Assert("status", OperationStatus.InvalidData, status);
        diagnostics.Assert("error raised", expected, decoder.LastError);
        Assert.AreEqual(OperationStatus.InvalidData, status, name);
        Assert.AreEqual(expected, decoder.LastError, name);
        Assert.IsFalse(ZstandardDecoder.TryDecompress(source, new byte[200_000], out _), name);
    }

    private static byte[] Frame(params byte[][] blocks) => Concatenate([FrameHeader(0x00, OneKibibyteWindow), .. blocks]);

    private static byte[] FrameIn(byte windowDescriptor, params byte[][] blocks) => Concatenate([FrameHeader(0x00, windowDescriptor), .. blocks]);

    /// <summary>A compressed block of <paramref name="literalsSection" /> and a 1-byte <c>Number_of_Sequences</c> of 0.</summary>
    private static byte[] CompressedBlock(bool last, byte[] literalsSection) => CompressedBlockWithSequences(last, literalsSection, 0x00);

    private static byte[] CompressedBlockWithSequences(bool last, byte[] literalsSection, params byte[] sequencesSection) =>
        [.. BlockHeader(last, CompressedBlockType, literalsSection.Length + sequencesSection.Length), .. literalsSection, .. sequencesSection];

    private static byte[] RawLiterals(byte[] literals) => [.. UncompressedHeader(Raw, literals.Length), .. literals];

    private static byte[] RleLiterals(byte value, int size) => [.. UncompressedHeader(Rle, size), value];

    /// <summary>Compressed literals: <paramref name="treeDescription" />, then <paramref name="literals" /> coded under <paramref name="codedWeights" />.</summary>
    private static byte[] HuffmanLiterals(byte[] treeDescription, byte[] codedWeights, byte[] literals, int sizeFormat) =>
        HuffmanSection(Compressed, sizeFormat, literals.Length, [.. treeDescription, .. Streams(codedWeights, literals, sizeFormat)]);

    private static byte[] TreelessLiterals(byte[] codedWeights, byte[] literals, int sizeFormat) =>
        HuffmanSection(Treeless, sizeFormat, literals.Length, Streams(codedWeights, literals, sizeFormat));

    private static byte[] FseLetterLiterals(byte[] literals, int sizeFormat) =>
        HuffmanLiterals(FseTreeDescription(LetterWeights, LetterWeightCounts, 5), LetterWeights, literals, sizeFormat);

    private static byte[] Streams(byte[] codedWeights, byte[] literals, int sizeFormat) =>
        sizeFormat == 0 ? HuffmanStream(codedWeights, literals) : FourHuffmanStreams(codedWeights, literals);

    private static byte[] HuffmanSection(int type, int sizeFormat, int regeneratedSize, byte[] content) =>
        [.. HuffmanHeader(type, sizeFormat, regeneratedSize, content.Length), .. content];

    /// <summary>Prefixes an FSE weights description with its length, as a tree description's <c>headerByte</c>.</summary>
    private static byte[] WithHeaderLength(byte[] description) => [(byte)description.Length, .. description];

    private static byte[] Pattern(int length) => [.. Enumerable.Range(0, length).Select(index => (byte)(index * 31))];

    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    private static byte[] Repeat(byte value, int count) => Enumerable.Repeat(value, count).ToArray();
}
