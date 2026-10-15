namespace Curl.Api.EngineContracts.Headers;

/// <summary>
/// Where a header came from (<c>CURLH_*</c>), the bitmask <c>curl_easy_header</c> and
/// <c>curl_easy_nextheader</c> filter by and <c>struct curl_header</c>'s <c>origin</c>, with
/// upstream curl 8.22.0's numbers from <c>include/curl/header.h</c>.
/// </summary>
[Flags]
public enum CurlHeaderOrigin
{
    /// <summary>No bit set.</summary>
    None = 0,

    /// <summary><c>CURLH_HEADER</c> (<c>1 &lt;&lt; 0</c>): a plain server header.</summary>
    Header = 1 << 0,

    /// <summary><c>CURLH_TRAILER</c> (<c>1 &lt;&lt; 1</c>): a trailer.</summary>
    Trailer = 1 << 1,

    /// <summary><c>CURLH_CONNECT</c> (<c>1 &lt;&lt; 2</c>): a header of a CONNECT response.</summary>
    Connect = 1 << 2,

    /// <summary><c>CURLH_1XX</c> (<c>1 &lt;&lt; 3</c>): a header of a 1xx response.</summary>
    Informational = 1 << 3,

    /// <summary><c>CURLH_PSEUDO</c> (<c>1 &lt;&lt; 4</c>): an HTTP/2 or HTTP/3 pseudo header.</summary>
    Pseudo = 1 << 4,
}
