using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ws;

/// <summary>
/// Ends a WebSocket transfer with a curl exit code and the message curl prints for it;
/// <see cref="WsProtocolHandler" /> turns it into a failed <see cref="TransferResult" />.
/// </summary>
/// <param name="exitCode">The exit code the transfer ends with.</param>
/// <param name="message">The message curl prints after <c>curl: (n) </c>.</param>
internal sealed class WsTransferException(CurlExitCode exitCode, string message) : Exception(message)
{
    /// <summary>Gets the exit code the transfer ends with.</summary>
    internal CurlExitCode ExitCode { get; } = exitCode;

    /// <summary>
    /// Gets a value indicating whether a received frame broke the protocol, which curl 8.21.0's
    /// <c>-v</c> follows with its <c>[WS] decode frame error</c> and <c>[WS] decode payload
    /// error</c> lines (BL-813).
    /// </summary>
    internal bool IsFrameViolation { get; init; }
}
