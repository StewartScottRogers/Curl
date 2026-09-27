using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// The <c>-v</c> lines the handler reports through <see cref="ITransferEvents.ReportInfo" />
/// about what became of a connection, as curl 8.21.0 prints them (ADR-0050, BL-336 Notes).
/// </summary>
internal static class HttpConnectionInfoLines
{
    /// <summary>
    /// The line for a reused connection that failed before any byte of the response arrived,
    /// written before it is closed and the request is sent again on a fresh connection.
    /// </summary>
    internal const string ConnectionDiedRetrying = "Connection died, retrying a fresh connect (retry count: 1)";

    /// <summary>
    /// Formats the line for a connection left open for another transfer.
    /// </summary>
    /// <param name="connectionNumber">curl's number for the connection.</param>
    /// <param name="host">The host the connection was opened to.</param>
    /// <param name="port">The port the connection was opened to.</param>
    /// <returns>The line, such as <c>Connection #0 to host 127.0.0.1:18231 left intact</c>.</returns>
    internal static string LeftIntact(long connectionNumber, string host, int port) =>
        string.Create(CultureInfo.InvariantCulture, $"Connection #{connectionNumber} to host {host}:{port} left intact");

    /// <summary>
    /// Formats the line for a connection closed because its response did not let it persist.
    /// </summary>
    /// <param name="connectionNumber">curl's number for the connection.</param>
    /// <returns>The line, such as <c>shutting down connection #0</c>.</returns>
    internal static string ShuttingDown(long connectionNumber) =>
        string.Create(CultureInfo.InvariantCulture, $"shutting down connection #{connectionNumber}");

    /// <summary>
    /// Formats the line for a connection closed because its transfer failed.
    /// </summary>
    /// <param name="connectionNumber">curl's number for the connection.</param>
    /// <returns>The line, such as <c>closing connection #0</c>.</returns>
    internal static string Closing(long connectionNumber) =>
        string.Create(CultureInfo.InvariantCulture, $"closing connection #{connectionNumber}");

    /// <summary>
    /// Formats the line written before a request is sent again on a fresh connection.
    /// </summary>
    /// <param name="url">The URL the request is sent to.</param>
    /// <returns>The line, such as <c>Issue another request to this URL: 'http://127.0.0.1:18240/b'</c>.</returns>
    internal static string IssueAnotherRequest(CurlUrl url) =>
        $"Issue another request to this URL: '{HttpUrlText.Origin(url)}{HttpUrlText.Path(url)}{HttpUrlText.Query(url)}'";
}
