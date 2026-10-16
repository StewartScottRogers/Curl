namespace Libcurl.Global;

/// <summary>The result of <c>curl_global_sslset</c> (<c>CURLsslset</c>), with upstream curl 8.22.0's numbers.</summary>
public enum CurlSslSet
{
    /// <summary><c>CURLSSLSET_OK</c> (0): the backend asked for is the one in use.</summary>
    Ok = 0,

    /// <summary><c>CURLSSLSET_UNKNOWN_BACKEND</c> (1): no backend of that id or name is built in.</summary>
    UnknownBackend = 1,

    /// <summary><c>CURLSSLSET_TOO_LATE</c> (2): a backend is already chosen; a single-backend build answers this for any other backend.</summary>
    TooLate = 2,

    /// <summary><c>CURLSSLSET_NO_BACKENDS</c> (3): the build has no TLS backend.</summary>
    NoBackends = 3,
}
