namespace Curl.Conformance.Extended.Kafka.Records;

/// <summary>
/// Record batch v2 (magic 2), field by field, as the protocol guide's "Record Batch" defines it: written by the
/// reference broker for a fetch, and read back from a produce so a case checks each field and the CRC-32C, which
/// covers the attributes onward.
/// </summary>
/// <param name="BaseOffset">Its baseOffset.</param>
/// <param name="PartitionLeaderEpoch">Its partitionLeaderEpoch.</param>
/// <param name="Magic">Its magic.</param>
/// <param name="Crc">Its crc as sent.</param>
/// <param name="CrcMatches">Whether <paramref name="Crc"/> is the CRC-32C of the attributes onward.</param>
/// <param name="Attributes">Its attributes.</param>
/// <param name="LastOffsetDelta">Its lastOffsetDelta.</param>
/// <param name="BaseTimestamp">Its baseTimestamp.</param>
/// <param name="MaxTimestamp">Its maxTimestamp.</param>
/// <param name="ProducerId">Its producerId.</param>
/// <param name="ProducerEpoch">Its producerEpoch.</param>
/// <param name="BaseSequence">Its baseSequence.</param>
/// <param name="Records">Its records.</param>
public sealed record KafkaReferenceRecordBatch(
    long BaseOffset,
    int PartitionLeaderEpoch,
    sbyte Magic,
    uint Crc,
    bool CrcMatches,
    short Attributes,
    int LastOffsetDelta,
    long BaseTimestamp,
    long MaxTimestamp,
    long ProducerId,
    short ProducerEpoch,
    int BaseSequence,
    IReadOnlyList<KafkaReferenceRecord> Records)
{
    /// <summary>The attributes bit that marks a control batch.</summary>
    public const short ControlFlag = 0x20;

    // baseOffset INT64, batchLength INT32, partitionLeaderEpoch INT32, magic INT8, crc UINT32.
    private const int CrcCoveredStart = sizeof(long) + sizeof(int) + sizeof(int) + sizeof(sbyte) + sizeof(uint);

    /// <summary>Writes an uncompressed batch of <paramref name="records"/> with no producer, and its CRC-32C.</summary>
    /// <param name="baseOffset">Its base offset.</param>
    /// <param name="records">Its records; the last one's offset delta is the batch's last offset delta.</param>
    /// <param name="attributes">Its attributes (0, or <see cref="ControlFlag"/>).</param>
    /// <param name="baseTimestamp">Its base timestamp.</param>
    /// <returns>The batch's bytes.</returns>
    public static byte[] Write(long baseOffset, IReadOnlyList<KafkaReferenceRecord> records, short attributes = 0, long baseTimestamp = 1_700_000_000_000)
    {
        ArgumentNullException.ThrowIfNull(records);
        KafkaRecordsWriter covered = new KafkaRecordsWriter()
            .Int16(attributes)
            .Int32(records[^1].OffsetDelta)
            .Int64(baseTimestamp)
            .Int64(baseTimestamp + records.Max(record => record.TimestampDelta))
            .Int64(-1).Int16(-1).Int32(-1)
            .Int32(records.Count);
        foreach (KafkaReferenceRecord record in records)
        {
            byte[] bytes = WriteRecord(record);
            covered.VarLong(bytes.Length).Raw(bytes);
        }

        byte[] tail = covered.ToArray();
        return new KafkaRecordsWriter().Int64(baseOffset).Int32(sizeof(int) + sizeof(sbyte) + sizeof(uint) + tail.Length)
            .Int32(0).Int8(2).UInt32(KafkaReferenceCrc32C.Compute(tail)).Raw(tail).ToArray();
    }

    /// <summary>Reads one whole batch.</summary>
    /// <param name="batch">The batch's bytes, from its base offset to its last record.</param>
    /// <returns>The batch.</returns>
    public static KafkaReferenceRecordBatch Read(ReadOnlyMemory<byte> batch)
    {
        KafkaRecordsReader reader = new(batch);
        long baseOffset = reader.Int64();
        reader.Int32();
        int leaderEpoch = reader.Int32();
        sbyte magic = reader.Int8();
        uint crc = reader.UInt32();
        bool crcMatches = KafkaReferenceCrc32C.Compute(batch.Span[CrcCoveredStart..]) == crc;
        short attributes = reader.Int16();
        int lastOffsetDelta = reader.Int32();
        long baseTimestamp = reader.Int64();
        long maxTimestamp = reader.Int64();
        long producerId = reader.Int64();
        short producerEpoch = reader.Int16();
        int baseSequence = reader.Int32();
        List<KafkaReferenceRecord> records = [];
        for (int count = reader.Int32(); count > 0; count--)
        {
            records.Add(ReadRecord(new KafkaRecordsReader(reader.Take((int)reader.VarLong()))));
        }

        return new(baseOffset, leaderEpoch, magic, crc, crcMatches, attributes, lastOffsetDelta, baseTimestamp, maxTimestamp, producerId, producerEpoch, baseSequence, records);
    }

    private static byte[] WriteRecord(KafkaReferenceRecord record)
    {
        KafkaRecordsWriter writer = new KafkaRecordsWriter().Int8(0).VarLong(record.TimestampDelta).VarLong(record.OffsetDelta)
            .NullableVarBytes(record.Key).NullableVarBytes(record.Value).VarLong(record.Headers.Count);
        foreach (KeyValuePair<string, byte[]?> header in record.Headers)
        {
            writer.NullableVarBytes(System.Text.Encoding.UTF8.GetBytes(header.Key)).NullableVarBytes(header.Value);
        }

        return writer.ToArray();
    }

    private static KafkaReferenceRecord ReadRecord(KafkaRecordsReader reader)
    {
        reader.Int8();
        long timestampDelta = reader.VarLong();
        int offsetDelta = (int)reader.VarLong();
        byte[]? key = reader.NullableVarBytes();
        byte[]? value = reader.NullableVarBytes();
        List<KeyValuePair<string, byte[]?>> headers = [];
        for (long count = reader.VarLong(); count > 0; count--)
        {
            headers.Add(new(System.Text.Encoding.UTF8.GetString(reader.NullableVarBytes()!), reader.NullableVarBytes()));
        }

        return new(offsetDelta, timestampDelta, key, value, headers);
    }
}
