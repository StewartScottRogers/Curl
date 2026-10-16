namespace Curl.Api.EngineContracts.Share;

/// <summary>
/// A kind of data a share handle shares or locks (<c>curl_lock_data</c>, <c>CURL_LOCK_DATA_*</c>),
/// with upstream curl 8.22.0's numbers from <c>include/curl/curl.h</c>.
/// </summary>
public enum CurlLockData
{
    /// <summary><c>CURL_LOCK_DATA_NONE</c> (0): no data.</summary>
    None = 0,

    /// <summary><c>CURL_LOCK_DATA_SHARE</c> (1): the share handle's own state; locked, never shared.</summary>
    Share = 1,

    /// <summary><c>CURL_LOCK_DATA_COOKIE</c> (2): the cookie store.</summary>
    Cookie = 2,

    /// <summary><c>CURL_LOCK_DATA_DNS</c> (3): the DNS cache.</summary>
    Dns = 3,

    /// <summary><c>CURL_LOCK_DATA_SSL_SESSION</c> (4): the TLS session cache.</summary>
    SslSession = 4,

    /// <summary><c>CURL_LOCK_DATA_CONNECT</c> (5): the connection cache.</summary>
    Connect = 5,

    /// <summary><c>CURL_LOCK_DATA_PSL</c> (6): the public suffix list.</summary>
    Psl = 6,

    /// <summary><c>CURL_LOCK_DATA_HSTS</c> (7): the HSTS cache.</summary>
    Hsts = 7,
}
