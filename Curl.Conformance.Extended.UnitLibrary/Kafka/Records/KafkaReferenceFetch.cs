namespace Curl.Conformance.Extended.Kafka.Records;

/// <summary>One Fetch v4 request as the reference broker read it.</summary>
/// <param name="ReplicaId">Its replica_id (-1 for a consumer).</param>
/// <param name="MaxWaitMilliseconds">Its max_wait_ms.</param>
/// <param name="MinBytes">Its min_bytes.</param>
/// <param name="MaxBytes">Its max_bytes.</param>
/// <param name="IsolationLevel">Its isolation_level (0 READ_UNCOMMITTED, 1 READ_COMMITTED).</param>
/// <param name="Topic">The topic it names.</param>
/// <param name="Partition">The partition it names.</param>
/// <param name="FetchOffset">The partition's fetch_offset.</param>
/// <param name="PartitionMaxBytes">The partition's max_bytes.</param>
public sealed record KafkaReferenceFetch(int ReplicaId, int MaxWaitMilliseconds, int MinBytes, int MaxBytes, sbyte IsolationLevel, string Topic, int Partition, long FetchOffset, int PartitionMaxBytes);
