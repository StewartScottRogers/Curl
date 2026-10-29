namespace Curl.Extension.Kafka.Wire.KafkaSasl;

/// <summary>The error messages a refused SASL PLAIN authentication ends with (ADR-1082).</summary>
internal static class KafkaSaslMessages
{
    /// <summary>The broker does not enable PLAIN; <paramref name="mechanisms"/> are the ones it does.</summary>
    internal static string PlainNotEnabled(IReadOnlyList<string> mechanisms) =>
        $"Kafka broker does not enable SASL PLAIN, it enables: {string.Join(", ", mechanisms)}";

    /// <summary>The broker refused the credentials with <paramref name="errorCode"/> and <paramref name="errorMessage"/>.</summary>
    internal static string AuthenticationFailed(short errorCode, string? errorMessage) =>
        $"Kafka SASL PLAIN authentication failed: {errorMessage ?? $"error {errorCode}"}";
}
