using System.Collections.Frozen;

namespace Curl.Extension.HttpCache;

/// <summary>
/// Decides whether a response may be stored (RFC 9111 §3), for a private cache or a shared one.
/// Only GET is cached, and only a final status other than 206 and 304, which this cache does not
/// combine or store. <c>no-store</c> in the request or the response forbids storing (§5.2.1.5,
/// §5.2.2.5). A shared cache also refuses <c>private</c> (§5.2.2.7) and a response to a request
/// with <c>Authorization</c> unless the response allows it (§3.5). The response must then carry
/// explicit freshness, <c>public</c>, or a heuristically cacheable status (§4.2.2).
/// </summary>
/// <param name="isSharedCache">Whether the cache is shared between users; one user's cache directory is private.</param>
public sealed class ResponseStorability(bool isSharedCache)
{
    private static readonly FrozenSet<int> HeuristicallyCacheableStatusCodes =
        FrozenSet.Create(200, 203, 204, 206, 300, 301, 308, 404, 405, 410, 414, 501);

    /// <summary>Whether a response to <paramref name="method"/> with <paramref name="statusCode"/> and these header fields may be stored.</summary>
    public bool IsStorable(
        string method,
        int statusCode,
        IReadOnlyList<KeyValuePair<string, string>> requestHeaders,
        IReadOnlyList<KeyValuePair<string, string>> responseHeaders)
    {
        if (!string.Equals(method, "GET", StringComparison.Ordinal) || !IsStorableStatus(statusCode))
        {
            return false;
        }

        var response = CacheControlDirectives.Parse(responseHeaders);
        return !ForbidsStoring(requestHeaders, response) && AllowsCaching(statusCode, responseHeaders, response);
    }

    /// <summary>Whether <paramref name="statusCode"/> is heuristically cacheable (RFC 9110 §15.1).</summary>
    public static bool IsHeuristicallyCacheable(int statusCode) => HeuristicallyCacheableStatusCodes.Contains(statusCode);

    private bool ForbidsStoring(IReadOnlyList<KeyValuePair<string, string>> requestHeaders, CacheControlDirectives response) =>
        CacheControlDirectives.Parse(requestHeaders).Has("no-store")
        || response.Has("no-store")
        || (isSharedCache && !SharedCacheMayStore(requestHeaders, response));

    private static bool IsStorableStatus(int statusCode) => statusCode is >= 200 and <= 599 and not 206 and not 304;

    private static bool SharedCacheMayStore(IReadOnlyList<KeyValuePair<string, string>> requestHeaders, CacheControlDirectives response)
    {
        if (response.Has("private"))
        {
            return false;
        }

        return HttpHeaderValues.FindFirst(requestHeaders, "Authorization") is null
            || response.Has("must-revalidate")
            || response.Has("public")
            || response.Has("s-maxage");
    }

    private bool AllowsCaching(int statusCode, IReadOnlyList<KeyValuePair<string, string>> responseHeaders, CacheControlDirectives response) =>
        HttpHeaderValues.FindFirst(responseHeaders, "Expires") is not null
        || response.Has("max-age")
        || (isSharedCache && response.Has("s-maxage"))
        || response.Has("public")
        || IsHeuristicallyCacheable(statusCode);
}
