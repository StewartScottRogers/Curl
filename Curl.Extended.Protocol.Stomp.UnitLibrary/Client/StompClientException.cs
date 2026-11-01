using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Stomp.Client;

/// <summary>Ends a STOMP transfer with the exit code ADR-1059 gives the failure.</summary>
internal sealed class StompClientException(CurlExitCode exitCode, string message) : Exception(message)
{
    /// <summary>The exit code the transfer ends with.</summary>
    internal CurlExitCode ExitCode { get; } = exitCode;
}
