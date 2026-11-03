using System.Buffers.Binary;

namespace Curl.Conformance.Extended.Kafka.Compression;

/// <summary>
/// Writes a zstd frame (RFC 8878 section 3.1.1) block by block as a case lays it out: a single-segment header with a
/// four-byte content size, a dictionary id when one is given, then raw, RLE and compressed blocks, the last marked last,
/// and the content checksum (the low 32 bits of XXH64) when asked for. A compressed block is one
/// <see cref="RleSequencesBlock"/> makes: raw literals and sequences whose three codes are RLE, so only the extra bits
/// of each sequence's offset, match length and literals length are in its bit stream.
/// </summary>
/// <param name="contentChecksum">Whether the frame ends with the content checksum.</param>
/// <param name="dictionaryId">The dictionary id to write, or 0 for none, written in one byte.</param>
public sealed class KafkaReferenceZstdFrameWriter(bool contentChecksum = false, byte dictionaryId = 0)
{
    private const int SingleSegmentFlag = 0x20;
    private const int FourByteContentSize = 0x80;
    private const int ChecksumFlag = 0x04;
    private const int RawBlock = 0;
    private const int RleBlock = 1;
    private const int CompressedBlock = 2;
    private const byte AllRleModes = 0x54;

    private static ReadOnlySpan<byte> Magic => [0x28, 0xB5, 0x2F, 0xFD];

    private readonly List<(int Type, int Size, byte[] Stored)> blocks = [];

    private readonly List<byte> content = [];

    /// <summary>
    /// A compressed block's content: a raw literals section of <paramref name="literals"/> (fewer than 32), then
    /// <paramref name="sequenceCount"/> sequences (fewer than 128) whose literals length, offset and match length codes
    /// are each one RLE symbol, and the bit stream holding <paramref name="extraBits"/> in the order the decoder reads
    /// them (each sequence's offset, match length and literals length extra bits; RLE states take none).
    /// </summary>
    /// <param name="literals">The literals.</param>
    /// <param name="literalsLengthCode">The literals length code every sequence uses.</param>
    /// <param name="offsetCode">The offset code every sequence uses.</param>
    /// <param name="matchLengthCode">The match length code every sequence uses.</param>
    /// <param name="sequenceCount">The number of sequences.</param>
    /// <param name="extraBits">Each extra-bits field's value and width, in reading order.</param>
    /// <returns>The block's content.</returns>
    public static byte[] RleSequencesBlock(byte[] literals, byte literalsLengthCode, byte offsetCode, byte matchLengthCode, int sequenceCount, params (int Value, int Width)[] extraBits)
    {
        ArgumentNullException.ThrowIfNull(literals);
        ArgumentNullException.ThrowIfNull(extraBits);
        List<bool> bits = [true];
        foreach ((int value, int width) in extraBits)
        {
            for (int bit = width - 1; bit >= 0; bit--)
            {
                bits.Add(((value >> bit) & 1) != 0);
            }
        }

        // The stream is read from its last byte down, so the marker bit is the highest set bit and the first field read sits just below it.
        byte[] stream = new byte[(bits.Count + 7) / 8];
        for (int index = 0; index < bits.Count; index++)
        {
            int position = bits.Count - 1 - index;
            stream[position / 8] |= (byte)(bits[index] ? 1 << (position % 8) : 0);
        }

        return [(byte)(literals.Length << 3), .. literals, (byte)sequenceCount, AllRleModes, literalsLengthCode, offsetCode, matchLengthCode, .. stream];
    }

    /// <summary>Adds a raw block.</summary>
    /// <param name="bytes">Its content.</param>
    /// <returns>This writer.</returns>
    public KafkaReferenceZstdFrameWriter Raw(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        blocks.Add((RawBlock, bytes.Length, bytes));
        content.AddRange(bytes);
        return this;
    }

    /// <summary>Adds an RLE block.</summary>
    /// <param name="value">The byte repeated.</param>
    /// <param name="count">How many times.</param>
    /// <returns>This writer.</returns>
    public KafkaReferenceZstdFrameWriter Rle(byte value, int count)
    {
        blocks.Add((RleBlock, count, [value]));
        content.AddRange(Enumerable.Repeat(value, count));
        return this;
    }

    /// <summary>Adds a compressed block.</summary>
    /// <param name="block">Its content, such as <see cref="RleSequencesBlock"/> gives.</param>
    /// <param name="decodesTo">What it decodes to, for the content size and checksum.</param>
    /// <returns>This writer.</returns>
    public KafkaReferenceZstdFrameWriter Compressed(byte[] block, byte[] decodesTo)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(decodesTo);
        blocks.Add((CompressedBlock, block.Length, block));
        content.AddRange(decodesTo);
        return this;
    }

    /// <summary>The frame.</summary>
    /// <returns>Its bytes.</returns>
    public byte[] ToArray()
    {
        List<byte> frame = Header();
        for (int index = 0; index < blocks.Count; index++)
        {
            (int type, int size, byte[] stored) = blocks[index];
            int header = (size << 3) | (type << 1) | (index == blocks.Count - 1 ? 1 : 0);
            frame.AddRange([(byte)header, (byte)(header >> 8), (byte)(header >> 16), .. stored]);
        }

        if (contentChecksum)
        {
            AppendUInt32(frame, (uint)KafkaReferenceXxHash.Hash64([.. content]));
        }

        return [.. frame];
    }

    // The magic, the descriptor, the one-byte dictionary id when there is one, and the four-byte content size.
    private List<byte> Header()
    {
        int dictionaryIdFlag = dictionaryId == 0 ? 0 : 1;
        List<byte> header = [.. Magic, (byte)(FourByteContentSize | SingleSegmentFlag | (contentChecksum ? ChecksumFlag : 0) | dictionaryIdFlag)];
        if (dictionaryId != 0)
        {
            header.Add(dictionaryId);
        }

        AppendUInt32(header, (uint)content.Count);
        return header;
    }

    private static void AppendUInt32(List<byte> output, uint value)
    {
        byte[] bytes = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        output.AddRange(bytes);
    }
}
