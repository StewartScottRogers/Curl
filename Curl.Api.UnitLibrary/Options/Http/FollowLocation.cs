namespace Curl.Api.Options.Http;

/// <summary>
/// Whether and how an easy handle follows redirects: the values of
/// <c>CURLOPT_FOLLOWLOCATION</c>, 0 and the <c>CURLFOLLOW_*</c> constants. Each member's
/// value is libcurl's.
/// </summary>
public enum FollowLocation
{
    /// <summary>0: redirects are not followed; libcurl's default.</summary>
    None = 0,

    /// <summary>
    /// <c>CURLFOLLOW_ALL</c> (1): every redirect is followed, the method changed as
    /// <c>CURLOPT_POSTREDIR</c> says.
    /// </summary>
    All = 1,

    /// <summary>
    /// <c>CURLFOLLOW_OBEYCODE</c> (2): redirects are followed and a custom request method is
    /// changed as the response code says.
    /// </summary>
    ObeyCode = 2,

    /// <summary>
    /// <c>CURLFOLLOW_FIRSTONLY</c> (3): a custom request method is used for the first request
    /// only, not after a redirect.
    /// </summary>
    FirstOnly = 3,
}
