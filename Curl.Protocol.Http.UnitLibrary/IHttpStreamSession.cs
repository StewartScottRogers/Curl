using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// One connection that carries each request on a stream of its own: HTTP/2 over a TCP
/// connection (<see cref="Http2Session" />) or HTTP/3 over a QUIC connection
/// (<see cref="Http3Session" />).
/// </summary>
internal interface IHttpStreamSession
{
    /// <summary>
    /// Gets the version the session speaks as curl names it in a request line and a status
    /// line: <c>HTTP/2</c> or <c>HTTP/3</c>.
    /// </summary>
    string VersionName { get; }

    /// <summary>
    /// Gets curl's <c>-v</c> line for a new connection that speaks this version:
    /// <c>using HTTP/2</c> or <c>using HTTP/3</c>.
    /// </summary>
    string UsingLine { get; }

    /// <summary>
    /// Gets a value indicating whether the connection can carry another request.
    /// </summary>
    bool AcceptsNewStreams { get; }

    /// <summary>
    /// Creates the stream a request is sent and its response read on; nothing is sent until
    /// its head is written.
    /// </summary>
    /// <param name="scheme">The URL's scheme, sent as <c>:scheme</c>.</param>
    /// <param name="bodyLength">
    /// The request body's length, 0 when there is none, or <see langword="null" /> when it is
    /// unknown.
    /// </param>
    /// <param name="ignoresBody">
    /// <see langword="true" /> when no response body is wanted (<c>-I</c>): an HTTP/3 stream then
    /// takes a reset after the final head as its end (ADR-0187).
    /// </param>
    /// <param name="openedLines">
    /// Reports curl's <c>-v</c> lines for the stream once its head is sent, or <see langword="null" /> for none.
    /// </param>
    /// <param name="diagnosticLog">
    /// The transfer's diagnostic log, which the stream's frames are written to (<see cref="HttpFrameLog" />),
    /// or <see langword="null" /> for none.
    /// </param>
    /// <param name="traceEvents">
    /// Where the session's <c>--trace-config http/2</c> lines go while this stream is the latest opened
    /// (<see cref="Http2FrameTrace" />, BL-1167), or <see langword="null" /> for none. HTTP/3 writes none yet.
    /// </param>
    /// <returns>The stream.</returns>
    IHttpStreamConnection CreateStream(string scheme, long? bodyLength, bool ignoresBody, HttpStreamOpenedLines? openedLines = null, IDiagnosticLog? diagnosticLog = null, ITransferEvents? traceEvents = null);
}
