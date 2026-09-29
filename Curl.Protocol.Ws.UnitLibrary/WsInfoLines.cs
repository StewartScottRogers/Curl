using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ws;

/// <summary>
/// The <c>-v</c> information lines curl 8.21.0 writes around a WebSocket upgrade and transfer,
/// each measured with <c>Record-CurlExchange.ps1</c> (BL-584).
/// </summary>
internal static class WsInfoLines
{
    /// <summary>The line written once the connection is up, before the upgrade request.</summary>
    internal const string UsingHttp1 = "using HTTP/1.x";

    /// <summary>The line written once the upgrade request has been sent.</summary>
    internal const string RequestSent = "Request completely sent off";

    /// <summary>The first of the two lines written once a <c>101</c> head has been read.</summary>
    internal const string SwitchingToWebSocket = "Received 101, Switching to WebSocket";

    /// <summary>The second of the two lines written once a <c>101</c> head has been read.</summary>
    internal const string SwitchedToWebSocket = "[WS] Received 101, switch to WebSocket";

    /// <summary>
    /// Formats the line for the upload frame sent after the upgrade.
    /// </summary>
    /// <param name="frameLength">The frame's length in bytes, head included.</param>
    /// <returns>The line, such as <c>upload completely sent off: 10 bytes</c>.</returns>
    internal static string UploadSent(long frameLength) =>
        string.Create(CultureInfo.InvariantCulture, $"upload completely sent off: {frameLength} bytes");

    /// <summary>
    /// Formats the first of the two lines curl writes after a frame violation's message
    /// (BL-813).
    /// </summary>
    /// <param name="exitCode">The exit code the violation ends the transfer with.</param>
    /// <returns>The line, such as <c>[WS] decode frame error 56</c>.</returns>
    internal static string DecodeFrameError(CurlExitCode exitCode) =>
        string.Create(CultureInfo.InvariantCulture, $"[WS] decode frame error {(int)exitCode}");

    /// <summary>
    /// Formats the second of the two lines curl writes after a frame violation's message
    /// (BL-813).
    /// </summary>
    /// <param name="exitCode">The exit code the violation ends the transfer with.</param>
    /// <returns>The line, such as <c>[WS] decode payload error 56</c>.</returns>
    internal static string DecodePayloadError(CurlExitCode exitCode) =>
        string.Create(CultureInfo.InvariantCulture, $"[WS] decode payload error {(int)exitCode}");

    /// <summary>
    /// Formats the line for a transfer that ended after the upgrade, by the server closing the
    /// connection or with <c>Empty reply from server</c>.
    /// </summary>
    /// <param name="connectionNumber">curl's number for the connection.</param>
    /// <returns>The line, such as <c>shutting down connection #0</c>.</returns>
    internal static string ShuttingDown(long connectionNumber) =>
        string.Create(CultureInfo.InvariantCulture, $"shutting down connection #{connectionNumber}");

    /// <summary>Formats the line for any other failed transfer.</summary>
    /// <param name="connectionNumber">curl's number for the connection.</param>
    /// <returns>The line, such as <c>closing connection #0</c>.</returns>
    internal static string Closing(long connectionNumber) =>
        string.Create(CultureInfo.InvariantCulture, $"closing connection #{connectionNumber}");
}
