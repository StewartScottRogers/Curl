namespace Curl.Extension.HttpCache;

/// <summary>
/// Decides whether a transfer is a request this cache answers or stores: a plain HTTP GET of the whole
/// resource (RFC 9111 §2), with no body, upload, custom method, <c>HEAD</c>, range, resume or curl's own
/// time condition, and gives its target URI and request header fields.
/// </summary>
internal static class CacheableRequest
{
    /// <summary>The target URI of a cacheable transfer; <see langword="null" /> when the transfer is not one.</summary>
    public static Uri? TargetUriOf(ITransferContext context) =>
        context.Http is { } http && IsPlainGet(http) && SendsNoBody(context) && AsksForTheWholeResource(context)
            ? TargetUri(context.Url)
            : null;

    /// <summary>The request header fields curl's options and the other hooks give, as name and value pairs.</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> HeaderFieldsOf(HttpRequestOptions http) =>
        [.. http.Headers
            .Select(line => line.Split(':', 2))
            .Where(parts => parts.Length == 2)
            .Select(parts => new KeyValuePair<string, string>(parts[0].Trim(), parts[1].Trim()))];

    private static bool IsPlainGet(HttpRequestOptions http) => http.CustomMethod is null && http.Body is null;

    private static bool SendsNoBody(ITransferContext context) =>
        context is { PostData: null, PostWithoutData: false, Upload: null };

    private static bool AsksForTheWholeResource(ITransferContext context) =>
        context is { NoBody: false, Range: null, ResumeFrom: null, TimeCondition: null };

    private static Uri? TargetUri(CurlUrl url)
    {
        var port = url.IsDefaultPort ? string.Empty : ":" + url.Port;
        var query = url.Query is null ? string.Empty : "?" + url.Query;
        // A URL System.Uri cannot hold leaves uri null, so the transfer runs uncached.
        _ = Uri.TryCreate(url.Scheme + "://" + url.Host + port + url.AbsolutePath + query, UriKind.Absolute, out var uri);
        return uri;
    }
}
