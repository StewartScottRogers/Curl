using Curl.Protocol.Abstractions;

namespace Curl.Extension.Mqtt5.Client;

/// <summary>Ends an MQTT 5 transfer with the exit code curl's <c>mqtt://</c> gives the equivalent failure (ADR-1049).</summary>
internal sealed class Mqtt5ClientException(CurlExitCode exitCode, string message) : Exception(message)
{
    /// <summary>The exit code the transfer ends with.</summary>
    internal CurlExitCode ExitCode { get; } = exitCode;
}
