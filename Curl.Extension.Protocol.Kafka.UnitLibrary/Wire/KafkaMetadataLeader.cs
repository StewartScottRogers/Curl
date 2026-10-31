namespace Curl.Extension.Protocol.Kafka.Wire;

/// <summary>
/// What a Metadata response says about one partition: the <see cref="Host"/> and <see cref="Port"/> of the broker that
/// leads it with <see cref="ErrorCode"/> 0, or the error code that explains why no broker does.
/// </summary>
internal sealed record KafkaMetadataLeader(short ErrorCode, string Host, int Port);
