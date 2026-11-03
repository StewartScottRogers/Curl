namespace Curl.Conformance.Extended.Kafka;

/// <summary>One request the client sent, split at request header v1.</summary>
/// <param name="ApiKey">The header's api_key.</param>
/// <param name="ApiVersion">The header's api_version.</param>
/// <param name="CorrelationId">The header's correlation_id.</param>
/// <param name="ClientId">The header's client_id.</param>
/// <param name="Body">The request body after the header.</param>
public sealed record KafkaReferenceRequest(short ApiKey, short ApiVersion, int CorrelationId, string ClientId, ReadOnlyMemory<byte> Body);
