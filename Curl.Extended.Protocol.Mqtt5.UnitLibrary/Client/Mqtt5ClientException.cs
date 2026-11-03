using Curl.Extended.Protocol.Mqtt5.Packets;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Mqtt5.Client;

/// <summary>
/// Ends an MQTT 5 transfer with the exit code curl's <c>mqtt://</c> gives the equivalent failure (ADR-1049). A failure the
/// server caused by sending a Malformed Packet or a Protocol Error carries the reason code the client's closing
/// DISCONNECT gives (section 4.13.1).
/// </summary>
internal sealed class Mqtt5ClientException(CurlExitCode exitCode, string message, Mqtt5ReasonCode? disconnectReasonCode = null) : Exception(message)
{
    /// <summary>The exit code the transfer ends with.</summary>
    internal CurlExitCode ExitCode { get; } = exitCode;

    /// <summary>The reason code of the DISCONNECT the client sends before it closes, or none when it sends none.</summary>
    internal Mqtt5ReasonCode? DisconnectReasonCode { get; } = disconnectReasonCode;
}
