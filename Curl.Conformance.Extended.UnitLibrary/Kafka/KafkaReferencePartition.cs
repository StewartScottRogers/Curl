namespace Curl.Conformance.Extended.Kafka;

/// <summary>A partition as Metadata lists it.</summary>
/// <param name="Index">Its partition_index.</param>
/// <param name="ErrorCode">Its error_code.</param>
/// <param name="LeaderId">Its leader_id, -1 for none.</param>
public sealed record KafkaReferencePartition(int Index, short ErrorCode, int LeaderId);
