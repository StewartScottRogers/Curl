using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Kafka.Client;

/// <summary>Ends a Kafka transfer with the exit code ADR-1076 gives the failure.</summary>
internal sealed class KafkaClientException(CurlExitCode exitCode, string message) : Exception(message)
{
    /// <summary>The exit code the transfer ends with.</summary>
    internal CurlExitCode ExitCode { get; } = exitCode;
}
