using System.IO.Compression;
using Curl.Extension.Kafka.Wire.Compression;
using Curl.Extension.Kafka.Wire.Compression.Zstd;

namespace Curl.Extension.Kafka.Wire;

/// <summary>
/// Writes and reads record batches with magic 2 (the protocol guide's "Messages", "Record Batch" and "Record"):
/// a batch header whose CRC-32C covers everything from the attributes on, then records with zig-zag varint fields.
/// Reading takes uncompressed, gzip, snappy, lz4 and zstd batches, skips control batches and a partial batch at the end
/// of a fetch, and keeps only records at or after the offset asked for.
/// </summary>
internal static class KafkaRecordBatch
{
    /// <summary>The record batch format version this extension writes and reads.</summary>
    internal const sbyte Magic = 2;

    /// <summary>The attributes' compression codec for gzip.</summary>
    internal const int GzipCodec = 1;

    /// <summary>The attributes' compression codec for snappy, raw or xerial-framed.</summary>
    internal const int SnappyCodec = 2;

    /// <summary>The attributes' compression codec for the LZ4 frame format.</summary>
    internal const int Lz4Codec = 3;

    /// <summary>The attributes' compression codec for zstd (RFC 8878).</summary>
    internal const int ZstdCodec = 4;

    /// <summary>The attributes' bit that marks a control batch (transaction markers).</summary>
    internal const short ControlFlag = 0x20;

    private const int CompressionMask = 0x07;

    private const int BatchPrefixLength = sizeof(long) + sizeof(int);

    private const int CrcCoveredStart = sizeof(int) + sizeof(sbyte) + sizeof(uint);

    /// <summary>The bytes from <c>baseTimestamp</c> to <c>baseSequence</c> that reading skips.</summary>
    private const int SkippedHeaderLength = (3 * sizeof(long)) + sizeof(short) + sizeof(int);

    /// <summary>The codecs the BCL has no decoder for, each read by one written by hand under <c>Wire/Compression</c>.</summary>
    private static readonly Dictionary<int, HandWrittenDecoder> HandWrittenDecoders = new()
    {
        [SnappyCodec] = new("snappy", SnappyDecompressor.Decompress),
        [Lz4Codec] = new("lz4", Lz4FrameDecompressor.Decompress),
        [ZstdCodec] = new("zstd", ZstdFrameDecompressor.Decompress),
    };

    /// <summary>Gives a batch of one record with no key and no headers whose value is <paramref name="value"/>.</summary>
    internal static byte[] Encode(ReadOnlySpan<byte> value, long timestampMilliseconds)
    {
        byte[] record = new KafkaWriter().Int8(0).VarLong(0).VarInt(0).VarInt(-1).VarInt(value.Length).Raw(value).VarInt(0).ToArray();
        byte[] covered = new KafkaWriter()
            .Int16(0)
            .Int32(0)
            .Int64(timestampMilliseconds)
            .Int64(timestampMilliseconds)
            .Int64(-1)
            .Int16(-1)
            .Int32(-1)
            .Int32(1)
            .VarInt(record.Length)
            .Raw(record)
            .ToArray();
        return new KafkaWriter().Int64(0).Int32(CrcCoveredStart + covered.Length).Int32(-1).Int8(Magic).UInt32(Crc32C.Compute(covered)).Raw(covered).ToArray();
    }

    /// <summary>
    /// Reads the batches in a fetch's <c>records</c>, keeping records at or after <paramref name="fromOffset"/>; the
    /// result's next offset follows the last whole batch, control batches included.
    /// </summary>
    internal static KafkaRecordSet Decode(ReadOnlyMemory<byte> records, long fromOffset)
    {
        List<KafkaRecord> found = [];
        long nextOffset = fromOffset;
        KafkaReader reader = new(records);
        while (reader.Remaining >= BatchPrefixLength)
        {
            long baseOffset = reader.Int64();
            int length = reader.Int32();
            if (length > reader.Remaining)
            {
                break;
            }

            long lastOffset = DecodeBatch(baseOffset, reader.Take(length), fromOffset, found);
            nextOffset = Math.Max(nextOffset, lastOffset + 1);
        }

        return new KafkaRecordSet(found, nextOffset);
    }

    private static long DecodeBatch(long baseOffset, ReadOnlyMemory<byte> batch, long fromOffset, List<KafkaRecord> found)
    {
        KafkaReader reader = new(batch);
        reader.Int32();
        sbyte magic = reader.Int8();
        if (magic != Magic)
        {
            throw new FormatException($"Kafka record batch has magic {magic}, not {Magic}");
        }

        uint crc = reader.UInt32();
        if (Crc32C.Compute(batch.Span[CrcCoveredStart..]) != crc)
        {
            throw new FormatException("Kafka record batch fails its CRC-32C check");
        }

        short attributes = reader.Int16();
        int lastOffsetDelta = reader.Int32();
        reader.Take(SkippedHeaderLength);
        int count = reader.Int32();
        if ((attributes & ControlFlag) == 0)
        {
            KafkaReader records = new(Decompressed(attributes & CompressionMask, reader.Take(reader.Remaining)));
            ReadRecords(records, count, baseOffset, fromOffset, found);
        }

        return baseOffset + lastOffsetDelta;
    }

    private static ReadOnlyMemory<byte> Decompressed(int codec, ReadOnlyMemory<byte> records) => codec switch
    {
        0 => records,
        GzipCodec => Gunzipped(records),
        _ when HandWrittenDecoders.TryGetValue(codec, out HandWrittenDecoder? decoder) => Decoded(decoder, records),
        _ => throw new FormatException($"Kafka record batch uses compression codec {codec}, which the kafka extension does not read"),
    };

    private static byte[] Decoded(HandWrittenDecoder decoder, ReadOnlyMemory<byte> records)
    {
        try
        {
            return decoder.Decompress(records.Span);
        }
        catch (FormatException corrupt)
        {
            throw new FormatException($"Kafka record batch holds bad {decoder.CodecName} data", corrupt);
        }
    }

    private static byte[] Gunzipped(ReadOnlyMemory<byte> records)
    {
        try
        {
            using GZipStream gzip = new(new MemoryStream(records.ToArray()), CompressionMode.Decompress);
            using MemoryStream plain = new();
            gzip.CopyTo(plain);
            return plain.ToArray();
        }
        catch (InvalidDataException)
        {
            throw new FormatException("Kafka record batch holds bad gzip data");
        }
    }

    private static void ReadRecords(KafkaReader reader, int count, long baseOffset, long fromOffset, List<KafkaRecord> found)
    {
        for (int index = 0; index < count; index++)
        {
            KafkaReader record = new(reader.Take(reader.VarInt()));
            record.Int8();
            record.VarLong();
            long offset = baseOffset + record.VarInt();
            record.NullableVarBytes();
            ReadOnlyMemory<byte> value = record.NullableVarBytes() ?? ReadOnlyMemory<byte>.Empty;
            if (offset >= fromOffset)
            {
                found.Add(new KafkaRecord(offset, value));
            }
        }
    }

    /// <summary>Decompresses a codec's bytes, throwing <see cref="FormatException"/> when they are corrupt.</summary>
    private delegate byte[] Decompressor(ReadOnlySpan<byte> input);

    /// <summary>A codec's name, as a corrupt batch's message gives it, and its hand-written decompressor.</summary>
    private sealed record HandWrittenDecoder(string CodecName, Decompressor Decompress);
}
