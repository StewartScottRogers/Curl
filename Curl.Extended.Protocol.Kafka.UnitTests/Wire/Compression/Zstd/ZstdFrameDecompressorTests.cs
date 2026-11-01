using System.Numerics;
using System.Text;

namespace Curl.Extended.Protocol.Kafka.Wire.Compression.Zstd;

/// <summary>Checks zstd frame decompression (RFC 8878) against vectors assembled byte by byte from the format.</summary>
[TestClass]
public sealed class ZstdFrameDecompressorTests
{
    private const int Raw = 0;

    private const int Rle = 1;

    private const int Compressed = 2;

    // Magic number 0xFD2FB528 little-endian, frame header descriptor 0 (window descriptor follows, no checksum, no
    // content size, no dictionary), window descriptor 0.
    private static readonly byte[] PlainHeader = [0x28, 0xB5, 0x2F, 0xFD, 0x00, 0x00];

    // Huffman tree: header 0x81 = 2 direct weights, 0x11 = weights 1 and 1; symbol 2 gets weight 2.
    // Codes: symbol 0 = 00, symbol 1 = 01, symbol 2 = 1.
    private static readonly byte[] HuffmanTree = [0x81, 0x11];

    // The same FSE description the Huffman table tests use; its table has accuracy log 5.
    private static readonly byte[] FseTable = [0x10, 0x88, 0x1F];

    [TestMethod]
    public void Decompress_RawBlock_GivesTheBlockBytes() =>
        Assert.AreEqual("hello", Text(ZstdFrameDecompressor.Decompress(Frame(Block(Raw, true, Bytes("hello"))))));

    [TestMethod]
    public void Decompress_RleBlock_RepeatsTheByte() =>
        Assert.AreEqual("aaaa", Text(ZstdFrameDecompressor.Decompress(Frame(Block(Rle, true, [(byte)'a'], 4)))));

    [TestMethod]
    public void Decompress_SeveralBlocks_JoinsThem() =>
        Assert.AreEqual("abcd", Text(ZstdFrameDecompressor.Decompress(Frame(Block(Raw, false, Bytes("ab")), Block(Raw, true, Bytes("cd"))))));

    [TestMethod]
    public void Decompress_SkippableFrameAndTwoFrames_SkipsAndJoins()
    {
        // Skippable magic 0x184D2A5F little-endian, length 2, two ignored bytes.
        byte[] skippable = [0x5F, 0x2A, 0x4D, 0x18, 0x02, 0x00, 0x00, 0x00, 0xAA, 0xBB];
        byte[] input = [.. skippable, .. Frame(Block(Raw, true, Bytes("ab"))), .. Frame(Block(Raw, true, Bytes("cd")))];

        Assert.AreEqual("abcd", Text(ZstdFrameDecompressor.Decompress(input)));
    }

    [TestMethod]
    public void Decompress_NoInput_GivesNothing() => Assert.AreEqual(0, ZstdFrameDecompressor.Decompress([]).Length);

    [TestMethod]
    public void Decompress_ChecksumFlag_SkipsTheChecksum()
    {
        // Descriptor 0x04 = content checksum present; the four checksum bytes are not verified.
        byte[] input = [0x28, 0xB5, 0x2F, 0xFD, 0x04, 0x00, .. Block(Raw, true, Bytes("ab")), 0x01, 0x02, 0x03, 0x04];

        Assert.AreEqual("ab", Text(ZstdFrameDecompressor.Decompress(input)));
    }

    [TestMethod]
    [DataRow((byte)0x01, 1)]
    [DataRow((byte)0x02, 2)]
    [DataRow((byte)0x03, 4)]
    public void Decompress_DictionaryIdZero_IsAccepted(byte flag, int idLength)
    {
        byte[] input = [0x28, 0xB5, 0x2F, 0xFD, flag, 0x00, .. new byte[idLength], .. Block(Raw, true, Bytes("ab"))];

        Assert.AreEqual("ab", Text(ZstdFrameDecompressor.Decompress(input)));
    }

    [TestMethod]
    public void Decompress_SingleSegmentWithOneByteContentSize_Decompresses()
    {
        // Descriptor 0x20 = single segment: no window descriptor, a one-byte content size of 2.
        byte[] input = [0x28, 0xB5, 0x2F, 0xFD, 0x20, 0x02, .. Block(Raw, true, Bytes("ab"))];

        Assert.AreEqual("ab", Text(ZstdFrameDecompressor.Decompress(input)));
    }

    [TestMethod]
    public void Decompress_TwoByteContentSize_AddsTwoHundredFiftySix()
    {
        // Descriptor 0x40 = two-byte content size; stored value 0 means 256.
        byte[] input = [0x28, 0xB5, 0x2F, 0xFD, 0x40, 0x00, 0x00, 0x00, .. Block(Raw, true, new byte[256])];

        Assert.AreEqual(256, ZstdFrameDecompressor.Decompress(input).Length);
    }

    [TestMethod]
    public void Decompress_FourByteContentSize_Decompresses()
    {
        byte[] input = [0x28, 0xB5, 0x2F, 0xFD, 0x80, 0x00, 0x02, 0x00, 0x00, 0x00, .. Block(Raw, true, Bytes("ab"))];

        Assert.AreEqual("ab", Text(ZstdFrameDecompressor.Decompress(input)));
    }

    [TestMethod]
    public void Decompress_EightByteContentSize_Decompresses()
    {
        byte[] input = [0x28, 0xB5, 0x2F, 0xFD, 0xC0, 0x00, 0x02, 0, 0, 0, 0, 0, 0, 0, .. Block(Raw, true, Bytes("ab"))];

        Assert.AreEqual("ab", Text(ZstdFrameDecompressor.Decompress(input)));
    }

    [TestMethod]
    public void Decompress_ContentSizeNotMatchingTheBlocks_ThrowsFormatExceptionNamingZstd() =>
        AssertRejected([0x28, 0xB5, 0x2F, 0xFD, 0x20, 0x03, .. Block(Raw, true, Bytes("ab"))]);

    [TestMethod]
    public void Decompress_WrongMagic_ThrowsFormatExceptionNamingZstd() => AssertRejected([0x27, 0xB5, 0x2F, 0xFD, 0x00, 0x00]);

    [TestMethod]
    public void Decompress_DictionaryIdNotZero_ThrowsFormatExceptionNamingZstd() =>
        AssertRejected([0x28, 0xB5, 0x2F, 0xFD, 0x01, 0x00, 0x07, .. Block(Raw, true, Bytes("ab"))]);

    [TestMethod]
    public void Decompress_ReservedHeaderBit_ThrowsFormatExceptionNamingZstd() =>
        AssertRejected([0x28, 0xB5, 0x2F, 0xFD, 0x08, 0x00, .. Block(Raw, true, Bytes("ab"))]);

    [TestMethod]
    public void Decompress_ReservedBlockType_ThrowsFormatExceptionNamingZstd() => AssertRejected(Frame(Block(3, true, [])));

    [TestMethod]
    public void Decompress_BlockLargerThan128KiB_ThrowsFormatExceptionNamingZstd() =>
        AssertRejected(Frame(Block(Raw, true, [], (128 * 1024) + 1)));

    [TestMethod]
    public void Decompress_TruncatedInputs_ThrowFormatExceptionNamingZstd()
    {
        AssertRejected([0x28, 0xB5]);
        AssertRejected([0x28, 0xB5, 0x2F, 0xFD]);
        AssertRejected([0x28, 0xB5, 0x2F, 0xFD, 0x20]);
        AssertRejected(PlainHeader);
        AssertRejected([.. PlainHeader, 0x01]);
        AssertRejected([.. PlainHeader, .. Block(Raw, true, Bytes("ab"))[..4]]);
        AssertRejected([0x28, 0xB5, 0x2F, 0xFD, 0x04, 0x00, .. Block(Raw, true, Bytes("ab")), 0x01]);
        AssertRejected([0x5F, 0x2A, 0x4D, 0x18]);
        AssertRejected([0x5F, 0x2A, 0x4D, 0x18, 0x0A, 0x00, 0x00, 0x00, 0x01]);
    }

    [TestMethod]
    public void Decompress_CompressedBlockWithRawLiteralsAndNoSequences_GivesTheLiterals() =>
        Assert.AreEqual("abc", Text(CompressedBlockOutput([.. RawLiterals("abc"), 0x00])));

    [TestMethod]
    public void Decompress_RawLiteralsWithTwelveBitSize_GivesTheLiterals()
    {
        // Type 0, size format 1: two bytes, the size in bits 4 to 15 (40 = 0x028).
        string text = new('x', 40);
        byte[] literals = [0x04 | 0x80, 0x02, .. Bytes(text)];

        Assert.AreEqual(text, Text(CompressedBlockOutput([.. literals, 0x00])));
    }

    [TestMethod]
    public void Decompress_RawLiteralsWithTwentyBitSize_GivesTheLiterals()
    {
        // Type 0, size format 3: three bytes, the size in bits 4 to 23.
        string text = new('y', 40);
        byte[] literals = [0x0C | 0x80, 0x02, 0x00, .. Bytes(text)];

        Assert.AreEqual(text, Text(CompressedBlockOutput([.. literals, 0x00])));
    }

    [TestMethod]
    public void Decompress_RleLiterals_RepeatsTheByte() =>
        Assert.AreEqual("zzzzz", Text(CompressedBlockOutput([(5 << 3) | 1, (byte)'z', 0x00])));

    [TestMethod]
    public void Decompress_HuffmanLiteralsOneStream_DecodesTheSymbols()
    {
        // Symbols 2, 0, 1 are the bits 1 00 01 under the end marker: 0x31.
        byte[] payload = [.. HuffmanTree, 0x31];

        CollectionAssert.AreEqual(new byte[] { 2, 0, 1 }, CompressedBlockOutput([.. CompressedLiteralsHeader(2, 0, 3, payload.Length), .. payload, 0x00]));
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public void Decompress_HuffmanLiteralsFourStreams_DecodesEachStream(int sizeFormat)
    {
        CollectionAssert.AreEqual(new byte[] { 2, 0, 1, 2 }, CompressedBlockOutput([.. FourStreamLiterals(sizeFormat, 4), 0x00]));
    }

    [TestMethod]
    public void Decompress_TreelessLiterals_ReuseTheEarlierTable()
    {
        byte[] first = [.. HuffmanTree, 0x31];
        byte[] block1 = [.. CompressedLiteralsHeader(2, 0, 3, first.Length), .. first, 0x00];
        // Treeless (type 3): only the stream, the same symbols 2, 0, 1.
        byte[] block2 = [.. CompressedLiteralsHeader(3, 0, 3, 1), 0x31, 0x00];

        byte[] output = ZstdFrameDecompressor.Decompress(Frame(Block(Compressed, false, block1), Block(Compressed, true, block2)));

        CollectionAssert.AreEqual(new byte[] { 2, 0, 1, 2, 0, 1 }, output);
    }

    [TestMethod]
    public void Decompress_TreelessLiteralsWithoutAnEarlierTable_ThrowsFormatExceptionNamingZstd() =>
        AssertRejected(Frame(Block(Compressed, true, [.. CompressedLiteralsHeader(3, 0, 3, 1), 0x31, 0x00])));

    [TestMethod]
    public void Decompress_LiteralsProblems_ThrowFormatExceptionNamingZstd()
    {
        AssertRejected(Frame(Block(Compressed, true, [])));
        AssertRejected(Frame(Block(Compressed, true, [0x04])));
        AssertRejected(Frame(Block(Compressed, true, [(5 << 3) | 1])));
        AssertRejected(Frame(Block(Compressed, true, [(5 << 3), 0x61])));
        AssertRejected(Frame(Block(Compressed, true, [0x0A])));
        AssertRejected(Frame(Block(Compressed, true, CompressedLiteralsHeader(2, 0, 3, 0))));
        AssertRejected(Frame(Block(Compressed, true, [.. CompressedLiteralsHeader(2, 0, 3, 9), .. HuffmanTree])));
        // Two symbols asked of a stream holding three: the stream does not end where its symbols do.
        AssertRejected(Frame(Block(Compressed, true, [.. CompressedLiteralsHeader(2, 0, 2, 3), .. HuffmanTree, 0x31, 0x00])));
    }

    [TestMethod]
    public void Decompress_FourStreamProblems_ThrowFormatExceptionNamingZstd()
    {
        // Payload (tree plus streams) shorter than the six-byte jump table.
        AssertRejected(Frame(Block(Compressed, true, [.. CompressedLiteralsHeader(2, 1, 4, 5), .. HuffmanTree, 0x01, 0x01, 0x01])));
        // One symbol cannot fill four streams.
        AssertRejected(Frame(Block(Compressed, true, [.. FourStreamLiterals(1, 1), 0x00])));
        // First stream longer than the payload.
        byte[] longStream = [.. HuffmanTree, 0x09, 0x00, 0x01, 0x00, 0x01, 0x00, 0x03, 0x04, 0x05, 0x03];
        AssertRejected(Frame(Block(Compressed, true, [.. CompressedLiteralsHeader(2, 1, 4, longStream.Length), .. longStream, 0x00])));
        // Last stream empty: it has no end marker.
        byte[] emptyLast = [.. HuffmanTree, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x03, 0x04, 0x05];
        AssertRejected(Frame(Block(Compressed, true, [.. CompressedLiteralsHeader(2, 1, 4, emptyLast.Length), .. emptyLast, 0x00])));
    }

    [TestMethod]
    public void Execute_RleTablesWithAnOffsetCode_CopiesTheMatchThenTheRestOfTheLiterals()
    {
        // LL code 3 = 3 literals, OF code 2 with extra bits 10 = value 6 = offset 3, ML code 0 = length 3.
        List<byte> output = Run(new ZstdSequences(), RleSection(1, 3, 2, 0, (2, 2)), Bytes("abcd"));

        Assert.AreEqual("abcabcd", Text([.. output]));
    }

    [TestMethod]
    public void Decompress_CompressedBlockWithSequences_ExecutesThem() =>
        Assert.AreEqual("abcabcd", Text(CompressedBlockOutput([.. RawLiterals("abcd"), .. RleSection(1, 3, 2, 0, (2, 2))])));

    [TestMethod]
    public void Execute_MatchOverlappingItsOwnOutput_RepeatsTheByte()
    {
        // OF code 0 = value 1 = recent offset 1; ML code 5 = length 8.
        List<byte> output = Run(new ZstdSequences(), RleSection(1, 1, 0, 5, []), Bytes("a"));

        Assert.AreEqual("aaaaaaaaa", Text([.. output]));
    }

    [TestMethod]
    public void Execute_LargeLiteralAndMatchLengthCodes_AddTheirBaselineAndExtraBits()
    {
        // LL code 16 = 16 + 1 extra bit (1) = 17 literals; ML code 32 = 35 + 1 extra bit (1) = 36; OF code 0.
        List<byte> output = Run(new ZstdSequences(), RleSection(1, 16, 0, 32, (1, 1), (1, 1)), new byte[17]);

        Assert.AreEqual(17 + 36, output.Count);
    }

    [TestMethod]
    public void Execute_RecentOffsets_FollowTheRepeatRules()
    {
        ZstdSequences sequences = new();
        List<byte> output = [];
        for (int index = 0; index < 20; index++)
        {
            output.Add((byte)((index * 13) + 1));
        }

        List<byte> expected = [.. output];

        // Initial recent offsets are 1, 4, 8. Each step is (literal count, offset value, offset it must resolve to).
        (int Literals, int Value, int Offset)[] steps =
        [
            (0, 8, 5),
            (0, 1, 1),
            (1, 3, 4),
            (0, 3, 3),
            (1, 2, 4),
            (1, 1, 4),
            (0, 2, 1),
        ];
        foreach ((int literalCount, int value, int offset) in steps)
        {
            byte[] literals = Enumerable.Repeat((byte)0xE0, literalCount).ToArray();
            int code = BitOperations.Log2((uint)value);
            Run(sequences, RleSection(1, literalCount, code, 0, ((uint)(value - (1 << code)), code)), literals, output);
            expected.AddRange(literals);
            for (int copied = 0; copied < 3; copied++)
            {
                expected.Add(expected[^offset]);
            }

            CollectionAssert.AreEqual(expected, output);
        }
    }

    [TestMethod]
    public void Execute_RepeatOffsetOfZero_ThrowsFormatExceptionNamingZstd() =>
        AssertSequencesRejected(RleSection(1, 0, 1, 0, (1, 1)), [], [1]);

    [TestMethod]
    public void Execute_OffsetBeforeTheStartOfTheOutput_ThrowsFormatExceptionNamingZstd() =>
        AssertSequencesRejected(RleSection(1, 1, 2, 0, (2, 2)), Bytes("a"), []);

    [TestMethod]
    public void Execute_OffsetAboveTheIntegerRange_ThrowsFormatExceptionNamingZstd() =>
        AssertSequencesRejected(RleSection(1, 1, 31, 0, (0, 31)), Bytes("a"), []);

    [TestMethod]
    public void Execute_TwoByteSequenceCount_RunsEverySequence()
    {
        // First byte 128 and second 128 give 128 sequences; each is a repeat offset (no literals), length 3, no literals.
        byte[] section = [128, 128, 0x54, 0, 0, 0, 0x01];

        Assert.AreEqual(8 + (128 * 3), RunAfter(new ZstdSequences(), section, [], new byte[8]).Count);
    }

    [TestMethod]
    public void Execute_ThreeByteSequenceCount_RunsEverySequence()
    {
        // First byte 255 then 0, 0 give 0x7F00 = 32512 sequences.
        byte[] section = [255, 0, 0, 0x54, 0, 0, 0, 0x01];

        Assert.AreEqual(8 + (32512 * 3), RunAfter(new ZstdSequences(), section, [], new byte[8]).Count);
    }

    [TestMethod]
    public void Execute_PredefinedTablesOneSequence_UsesTheCodesTheStatesGive()
    {
        int literalLengthState = FindState(ZstdFiniteStateEntropyTable.PredefinedLiteralLength, 6, code => code == 2);
        int offsetState = FindState(ZstdFiniteStateEntropyTable.PredefinedOffset, 5, code => code == 3);
        int matchLengthState = FindState(ZstdFiniteStateEntropyTable.PredefinedMatchLength, 6, code => code == 4);
        // Codes: 2 literals, offset value 8 (extra 000) = offset 5, match length 7.
        byte[] section = [1, 0x00, .. BackwardStream(((uint)literalLengthState, 6), ((uint)offsetState, 5), ((uint)matchLengthState, 6), (0, 3))];

        List<byte> output = RunAfter(new ZstdSequences(), section, Bytes("xy"), [.. new byte[10]]);

        Assert.AreEqual(10 + 2 + 7, output.Count);
    }

    [TestMethod]
    public void Execute_PredefinedTablesTwoSequences_UpdatesTheStates()
    {
        ZstdFiniteStateEntropyTable literalLengthTable = ZstdFiniteStateEntropyTable.PredefinedLiteralLength;
        ZstdFiniteStateEntropyTable offsetTable = ZstdFiniteStateEntropyTable.PredefinedOffset;
        ZstdFiniteStateEntropyTable matchLengthTable = ZstdFiniteStateEntropyTable.PredefinedMatchLength;
        // Offset codes 2 to 5 are real offsets, not repeat values; literal codes below 16 and match codes below 32 have no extra bits.
        Func<int, bool> literalOk = code => code < 16;
        Func<int, bool> offsetOk = code => code is >= 2 and <= 5;
        Func<int, bool> matchOk = code => code < 32;
        int literalLengthState = FindState(literalLengthTable, 6, literalOk);
        int offsetState = FindState(offsetTable, 5, offsetOk);
        int matchLengthState = FindState(matchLengthTable, 6, matchOk);
        (uint Value, int Count) literalUpdate = FindUpdate(literalLengthTable, literalLengthState, literalOk, out int literalCode2);
        (uint Value, int Count) matchUpdate = FindUpdate(matchLengthTable, matchLengthState, matchOk, out int matchCode2);
        (uint Value, int Count) offsetUpdate = FindUpdate(offsetTable, offsetState, offsetOk, out int offsetCode2);
        int literalCode1 = literalLengthTable.Symbol(literalLengthState);
        int matchCode1 = matchLengthTable.Symbol(matchLengthState);
        int offsetCode1 = offsetTable.Symbol(offsetState);
        byte[] section =
        [
            2,
            0x00,
            .. BackwardStream(
                ((uint)literalLengthState, 6),
                ((uint)offsetState, 5),
                ((uint)matchLengthState, 6),
                (0, offsetCode1),
                literalUpdate,
                matchUpdate,
                offsetUpdate,
                (0, offsetCode2)),
        ];

        List<byte> output = RunAfter(new ZstdSequences(), section, new byte[literalCode1 + literalCode2], [.. new byte[100]]);

        Assert.AreEqual(100 + literalCode1 + literalCode2 + (matchCode1 + 3) + (matchCode2 + 3), output.Count);
    }

    [TestMethod]
    public void Execute_FseCompressedLiteralLengthTable_ReadsTheTableFromTheSection()
    {
        int state = FindState(ZstdFiniteStateEntropyTable.Read(FseTable, 9, 35, out _), 5, code => code == 1);
        // LL = FSE table (symbol 1 = 1 literal), OF = RLE code 0, ML = RLE code 0 (length 3).
        byte[] section = [1, 0x94, .. FseTable, 0, 0, .. BackwardStream(((uint)state, 5))];

        List<byte> output = Run(new ZstdSequences(), section, Bytes("a"));

        Assert.AreEqual("aaaa", Text([.. output]));
    }

    [TestMethod]
    public void Execute_RepeatMode_ReusesTheEarlierTables()
    {
        ZstdSequences sequences = new();
        List<byte> output = Run(sequences, RleSection(1, 1, 0, 0, []), Bytes("a"));

        // Modes 0xFC = repeat all three tables; their states are zero bits.
        Run(sequences, [1, 0xFC, 0x01], Bytes("b"), output);

        Assert.AreEqual("aaaabbbb", Text([.. output]));
    }

    [TestMethod]
    public void Execute_SectionProblems_ThrowFormatExceptionNamingZstd()
    {
        AssertSequencesRejected([], [], []);
        AssertSequencesRejected([128], [], []);
        AssertSequencesRejected([1], [], []);
        AssertSequencesRejected([1, 0x55, 0, 0, 0, 0x01], [], []);
        AssertSequencesRejected([1, 0xFC, 0x01], [], []);
        AssertSequencesRejected([1, 0x54, 36, 0, 0, 0x01], [], []);
        AssertSequencesRejected([1, 0x54, 0, 0], [], []);
        AssertSequencesRejected([1, 0x00, 0x00, 0x00, 0x00], [], []);
        AssertSequencesRejected([1, 0x00], [], []);
        AssertSequencesRejected(RleSection(1, 0, 1, 0, (1, 0)), [], [1]);
        AssertSequencesRejected(RleSection(1, 3, 0, 0, []), Bytes("ab"), [1]);
        AssertSequencesRejected(RleSection(1, 0, 0, 0, (1, 1)), [], new byte[8]);
        AssertSequencesRejected(RleSection(1, 0, 2, 0, []), [], [1]);
    }

    private static byte[] Bytes(string text) => Encoding.ASCII.GetBytes(text);

    private static string Text(byte[] bytes) => Encoding.ASCII.GetString(bytes);

    private static byte[] Block(int type, bool last, byte[] content, int? size = null)
    {
        int declared = size ?? content.Length;
        uint header = (uint)((declared << 3) | (type << 1) | (last ? 1 : 0));
        return [(byte)header, (byte)(header >> 8), (byte)(header >> 16), .. content];
    }

    private static byte[] Frame(params byte[][] blocks) => [.. PlainHeader, .. blocks.SelectMany(block => block)];

    private static byte[] RawLiterals(string text) => [(byte)(text.Length << 3), .. Bytes(text)];

    private static byte[] CompressedBlockOutput(byte[] content) => ZstdFrameDecompressor.Decompress(Frame(Block(Compressed, true, content)));

    private static byte[] CompressedLiteralsHeader(int type, int sizeFormat, int regenerated, int compressed)
    {
        int sizeBits = sizeFormat switch { 2 => 14, 3 => 18, _ => 10 };
        int length = sizeFormat switch { 2 => 4, 3 => 5, _ => 3 };
        long value = (long)type | (long)(sizeFormat << 2) | ((long)regenerated << 4) | ((long)compressed << (4 + sizeBits));
        byte[] header = new byte[length];
        for (int index = 0; index < length; index++)
        {
            header[index] = (byte)(value >> (8 * index));
        }

        return header;
    }

    // Four streams, each one symbol (2, 0, 1, 2 for four literals): 1 -> 0x03, 00 -> 0x04, 01 -> 0x05, each under the marker bit.
    private static byte[] FourStreamLiterals(int sizeFormat, int regenerated)
    {
        byte[] payload = [.. HuffmanTree, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x03, 0x04, 0x05, 0x03];
        return [.. CompressedLiteralsHeader(2, sizeFormat, regenerated, payload.Length), .. payload];
    }

    // The stream is read backward: the first field is the first read, the most significant bits just under the marker.
    private static byte[] BackwardStream(params (uint Value, int Count)[] fields)
    {
        int total = fields.Sum(field => field.Count);
        byte[] bytes = new byte[(total + 8) / 8];
        bytes[total >> 3] |= (byte)(1 << (total & 7));
        int index = total - 1;
        foreach ((uint value, int count) in fields)
        {
            for (int bit = count - 1; bit >= 0; bit--)
            {
                if (((value >> bit) & 1) != 0)
                {
                    bytes[index >> 3] |= (byte)(1 << (index & 7));
                }

                index--;
            }
        }

        return bytes;
    }

    // One count byte below 128, modes 0x54 (RLE for all three), the three symbols, then the extra bits in read order.
    private static byte[] RleSection(int count, int literalLengthCode, int offsetCode, int matchLengthCode, params (uint Value, int Count)[] extraBits) =>
        [(byte)count, 0x54, (byte)literalLengthCode, (byte)offsetCode, (byte)matchLengthCode, .. BackwardStream(extraBits)];

    private static List<byte> Run(ZstdSequences sequences, byte[] section, byte[] literals, List<byte>? output = null)
    {
        output ??= [];
        sequences.Execute(section, literals, output);
        return output;
    }

    private static List<byte> RunAfter(ZstdSequences sequences, byte[] section, byte[] literals, byte[] earlierOutput) =>
        Run(sequences, section, literals, new List<byte>(earlierOutput));

    private static int FindState(ZstdFiniteStateEntropyTable table, int accuracyLog, Func<int, bool> wanted)
    {
        for (int state = 0; state < 1 << accuracyLog; state++)
        {
            if (wanted(table.Symbol(state)))
            {
                return state;
            }
        }

        Assert.Fail("no state gives a wanted symbol");
        return -1;
    }

    // Tries bit patterns until the state after reading them gives a wanted symbol; gives the bits the update reads.
    private static (uint Value, int Count) FindUpdate(ZstdFiniteStateEntropyTable table, int state, Func<int, bool> wanted, out int symbol)
    {
        for (uint pattern = 0; pattern < 256; pattern++)
        {
            ZstdBackwardBitReader reader = new(BackwardStream((pattern, 8)));
            int next = table.NextState(state, reader);
            if (wanted(table.Symbol(next)))
            {
                int consumed = 8 - reader.RemainingBits;
                symbol = table.Symbol(next);
                return (pattern >> (8 - consumed), consumed);
            }
        }

        Assert.Fail("no update gives a wanted symbol");
        symbol = -1;
        return (0, 0);
    }

    private static void AssertRejected(byte[] input)
    {
        FormatException exception = Assert.ThrowsExactly<FormatException>(() => ZstdFrameDecompressor.Decompress(input));

        StringAssert.Contains(exception.Message, "zstd");
    }

    private static void AssertSequencesRejected(byte[] section, byte[] literals, byte[] earlierOutput)
    {
        FormatException exception = Assert.ThrowsExactly<FormatException>(() => RunAfter(new ZstdSequences(), section, literals, earlierOutput));

        StringAssert.Contains(exception.Message, "zstd");
    }
}
