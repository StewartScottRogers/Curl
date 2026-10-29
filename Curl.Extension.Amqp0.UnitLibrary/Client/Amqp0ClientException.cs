using Curl.Protocol.Abstractions;

namespace Curl.Extension.Amqp0.Client;

/// <summary>Ends an AMQP 0-9-1 transfer with the exit code ADR-1060 gives the failure.</summary>
internal sealed class Amqp0ClientException(CurlExitCode exitCode, string message) : Exception(message)
{
    /// <summary>The exit code the transfer ends with.</summary>
    internal CurlExitCode ExitCode { get; } = exitCode;
}
