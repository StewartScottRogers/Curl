namespace Curl.Extension.Kafka.Wire;

/// <summary>The records a fetch's batches held at or after the offset asked for, and the offset to fetch next.</summary>
internal sealed record KafkaRecordSet(IReadOnlyList<KafkaRecord> Records, long NextOffset);
