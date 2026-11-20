namespace Curl.Extended.Protocol.Kafka.Wire.KafkaSasl;

/// <summary>A SaslAuthenticate response: its error code, the broker's error message if it gave one, and its <c>auth_bytes</c> (a SCRAM message; empty for PLAIN).</summary>
internal sealed record KafkaSaslAuthenticateResponse(short ErrorCode, string? ErrorMessage, ReadOnlyMemory<byte> AuthBytes = default);
