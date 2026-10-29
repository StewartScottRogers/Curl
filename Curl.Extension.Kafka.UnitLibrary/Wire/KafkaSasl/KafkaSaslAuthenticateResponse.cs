namespace Curl.Extension.Kafka.Wire.KafkaSasl;

/// <summary>A SaslAuthenticate response: its error code and the broker's error message, if it gave one.</summary>
internal sealed record KafkaSaslAuthenticateResponse(short ErrorCode, string? ErrorMessage);
