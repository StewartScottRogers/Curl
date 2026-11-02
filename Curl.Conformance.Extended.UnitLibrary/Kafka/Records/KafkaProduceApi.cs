namespace Curl.Conformance.Extended.Kafka.Records;

/// <summary>
/// Produce v3 (api_key 0): reads transactional_id, acks, timeout and the one partition's record batch, keeps what it
/// read for the case to check, stores the batch in <paramref name="log"/> and answers its base offset, or answers
/// <paramref name="errorCode"/> with base offset -1 and stores nothing.
/// </summary>
/// <param name="log">The partition's log.</param>
/// <param name="errorCode">The error code to answer, 0 for none.</param>
public sealed class KafkaProduceApi(KafkaReferenceLog log, short errorCode = 0) : IKafkaReferenceApi
{
    private readonly List<KafkaReferenceProduce> produced = [];

    /// <summary>Gets every Produce request read, in order.</summary>
    public IReadOnlyList<KafkaReferenceProduce> Produced => produced;

    /// <inheritdoc/>
    public short ApiKey => 0;

    /// <inheritdoc/>
    public byte[] Answer(KafkaReferenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        KafkaRecordsReader reader = new(request.Body);
        string? transactionalId = reader.NullableString();
        short acks = reader.Int16();
        int timeout = reader.Int32();
        reader.Int32();
        string topic = reader.NullableString()!;
        reader.Int32();
        int partition = reader.Int32();
        byte[] batch = reader.NullableBytes()!;
        produced.Add(new KafkaReferenceProduce(transactionalId, acks, timeout, topic, partition, KafkaReferenceRecordBatch.Read(batch)));
        long baseOffset = errorCode == 0 ? log.Append(batch) : -1;
        return new KafkaRecordsWriter().Int32(1).String(topic).Int32(1)
            .Int32(partition).Int16(errorCode).Int64(baseOffset).Int64(-1)
            .Int32(0).ToArray();
    }
}
