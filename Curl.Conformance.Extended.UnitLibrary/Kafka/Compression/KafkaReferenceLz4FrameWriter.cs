using System.Buffers.Binary;

namespace Curl.Conformance.Extended.Kafka.Compression;

/// <summary>
/// Encodes an LZ4 frame (lz4/lz4 doc/lz4_Frame_format.md and lz4_Block_format.md): the magic, a version 01 descriptor
/// with a 64 KB block maximum and its XXH32 header checksum, data blocks (compressed, or stored with the high bit of the
/// size set), the end mark, and the block and content checksums when asked for. Blocks are linked when asked for, so a
/// match may copy from an earlier block.
/// </summary>
/// <param name="blockChecksums">Whether each block is followed by the XXH32 of its stored bytes.</param>
/// <param name="contentChecksum">Whether the end mark is followed by the XXH32 of the whole content.</param>
/// <param name="linkedBlocks">Whether a block may copy from earlier blocks (block independence flag clear).</param>
public sealed class KafkaReferenceLz4FrameWriter(bool blockChecksums = false, bool contentChecksum = false, bool linkedBlocks = false)
{
    private const byte Version = 0x40;
    private const byte IndependentBlocksFlag = 0x20;
    private const byte BlockChecksumFlag = 0x10;
    private const byte ContentChecksumFlag = 0x04;
    private const byte BlockMaximum64Kb = 0x40;
    private const int StoredBlockBit = int.MinValue;
    private const int MaximumOffset = 65535;
    private const int LastLiterals = 5;
    private const int LastMatchStart = 12;
    private const int NibbleLimit = 15;

    private static ReadOnlySpan<byte> Magic => [0x04, 0x22, 0x4D, 0x18];

    /// <summary>Encodes <paramref name="data"/> as one frame of blocks each holding <paramref name="blockSize"/> bytes, the last fewer.</summary>
    /// <param name="data">The content.</param>
    /// <param name="blockSize">The content bytes per block, at most 64 KB.</param>
    /// <param name="storedBlocks">Whether every block is stored uncompressed instead.</param>
    /// <returns>The frame.</returns>
    public byte[] Write(byte[] data, int blockSize, bool storedBlocks = false)
    {
        ArgumentNullException.ThrowIfNull(data);
        byte flags = Flags();
        List<byte> frame = [.. Magic, flags, BlockMaximum64Kb, (byte)(KafkaReferenceXxHash.Hash32([flags, BlockMaximum64Kb]) >> 8)];
        KafkaReferenceMatchFinder finder = new(data);
        for (int start = 0; start < data.Length; start += blockSize)
        {
            int end = Math.Min(start + blockSize, data.Length);
            AppendBlock(frame, storedBlocks ? data[start..end] : EncodeBlock(data, finder, start, end), storedBlocks);
        }

        AppendInt32(frame, 0);
        AppendChecksumIf(contentChecksum, frame, data);
        return [.. frame];
    }

    private static void AppendChecksumIf(bool wanted, List<byte> frame, byte[] bytes)
    {
        if (wanted)
        {
            AppendInt32(frame, (int)KafkaReferenceXxHash.Hash32(bytes));
        }
    }

    private byte Flags()
    {
        int flags = Version | (linkedBlocks ? 0 : IndependentBlocksFlag);
        flags |= blockChecksums ? BlockChecksumFlag : 0;
        return (byte)(flags | (contentChecksum ? ContentChecksumFlag : 0));
    }

    private void AppendBlock(List<byte> frame, byte[] block, bool stored)
    {
        AppendInt32(frame, block.Length | (stored ? StoredBlockBit : 0));
        frame.AddRange(block);
        AppendChecksumIf(blockChecksums, frame, block);
    }

    private static void AppendInt32(List<byte> output, int value)
    {
        byte[] bytes = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        output.AddRange(bytes);
    }

    private byte[] EncodeBlock(byte[] data, KafkaReferenceMatchFinder finder, int start, int end)
    {
        List<byte> block = [];
        int windowStart = linkedBlocks ? 0 : start;
        int literalStart = start;
        int position = start;
        while (position + LastMatchStart <= end)
        {
            (int offset, int length) = finder.Find(position, windowStart, MaximumOffset, end - LastLiterals - position, end - LastLiterals);
            if (length == 0)
            {
                position++;
                continue;
            }

            WriteSequence(block, data.AsSpan(literalStart, position - literalStart), length - KafkaReferenceMatchFinder.MinimumLength);
            block.AddRange([(byte)offset, (byte)(offset >> 8)]);
            WriteLengthExtension(block, length - KafkaReferenceMatchFinder.MinimumLength);
            position += length;
            literalStart = position;
        }

        WriteSequence(block, data.AsSpan(literalStart, end - literalStart), 0);
        return [.. block];
    }

    // The token and the literals; the match's offset and length extension follow it unless it is the last sequence.
    private static void WriteSequence(List<byte> block, ReadOnlySpan<byte> literals, int matchLengthLessMinimum)
    {
        block.Add((byte)((Math.Min(literals.Length, NibbleLimit) << 4) | Math.Min(matchLengthLessMinimum, NibbleLimit)));
        WriteLengthExtension(block, literals.Length);
        block.AddRange(literals);
    }

    private static void WriteLengthExtension(List<byte> block, int length)
    {
        if (length < NibbleLimit)
        {
            return;
        }

        int rest = length - NibbleLimit;
        for (; rest >= byte.MaxValue; rest -= byte.MaxValue)
        {
            block.Add(byte.MaxValue);
        }

        block.Add((byte)rest);
    }
}
