using System.Buffers.Binary;

namespace Curl.Extended.Protocol.Kafka.Wire.Compression;

/// <summary>
/// Decompresses Kafka's lz4 payloads (lz4 Frame Format and Block Format specifications): a frame with magic 0x184D2204, a
/// descriptor, data blocks (high bit set means stored uncompressed) and an end mark. The header, block and content
/// checksums (xxHash32) are skipped, not verified, because the record batch carries its own CRC. Matches may reach into
/// earlier blocks, so linked blocks decode too. Corrupt or truncated input throws <see cref="FormatException"/>.
/// </summary>
internal static class Lz4FrameDecompressor
{
    private const byte ContentChecksumFlag = 0x04;

    private const byte BlockChecksumFlag = 0x10;

    private const byte ContentSizeFlag = 0x08;

    private const byte DictionaryIdFlag = 0x01;

    private const int StoredBlockBit = int.MinValue;

    private const int MinimumMatch = 4;

    private static ReadOnlySpan<byte> Magic => [0x04, 0x22, 0x4D, 0x18];

    /// <summary>Decompresses one lz4 frame.</summary>
    internal static byte[] Decompress(ReadOnlySpan<byte> input)
    {
        if (!input.StartsWith(Magic))
        {
            throw Corrupt("the magic number is wrong");
        }

        int position = Magic.Length;
        byte flags = SkipDescriptor(input, ref position);
        List<byte> output = [];
        while (true)
        {
            int size = BinaryPrimitives.ReadInt32LittleEndian(Take(input, ref position, sizeof(int)));
            if (size == 0)
            {
                break;
            }

            ReadOnlySpan<byte> block = Take(input, ref position, size & ~StoredBlockBit);
            if ((size & StoredBlockBit) != 0)
            {
                output.AddRange(block);
            }
            else
            {
                DecodeBlock(block, output);
            }

            SkipIf(flags, BlockChecksumFlag, input, ref position);
        }

        SkipIf(flags, ContentChecksumFlag, input, ref position);
        return output.ToArray();
    }

    /// <summary>Reads the frame descriptor, skipping its optional fields and checksum byte, and returns the FLG byte.</summary>
    private static byte SkipDescriptor(ReadOnlySpan<byte> input, ref int position)
    {
        byte flags = Take(input, ref position, 2)[0];
        if (flags >> 6 != 1)
        {
            throw Corrupt("the frame version is not supported");
        }

        if ((flags & ContentSizeFlag) != 0)
        {
            Take(input, ref position, sizeof(long));
        }

        if ((flags & DictionaryIdFlag) != 0)
        {
            Take(input, ref position, sizeof(int));
        }

        Take(input, ref position, 1);
        return flags;
    }

    private static void SkipIf(byte flags, byte flag, ReadOnlySpan<byte> input, ref int position)
    {
        if ((flags & flag) != 0)
        {
            Take(input, ref position, sizeof(int));
        }
    }

    private static void DecodeBlock(ReadOnlySpan<byte> block, List<byte> output)
    {
        int position = 0;
        while (true)
        {
            byte token = Take(block, ref position, 1)[0];
            int literalLength = ReadLength(block, ref position, token >> 4);
            output.AddRange(Take(block, ref position, literalLength));
            if (position == block.Length)
            {
                return;
            }

            int offset = BinaryPrimitives.ReadUInt16LittleEndian(Take(block, ref position, sizeof(ushort)));
            int matchLength = ReadLength(block, ref position, token & 0x0F) + MinimumMatch;
            CopyMatch(output, offset, matchLength);
        }
    }

    private static int ReadLength(ReadOnlySpan<byte> block, ref int position, int nibble)
    {
        // Every extension byte is read from the block, so the sum is at most 255 times the block length and fits an int.
        int length = nibble;
        if (nibble == 15)
        {
            byte extension;
            do
            {
                extension = Take(block, ref position, 1)[0];
                length += extension;
            }
            while (extension == 255);
        }

        return length;
    }

    private static void CopyMatch(List<byte> output, int offset, int length)
    {
        if (offset == 0 || offset > output.Count)
        {
            throw Corrupt("a match offset is zero or before the start of the output");
        }

        for (int index = 0; index < length; index++)
        {
            output.Add(output[output.Count - offset]);
        }
    }

    private static ReadOnlySpan<byte> Take(ReadOnlySpan<byte> input, ref int position, int count)
    {
        if (count > input.Length - position)
        {
            throw Corrupt("the input is truncated");
        }

        ReadOnlySpan<byte> taken = input.Slice(position, count);
        position += count;
        return taken;
    }

    private static FormatException Corrupt(string reason) => new($"Corrupt lz4 data: {reason}.");
}
