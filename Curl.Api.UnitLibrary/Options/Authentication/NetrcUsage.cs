namespace Curl.Api.Options.Authentication;

/// <summary>
/// Whether the <c>.netrc</c> file supplies credentials: the <c>CURL_NETRC_*</c> values of
/// <c>CURLOPT_NETRC</c>. Each member's value is libcurl's.
/// </summary>
public enum NetrcUsage
{
    /// <summary><c>CURL_NETRC_IGNORED</c> (0): the <c>.netrc</c> file is not read; libcurl's default.</summary>
    Ignored = 0,

    /// <summary>
    /// <c>CURL_NETRC_OPTIONAL</c> (1): the <c>.netrc</c> file is read, and credentials in the
    /// URL or the options win over it.
    /// </summary>
    Optional = 1,

    /// <summary>
    /// <c>CURL_NETRC_REQUIRED</c> (2): the <c>.netrc</c> file is read, and credentials in the
    /// URL are ignored.
    /// </summary>
    Required = 2,
}
