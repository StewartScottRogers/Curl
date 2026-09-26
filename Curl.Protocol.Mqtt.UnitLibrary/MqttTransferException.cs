using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Mqtt;

/// <summary>
/// Ends an MQTT session early with the curl exit code and message the transfer reports.
/// Thrown inside the session and caught by <see cref="MqttProtocolHandler" />, which
/// turns it into a <see cref="TransferResult" />; it never leaves this library.
/// </summary>
/// <param name="exitCode">The curl exit code the transfer reports.</param>
/// <param name="message">The message curl prints for the failure.</param>
internal sealed class MqttTransferException(CurlExitCode exitCode, string message)
    : Exception(message)
{
    /// <summary>
    /// Gets the curl exit code the transfer reports.
    /// </summary>
    internal CurlExitCode ExitCode { get; } = exitCode;
}
