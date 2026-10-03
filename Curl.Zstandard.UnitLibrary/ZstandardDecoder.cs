using System.Buffers;
using System.Buffers.Binary;

namespace Curl.Zstandard;

/// <summary>
/// A streaming push decoder for Zstandard (RFC 8878), shaped after the base class
/// library's <see cref="System.IO.Compression.BrotliDecoder" /> (ADR-0185): each call to
/// <see cref="Decompress" /> consumes what it can of its source and writes what it can to
/// its destination, keeping its state between calls, so input and output may arrive a byte
/// at a time.
/// </summary>
/// <remarks>
/// It decodes Zstandard frames of raw, RLE and compressed blocks, checks each frame's
/// <c>Content_Checksum</c>, and skips skippable frames. A compressed block is gathered
/// whole, then its literals section (raw, RLE, and Huffman-coded with or without its own
/// tree, in one or four streams) and its sequences section (predefined, RLE,
/// FSE-compressed and repeated tables, the three repeat offsets) are decoded and executed
/// into a block buffer before any of it is given. Every byte given also goes into the
/// frame's history, which later blocks' matches copy from; it holds as much as libzstd's
/// streaming decoder buffers, <c>Window_Size</c> plus two <c>Block_Maximum_Size</c> plus 64
/// bytes, and a match may reach back that far (ADR-0196). A compressed block whose <c>Block_Size</c> is 0 is
/// an empty block, as libzstd's streaming decoder, the one curl drives, treats it
/// (ADR-0192).
/// </remarks>
public sealed class ZstandardDecoder
{
    /// <summary>The default largest window, 2^27 bytes: libzstd's <c>ZSTD_WINDOWLOG_LIMIT_DEFAULT</c>, the limit curl decodes under.</summary>
    public const int DefaultMaxWindowLog = 27;

    /// <summary>The smallest <c>maxWindowLog</c> accepted: RFC 8878's minimum window, 1 KiB.</summary>
    public const int MinimumMaxWindowLog = 10;

    /// <summary>The largest <c>maxWindowLog</c> accepted.</summary>
    public const int MaximumMaxWindowLog = 31;

    private const uint ZstandardFrameMagic = 0xFD2FB528;

    private const uint SkippableFrameMagic = 0x184D2A50;

    private const uint SkippableFrameMagicMask = 0xFFFFFFF0;

    private const int MagicLength = 4;

    private const int BlockHeaderLength = 3;

    private const int ChecksumLength = 4;

    /// <summary>RFC 8878 section 3.1.1.2.3: no block regenerates more than 128 KiB.</summary>
    private const int BlockSizeLimit = 128 * 1024;

    private const int RawBlockType = 0;

    private const int RleBlockType = 1;

    private const int CompressedBlockType = 2;

    /// <summary>libzstd's streaming buffer holds two blocks and <c>2 * WILDCOPY_OVERLENGTH</c> bytes past the window (ADR-0196).</summary>
    private const int HistoryMargin = 64;

    private readonly ulong maxWindowSize;

    /// <summary>The frame's latest content, which matches copy from.</summary>
    private readonly ZstandardHistory history = new();

    /// <summary>Holds a fixed-size field while its bytes arrive: at most a 13-byte frame header after its descriptor.</summary>
    private readonly byte[] field = new byte[13];

    private readonly XxHash64Accumulator contentChecksum = new();

    private int fieldLength;

    private int fieldNeeded = MagicLength;

    private Stage stage = Stage.Magic;

    private byte frameHeaderDescriptor;

    private ZstandardFrameHeader frameHeader;

    private ulong frameContentWritten;

    private bool lastBlock;

    /// <summary>The bytes still to skip in a skippable frame, or still to give from a raw or RLE block.</summary>
    private long remaining;

    private byte rleValue;

    /// <summary>Holds a compressed block while its bytes arrive; allocated at the first compressed block.</summary>
    private byte[]? compressedBlock;

    /// <summary>Decodes compressed blocks' literals; created at the first compressed block.</summary>
    private ZstandardLiteralsDecoder? literalsDecoder;

    /// <summary>Decodes and executes compressed blocks' sequences; created at the first compressed block.</summary>
    private ZstandardSequencesDecoder? sequencesDecoder;

    /// <summary>What a compressed block regenerates; allocated at the first compressed block.</summary>
    private byte[]? blockContent;

    private int blockContentLength;

    /// <summary>Creates a decoder that refuses frames whose window exceeds <c>2^<paramref name="maxWindowLog" /></c> bytes.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxWindowLog" /> is outside <see cref="MinimumMaxWindowLog" /> to <see cref="MaximumMaxWindowLog" />.</exception>
    public ZstandardDecoder(int maxWindowLog = DefaultMaxWindowLog)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxWindowLog, MinimumMaxWindowLog);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxWindowLog, MaximumMaxWindowLog);
        maxWindowSize = 1UL << maxWindowLog;
    }

    private enum Stage
    {
        Magic,
        SkippableFrameSize,
        SkippableFrameContent,
        FrameHeaderDescriptor,
        FrameHeaderFields,
        BlockHeader,
        RawBlock,
        RleBlockValue,
        RleBlock,
        CompressedBlock,
        CompressedBlockContent,
        Checksum,
        Failed,
    }

    /// <summary>Why the last <see cref="OperationStatus.InvalidData" /> happened; <see cref="ZstandardDecodeError.None" /> until then.</summary>
    public ZstandardDecodeError LastError { get; private set; }

    /// <summary>
    /// Decodes every frame in <paramref name="source" /> into <paramref name="destination" />
    /// in one call.
    /// </summary>
    /// <returns>
    /// <see langword="true" /> when <paramref name="source" /> is whole frames that decode into
    /// <paramref name="destination" />; <see langword="false" /> when the data is invalid, ends
    /// mid-frame, or decodes to more than <paramref name="destination" /> holds.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxWindowLog" /> is outside <see cref="MinimumMaxWindowLog" /> to <see cref="MaximumMaxWindowLog" />.</exception>
    public static bool TryDecompress(ReadOnlySpan<byte> source, Span<byte> destination, out int bytesWritten, int maxWindowLog = DefaultMaxWindowLog)
    {
        var decoder = new ZstandardDecoder(maxWindowLog);
        var status = OperationStatus.Done;
        bytesWritten = 0;
        while (!source.IsEmpty && status == OperationStatus.Done)
        {
            status = decoder.Decompress(source, destination[bytesWritten..], out var consumed, out var written);
            source = source[consumed..];
            bytesWritten += written;
        }

        return status == OperationStatus.Done;
    }

    /// <summary>
    /// Decodes as much of <paramref name="source" /> into <paramref name="destination" /> as
    /// the two allow.
    /// </summary>
    /// <returns>
    /// <see cref="OperationStatus.Done" /> at the end of each frame (the next call starts the
    /// next frame); <see cref="OperationStatus.NeedMoreData" /> when all of
    /// <paramref name="source" /> was consumed mid-frame;
    /// <see cref="OperationStatus.DestinationTooSmall" /> when <paramref name="destination" />
    /// filled with decoded bytes still to give; <see cref="OperationStatus.InvalidData" />,
    /// with <see cref="LastError" /> saying why, for anything RFC 8878 forbids, after which
    /// every call fails the same way.
    /// </returns>
    public OperationStatus Decompress(ReadOnlySpan<byte> source, Span<byte> destination, out int bytesConsumed, out int bytesWritten)
    {
        var buffers = new DecodeBuffers(source, destination);
        OperationStatus? status;
        do
        {
            status = Step(ref buffers);
        }
        while (status is null);

        bytesConsumed = buffers.Consumed;
        bytesWritten = buffers.Written;
        return status.Value;
    }

    /// <summary>Advances by one stage; <see langword="null" /> means carry on with the next.</summary>
    private OperationStatus? Step(ref DecodeBuffers buffers) => stage switch
    {
        Stage.Failed => OperationStatus.InvalidData,
        Stage.SkippableFrameContent => SkipSkippableFrameContent(ref buffers),
        Stage.RawBlock => CopyRawBlock(ref buffers),
        Stage.RleBlock => RepeatRleValue(ref buffers),
        Stage.CompressedBlockContent => GiveBlockContent(ref buffers),
        _ => GatherField(ref buffers),
    };

    /// <summary>Gathers the <c>fieldNeeded</c> bytes the stage reads: a header field, or a whole compressed block.</summary>
    private OperationStatus? GatherField(ref DecodeBuffers buffers)
    {
        var target = stage == Stage.CompressedBlock ? compressedBlock! : field;
        var count = Math.Min(fieldNeeded - fieldLength, buffers.Source.Length);
        buffers.Take(count).CopyTo(target.AsSpan(fieldLength));
        fieldLength += count;
        if (fieldLength < fieldNeeded)
        {
            return stage == Stage.Magic && !CanBeginMagic(field.AsSpan(0, fieldLength))
                ? Fail(ZstandardDecodeError.PrefixUnknown)
                : OperationStatus.NeedMoreData;
        }

        fieldLength = 0;
        return stage == Stage.CompressedBlock ? DecodeCompressedBlock(target.AsSpan(0, fieldNeeded)) : ReadField(field.AsSpan(0, fieldNeeded));
    }

    private OperationStatus? ReadField(ReadOnlySpan<byte> value) => stage switch
    {
        Stage.Magic => ReadMagic(BinaryPrimitives.ReadUInt32LittleEndian(value)),
        Stage.SkippableFrameSize => ReadSkippableFrameSize(BinaryPrimitives.ReadUInt32LittleEndian(value)),
        Stage.FrameHeaderDescriptor => ReadFrameHeaderDescriptor(value[0]),
        Stage.FrameHeaderFields => ReadFrameHeaderFields(value),
        Stage.BlockHeader => ReadBlockHeader(value[0] | (value[1] << 8) | (value[2] << 16)),
        Stage.RleBlockValue => ReadRleValue(value[0]),
        _ => ReadChecksum(BinaryPrimitives.ReadUInt32LittleEndian(value)),
    };

    /// <summary>
    /// Whether the 0 to 3 bytes gathered so far can still begin the Zstandard magic or a
    /// skippable-frame magic, as libzstd's <c>ZSTD_getFrameHeader_advanced</c> judges a partial
    /// magic on every call, so bytes that cannot fail at once rather than at the fourth byte.
    /// </summary>
    private static bool CanBeginMagic(ReadOnlySpan<byte> prefix)
    {
        if (((ReadOnlySpan<byte>)[0x28, 0xB5, 0x2F, 0xFD]).StartsWith(prefix))
        {
            return true;
        }

        return (prefix[0] & 0xF0) == 0x50 && ((ReadOnlySpan<byte>)[0x2A, 0x4D, 0x18]).StartsWith(prefix[1..]);
    }

    private OperationStatus? ReadMagic(uint magic)
    {
        if (magic == ZstandardFrameMagic)
        {
            return Expect(Stage.FrameHeaderDescriptor, 1);
        }

        return (magic & SkippableFrameMagicMask) == SkippableFrameMagic
            ? Expect(Stage.SkippableFrameSize, 4)
            : Fail(ZstandardDecodeError.PrefixUnknown);
    }

    private OperationStatus? ReadSkippableFrameSize(uint size)
    {
        remaining = size;
        stage = Stage.SkippableFrameContent;
        return null;
    }

    private OperationStatus? SkipSkippableFrameContent(ref DecodeBuffers buffers)
    {
        var count = (int)Math.Min(remaining, buffers.Source.Length);
        buffers.Take(count);
        remaining -= count;
        return remaining == 0 ? EndFrame() : OperationStatus.NeedMoreData;
    }

    private OperationStatus? ReadFrameHeaderDescriptor(byte descriptor)
    {
        if (ZstandardFrameHeader.HasReservedBit(descriptor))
        {
            return Fail(ZstandardDecodeError.FrameParameterUnsupported);
        }

        frameHeaderDescriptor = descriptor;
        return Expect(Stage.FrameHeaderFields, ZstandardFrameHeader.FieldsLength(descriptor));
    }

    private OperationStatus? ReadFrameHeaderFields(ReadOnlySpan<byte> fields)
    {
        frameHeader = ZstandardFrameHeader.Read(frameHeaderDescriptor, fields);
        if (frameHeader.DictionaryId != 0)
        {
            return Fail(ZstandardDecodeError.DictionaryWrong);
        }

        if (frameHeader.WindowSize > maxWindowSize)
        {
            return Fail(ZstandardDecodeError.FrameParameterWindowTooLarge);
        }

        frameContentWritten = 0;
        contentChecksum.Reset();
        history.Reset(frameHeader.WindowSize + (2UL * (ulong)BlockMaximumSize) + HistoryMargin);
        literalsDecoder?.StartFrame();
        sequencesDecoder?.StartFrame();
        return Expect(Stage.BlockHeader, BlockHeaderLength);
    }

    private OperationStatus? ReadBlockHeader(int blockHeader)
    {
        lastBlock = (blockHeader & 1) != 0;
        var blockType = (blockHeader >> 1) & 3;
        var blockSize = blockHeader >> 3;
        var error = CheckBlock(blockType, blockSize);
        if (error != ZstandardDecodeError.None)
        {
            return Fail(error);
        }

        remaining = blockSize;
        return blockType switch
        {
            RawBlockType => Expect(Stage.RawBlock, 0),
            RleBlockType => Expect(Stage.RleBlockValue, 1),
            _ when blockSize == 0 => EndBlock(),
            _ => ExpectCompressedBlock(blockSize),
        };
    }

    /// <summary>RFC 8878 section 3.1.1.2.3: <c>Block_Maximum_Size</c>, the smaller of the window and 128 KiB.</summary>
    private int BlockMaximumSize => (int)Math.Min(frameHeader.WindowSize, BlockSizeLimit);

    /// <summary>
    /// Returns what is wrong with a block of <paramref name="blockType" /> whose
    /// <c>Block_Size</c> is <paramref name="blockSize" />, or <see cref="ZstandardDecodeError.None" />.
    /// A raw or RLE block's size is what it regenerates, so it must also fit the declared content size.
    /// </summary>
    private ZstandardDecodeError CheckBlock(int blockType, int blockSize)
    {
        if (blockType > CompressedBlockType || blockSize > BlockMaximumSize)
        {
            return ZstandardDecodeError.CorruptionDetected;
        }

        return blockType != CompressedBlockType && ExceedsContentSize(blockSize)
            ? ZstandardDecodeError.CorruptionDetected
            : ZstandardDecodeError.None;
    }

    /// <summary>Whether <paramref name="count" /> more bytes of content would pass the declared <c>Frame_Content_Size</c>.</summary>
    private bool ExceedsContentSize(int count) =>
        frameContentWritten + (ulong)count > frameHeader.ContentSize.GetValueOrDefault(ulong.MaxValue);

    private OperationStatus? ExpectCompressedBlock(int blockSize)
    {
        compressedBlock ??= new byte[BlockSizeLimit];
        literalsDecoder ??= new ZstandardLiteralsDecoder(BlockSizeLimit);
        sequencesDecoder ??= new ZstandardSequencesDecoder();
        blockContent ??= new byte[BlockSizeLimit];
        return Expect(Stage.CompressedBlock, blockSize);
    }

    /// <summary>
    /// Decodes a whole compressed block (RFC 8878 section 3.1.1.3): its literals section,
    /// then its sequences section, executed into <see cref="blockContent" />, which must not
    /// pass the declared <c>Frame_Content_Size</c>.
    /// </summary>
    private OperationStatus? DecodeCompressedBlock(ReadOnlySpan<byte> block)
    {
        var error = literalsDecoder!.Decode(block, BlockMaximumSize, out var literalsSectionLength);
        if (error != ZstandardDecodeError.None)
        {
            return Fail(error);
        }

        var output = blockContent.AsSpan(0, BlockMaximumSize);
        if (!sequencesDecoder!.Decode(block[literalsSectionLength..], literalsDecoder.Literals, output, history, out blockContentLength)
            || ExceedsContentSize(blockContentLength))
        {
            return Fail(ZstandardDecodeError.CorruptionDetected);
        }

        remaining = blockContentLength;
        stage = Stage.CompressedBlockContent;
        return null;
    }

    /// <summary>Gives what a compressed block regenerated, as far as the destination allows.</summary>
    private OperationStatus? GiveBlockContent(ref DecodeBuffers buffers)
    {
        var content = buffers.Give((int)Math.Min(remaining, buffers.Destination.Length));
        blockContent.AsSpan(blockContentLength - (int)remaining, content.Length).CopyTo(content);
        AddContent(content);
        return remaining == 0 ? EndBlock() : OperationStatus.DestinationTooSmall;
    }

    private OperationStatus? CopyRawBlock(ref DecodeBuffers buffers)
    {
        var count = (int)Math.Min(remaining, Math.Min(buffers.Source.Length, buffers.Destination.Length));
        var content = buffers.Give(count);
        buffers.Take(count).CopyTo(content);
        AddContent(content);
        if (remaining == 0)
        {
            return EndBlock();
        }

        return buffers.Source.IsEmpty ? OperationStatus.NeedMoreData : OperationStatus.DestinationTooSmall;
    }

    private OperationStatus? ReadRleValue(byte value)
    {
        rleValue = value;
        stage = Stage.RleBlock;
        return null;
    }

    private OperationStatus? RepeatRleValue(ref DecodeBuffers buffers)
    {
        var content = buffers.Give((int)Math.Min(remaining, buffers.Destination.Length));
        content.Fill(rleValue);
        AddContent(content);
        return remaining == 0 ? EndBlock() : OperationStatus.DestinationTooSmall;
    }

    private void AddContent(ReadOnlySpan<byte> content)
    {
        remaining -= content.Length;
        frameContentWritten += (ulong)content.Length;
        contentChecksum.Append(content);
        history.Append(content);
    }

    private OperationStatus? EndBlock()
    {
        if (!lastBlock)
        {
            return Expect(Stage.BlockHeader, BlockHeaderLength);
        }

        if (frameContentWritten != frameHeader.ContentSize.GetValueOrDefault(frameContentWritten))
        {
            return Fail(ZstandardDecodeError.CorruptionDetected);
        }

        return frameHeader.HasChecksum ? Expect(Stage.Checksum, ChecksumLength) : EndFrame();
    }

    private OperationStatus? ReadChecksum(uint checksum) =>
        checksum == (uint)contentChecksum.GetCurrentHash() ? EndFrame() : Fail(ZstandardDecodeError.ChecksumWrong);

    private OperationStatus EndFrame()
    {
        Expect(Stage.Magic, MagicLength);
        return OperationStatus.Done;
    }

    /// <summary>Moves to <paramref name="next" />, which gathers <paramref name="length" /> bytes before it is read.</summary>
    private OperationStatus? Expect(Stage next, int length)
    {
        stage = next;
        fieldNeeded = length;
        return null;
    }

    private OperationStatus Fail(ZstandardDecodeError error)
    {
        LastError = error;
        stage = Stage.Failed;
        return OperationStatus.InvalidData;
    }

    /// <summary>What is left of one <see cref="Decompress" /> call's source and destination, and how much of each is used.</summary>
    private ref struct DecodeBuffers(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        public ReadOnlySpan<byte> Source = source;

        public Span<byte> Destination = destination;

        public int Consumed;

        public int Written;

        /// <summary>Consumes the next <paramref name="count" /> bytes of the source.</summary>
        public ReadOnlySpan<byte> Take(int count)
        {
            var taken = Source[..count];
            Source = Source[count..];
            Consumed += count;
            return taken;
        }

        /// <summary>Claims the next <paramref name="count" /> bytes of the destination for decoded content.</summary>
        public Span<byte> Give(int count)
        {
            var given = Destination[..count];
            Destination = Destination[count..];
            Written += count;
            return given;
        }
    }
}
