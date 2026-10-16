namespace Curl.Api.Options.Http;

/// <summary>
/// The HTTP version an easy handle asks for: the <c>CURL_HTTP_VERSION_*</c> values of
/// <c>CURLOPT_HTTP_VERSION</c>. Each member's value is libcurl's.
/// </summary>
public enum HttpVersion
{
    /// <summary>
    /// <c>CURL_HTTP_VERSION_NONE</c> (0): libcurl picks; libcurl 8.22.0's default, which
    /// tries HTTP/2 over TLS and HTTP/1.1 otherwise.
    /// </summary>
    None = 0,

    /// <summary><c>CURL_HTTP_VERSION_1_0</c> (1): HTTP/1.0.</summary>
    Http10 = 1,

    /// <summary><c>CURL_HTTP_VERSION_1_1</c> (2): HTTP/1.1.</summary>
    Http11 = 2,

    /// <summary>
    /// <c>CURL_HTTP_VERSION_2_0</c> (3, also <c>CURL_HTTP_VERSION_2</c>): HTTP/2, falling back
    /// to HTTP/1.1 when it cannot be negotiated.
    /// </summary>
    Http2 = 3,

    /// <summary><c>CURL_HTTP_VERSION_2TLS</c> (4): HTTP/2 over TLS only, HTTP/1.1 in clear text.</summary>
    Http2Tls = 4,

    /// <summary>
    /// <c>CURL_HTTP_VERSION_2_PRIOR_KNOWLEDGE</c> (5): HTTP/2 in clear text without an
    /// <c>Upgrade</c>.
    /// </summary>
    Http2PriorKnowledge = 5,

    /// <summary><c>CURL_HTTP_VERSION_3</c> (30): HTTP/3, falling back to older versions.</summary>
    Http3 = 30,

    /// <summary><c>CURL_HTTP_VERSION_3ONLY</c> (31): HTTP/3 with no fallback.</summary>
    Http3Only = 31,
}
