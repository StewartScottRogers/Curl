using System.Buffers;
using System.Text;
using Curl.Testing;
using static Curl.Zstandard.ZstandardTestFrames;

namespace Curl.Zstandard;

/// <summary>
/// Attacks <see cref="ZstandardDecoder" />'s public surface by the method in
/// <c>Documentation/Wiki/Adversarial-Testing.md</c> (BL-1524): window, block and content-size
/// fields at and one past their limits, truncated and bit-flipped frames, every invalid
/// partition of the frame header, decompression bombs, and decoders reused, interleaved and
/// run in parallel. The oracle is RFC 8878 and the decoder's documented contract: a refusal
/// it names, never an exception, a hang or output that differs from the frame's content.
/// </summary>
[TestClass]
public sealed class ZstandardDecoderAdversarialTests
{
    /// <summary>A <c>Window_Descriptor</c> of exponent 7, mantissa 0: a 128 KiB window, so a block may be 128 KiB.</summary>
    private const byte OneHundredTwentyEightKibibyteWindow = 0x38;

    private const int BlockSizeLimit = 128 * 1024;

    /// <summary>
    /// "Hello, world" in a single-segment frame with a <c>Content_Checksum</c>, as curl 8.21.0
    /// with libzstd 1.5.7 decodes it (the measured frame in <see cref="ZstandardDecoderTests" />).
    /// </summary>
    private const string MeasuredHelloWorldFrame = "28B52FFD240C61000048656C6C6F2C20776F726C64D7B42074";

    private static readonly byte[] HelloWorld = Encoding.ASCII.GetBytes("Hello, world");

    public TestContext TestContext { get; set; } = null!;

    public static IEnumerable<object[]> BoundaryFrames =>
    [
        ["RLE block exactly the 1 KiB window", Concatenate(FrameHeader(0x00, OneKibibyteWindow), RleBlock(true, 1, 1024)), Repeat(1, 1024)],
        ["raw block exactly 128 KiB", Concatenate(FrameHeader(0x00, OneHundredTwentyEightKibibyteWindow), RawBlock(true, Repeat(2, BlockSizeLimit))), Repeat(2, BlockSizeLimit)],
        ["2-byte content size of its smallest value, 256", Concatenate(FrameHeader(0x40, OneKibibyteWindow, 0, 0), RleBlock(true, 3, 256)), Repeat(3, 256)],
        ["2-byte content size of its largest value, 65791", Concatenate(FrameHeader(0x40, 0x30, 0xFF, 0xFF), RleBlock(false, 4, 65536), RleBlock(true, 5, 255)), Concatenate(Repeat(4, 65536), Repeat(5, 255))],
        ["single-segment frame declaring 0 bytes with an empty raw block", Concatenate(FrameHeader(0x20, 0), RawBlock(true, [])), Array.Empty<byte>()],
        ["empty compressed block before the last block", Concatenate(FrameHeader(0x00, OneKibibyteWindow), BlockHeader(false, CompressedBlockType, 0), RawBlock(true, HelloWorld)), HelloWorld],
        ["4-byte zero dictionary ID", Concatenate(FrameHeader(0x03, OneKibibyteWindow, 0, 0, 0, 0), RawBlock(true, HelloWorld)), HelloWorld],
        ["skippable frames of every magic nibble", Concatenate([.. Enumerable.Range(0, 16).Select(nibble => SkippableFrame(nibble, [(byte)nibble]))]), Array.Empty<byte>()],
    ];

    public static IEnumerable<object[]> OnePastBoundaryFrames =>
    [
        ["raw block one past 128 KiB", Concatenate(FrameHeader(0x00, OneHundredTwentyEightKibibyteWindow), BlockHeader(true, RawBlockType, BlockSizeLimit + 1)), ZstandardDecodeError.CorruptionDetected, ZstandardDecoder.DefaultMaxWindowLog],
        ["compressed block one past 128 KiB", Concatenate(FrameHeader(0x00, 0x40), BlockHeader(true, CompressedBlockType, BlockSizeLimit + 1)), ZstandardDecodeError.CorruptionDetected, ZstandardDecoder.DefaultMaxWindowLog],
        ["RLE block one short of a 2-byte content size of 256", Concatenate(FrameHeader(0x40, OneKibibyteWindow, 0, 0), RleBlock(true, 3, 255)), ZstandardDecodeError.CorruptionDetected, ZstandardDecoder.DefaultMaxWindowLog],
        ["single-segment frame declaring 0 bytes with a 1-byte raw block", Concatenate(FrameHeader(0x20, 0), RawBlock(true, [9])), ZstandardDecodeError.CorruptionDetected, ZstandardDecoder.DefaultMaxWindowLog],
        ["window one mantissa step past 2^31", Concatenate(FrameHeader(0x00, 0xA9), RawBlock(true, [])), ZstandardDecodeError.FrameParameterWindowTooLarge, ZstandardDecoder.MaximumMaxWindowLog],
        ["largest window descriptor, 2^41 and seven eighths", Concatenate(FrameHeader(0x00, 0xFF), RawBlock(true, [])), ZstandardDecodeError.FrameParameterWindowTooLarge, ZstandardDecoder.MaximumMaxWindowLog],
        ["window 2^28 one exponent past the default limit", Concatenate(FrameHeader(0x00, 0x90), RawBlock(true, [])), ZstandardDecodeError.FrameParameterWindowTooLarge, ZstandardDecoder.DefaultMaxWindowLog],
        ["single-segment 8-byte content size of ulong.MaxValue", Concatenate(FrameHeader(0xE0, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF), RawBlock(true, [])), ZstandardDecodeError.FrameParameterWindowTooLarge, ZstandardDecoder.MaximumMaxWindowLog],
        ["8-byte content size of ulong.MaxValue and one byte of content", Concatenate(FrameHeader(0xC0, OneKibibyteWindow, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF), RawBlock(true, [1])), ZstandardDecodeError.CorruptionDetected, ZstandardDecoder.DefaultMaxWindowLog],
        ["largest 4-byte dictionary ID", Concatenate(FrameHeader(0x03, OneKibibyteWindow, 0xFF, 0xFF, 0xFF, 0xFF), RawBlock(true, [])), ZstandardDecodeError.DictionaryWrong, ZstandardDecoder.DefaultMaxWindowLog],
        ["1-byte dictionary ID of 1", Concatenate(FrameHeader(0x01, OneKibibyteWindow, 1), RawBlock(true, [])), ZstandardDecodeError.DictionaryWrong, ZstandardDecoder.DefaultMaxWindowLog],
    ];

    public static IEnumerable<object[]> InvalidPartitionFrames =>
    [
        ["magic with its bytes reversed", new byte[] { 0xFD, 0x2F, 0xB5, 0x28 }, ZstandardDecodeError.PrefixUnknown],
        ["magic one below the Zstandard magic", new byte[] { 0x27, 0xB5, 0x2F, 0xFD }, ZstandardDecodeError.PrefixUnknown],
        ["skippable magic with a wrong high byte", new byte[] { 0x50, 0x2A, 0x4D, 0x19 }, ZstandardDecodeError.PrefixUnknown],
        ["reserved bit with every other descriptor bit set", Concatenate(FrameHeader(0xFF)), ZstandardDecodeError.FrameParameterUnsupported],
        ["checksum with every byte wrong", Concatenate(FrameHeader(0x04, OneKibibyteWindow), RawBlock(true, HelloWorld), [.. Checksum(HelloWorld).Select(value => (byte)~value)]), ZstandardDecodeError.ChecksumWrong],
        ["checksum of empty content where content was given", Concatenate(FrameHeader(0x04, OneKibibyteWindow), RawBlock(true, HelloWorld), Checksum([])), ZstandardDecodeError.ChecksumWrong],
        ["reserved block type after a good block", Concatenate(FrameHeader(0x00, OneKibibyteWindow), RawBlock(false, HelloWorld), BlockHeader(true, ReservedBlockType, 1)), ZstandardDecodeError.CorruptionDetected],
        ["compressed block of all 0xFF", Concatenate(FrameHeader(0x00, OneKibibyteWindow), BlockHeader(true, CompressedBlockType, 64), Repeat(0xFF, 64)), ZstandardDecodeError.CorruptionDetected],
        ["garbage where the next frame's magic should be", Concatenate(FrameHeader(0x00, OneKibibyteWindow), RawBlock(true, HelloWorld), [0x00, 0x00, 0x00, 0x00]), ZstandardDecodeError.PrefixUnknown],
    ];

    [TestMethod]
    [DynamicData(nameof(BoundaryFrames))]
    public void TryDecompress_FieldExactlyAtItsLimit_WritesTheContent(string name, byte[] source, byte[] expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.ArrangeSource(name, source);
        var destination = new byte[expected.Length];

        var decoded = ZstandardDecoder.TryDecompress(source, destination, out var bytesWritten);

        diagnostics.Act("decoded", decoded);
        diagnostics.ActOutput(expected, destination.AsSpan(0, bytesWritten));
        diagnostics.Assert("decoded", true, decoded);
        Assert.IsTrue(decoded, name);
        Assert.AreEqual(expected.Length, bytesWritten, name);
        CollectionAssert.AreEqual(expected, destination, name);
    }

    [TestMethod]
    [DynamicData(nameof(OnePastBoundaryFrames))]
    public void Decompress_FieldOnePastItsLimit_IsInvalidDataWithTheNamedError(string name, byte[] source, ZstandardDecodeError expected, int maxWindowLog)
    {
        AssertInvalidData(name, source, expected, maxWindowLog);
    }

    [TestMethod]
    [DynamicData(nameof(InvalidPartitionFrames))]
    public void Decompress_InvalidPartition_IsInvalidDataWithTheNamedError(string name, byte[] source, ZstandardDecodeError expected)
    {
        AssertInvalidData(name, source, expected, ZstandardDecoder.DefaultMaxWindowLog);
    }

    [TestMethod]
    [DataRow(ZstandardDecoder.MinimumMaxWindowLog)]
    [DataRow(ZstandardDecoder.MaximumMaxWindowLog)]
    public void Constructor_MaxWindowLogExactlyAtItsLimit_DecodesAOneKibibyteWindowFrame(int maxWindowLog)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("max window log", maxWindowLog);
        var source = Concatenate(FrameHeader(0x00, OneKibibyteWindow), RleBlock(true, 6, 1024));
        var destination = new byte[1024];

        var status = new ZstandardDecoder(maxWindowLog).Decompress(source, destination, out var consumed, out var written);

        diagnostics.Act("status", status);
        diagnostics.Act("written", written);
        diagnostics.Assert("status", OperationStatus.Done, status);
        Assert.AreEqual(OperationStatus.Done, status);
        Assert.AreEqual(source.Length, consumed);
        Assert.AreEqual(1024, written);
    }

    [TestMethod]
    public void Decompress_EmptySourceAndDestinationOnANewDecoder_NeedsMoreDataWithoutFailing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var decoder = new ZstandardDecoder();

        var status = decoder.Decompress([], [], out var consumed, out var written);

        diagnostics.Act("status", status);
        diagnostics.Act("error raised", decoder.LastError);
        diagnostics.Assert("status", OperationStatus.NeedMoreData, status);
        Assert.AreEqual(OperationStatus.NeedMoreData, status);
        Assert.AreEqual(ZstandardDecodeError.None, decoder.LastError);
        Assert.AreEqual(0, consumed);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    [DataRow(RawBlockType)]
    [DataRow(RleBlockType)]
    public void Decompress_EmptyDestinationRepeatedlyInsideABlock_IsDestinationTooSmallUntilRoomIsGiven(int blockType)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var block = blockType == RawBlockType ? RawBlock(true, HelloWorld) : RleBlock(true, (byte)'z', HelloWorld.Length);
        var expected = blockType == RawBlockType ? HelloWorld : Repeat((byte)'z', HelloWorld.Length);
        var source = Concatenate(FrameHeader(0x00, OneKibibyteWindow), block);
        diagnostics.ArrangeSource("frame", source);
        var decoder = new ZstandardDecoder();
        var position = 0;

        var statuses = new List<OperationStatus>();
        for (var call = 0; call < 3; call++)
        {
            statuses.Add(decoder.Decompress(source.AsSpan(position), [], out var consumed, out var written));
            position += consumed;
            Assert.AreEqual(0, written, $"call {call}");
        }

        var destination = new byte[expected.Length];
        var last = decoder.Decompress(source.AsSpan(position), destination, out var lastConsumed, out var lastWritten);

        diagnostics.Act("statuses with no destination", string.Join(", ", statuses));
        diagnostics.Act("last status", last);
        diagnostics.ActOutput(expected, destination.AsSpan(0, lastWritten));
        CollectionAssert.AreEqual(Enumerable.Repeat(OperationStatus.DestinationTooSmall, 3).ToArray(), statuses);
        Assert.AreEqual(OperationStatus.Done, last);
        Assert.AreEqual(source.Length, position + lastConsumed);
        CollectionAssert.AreEqual(expected, destination);
    }

    [TestMethod]
    public void Decompress_EveryProperPrefixOfALibzstdFrame_NeedsMoreDataAndNeverFails()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var source = ReadResource("readme-level3.zst");
        diagnostics.ArrangeSource("readme-level3.zst", source);
        var destination = new byte[4096];

        var firstUnexpected = -1;
        var unexpectedStatus = OperationStatus.NeedMoreData;
        for (var length = 0; length < source.Length && firstUnexpected < 0; length++)
        {
            var status = new ZstandardDecoder().Decompress(source.AsSpan(0, length), destination, out _, out _);
            if (status != OperationStatus.NeedMoreData)
            {
                firstUnexpected = length;
                unexpectedStatus = status;
            }
        }

        diagnostics.Act("first prefix length that did not need more data", firstUnexpected);
        diagnostics.Act("its status", unexpectedStatus);
        diagnostics.Assert("first prefix length that did not need more data", -1, firstUnexpected);
        Assert.AreEqual(-1, firstUnexpected, $"prefix of {firstUnexpected} bytes returned {unexpectedStatus}");
    }

    [TestMethod]
    public void TryDecompress_EveryProperPrefixOfAFrameWithAChecksum_ReturnsFalse()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var source = Convert.FromHexString(MeasuredHelloWorldFrame);
        diagnostics.ArrangeSource("measured frame", source);

        var accepted = Enumerable.Range(1, source.Length - 1)
            .Where(length => ZstandardDecoder.TryDecompress(source.AsSpan(0, length), new byte[64], out _))
            .ToArray();

        diagnostics.Act("prefix lengths accepted", string.Join(", ", accepted));
        diagnostics.Assert("prefix lengths accepted", string.Empty, string.Join(", ", accepted));
        Assert.IsEmpty(accepted);
    }

    [TestMethod]
    public void Decompress_FrameSplitIntoTwoCallsAtEveryOffset_WritesTheSameContent()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var source = Convert.FromHexString(MeasuredHelloWorldFrame);
        diagnostics.ArrangeSource("measured frame", source);

        var failedSplits = new List<string>();
        for (var split = 0; split <= source.Length; split++)
        {
            var decoder = new ZstandardDecoder();
            var destination = new byte[HelloWorld.Length];
            var status = decoder.Decompress(source.AsSpan(0, split), destination, out var firstConsumed, out var firstWritten);
            var secondWritten = 0;
            if (status != OperationStatus.Done)
            {
                status = decoder.Decompress(source.AsSpan(firstConsumed), destination.AsSpan(firstWritten), out _, out secondWritten);
            }

            if (status != OperationStatus.Done || firstWritten + secondWritten != HelloWorld.Length || !destination.SequenceEqual(HelloWorld))
            {
                failedSplits.Add($"{split}: {status}, {firstWritten}+{secondWritten} bytes");
            }
        }

        diagnostics.Act("failed splits", string.Join("; ", failedSplits));
        diagnostics.Assert("failed splits", string.Empty, string.Join("; ", failedSplits));
        Assert.IsEmpty(failedSplits);
    }

    [TestMethod]
    public void TryDecompress_EverySingleBitFlipOfAFrameWithAChecksum_NeverThrowsAndNeverYieldsOtherContent()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var source = Convert.FromHexString(MeasuredHelloWorldFrame);
        diagnostics.ArrangeSource("measured frame", source);

        var wrongContent = new List<int>();
        for (var bit = 0; bit < source.Length * 8; bit++)
        {
            var flipped = (byte[])source.Clone();
            flipped[bit / 8] ^= (byte)(1 << (bit % 8));
            var destination = new byte[256];
            if (ZstandardDecoder.TryDecompress(flipped, destination, out var written) && !destination.AsSpan(0, written).SequenceEqual(HelloWorld))
            {
                wrongContent.Add(bit);
            }
        }

        diagnostics.Act("bits whose flip decoded to other content", string.Join(", ", wrongContent));
        diagnostics.Assert("bits whose flip decoded to other content", string.Empty, string.Join(", ", wrongContent));
        Assert.IsEmpty(wrongContent);
    }

    [TestMethod]
    [DataRow("readme-level3.zst", 2765, 20261007)]
    [DataRow("source-level19.zst", 61181, 1524)]
    public void TryDecompress_RandomBitFlipsInACompressedLibzstdFrame_NeverThrowAndNeverYieldOtherContent(string name, int contentLength, int seed)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var source = ReadResource(name);
        diagnostics.ArrangeSource(name, source);
        diagnostics.Arrange("seed", seed);
        var original = new byte[contentLength];
        Assert.IsTrue(ZstandardDecoder.TryDecompress(source, original, out _), "the unflipped frame decodes");
        var random = new Random(seed);
        var destination = new byte[contentLength];

        var wrongContent = new List<string>();
        for (var trial = 0; trial < 200; trial++)
        {
            var flipped = (byte[])source.Clone();
            var flips = random.Next(1, 4);
            var description = new List<int>();
            for (var flip = 0; flip < flips; flip++)
            {
                var bit = random.Next(source.Length * 8);
                flipped[bit / 8] ^= (byte)(1 << (bit % 8));
                description.Add(bit);
            }

            if (ZstandardDecoder.TryDecompress(flipped, destination, out var written)
                && !destination.AsSpan(0, written).SequenceEqual(original))
            {
                wrongContent.Add($"trial {trial}, bits {string.Join("/", description)}");
            }
        }

        diagnostics.Act("flips that decoded to other content", string.Join("; ", wrongContent));
        diagnostics.Assert("flips that decoded to other content", string.Empty, string.Join("; ", wrongContent));
        Assert.IsEmpty(wrongContent);
    }

    [TestMethod]
    public void Decompress_RandomCompressedBlockBodies_NeverThrowAndNameEveryRefusal()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const int seed = 1524;
        diagnostics.Arrange("seed", seed);
        var random = new Random(seed);
        var destination = new byte[BlockSizeLimit];

        var unnamedRefusals = new List<string>();
        for (var trial = 0; trial < 500; trial++)
        {
            var body = new byte[random.Next(1, 300)];
            random.NextBytes(body);
            var source = Concatenate(FrameHeader(0x00, OneHundredTwentyEightKibibyteWindow), BlockHeader(true, CompressedBlockType, body.Length), body);
            var decoder = new ZstandardDecoder();
            var status = decoder.Decompress(source, destination, out _, out _);
            var named = status == OperationStatus.InvalidData ? decoder.LastError != ZstandardDecodeError.None : decoder.LastError == ZstandardDecodeError.None;
            if (!named || status is OperationStatus.NeedMoreData)
            {
                unnamedRefusals.Add($"trial {trial}: {status}, {decoder.LastError}");
            }
        }

        diagnostics.Act("trials without a named outcome", string.Join("; ", unnamedRefusals));
        diagnostics.Assert("trials without a named outcome", string.Empty, string.Join("; ", unnamedRefusals));
        Assert.IsEmpty(unnamedRefusals);
    }

    [TestMethod]
    public void Decompress_SkippableFrameDeclaringUIntMaxValueBytes_NeedsMoreDataAndTryDecompressReturnsFalse()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] source = [0x50, 0x2A, 0x4D, 0x18, 0xFF, 0xFF, 0xFF, 0xFF, 1, 2, 3];
        diagnostics.ArrangeSource("skippable frame of 4 GiB with 3 bytes given", source);
        var decoder = new ZstandardDecoder();

        var status = decoder.Decompress(source, new byte[16], out var consumed, out var written);
        var decoded = ZstandardDecoder.TryDecompress(source, new byte[16], out _);

        diagnostics.Act("status", status);
        diagnostics.Act("consumed", consumed);
        diagnostics.Act("decoded", decoded);
        diagnostics.Assert("status", OperationStatus.NeedMoreData, status);
        Assert.AreEqual(OperationStatus.NeedMoreData, status);
        Assert.AreEqual(source.Length, consumed);
        Assert.AreEqual(0, written);
        Assert.IsFalse(decoded);
    }

    [TestMethod]
    public void Decompress_DecompressionBombOf400RleBlocks_StreamsFiftyMebibytesInBoundedMemory()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const int blocks = 400;
        var source = Concatenate(
        [
            FrameHeader(0x00, OneHundredTwentyEightKibibyteWindow),
            .. Enumerable.Range(0, blocks).Select(index => RleBlock(index == blocks - 1, (byte)index, BlockSizeLimit)),
        ]);
        diagnostics.Arrange("source length", source.Length);
        diagnostics.Arrange("content length", (long)blocks * BlockSizeLimit);
        var destination = new byte[64 * 1024];
        var decoder = new ZstandardDecoder();
        long total = 0;
        var position = 0;
        var wrongBytes = 0L;
        OperationStatus status;

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        do
        {
            status = decoder.Decompress(source.AsSpan(position), destination, out var consumed, out var written);
            var expected = (byte)(total / BlockSizeLimit);
            wrongBytes += destination.AsSpan(0, written).IndexOfAnyExcept(expected) >= 0 ? 1 : 0;
            position += consumed;
            total += written;
        }
        while (status == OperationStatus.DestinationTooSmall || (status == OperationStatus.NeedMoreData && position < source.Length));

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        diagnostics.Act("status", status);
        diagnostics.Act("bytes written", total);
        diagnostics.Act("bytes allocated while decoding", allocated);
        diagnostics.Act("pieces with a wrong byte", wrongBytes);
        diagnostics.Assert("bytes written", (long)blocks * BlockSizeLimit, total);
        Assert.AreEqual(OperationStatus.Done, status);
        Assert.AreEqual((long)blocks * BlockSizeLimit, total);
        Assert.AreEqual(0L, wrongBytes);
        Assert.IsLessThan(4L * 1024 * 1024, allocated, "a frame's history holds its window plus two blocks, never its whole content");
    }

    [TestMethod]
    public void Decompress_TinyFrameDeclaringTheLargestDefaultWindow_AllocatesOnlyWhatItUses()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var source = Concatenate(FrameHeader(0x00, 0x88), RleBlock(true, 1, 1));
        diagnostics.ArrangeSource("1-byte frame with a 2^27 window", source);
        var destination = new byte[1];
        var decoder = new ZstandardDecoder();

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var status = decoder.Decompress(source, destination, out _, out var written);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        diagnostics.Act("status", status);
        diagnostics.Act("bytes allocated while decoding", allocated);
        Assert.AreEqual(OperationStatus.Done, status);
        Assert.AreEqual(1, written);
        Assert.IsLessThan(1024L * 1024, allocated, "a declared 128 MiB window is not allocated up front");
    }

    [TestMethod]
    public void Decompress_AfterInvalidDataThenAValidFrame_StaysFailedAndKeepsTheFirstError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var decoder = new ZstandardDecoder();
        decoder.Decompress(Concatenate(FrameHeader(0x01, OneKibibyteWindow, 7)), new byte[16], out _, out _);

        var status = decoder.Decompress(Convert.FromHexString(MeasuredHelloWorldFrame), new byte[16], out var consumed, out var written);

        diagnostics.Act("status", status);
        diagnostics.Act("error raised", decoder.LastError);
        Assert.AreEqual(OperationStatus.InvalidData, status);
        Assert.AreEqual(ZstandardDecodeError.DictionaryWrong, decoder.LastError);
        Assert.AreEqual(0, consumed);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void Decompress_OneDecoderReusedAcrossDifferentLibzstdFrames_WritesEachFramesContent()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string[] names = ["binary-level9.zst", "readme-level3.zst", "source-level19.zst", "readme-level3.zst"];
        diagnostics.Arrange("frames in order", string.Join(", ", names));
        var decoder = new ZstandardDecoder();

        foreach (var name in names)
        {
            var source = ReadResource(name);
            var expected = DecompressAlone(source);
            var output = new byte[expected.Length];
            var status = decoder.Decompress(source, output, out var consumed, out var written);

            diagnostics.Act($"{name} status", status);
            Assert.AreEqual(OperationStatus.Done, status, name);
            Assert.AreEqual(source.Length, consumed, name);
            Assert.AreEqual(expected.Length, written, name);
            CollectionAssert.AreEqual(expected, output, name);
        }
    }

    [TestMethod]
    public void Decompress_TwoDecodersFedAlternateBytes_DoNotDisturbEachOther()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var first = ReadResource("readme-level3.zst");
        var second = ReadResource("source-level-5.zst");
        var firstExpected = DecompressAlone(first);
        var secondExpected = DecompressAlone(second);
        var feeds = new[] { new Feed(first, firstExpected.Length), new Feed(second, secondExpected.Length) };

        while (feeds.Any(feed => feed.Status != OperationStatus.Done && feed.Status != OperationStatus.InvalidData))
        {
            foreach (var feed in feeds.Where(feed => feed.Status != OperationStatus.Done && feed.Status != OperationStatus.InvalidData))
            {
                feed.Step();
            }
        }

        diagnostics.Act("first status", feeds[0].Status);
        diagnostics.Act("second status", feeds[1].Status);
        diagnostics.ActOutput(secondExpected, feeds[1].Output.AsSpan(0, feeds[1].Written));
        Assert.AreEqual(OperationStatus.Done, feeds[0].Status);
        Assert.AreEqual(OperationStatus.Done, feeds[1].Status);
        CollectionAssert.AreEqual(firstExpected, feeds[0].Output);
        CollectionAssert.AreEqual(secondExpected, feeds[1].Output);
    }

    [TestMethod]
    public async Task TryDecompress_SixteenTasksAtOnce_EachWritesTheSameContentAsOneAlone()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var source = ReadResource("source-level19.zst");
        var expected = DecompressAlone(source);

        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
        {
            var destination = new byte[expected.Length];
            return ZstandardDecoder.TryDecompress(source, destination, out _) && destination.AsSpan().SequenceEqual(expected);
        })));

        diagnostics.Act("tasks that wrote the content", results.Count(result => result));
        diagnostics.Assert("tasks that wrote the content", 16, results.Count(result => result));
        Assert.IsTrue(results.All(result => result));
    }

    /// <summary>
    /// Runs <see cref="ZstandardDecoder.Decompress" /> over <paramref name="source" />, a call
    /// per frame, and checks the call that refuses it reports nothing consumed or written and
    /// names <paramref name="expected" />.
    /// </summary>
    private void AssertInvalidData(string name, byte[] source, ZstandardDecodeError expected, int maxWindowLog)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.ArrangeSource(name, source);
        diagnostics.Arrange("max window log", maxWindowLog);
        var decoder = new ZstandardDecoder(maxWindowLog);
        var position = 0;

        OperationStatus status;
        int consumed;
        int written;
        do
        {
            status = decoder.Decompress(source.AsSpan(position), new byte[BlockSizeLimit * 2], out consumed, out written);
            position += consumed;
        }
        while (status == OperationStatus.Done && position < source.Length);

        diagnostics.Act("status", status);
        diagnostics.Act("error raised", decoder.LastError);
        diagnostics.Assert("error raised", expected, decoder.LastError);
        Assert.AreEqual(OperationStatus.InvalidData, status, name);
        Assert.AreEqual(expected, decoder.LastError, name);
        Assert.AreEqual(0, consumed, name);
        Assert.AreEqual(0, written, name);
    }

    private static byte[] DecompressAlone(byte[] source)
    {
        var destination = new byte[1024 * 1024];
        Assert.IsTrue(ZstandardDecoder.TryDecompress(source, destination, out var written), "the frame decodes alone");
        return destination[..written];
    }

    private static byte[] ReadResource(string name)
    {
        using var stream = typeof(ZstandardDecoderAdversarialTests).Assembly.GetManifestResourceStream(name)!;
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static byte[] Repeat(byte value, int count) => Enumerable.Repeat(value, count).ToArray();

    /// <summary>One decoder fed its source a byte per <see cref="Step" />, into a destination a byte at a time.</summary>
    private sealed class Feed(byte[] source, int contentLength)
    {
        private readonly ZstandardDecoder decoder = new();

        private int position;

        public byte[] Output { get; } = new byte[contentLength];

        public int Written { get; private set; }

        public OperationStatus Status { get; private set; } = OperationStatus.NeedMoreData;

        public void Step()
        {
            var piece = source.AsSpan(position, Math.Min(1, source.Length - position));
            var room = Output.AsSpan(Written, Math.Min(1, Output.Length - Written));
            Status = decoder.Decompress(piece, room, out var consumed, out var written);
            position += consumed;
            Written += written;
        }
    }
}
