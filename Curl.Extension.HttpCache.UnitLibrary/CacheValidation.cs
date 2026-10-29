namespace Curl.Extension.HttpCache;

/// <summary>
/// Validates a stored response with its origin server: builds the conditional request header
/// fields from its validators (RFC 9111 §4.3.1), decides whether a 304 Not Modified response
/// selects it (§4.3.4), and updates its header fields from that 304 (§3.2, §4.3.4).
/// </summary>
public static class CacheValidation
{
    private static readonly string[] FieldsNeverUpdated =
    [
        "Content-Length", "Connection", "Proxy-Connection", "Keep-Alive", "TE", "Transfer-Encoding", "Upgrade",
        "Proxy-Authenticate", "Proxy-Authentication-Info", "Proxy-Authorization",
    ];

    /// <summary>
    /// The header fields that make a request conditional on <paramref name="stored"/>:
    /// <c>If-None-Match</c> with its <c>ETag</c> and <c>If-Modified-Since</c> with its <c>Last-Modified</c>,
    /// each only when the stored response has that validator.
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, string>> BuildConditionalHeaders(StoredResponse stored)
    {
        var headers = new List<KeyValuePair<string, string>>();
        var entityTag = HttpHeaderValues.FindFirst(stored.Headers, "ETag");
        if (entityTag is not null)
        {
            headers.Add(new("If-None-Match", entityTag));
        }

        var lastModified = HttpHeaderValues.FindFirst(stored.Headers, "Last-Modified");
        if (lastModified is not null)
        {
            headers.Add(new("If-Modified-Since", lastModified));
        }

        return headers;
    }

    /// <summary>
    /// Whether a 304 response with <paramref name="notModifiedHeaders"/> selects <paramref name="stored"/>:
    /// its <c>ETag</c> matches the stored one by weak comparison when it has one, else its
    /// <c>Last-Modified</c> is the stored one, else neither response has a validator.
    /// </summary>
    public static bool SelectsStoredResponse(StoredResponse stored, IReadOnlyList<KeyValuePair<string, string>> notModifiedHeaders)
    {
        var storedEntityTag = HttpHeaderValues.FindFirst(stored.Headers, "ETag");
        var storedLastModified = HttpHeaderValues.FindFirst(stored.Headers, "Last-Modified");
        var entityTag = HttpHeaderValues.FindFirst(notModifiedHeaders, "ETag");
        if (entityTag is not null)
        {
            return storedEntityTag is not null && string.Equals(OpaqueTag(entityTag), OpaqueTag(storedEntityTag), StringComparison.Ordinal);
        }

        var lastModified = HttpHeaderValues.FindFirst(notModifiedHeaders, "Last-Modified");
        if (lastModified is not null)
        {
            return string.Equals(lastModified, storedLastModified, StringComparison.Ordinal);
        }

        return storedEntityTag is null && storedLastModified is null;
    }

    /// <summary>
    /// <paramref name="stored"/> with every field the 304 carries replacing all stored fields of that name,
    /// except <c>Content-Length</c>, hop-by-hop fields and fields the 304's <c>Connection</c> names,
    /// and with the request and response times of the validation.
    /// </summary>
    public static StoredResponse ApplyNotModified(
        StoredResponse stored,
        IReadOnlyList<KeyValuePair<string, string>> notModifiedHeaders,
        DateTimeOffset requestTime,
        DateTimeOffset responseTime)
    {
        var neverUpdated = new HashSet<string>(FieldsNeverUpdated, StringComparer.OrdinalIgnoreCase);
        neverUpdated.UnionWith((HttpHeaderValues.FindFirst(notModifiedHeaders, "Connection") ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        var updates = notModifiedHeaders.Where(header => !neverUpdated.Contains(header.Key)).ToList();
        var replaced = new HashSet<string>(updates.Select(header => header.Key), StringComparer.OrdinalIgnoreCase);
        var headers = stored.Headers.Where(header => !replaced.Contains(header.Key)).Concat(updates).ToList();
        return stored with { Headers = headers, RequestTime = requestTime, ResponseTime = responseTime };
    }

    private static string OpaqueTag(string entityTag) =>
        entityTag.StartsWith("W/", StringComparison.Ordinal) ? entityTag[2..] : entityTag;
}
