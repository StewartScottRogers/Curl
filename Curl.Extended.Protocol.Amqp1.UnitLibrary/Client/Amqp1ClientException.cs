using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Amqp1.Client;

/// <summary>Ends an AMQP 1.0 transfer with the exit code ADR-1062 maps the failure to.</summary>
internal sealed class Amqp1ClientException(CurlExitCode exitCode, string message) : Exception(message)
{
    /// <summary>The exit code the transfer ends with.</summary>
    internal CurlExitCode ExitCode { get; } = exitCode;
}
