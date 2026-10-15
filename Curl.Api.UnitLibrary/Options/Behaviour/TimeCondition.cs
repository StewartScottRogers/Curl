namespace Curl.Api.Options.Behaviour;

/// <summary>
/// How <c>CURLOPT_TIMEVALUE</c> conditions a transfer: the <c>CURL_TIMECOND_*</c> values of
/// <c>CURLOPT_TIMECONDITION</c>. Each member's value is libcurl's.
/// </summary>
public enum TimeCondition
{
    /// <summary><c>CURL_TIMECOND_NONE</c> (0): no condition; libcurl's default.</summary>
    None = 0,

    /// <summary><c>CURL_TIMECOND_IFMODSINCE</c> (1): transfer only if modified since the time value.</summary>
    IfModifiedSince = 1,

    /// <summary><c>CURL_TIMECOND_IFUNMODSINCE</c> (2): transfer only if not modified since the time value.</summary>
    IfUnmodifiedSince = 2,

    /// <summary><c>CURL_TIMECOND_LASTMOD</c> (3): transfer only if last modified at the time value.</summary>
    LastModified = 3,
}
