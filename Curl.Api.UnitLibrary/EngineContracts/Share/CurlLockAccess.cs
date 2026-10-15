namespace Curl.Api.EngineContracts.Share;

/// <summary>
/// The access a share lock callback is asked for (<c>curl_lock_access</c>, <c>CURL_LOCK_ACCESS_*</c>),
/// with upstream curl 8.22.0's numbers from <c>include/curl/curl.h</c>.
/// </summary>
public enum CurlLockAccess
{
    /// <summary><c>CURL_LOCK_ACCESS_NONE</c> (0): unspecified access.</summary>
    None = 0,

    /// <summary><c>CURL_LOCK_ACCESS_SHARED</c> (1): read access, which others may hold at the same time.</summary>
    Shared = 1,

    /// <summary><c>CURL_LOCK_ACCESS_SINGLE</c> (2): write access, held by one caller alone.</summary>
    Single = 2,
}
