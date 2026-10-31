namespace Curl.Extended.Protocol.Kafka.Wire;

/// <summary>
/// One partition's answer: its <c>error_code</c>, an offset (Produce's <c>base_offset</c>, Fetch's
/// <c>high_watermark</c>) and, for Fetch, its <c>records</c>.
/// </summary>
internal sealed record KafkaPartitionResponse(short ErrorCode, long Offset, ReadOnlyMemory<byte> Records);
