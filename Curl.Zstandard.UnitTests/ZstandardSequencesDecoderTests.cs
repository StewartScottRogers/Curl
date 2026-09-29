using System.Buffers;
using System.Text;
using static Curl.Zstandard.ZstandardTestFrames;
using static Curl.Zstandard.ZstandardTestLiterals;
using static Curl.Zstandard.ZstandardTestSequences;

namespace Curl.Zstandard;

/// <summary>
/// Drives <see cref="ZstandardDecoder" /> over hand-assembled compressed blocks with
/// sequences - each <c>Symbol_Compression_Modes</c> value, each repeat-offset rule,
/// overlapping matches and matches into earlier blocks - whole and a byte at a time, and
/// pins each malformed sequences section to <see cref="ZstandardDecodeError.CorruptionDetected" />
/// (BL-860).
/// </summary>
[TestClass]
public sealed class ZstandardSequencesDecoderTests
{
    /// <summary>A <c>Window_Descriptor</c> of 0x38: a 128 KiB window.</summary>
    private const byte OneHundredTwentyEightKibibyteWindow = 0x38;

    /// <summary>Literals length codes 0 to 5 over 32 states; code 5 is "less than 1".</summary>
    private static readonly SequenceTable LiteralsLengthFse = FseTable([8, 8, 8, 4, 3, -1], 5);

    /// <summary>Offset codes 0 to 3 over 32 states.</summary>
    private static readonly SequenceTable OffsetFse = FseTable([2, 2, 12, 16], 5);

    /// <summary>Match length codes 0 to 5 over 32 states.</summary>
    private static readonly SequenceTable MatchLengthFse = FseTable([16, 4, 4, 4, 2, 2], 5);

    private static readonly byte[] FirstThousand = Pattern(1000, 0);

    private static readonly byte[] SecondThousand = Pattern(1000, 101);

    private static readonly byte[] ThousandTwentyFour = Pattern(1024, 7);

    public static IEnumerable<object[]> ValidInputs =>
    [
        ["predefined tables, a new offset", Frame(Block(true, "abc", Predefined(new Sequence(3, 6, 6)))), Ascii("abcabcabc"), 1],
        ["a match overlapping itself at offset 1", Frame(Block(true, "a", Predefined(new Sequence(1, 4, 10)))), Ascii("aaaaaaaaaaa"), 1],
        ["a match overlapping itself at offset 2", Frame(Block(true, "ab", Predefined(new Sequence(2, 5, 7)))), Ascii("ababababa"), 1],
        ["literals after the last sequence", Frame(Block(true, "abcXYZ", Predefined(new Sequence(3, 6, 3)))), Ascii("abcabcXYZ"), 1],
        ["every repeat-offset rule", Frame(Block(true, "0123456789ABCDEFGHIJ", Predefined(
            new Sequence(8, 3, 3),
            new Sequence(1, 2, 3),
            new Sequence(0, 1, 4),
            new Sequence(0, 2, 3),
            new Sequence(0, 3, 3),
            new Sequence(2, 1, 3),
            new Sequence(1, 3, 3)))), Ascii("0123456701288887012701701" + "9A19AB019CDEFGHIJ"), 1],
        ["RLE tables", Frame(Block(true, "abcd", SequencesSection([new Sequence(2, 5, 3), new Sequence(2, 7, 3)], RleTable(2), RleTable(2), RleTable(0)))), Ascii("ababacdbac"), 1],
        ["FSE-compressed tables", Frame(Block(true, "Hello, FSE world!", SequencesSection(
            [new Sequence(5, 8, 5), new Sequence(2, 2, 3), new Sequence(3, 12, 4)], LiteralsLengthFse, OffsetFse, MatchLengthFse))), Ascii("HelloHello,    FSEo,   world!"), 1],
        ["repeated FSE, RLE and predefined tables, a match into the previous block", Frame(
            Block(false, "abcdef", SequencesSection([new Sequence(3, 6, 3)], LiteralsLengthFse, RleTable(2), PredefinedMatchLength)),
            Block(true, "gh", SequencesSection([new Sequence(2, 7, 5)], Repeated(LiteralsLengthFse), Repeated(RleTable(2)), Repeated(PredefinedMatchLength)))), Ascii("abcabcdefghefghe"), 1],
        ["a match wholly in the previous block", Frame(RawBlock(false, Ascii("hello world ")), Block(true, "", Predefined(new Sequence(0, 15, 11)))), Ascii("hello world hello world"), 1],
        ["a match from the previous block into this one", Frame(RawBlock(false, Ascii("hello world ")), Block(true, "X", Predefined(new Sequence(1, 16, 13)))), Ascii("hello world Xhello world X"), 1],
        ["a match across the end of the full history", Frame(RawBlock(false, FirstThousand), RawBlock(false, SecondThousand), RawBlock(false, FirstThousand), RawBlock(false, SecondThousand), Block(true, "", Predefined(new Sequence(0, 903, 100)))),
            Concatenate(FirstThousand, SecondThousand, FirstThousand, SecondThousand, SecondThousand[100..200]), 1],
        ["a match further back than the window, still held as libzstd holds it", Frame(RawBlock(false, FirstThousand), RawBlock(false, SecondThousand), Block(true, "", Predefined(new Sequence(0, 1028, 3)))),
            Concatenate(FirstThousand, SecondThousand, FirstThousand[975..978]), 1],
        ["a match 3040 bytes back in a 1 KiB window", Frame(RawBlock(false, ThousandTwentyFour), RawBlock(false, ThousandTwentyFour), RawBlock(false, ThousandTwentyFour), Block(true, "", Predefined(new Sequence(0, 3043, 3)))),
            Concatenate(ThousandTwentyFour, ThousandTwentyFour, ThousandTwentyFour, ThousandTwentyFour[32..35]), 1],
        ["2-byte Number_of_Sequences", Frame(RleBlock(200, 200)), Repeat((byte)'x', 800), 1],
        ["3-byte Number_of_Sequences", FrameIn(OneHundredTwentyEightKibibyteWindow, RleBlock(32600, 32600)), Repeat((byte)'x', 130400), 1],
        ["declared content size and checksum", Concatenate(
            FrameHeader(0x84, OneKibibyteWindow, 9, 0, 0, 0), Block(true, "abc", Predefined(new Sequence(3, 6, 6))), Checksum(Ascii("abcabcabc"))), Ascii("abcabcabc"), 1],
        ["a new frame restores the repeat offsets", Concatenate(
            Frame(Block(true, "abcd", Predefined(new Sequence(4, 5, 3)))),
            Frame(Block(true, "abcdefgh", Predefined(new Sequence(8, 3, 3))))), Ascii("abcdcdcabcdefghabc"), 2],
    ];

    public static IEnumerable<object[]> InvalidInputs =>
    [
        ["reserved Symbol_Compression_Modes bits", Frame(Block(true, "ab", [1, 0x01, 0x01]))],
        ["RLE literals length code above 35", Frame(Block(true, "ab", [1, 0x40, 36, 0x01]))],
        ["RLE table with no code", Frame(Block(true, "ab", [1, 0x40]))],
        ["FSE literals length table with Accuracy_Log 10", Frame(Block(true, "ab", [1, 0x80, .. FseTableDescription([1024], 10), 0x01]))],
        ["FSE table description cut short", Frame(Block(true, "ab", [1, 0x80, FseTableDescription([8, 8, 8, 4, 3, -1], 5)[0]]))],
        ["repeated table with no previous table", Frame(Block(true, "abc", SequencesSection([new Sequence(3, 6, 3)], Repeated(PredefinedLiteralsLength), PredefinedOffset, PredefinedMatchLength)))],
        ["repeated table from the previous frame", Concatenate(
            Frame(Block(true, "abc", SequencesSection([new Sequence(3, 6, 3)], LiteralsLengthFse, PredefinedOffset, PredefinedMatchLength))),
            Frame(Block(true, "abc", SequencesSection([new Sequence(3, 6, 3)], Repeated(LiteralsLengthFse), PredefinedOffset, PredefinedMatchLength))))],
        ["bitstream without a padding bit", Frame(Block(true, "abc", [.. Predefined(new Sequence(3, 6, 3)), 0]))],
        ["bits left over in the bitstream", Frame(Block(true, "abc", PredefinedWithReads([new Sequence(3, 6, 3)], reads => reads.Add((1, 1)))))],
        ["bitstream read past its start", Frame(Block(true, "0123456789ABCDEF", PredefinedWithReads([new Sequence(16, 19, 3)], reads => reads.RemoveAt(reads.Count - 1))))],
        ["more literals than the literals section holds", Frame(Block(true, "ab", Predefined(new Sequence(3, 6, 3))))],
        ["a match before the frame's content", Frame(Block(true, "ab", Predefined(new Sequence(2, 6, 3))))],
        ["offset 0 from Repeated_Offset1 - 1", Frame(Block(true, "ab", Predefined(new Sequence(0, 3, 3))))],
        ["a match past Block_Maximum_Size", Frame(Block(true, "a", Predefined(new Sequence(1, 4, 1100))))],
        ["literals after the last sequence past Block_Maximum_Size", Frame(Block(true, Encoding.Latin1.GetString(FirstThousand), Predefined(new Sequence(1, 4, 100))))],
        ["a match past Frame_Content_Size", Concatenate(FrameHeader(0x80, OneKibibyteWindow, 5, 0, 0, 0), Block(true, "ab", Predefined(new Sequence(2, 5, 5))))],
        ["3-byte Number_of_Sequences cut short", Frame(Block(true, "ab", [255, 1]))],
    ];

    [TestMethod]
    [DynamicData(nameof(ValidInputs))]
    public void TryDecompress_BlocksWithSequences_WritesWhatTheyRegenerate(string name, byte[] source, byte[] expected, int frameCount)
    {
        var destination = new byte[expected.Length];

        var decoded = ZstandardDecoder.TryDecompress(source, destination, out var bytesWritten);

        Assert.IsTrue(decoded, name);
        Assert.AreEqual(expected.Length, bytesWritten, name);
        CollectionAssert.AreEqual(expected, destination, $"{name} ({frameCount} frames)");
    }

    [TestMethod]
    [DynamicData(nameof(ValidInputs))]
    public void Decompress_BlocksWithSequencesOneByteAtATime_WritesWhatTheyRegenerate(string name, byte[] source, byte[] expected, int frameCount)
    {
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

        Assert.AreEqual(OperationStatus.Done, status, name);
        Assert.AreEqual(frameCount, doneCount, name);
        CollectionAssert.AreEqual(expected, output, name);
    }

    [TestMethod]
    [DynamicData(nameof(InvalidInputs))]
    public void Decompress_MalformedSequencesSection_IsCorruptionDetected(string name, byte[] source)
    {
        var decoder = new ZstandardDecoder();

        OperationStatus status;
        var position = 0;
        do
        {
            status = decoder.Decompress(source.AsSpan(position), new byte[200_000], out var consumed, out _);
            position += consumed;
        }
        while (status == OperationStatus.Done);

        Assert.AreEqual(OperationStatus.InvalidData, status, name);
        Assert.AreEqual(ZstandardDecodeError.CorruptionDetected, decoder.LastError, name);
        Assert.IsFalse(ZstandardDecoder.TryDecompress(source, new byte[200_000], out _), name);
    }

    private static byte[] Frame(params byte[][] blocks) => Concatenate([FrameHeader(0x00, OneKibibyteWindow), .. blocks]);

    private static byte[] FrameIn(byte windowDescriptor, params byte[][] blocks) => Concatenate([FrameHeader(0x00, windowDescriptor), .. blocks]);

    /// <summary>A compressed block of raw <paramref name="literals" /> and <paramref name="sequencesSection" />.</summary>
    private static byte[] Block(bool last, string literals, byte[] sequencesSection)
    {
        byte[] literalsSection = [.. UncompressedHeader(Raw, literals.Length), .. Encoding.Latin1.GetBytes(literals)];
        return [.. BlockHeader(last, CompressedBlockType, literalsSection.Length + sequencesSection.Length), .. literalsSection, .. sequencesSection];
    }

    /// <summary>
    /// A last compressed block of <paramref name="literalCount" /> RLE literals 'x' and
    /// <paramref name="sequenceCount" /> sequences of RLE tables, each one literal and a
    /// 3-byte match at repeat offset 1, so the bitstream is its padding bit alone.
    /// </summary>
    private static byte[] RleBlock(int literalCount, int sequenceCount)
    {
        byte[] literalsSection = [.. UncompressedHeader(Rle, literalCount), (byte)'x'];
        var sequencesSection = SequencesSection(sequenceCount, RleTable(1), RleTable(0), RleTable(0), [0x01]);
        return [.. BlockHeader(true, CompressedBlockType, literalsSection.Length + sequencesSection.Length), .. literalsSection, .. sequencesSection];
    }

    private static byte[] Predefined(params Sequence[] sequences) =>
        SequencesSection(sequences, PredefinedLiteralsLength, PredefinedOffset, PredefinedMatchLength);

    /// <summary>A predefined-tables section whose bitstream reads are altered by <paramref name="alter" />.</summary>
    private static byte[] PredefinedWithReads(Sequence[] sequences, Action<List<(uint Value, int Bits)>> alter)
    {
        var reads = SequenceReads(sequences, PredefinedLiteralsLength, PredefinedOffset, PredefinedMatchLength);
        alter(reads);
        return SequencesSection(sequences.Length, PredefinedLiteralsLength, PredefinedOffset, PredefinedMatchLength, BackwardStream(reads));
    }

    private static byte[] Pattern(int length, int seed) => [.. Enumerable.Range(0, length).Select(index => (byte)((index * 31) + seed + (index / 7)))];

    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    private static byte[] Repeat(byte value, int count) => Enumerable.Repeat(value, count).ToArray();
}
