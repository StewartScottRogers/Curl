using System.Buffers.Binary;
using System.Globalization;
using Curl.Testing;

namespace Curl.Zstandard;

/// <summary>
/// Writes the decoder tests' diagnostic lines through <see cref="TestDiagnostics" /> (BL-1490):
/// a source as <c>BYTES</c> with each frame's decoded header and block types, and output
/// as <c>BYTES</c> with a <c>DIFF</c> against the expected bytes. Describing a frame never
/// throws, however malformed it is: it says where the frame was cut short or went wrong.
/// </summary>
internal static class ZstandardTestDiagnostics
{
    private const uint ZstandardMagic = 0xFD2FB528;

    private const uint SkippableMagicMask = 0xFFFFFFF0;

    private const uint SkippableMagicBase = 0x184D2A50;

    /// <summary>The most frames, and the most blocks in one frame, a description lists.</summary>
    private const int ListedLimit = 12;

    private static readonly string[] BlockTypeNames = ["raw", "RLE", "compressed", "reserved"];

    /// <summary>Writes the case name, the source as <c>BYTES</c> and its frames decoded.</summary>
    public static void ArrangeSource(this TestDiagnostics diagnostics, string name, ReadOnlySpan<byte> source)
    {
        diagnostics.Arrange("case", name);
        diagnostics.Bytes("source", source);
        diagnostics.Arrange("frames", DescribeFrames(source));
    }

    /// <summary>Writes the output's length as an <c>ACT</c> line, the output as <c>BYTES</c> and a <c>DIFF</c> against <paramref name="expected" />.</summary>
    public static void ActOutput(this TestDiagnostics diagnostics, ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
    {
        diagnostics.Act("output length", actual.Length);
        diagnostics.Bytes("output", actual);
        diagnostics.Diff("output", expected, actual);
    }

    /// <summary>Describes every frame in <paramref name="source" />: its kind, header fields and blocks, or where it stops making sense.</summary>
    public static string DescribeFrames(ReadOnlySpan<byte> source)
    {
        var parts = new List<string>();
        var position = 0;
        while (position < source.Length && parts.Count < ListedLimit)
        {
            var length = DescribeFrame(source[position..], out var part);
            parts.Add(part);
            position = length == 0 ? source.Length : position + length;
        }

        if (position < source.Length)
        {
            parts.Add(Invariant($"{source.Length - position} more bytes"));
        }

        return parts.Count == 0 ? "none (empty source)" : string.Join("; ", parts);
    }

    /// <summary>Describes the frame at the start of <paramref name="frame" /> and returns its length, or 0 when nothing after it can be described.</summary>
    private static int DescribeFrame(ReadOnlySpan<byte> frame, out string description)
    {
        if (frame.Length < 4)
        {
            description = Invariant($"{frame.Length} bytes, too short for a magic number");
            return 0;
        }

        var magic = BinaryPrimitives.ReadUInt32LittleEndian(frame);
        if ((magic & SkippableMagicMask) == SkippableMagicBase)
        {
            return DescribeSkippableFrame(frame, magic, out description);
        }

        if (magic != ZstandardMagic)
        {
            description = Invariant($"unknown magic 0x{magic:X8}");
            return 0;
        }

        return DescribeZstandardFrame(frame, out description);
    }

    private static int DescribeSkippableFrame(ReadOnlySpan<byte> frame, uint magic, out string description)
    {
        if (frame.Length < 8)
        {
            description = Invariant($"skippable frame 0x{magic:X8} cut short in its size");
            return 0;
        }

        var size = BinaryPrimitives.ReadUInt32LittleEndian(frame[4..]);
        var whole = (ulong)frame.Length - 8 >= size;
        description = Invariant($"skippable frame 0x{magic:X8}, {size} bytes{(whole ? string.Empty : ", cut short")}");
        return whole ? 8 + (int)size : 0;
    }

    private static int DescribeZstandardFrame(ReadOnlySpan<byte> frame, out string description)
    {
        if (frame.Length < 5)
        {
            description = "zstd frame cut short before its Frame_Header_Descriptor";
            return 0;
        }

        var position = DescribeFrameHeader(frame, out var header);
        if (position == 0)
        {
            description = Invariant($"zstd frame ({header}, header cut short)");
            return 0;
        }

        var hasChecksum = (frame[4] & 0x04) != 0;
        position = DescribeBlocks(frame, position, out var blocks);
        var checksum = position == 0 || !hasChecksum ? string.Empty : DescribeChecksum(frame, ref position);
        description = Invariant($"zstd frame ({header}; blocks: {blocks}{checksum})");
        return position;
    }

    /// <summary>Describes the frame header and returns where the first block starts, or 0 when the header is cut short.</summary>
    private static int DescribeFrameHeader(ReadOnlySpan<byte> frame, out string header)
    {
        var descriptor = frame[4];
        var singleSegment = (descriptor & 0x20) != 0;
        var dictionaryIdBytes = (descriptor & 3) == 3 ? 4 : descriptor & 3;
        var contentSizeBytes = (descriptor >> 6) == 0 ? (singleSegment ? 1 : 0) : 1 << (descriptor >> 6);
        var windowBytes = singleSegment ? 0 : 1;
        var fields = new List<string> { Invariant($"descriptor 0x{descriptor:X2}"), (descriptor & 0x04) != 0 ? "checksum flag set" : "no checksum" };
        if ((descriptor & 0x08) != 0)
        {
            fields.Add("reserved bit set");
        }

        var end = 5 + windowBytes + dictionaryIdBytes + contentSizeBytes;
        if (frame.Length < end)
        {
            header = string.Join(", ", fields);
            return 0;
        }

        fields.Add(singleSegment ? "single segment" : DescribeWindow(frame[5]));
        fields.Add(Invariant($"dictionary ID {(dictionaryIdBytes == 0 ? "absent" : ReadLittleEndian(frame.Slice(5 + windowBytes, dictionaryIdBytes)).ToString(CultureInfo.InvariantCulture))}"));
        fields.Add(Invariant($"content size {DescribeContentSize(frame.Slice(5 + windowBytes + dictionaryIdBytes, contentSizeBytes))}"));
        header = string.Join(", ", fields);
        return end;
    }

    private static string DescribeWindow(byte windowDescriptor)
    {
        var windowBase = 1UL << (10 + (windowDescriptor >> 3));
        var windowSize = windowBase + ((windowBase / 8) * (ulong)(windowDescriptor & 7));
        return Invariant($"window 0x{windowDescriptor:X2} ({windowSize} bytes)");
    }

    private static string DescribeContentSize(ReadOnlySpan<byte> field) =>
        field.Length switch
        {
            0 => "absent",
            2 => (ReadLittleEndian(field) + 256).ToString(CultureInfo.InvariantCulture),
            _ => ReadLittleEndian(field).ToString(CultureInfo.InvariantCulture),
        };

    /// <summary>Lists each block's type and size up to the last block, and returns where the frame continues, or 0 when the blocks are cut short or reserved.</summary>
    private static int DescribeBlocks(ReadOnlySpan<byte> frame, int position, out string blocks)
    {
        var listed = new List<string>();
        var count = 0;
        var last = false;
        while (!last)
        {
            if (frame.Length - position < 3)
            {
                blocks = Joined(listed, count, "cut short");
                return 0;
            }

            var value = (int)ReadLittleEndian(frame.Slice(position, 3));
            last = (value & 1) != 0;
            var type = (value >> 1) & 3;
            var size = value >> 3;
            count++;
            if (listed.Count < ListedLimit)
            {
                listed.Add(Invariant($"{BlockTypeNames[type]} {size}{(last ? " last" : string.Empty)}"));
            }

            var contentLength = type == ZstandardTestFrames.RleBlockType ? 1 : size;
            if (type == ZstandardTestFrames.ReservedBlockType || frame.Length - position - 3 < contentLength)
            {
                blocks = Joined(listed, count, type == ZstandardTestFrames.ReservedBlockType ? "stops at the reserved type" : "content cut short");
                return 0;
            }

            position += 3 + contentLength;
        }

        blocks = Joined(listed, count, null);
        return position;
    }

    private static string DescribeChecksum(ReadOnlySpan<byte> frame, ref int position)
    {
        if (frame.Length - position < 4)
        {
            position = 0;
            return "; checksum cut short";
        }

        var checksum = BinaryPrimitives.ReadUInt32LittleEndian(frame[position..]);
        position += 4;
        return Invariant($"; checksum 0x{checksum:X8}");
    }

    private static string Joined(List<string> listed, int count, string? ending)
    {
        var more = count > listed.Count ? Invariant($", {count - listed.Count} more") : string.Empty;
        var end = ending is null ? string.Empty : ", " + ending;
        return string.Join(", ", listed) + more + end;
    }

    private static ulong ReadLittleEndian(ReadOnlySpan<byte> field)
    {
        ulong value = 0;
        for (var index = field.Length - 1; index >= 0; index--)
        {
            value = (value << 8) | field[index];
        }

        return value;
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
