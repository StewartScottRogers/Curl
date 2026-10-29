namespace Curl.Extension.HttpCache;

/// <summary>What a cache may do with a stored response when a request matches it (RFC 9111 §4).</summary>
public enum CacheEntryUsability
{
    /// <summary>The response is fresh: it may be used without contacting the origin server (§4.2).</summary>
    Fresh,

    /// <summary>The response is stale but may still be used if validation cannot reach the origin server (§4.2.4).</summary>
    Stale,

    /// <summary>The response must be validated before any use: it has <c>no-cache</c>, or is stale with <c>must-revalidate</c> (§5.2.2.2, §5.2.2.4).</summary>
    MustValidate,
}
