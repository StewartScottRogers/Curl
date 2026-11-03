using System.Buffers.Binary;
using Curl.Conformance.Extended.Kafka.Records;

namespace Curl.Conformance.Extended.Kafka.Compression;

/// <summary>
/// Writes a record batch v2 whose records are compressed, as the "Record Batch" definition has it: the header as an
/// uncompressed batch has it, the attributes' low three bits naming the codec, and the records after the record count
/// replaced by what the case's encoder makes of them, with batchLength and the CRC-32C written for the new bytes.
/// </summary>
public static class KafkaCompressedRecordBatch
{
    /// <summary>The attributes' codec for gzip.</summary>
    public const short Gzip = 1;

    /// <summary>The attributes' codec for Snappy.</summary>
    public const short Snappy = 2;

    /// <summary>The attributes' codec for LZ4.</summary>
    public const short Lz4 = 3;

    /// <summary>The attributes' codec for zstd.</summary>
    public const short Zstd = 4;

    // baseOffset, batchLength, partitionLeaderEpoch, magic, crc, attributes, lastOffsetDelta, baseTimestamp,
    // maxTimestamp, producerId, producerEpoch, baseSequence and the records count.
    private const int RecordsStart = 61;

    private const int BatchLengthPosition = sizeof(long);

    private const int CrcPosition = 17;

    private const int AttributesPosition = 21;

    /// <summary>Writes a batch of <paramref name="records"/> compressed with <paramref name="codec"/>.</summary>
    /// <param name="records">Its records.</param>
    /// <param name="codec">The attributes' codec.</param>
    /// <param name="compress">The encoder, given the records' bytes as an uncompressed batch holds them.</param>
    /// <returns>The batch's bytes, base offset 0.</returns>
    public static byte[] Write(IReadOnlyList<KafkaReferenceRecord> records, short codec, Func<byte[], byte[]> compress)
    {
        ArgumentNullException.ThrowIfNull(compress);
        byte[] plain = KafkaReferenceRecordBatch.Write(0, records);
        byte[] batch = [.. plain.AsSpan(0, RecordsStart), .. compress(plain[RecordsStart..])];
        BinaryPrimitives.WriteInt32BigEndian(batch.AsSpan(BatchLengthPosition), batch.Length - BatchLengthPosition - sizeof(int));
        BinaryPrimitives.WriteInt16BigEndian(batch.AsSpan(AttributesPosition), codec);
        BinaryPrimitives.WriteUInt32BigEndian(batch.AsSpan(CrcPosition), KafkaReferenceCrc32C.Compute(batch.AsSpan(AttributesPosition)));
        return batch;
    }
}
