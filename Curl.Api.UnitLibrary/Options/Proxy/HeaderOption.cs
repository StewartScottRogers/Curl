namespace Curl.Api.Options.Proxy;

/// <summary>
/// Where <c>CURLOPT_HTTPHEADER</c>'s headers go when a request passes through a proxy: the
/// <c>CURLHEADER_*</c> values of <c>CURLOPT_HEADEROPT</c>. Each member's value is libcurl's.
/// </summary>
public enum HeaderOption
{
    /// <summary>
    /// <c>CURLHEADER_UNIFIED</c> (0): <c>CURLOPT_HTTPHEADER</c>'s headers go to the proxy
    /// too, and <c>CURLOPT_PROXYHEADER</c> is ignored.
    /// </summary>
    Unified = 0,

    /// <summary>
    /// <c>CURLHEADER_SEPARATE</c> (1): <c>CURLOPT_HTTPHEADER</c>'s headers go only to the
    /// origin and <c>CURLOPT_PROXYHEADER</c>'s only to the proxy; libcurl's default.
    /// </summary>
    Separate = 1,
}
