namespace Curl.Extended.Protocol.Kafka.Wire;

/// <summary>One record read from a fetch: its offset and its value, empty for a null value.</summary>
internal sealed record KafkaRecord(long Offset, ReadOnlyMemory<byte> Value);
