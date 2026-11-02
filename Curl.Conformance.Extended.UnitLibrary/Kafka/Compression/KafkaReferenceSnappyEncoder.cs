using Curl.Conformance.Extended.Kafka.Records;

namespace Curl.Conformance.Extended.Kafka.Compression;

/// <summary>
/// Encodes raw Snappy (google/snappy format_description.txt): the uncompressed length as a varint, then literal and
/// copy elements, every copy written as the one element <see cref="KafkaSnappyCopy"/> names, and frames raw Snappy chunks
/// as xerial, the framing Kafka's Java producer writes (snappy-java's SnappyOutputStream).
/// </summary>
public static class KafkaReferenceSnappyEncoder
{
    private const int OneByteOffsetLimit = 2047;
    private const int OneByteOffsetLongest = 11;
    private const int TwoByteOffsetLimit = 65535;
    private const int CopyLongest = 64;
    private const int OneByteLiteralTag = 60;

    private static ReadOnlySpan<byte> XerialMagic => [0x82, (byte)'S', (byte)'N', (byte)'A', (byte)'P', (byte)'P', (byte)'Y', 0x00];

    /// <summary>Encodes <paramref name="data"/> as raw Snappy.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="copy">The element every copy is written as.</param>
    /// <returns>The raw Snappy bytes.</returns>
    public static byte[] Compress(byte[] data, KafkaSnappyCopy copy)
    {
        ArgumentNullException.ThrowIfNull(data);
        List<byte> output = [];
        uint remaining = (uint)data.Length;
        for (; remaining >= 0x80; remaining >>= 7)
        {
            output.Add((byte)((remaining & 0x7F) | 0x80));
        }

        output.Add((byte)remaining);
        KafkaReferenceMatchFinder finder = new(data);
        (int maximumOffset, int maximumLength) = copy switch
        {
            KafkaSnappyCopy.OneByteOffset => (OneByteOffsetLimit, OneByteOffsetLongest),
            KafkaSnappyCopy.TwoByteOffset => (TwoByteOffsetLimit, CopyLongest),
            _ => (int.MaxValue, CopyLongest),
        };
        int literalStart = 0;
        int position = 0;
        while (position + KafkaReferenceMatchFinder.MinimumLength <= data.Length)
        {
            (int offset, int length) = finder.Find(position, 0, maximumOffset, maximumLength, data.Length);
            if (length == 0)
            {
                position++;
                continue;
            }

            WriteLiteral(output, data.AsSpan(literalStart, position - literalStart));
            WriteCopy(output, copy, offset, length);
            position += length;
            literalStart = position;
        }

        WriteLiteral(output, data.AsSpan(literalStart));
        return [.. output];
    }

    /// <summary>Frames raw Snappy chunks as xerial: the magic, version 1, compatible version 1, then each chunk's big-endian length and bytes.</summary>
    /// <param name="chunks">The raw Snappy chunks.</param>
    /// <returns>The framed bytes.</returns>
    public static byte[] Xerial(params byte[][] chunks)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        KafkaRecordsWriter writer = new KafkaRecordsWriter().Raw(XerialMagic).Int32(1).Int32(1);
        foreach (byte[] chunk in chunks)
        {
            writer.Int32(chunk.Length).Raw(chunk);
        }

        return writer.ToArray();
    }

    private static void WriteLiteral(List<byte> output, ReadOnlySpan<byte> literal)
    {
        if (literal.IsEmpty)
        {
            return;
        }

        int lengthLessOne = literal.Length - 1;
        if (lengthLessOne < OneByteLiteralTag)
        {
            output.Add((byte)(lengthLessOne << 2));
        }
        else if (lengthLessOne <= byte.MaxValue)
        {
            output.AddRange([OneByteLiteralTag << 2, (byte)lengthLessOne]);
        }
        else
        {
            output.AddRange([(OneByteLiteralTag + 1) << 2, (byte)lengthLessOne, (byte)(lengthLessOne >> 8)]);
        }

        output.AddRange(literal);
    }

    private static void WriteCopy(List<byte> output, KafkaSnappyCopy copy, int offset, int length)
    {
        switch (copy)
        {
            case KafkaSnappyCopy.OneByteOffset:
                output.AddRange([(byte)(1 | ((length - 4) << 2) | ((offset >> 8) << 5)), (byte)offset]);
                break;
            case KafkaSnappyCopy.TwoByteOffset:
                output.AddRange([(byte)(2 | ((length - 1) << 2)), (byte)offset, (byte)(offset >> 8)]);
                break;
            default:
                output.AddRange([(byte)(3 | ((length - 1) << 2)), (byte)offset, (byte)(offset >> 8), (byte)(offset >> 16), (byte)(offset >> 24)]);
                break;
        }
    }
}
