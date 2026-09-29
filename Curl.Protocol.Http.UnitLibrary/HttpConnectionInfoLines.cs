using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// The <c>-v</c> lines the handler reports through <see cref="ITransferEvents.ReportInfo" />
/// about the connection and the request sent on it, as curl 8.21.0 prints them (ADR-0050,
/// BL-336, BL-407 and BL-449 Notes).
/// </summary>
internal static class HttpConnectionInfoLines
{
    /// <summary>
    /// The line for a reused connection that failed before any byte of the response arrived,
    /// written before it is closed and the request is sent again on a fresh connection.
    /// </summary>
    internal const string ConnectionDiedRetrying = "Connection died, retrying a fresh connect (retry count: 1)";

    /// <summary>
    /// The line written before the first request on a connection this transfer opened, not on
    /// one it reuses (measured, BL-407 Notes).
    /// </summary>
    internal const string UsingHttp1 = "using HTTP/1.x";

    /// <summary>
    /// The line written in place of <see cref="UsingHttp1" /> when the connection speaks
    /// HTTP/2 (measured, ADR-0141).
    /// </summary>
    internal const string UsingHttp2 = "using HTTP/2";

    /// <summary>
    /// The line written in place of <see cref="UsingHttp1" /> when the connection is QUIC and
    /// speaks HTTP/3 (measured, ADR-0144).
    /// </summary>
    internal const string UsingHttp3 = "using HTTP/3";

    /// <summary>
    /// The line written once a request without a body has been sent.
    /// </summary>
    internal const string RequestSent = "Request completely sent off";

    /// <summary>
    /// The line written before the body of a request that waited for <c>100 Continue</c>
    /// when the wait ran out with nothing received (measured, BL-449 Notes).
    /// </summary>
    internal const string DoneWaitingForContinue = "Done waiting for 100-continue";

    /// <summary>
    /// The line written before every HTTP/1.0 status line, whether or not a
    /// <c>Connection: keep-alive</c> header follows (measured, BL-449 Notes).
    /// </summary>
    internal const string AssumeCloseAfterBody = "HTTP 1.0, assume close after body";

    /// <summary>
    /// The line written before each header line of an HTTP/1.0 head that
    /// <see cref="HttpConnectionPersistence.KeepsHttp10Alive" /> (measured, BL-467 Notes).
    /// </summary>
    internal const string Http10KeepAlive = "HTTP/1.0 connection set to keep alive";

    /// <summary>
    /// The line written before the empty line of an HTTP/1.1 head whose body can only end when
    /// the server closes (<see cref="HttpConnectionPersistence.LacksEndOfMessageIndicator" />,
    /// measured, BL-467 Notes).
    /// </summary>
    internal const string NoEndOfMessageIndicator = "no chunk, no close, no size. Assume close to signal end";

    /// <summary>
    /// The line written before the empty line of a head whose body is read and discarded on a
    /// connection that stays open, as for a redirect <c>-L</c> follows (measured, BL-449 Notes).
    /// </summary>
    internal const string IgnoringBody = "Ignoring the response-body";

    /// <summary>
    /// The line written after <see cref="IgnoringBody" /> when that body's length is known from
    /// its Content-Length (measured, BL-449 Notes).
    /// </summary>
    internal const string SettingSizeWhileIgnoring = "setting size while ignoring";

    /// <summary>
    /// Formats the line written once a request's whole body has been sent.
    /// </summary>
    /// <param name="bytesSent">The body bytes sent, chunk framing included.</param>
    /// <returns>The line, such as <c>upload completely sent off: 2 bytes</c>.</returns>
    internal static string UploadSent(long bytesSent) =>
        string.Create(CultureInfo.InvariantCulture, $"upload completely sent off: {bytesSent} bytes");

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
