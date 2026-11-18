namespace Curl.Extended.Protocol.Kafka.Wire.KafkaSasl;

/// <summary>The error messages a refused SASL PLAIN authentication ends with (ADR-1082).</summary>
internal static class KafkaSaslMessages
{
    /// <summary>The broker does not enable PLAIN; <paramref name="mechanisms"/> are the ones it does (none: the listener has no SASL).</summary>
    internal static string PlainNotEnabled(IReadOnlyList<string> mechanisms) =>
        mechanisms.Count == 0
            ? "SASL is not enabled on the Kafka listener, it offers no SASL mechanism"
            : $"Kafka broker does not enable SASL PLAIN, it enables: {string.Join(", ", mechanisms)}";

    /// <summary>The broker refused the credentials with <paramref name="errorCode"/> and <paramref name="errorMessage"/>.</summary>
    internal static string AuthenticationFailed(short errorCode, string? errorMessage) =>
        $"Kafka SASL PLAIN authentication failed: {errorMessage ?? $"error {errorCode}"}";
}
