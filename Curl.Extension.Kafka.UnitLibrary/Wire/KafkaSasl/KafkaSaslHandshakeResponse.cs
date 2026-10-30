namespace Curl.Extension.Kafka.Wire.KafkaSasl;

/// <summary>A SaslHandshake response: its error code and the mechanisms the broker enables.</summary>
internal sealed record KafkaSaslHandshakeResponse(short ErrorCode, IReadOnlyList<string> Mechanisms);
