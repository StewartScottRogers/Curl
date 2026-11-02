namespace Curl.Conformance.Extended.Kafka;

/// <summary>A topic as Metadata lists it.</summary>
/// <param name="Name">Its name.</param>
/// <param name="ErrorCode">Its error_code.</param>
/// <param name="Partitions">Its partitions.</param>
public sealed record KafkaReferenceTopic(string Name, short ErrorCode, IReadOnlyList<KafkaReferencePartition> Partitions);
