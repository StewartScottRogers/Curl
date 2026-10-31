using System.Buffers.Binary;

namespace Curl.Extension.Kafka.Wire.Compression;

/// <summary>
/// Decompresses Kafka's snappy payloads (google/snappy format_description.txt): raw snappy, a varint uncompressed length
/// then literal and copy elements, and the xerial framing Kafka producers use, an 8-byte magic, a version and a compatible
/// version, then length-prefixed raw snappy chunks. Corrupt or truncated input throws <see cref="FormatException"/>.
/// </summary>
internal static class SnappyDecompressor
{
    private const int XerialHeaderLength = 16;

    private const int MaxVarintBytes = 5;

    /// <summary>No snappy element expands its encoding more than this many times (a 3-byte copy writes at most 64 bytes).</summary>
    private const int MaxExpansion = 64;

    private static ReadOnlySpan<byte> XerialMagic => [0x82, (byte)'S', (byte)'N', (byte)'A', (byte)'P', (byte)'P', (byte)'Y', 0x00];

    /// <summary>Decompresses xerial-framed input when it starts with the xerial magic, and raw snappy otherwise.</summary>
    internal static byte[] Decompress(ReadOnlySpan<byte> input) =>
        input.StartsWith(XerialMagic) ? DecompressXerial(input) : DecompressRaw(input);

    private static byte[] DecompressXerial(ReadOnlySpan<byte> input)
    {
        if (input.Length < XerialHeaderLength)
        {
            throw Corrupt("the xerial header is truncated");
        }

        using MemoryStream output = new();
        ReadOnlySpan<byte> rest = input[XerialHeaderLength..];
        while (!rest.IsEmpty)
        {
            if (rest.Length < sizeof(int))
            {
                throw Corrupt("a xerial chunk length is truncated");
            }

            int length = BinaryPrimitives.ReadInt32BigEndian(rest);
            rest = rest[sizeof(int)..];
            if (length < 0 || length > rest.Length)
            {
                throw Corrupt("a xerial chunk is truncated");
            }

            output.Write(DecompressRaw(rest[..length]));
            rest = rest[length..];
        }

        return output.ToArray();
    }

    private static byte[] DecompressRaw(ReadOnlySpan<byte> input)
    {
        int position = 0;
        int declaredLength = ReadVarint(input, ref position);
        if (declaredLength > (long)input.Length * MaxExpansion)
        {
            throw Corrupt("the declared length is larger than the input can produce");
        }

        byte[] output = new byte[declaredLength];
        int written = 0;
        while (position < input.Length)
        {
            byte tag = input[position++];
            if ((tag & 3) == 0)
            {
                written = CopyLiteral(input, ref position, tag, output, written);
            }
            else
            {
                written = CopyEarlierOutput(input, ref position, tag, output, written);
            }
        }

        if (written != declaredLength)
        {
            throw Corrupt("the output does not match the declared length");
        }

        return output;
    }

    private static int ReadVarint(ReadOnlySpan<byte> input, ref int position)
    {
        long value = 0;
        for (int index = 0; index < MaxVarintBytes; index++)
        {
            if (position >= input.Length)
            {
                throw Corrupt("the length is truncated");
            }

            byte next = input[position++];
            value |= (long)(next & 0x7F) << (7 * index);
            if (next < 0x80)
            {
                return value > int.MaxValue ? throw Corrupt("the length is too large") : (int)value;
            }
        }

        throw Corrupt("the length is too long");
    }

    private static long ReadLittleEndian(ReadOnlySpan<byte> input, ref int position, int count)
    {
        if (input.Length - position < count)
        {
            throw Corrupt("an element is truncated");
        }

        long value = 0;
        for (int index = 0; index < count; index++)
        {
            value |= (long)input[position++] << (8 * index);
        }

        return value;
    }

    private static int CopyLiteral(ReadOnlySpan<byte> input, ref int position, byte tag, byte[] output, int written)
    {
        int code = tag >> 2;
        long length = code < 60 ? code + 1L : ReadLittleEndian(input, ref position, code - 59) + 1;
        if (length > input.Length - position)
        {
            throw Corrupt("a literal is truncated");
        }

        if (length > output.Length - written)
        {
            throw Corrupt("the output is longer than the declared length");
        }

        input.Slice(position, (int)length).CopyTo(output.AsSpan(written));
        position += (int)length;
        return written + (int)length;
    }

    private static int CopyEarlierOutput(ReadOnlySpan<byte> input, ref int position, byte tag, byte[] output, int written)
    {
        (int length, long offset) = ReadCopyElement(input, ref position, tag);
        if (offset == 0 || offset > written)
        {
            throw Corrupt("a copy offset is zero or before the start of the output");
        }

        if (length > output.Length - written)
        {
            throw Corrupt("the output is longer than the declared length");
        }

        for (int index = 0; index < length; index++)
        {
            output[written + index] = output[written + index - (int)offset];
        }

        return written + length;
    }

    private static (int Length, long Offset) ReadCopyElement(ReadOnlySpan<byte> input, ref int position, byte tag) =>
        (tag & 3) switch
        {
            1 => (4 + ((tag >> 2) & 7), ((long)(tag >> 5) << 8) | ReadLittleEndian(input, ref position, 1)),
            2 => (1 + (tag >> 2), ReadLittleEndian(input, ref position, 2)),
            _ => (1 + (tag >> 2), ReadLittleEndian(input, ref position, 4)),
        };

    private static FormatException Corrupt(string reason) => new($"Corrupt snappy data: {reason}.");
}
