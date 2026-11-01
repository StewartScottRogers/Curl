namespace Curl.Extended.Protocol.Kafka.Wire.Compression.Zstd;

/// <summary>
/// Decompresses zstd frames (RFC 8878), as Kafka's zstd codec sends them: the frame header, then raw, RLE and compressed
/// blocks. Skippable frames are skipped. The content checksum is skipped, not verified, because Kafka's own batch CRC
/// already covers the compressed bytes. A dictionary ID other than 0 is refused, as Kafka uses no dictionary. The window
/// size is not enforced: a match may reach back to the start of the frame's output. Corrupt or truncated input throws
/// <see cref="FormatException"/> whose message starts with "zstd:".
/// </summary>
internal static class ZstdFrameDecompressor
{
    private const uint FrameMagic = 0xFD2FB528;

    private const uint SkippableMagicMask = 0xFFFFFFF0;

    private const uint SkippableMagic = 0x184D2A50;

    private const int MagicLength = 4;

    private const int BlockHeaderLength = 3;

    private const int MaximumBlockSize = 128 * 1024;

    private const int ChecksumLength = 4;

    private const int RawBlock = 0;

    private const int RleBlock = 1;

    private const int CompressedBlock = 2;

    private static readonly int[] DictionaryIdLengths = [0, 1, 2, 4];

    // Indexed by the content size flag; flag 0 means no content size, or one byte in a single-segment frame.
    private static readonly int[] ContentSizeLengths = [0, 2, 4, 8];

    /// <summary>Decompresses every frame in <paramref name="input"/> and gives their contents one after another.</summary>
    internal static byte[] Decompress(ReadOnlySpan<byte> input)
    {
        List<byte> output = [];
        int position = 0;
        while (position < input.Length)
        {
            position += DecompressFrame(input[position..], output);
        }

        return output.ToArray();
    }

    private static FormatException Corrupt(string message) => new("zstd: " + message);

    private static void Require(ReadOnlySpan<byte> frame, long length)
    {
        if (frame.Length < length)
        {
            throw Corrupt("the input is truncated");
        }
    }

    private static ulong ReadLittleEndian(ReadOnlySpan<byte> bytes)
    {
        ulong value = 0;
        for (int index = bytes.Length - 1; index >= 0; index--)
        {
            value = (value << 8) | bytes[index];
        }

        return value;
    }

    private static int DecompressFrame(ReadOnlySpan<byte> frame, List<byte> output)
    {
        Require(frame, MagicLength);
        uint magic = (uint)ReadLittleEndian(frame[..MagicLength]);
        if ((magic & SkippableMagicMask) == SkippableMagic)
        {
            Require(frame, MagicLength + sizeof(uint));
            long skippableLength = MagicLength + sizeof(uint) + (long)ReadLittleEndian(frame.Slice(MagicLength, sizeof(uint)));
            Require(frame, skippableLength);
            return (int)skippableLength;
        }

        if (magic != FrameMagic)
        {
            throw Corrupt("the magic number is wrong");
        }

        return DecompressDataFrame(frame, output);
    }

    private static int DecompressDataFrame(ReadOnlySpan<byte> frame, List<byte> output)
    {
        int position = ReadHeader(frame, out bool hasChecksum, out ulong? contentSize);
        int outputStart = output.Count;
        ZstdLiterals literals = new();
        ZstdSequences sequences = new();
        bool last;
        do
        {
            position = ReadBlock(frame, position, literals, sequences, output, out last);
        }
        while (!last);

        if (contentSize.HasValue && contentSize.Value != (ulong)(output.Count - outputStart))
        {
            throw Corrupt("the frame content size does not match its blocks");
        }

        if (hasChecksum)
        {
            Require(frame, position + ChecksumLength);
            position += ChecksumLength;
        }

        return position;
    }

    private static int ReadHeader(ReadOnlySpan<byte> frame, out bool hasChecksum, out ulong? contentSize)
    {
        Require(frame, MagicLength + 1);
        int descriptor = frame[MagicLength];
        if ((descriptor & 0x08) != 0)
        {
            throw Corrupt("the reserved frame header bit is set");
        }

        bool singleSegment = (descriptor & 0x20) != 0;
        hasChecksum = (descriptor & 0x04) != 0;
        int dictionaryIdLength = DictionaryIdLengths[descriptor & 3];
        int contentSizeLength = (descriptor >> 6) == 0 && singleSegment ? 1 : ContentSizeLengths[descriptor >> 6];
        int dictionaryIdStart = MagicLength + 1 + (singleSegment ? 0 : 1);
        int headerLength = dictionaryIdStart + dictionaryIdLength + contentSizeLength;
        Require(frame, headerLength);
        if (ReadLittleEndian(frame.Slice(dictionaryIdStart, dictionaryIdLength)) != 0)
        {
            throw Corrupt("a dictionary is not supported");
        }

        contentSize = ReadContentSize(frame.Slice(dictionaryIdStart + dictionaryIdLength, contentSizeLength));
        return headerLength;
    }

    // A two-byte content size counts from 256.
    private static ulong? ReadContentSize(ReadOnlySpan<byte> field) =>
        field.IsEmpty ? null : ReadLittleEndian(field) + (field.Length == 2 ? 256UL : 0UL);

    private static int ReadBlock(ReadOnlySpan<byte> frame, int position, ZstdLiterals literals, ZstdSequences sequences, List<byte> output, out bool last)
    {
        Require(frame, position + BlockHeaderLength);
        uint header = (uint)ReadLittleEndian(frame.Slice(position, BlockHeaderLength));
        position += BlockHeaderLength;
        last = (header & 1) != 0;
        int type = (int)((header >> 1) & 3);
        int size = (int)(header >> 3);
        if (size > MaximumBlockSize)
        {
            throw Corrupt("a block is larger than 128 KiB");
        }

        int storedLength = type == RleBlock ? 1 : size;
        Require(frame, position + storedLength);
        ReadOnlySpan<byte> content = frame.Slice(position, storedLength);
        switch (type)
        {
            case RawBlock:
                output.AddRange(content);
                break;
            case RleBlock:
                for (int repeated = 0; repeated < size; repeated++)
                {
                    output.Add(content[0]);
                }

                break;
            case CompressedBlock:
                byte[] blockLiterals = literals.Read(content, out int literalsLength);
                sequences.Execute(content[literalsLength..], blockLiterals, output);
                break;
            default:
                throw Corrupt("a block has the reserved type");
        }

        return position + storedLength;
    }
}
