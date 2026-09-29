using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Rtsp;

/// <summary>
/// The <c>-v</c> information lines curl 8.21.0 writes during an RTSP transfer, measured with
/// <c>Record-CurlExchange.ps1</c> (ADR-0169, BL-593), and what becomes of the connection each
/// outcome leaves.
/// </summary>
internal static class RtspVerboseLines
{
    /// <summary>The line written once the request has been sent.</summary>
    internal const string RequestSent = "Request completely sent off";

    /// <summary>
    /// Determines whether curl keeps the connection a transfer ended with
    /// <paramref name="result" />: after a success, a <c>CSeq</c> mismatch (85) or a refused
    /// <c>-H Session</c> header (43), unless a reply body was read; never after any other
    /// failure.
    /// </summary>
    /// <param name="result">The transfer's result.</param>
    /// <returns><see langword="true" /> when the connection is left intact for reuse.</returns>
    internal static bool LeavesIntact(TransferResult result) =>
        result.ExitCode is CurlExitCode.Ok or CurlExitCode.RtspCseqError or CurlExitCode.BadFunctionArgument
        && result.BytesTransferred == 0;

    /// <summary>
    /// Formats the line for what became of a connection curl does not keep: <c>shutting down</c>
    /// after a reply with a body or an empty reply (52), <c>closing</c> after any other failure.
    /// </summary>
    /// <param name="result">The transfer's result.</param>
    /// <param name="connectionNumber">curl's number for the connection.</param>
    /// <returns>The line, such as <c>closing connection #0</c>.</returns>
    internal static string Dropped(TransferResult result, long connectionNumber) =>
        result.ExitCode is CurlExitCode.Ok or CurlExitCode.RtspCseqError or CurlExitCode.GotNothing
            ? string.Create(CultureInfo.InvariantCulture, $"shutting down connection #{connectionNumber}")
            : string.Create(CultureInfo.InvariantCulture, $"closing connection #{connectionNumber}");

    /// <summary>Formats the line for a connection curl keeps once the transfer is done.</summary>
    /// <param name="connectionNumber">curl's number for the connection.</param>
    /// <param name="host">The host the connection was opened to.</param>
    /// <param name="port">The port the connection was opened to.</param>
    /// <returns>The line, such as <c>Connection #0 to host 127.0.0.1:554 left intact</c>.</returns>
    internal static string LeftIntact(long connectionNumber, string host, int port) =>
        string.Create(CultureInfo.InvariantCulture, $"Connection #{connectionNumber} to host {host}:{port} left intact");
}
