using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Redis.Client;

/// <summary>Ends a Redis transfer with the exit code ADR-1079 gives the failure.</summary>
internal sealed class RedisClientException(CurlExitCode exitCode, string message) : Exception(message)
{
    /// <summary>The exit code the transfer ends with.</summary>
    internal CurlExitCode ExitCode { get; } = exitCode;
}
