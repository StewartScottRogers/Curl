namespace Curl.Api.Options.Network;

/// <summary>
/// The IP versions a name may resolve to: libcurl's <c>CURL_IPRESOLVE_*</c> values for
/// <c>CURLOPT_IPRESOLVE</c>, with the same numbers.
/// </summary>
public enum IpResolve
{
    /// <summary><c>CURL_IPRESOLVE_WHATEVER</c>: any IP version the system allows (libcurl's default).</summary>
    Whatever = 0,

    /// <summary><c>CURL_IPRESOLVE_V4</c>: IPv4 addresses only.</summary>
    V4 = 1,

    /// <summary><c>CURL_IPRESOLVE_V6</c>: IPv6 addresses only.</summary>
    V6 = 2,
}
