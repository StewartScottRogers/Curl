namespace Curl.Protocol.Abstractions;

/// <summary>
/// The HTTP version a request line asks for (ADR-0014).
/// </summary>
/// <remarks>
/// <see cref="Http11" /> is first so that the default value is curl's default. The HTTP/3
/// values are ADR-0144's and the HTTP/2 value ADR-0141's, appended in the order the tasks
/// implementing them added them.
/// </remarks>
public enum HttpVersionPreference
{
    /// <summary>
    /// HTTP/1.1: the default, and <c>--http1.1</c>.
    /// </summary>
    Http11 = 0,

    /// <summary>
    /// HTTP/1.0, per <c>-0</c>/<c>--http1.0</c>.
    /// </summary>
    Http10,

    /// <summary>
    /// HTTP/3, per <c>--http3</c>: on an <c>https://</c> URL the HTTP handler races a QUIC
    /// connection against TCP and uses whichever connects first; on <c>http://</c> it is
    /// ignored (ADR-0144).
    /// </summary>
    Http3,

    /// <summary>
    /// HTTP/3 only, per <c>--http3-only</c>: the HTTP handler connects over QUIC and never
    /// falls back to TCP (ADR-0144).
    /// </summary>
    Http3Only,

    /// <summary>
    /// HTTP/2 with prior knowledge, per <c>--http2-prior-knowledge</c>: the HTTP handler
    /// speaks HTTP/2 from the first byte, with no upgrade and whatever TLS negotiated
    /// (ADR-0141).
    /// </summary>
    Http2PriorKnowledge,
}
