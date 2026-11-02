namespace Curl.Conformance.Extended.Kafka.Records;

/// <summary>One Produce v3 request as the reference broker read it.</summary>
/// <param name="TransactionalId">Its transactional_id, null outside a transaction.</param>
/// <param name="Acks">Its acks (-1 waits for every in-sync replica).</param>
/// <param name="TimeoutMilliseconds">Its timeout.</param>
/// <param name="Topic">The topic it names.</param>
/// <param name="Partition">The partition it names.</param>
/// <param name="Batch">The record batch it carries.</param>
public sealed record KafkaReferenceProduce(string? TransactionalId, short Acks, int TimeoutMilliseconds, string Topic, int Partition, KafkaReferenceRecordBatch Batch);
