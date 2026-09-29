namespace Curl.Cli;

/// <summary>
/// The HTTP version option the command line gave last (ADR-0141): what the request line
/// asks for and what the TLS handshake offers through ALPN both follow from it.
/// </summary>
public enum RequestedHttpVersion
{
    /// <summary>HTTP/1.0, per <c>-0</c>/<c>--http1.0</c>: ALPN offers <c>http/1.1</c> alone.</summary>
    Http10,

    /// <summary>HTTP/1.1, per <c>--http1.1</c>: ALPN offers <c>http/1.1</c> alone.</summary>
    Http11,

    /// <summary>
    /// HTTP/2 where the server agrees, per <c>--http2</c>: ALPN offers <c>h2,http/1.1</c> and the
    /// transfer speaks HTTP/2 when the server picks <c>h2</c>, HTTP/1.1 otherwise.
    /// </summary>
    Http2,

    /// <summary>
    /// HTTP/2 from the first byte, per <c>--http2-prior-knowledge</c>: ALPN offers <c>h2</c>
    /// alone, and a cleartext transfer sends the client preface with no upgrade.
    /// </summary>
    Http2PriorKnowledge,

    /// <summary>
    /// HTTP/3 where it connects, per <c>--http3</c>: an <c>https://</c> transfer tries QUIC first
    /// and falls back to TCP, where ALPN offers <c>h2,http/1.1</c>; an <c>http://</c> one ignores it
    /// (ADR-0144).
    /// </summary>
    Http3,

    /// <summary>
    /// HTTP/3 alone, per <c>--http3-only</c>: an <c>https://</c> transfer connects over QUIC and never
    /// falls back; an <c>http://</c> one fails with exit 3 (ADR-0144).
    /// </summary>
    Http3Only,
}
