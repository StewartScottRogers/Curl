using Curl.Http2;

namespace Curl.Http3;

/// <summary>
/// The static table of RFC 9204 appendix A: 99 fixed entries, indexes 0 to 98.
/// </summary>
internal static class QpackStaticTable
{
    /// <summary>The number of entries.</summary>
    public const int Count = 99;

    private static readonly HeaderField[] Entries =
    [
        new(":authority", string.Empty),
        new(":path", "/"),
        new("age", "0"),
        new("content-disposition", string.Empty),
        new("content-length", "0"),
        new("cookie", string.Empty),
        new("date", string.Empty),
        new("etag", string.Empty),
        new("if-modified-since", string.Empty),
        new("if-none-match", string.Empty),
        new("last-modified", string.Empty),
        new("link", string.Empty),
        new("location", string.Empty),
        new("referer", string.Empty),
        new("set-cookie", string.Empty),
        new(":method", "CONNECT"),
        new(":method", "DELETE"),
        new(":method", "GET"),
        new(":method", "HEAD"),
        new(":method", "OPTIONS"),
        new(":method", "POST"),
        new(":method", "PUT"),
        new(":scheme", "http"),
        new(":scheme", "https"),
        new(":status", "103"),
        new(":status", "200"),
        new(":status", "304"),
        new(":status", "404"),
        new(":status", "503"),
        new("accept", "*/*"),
        new("accept", "application/dns-message"),
        new("accept-encoding", "gzip, deflate, br"),
        new("accept-ranges", "bytes"),
        new("access-control-allow-headers", "cache-control"),
        new("access-control-allow-headers", "content-type"),
        new("access-control-allow-origin", "*"),
        new("cache-control", "max-age=0"),
        new("cache-control", "max-age=2592000"),
        new("cache-control", "max-age=604800"),
        new("cache-control", "no-cache"),
        new("cache-control", "no-store"),
        new("cache-control", "public, max-age=31536000"),
        new("content-encoding", "br"),
        new("content-encoding", "gzip"),
        new("content-type", "application/dns-message"),
        new("content-type", "application/javascript"),
        new("content-type", "application/json"),
        new("content-type", "application/x-www-form-urlencoded"),
        new("content-type", "image/gif"),
        new("content-type", "image/jpeg"),
        new("content-type", "image/png"),
        new("content-type", "text/css"),
        new("content-type", "text/html; charset=utf-8"),
        new("content-type", "text/plain"),
        new("content-type", "text/plain;charset=utf-8"),
        new("range", "bytes=0-"),
        new("strict-transport-security", "max-age=31536000"),
        new("strict-transport-security", "max-age=31536000; includesubdomains"),
        new("strict-transport-security", "max-age=31536000; includesubdomains; preload"),
        new("vary", "accept-encoding"),
        new("vary", "origin"),
        new("x-content-type-options", "nosniff"),
        new("x-xss-protection", "1; mode=block"),
        new(":status", "100"),
        new(":status", "204"),
        new(":status", "206"),
        new(":status", "302"),
        new(":status", "400"),
        new(":status", "403"),
        new(":status", "421"),
        new(":status", "425"),
        new(":status", "500"),
        new("accept-language", string.Empty),
        new("access-control-allow-credentials", "FALSE"),
        new("access-control-allow-credentials", "TRUE"),
        new("access-control-allow-headers", "*"),
        new("access-control-allow-methods", "get"),
        new("access-control-allow-methods", "get, post, options"),
        new("access-control-allow-methods", "options"),
        new("access-control-expose-headers", "content-length"),
        new("access-control-request-headers", "content-type"),
        new("access-control-request-method", "get"),
        new("access-control-request-method", "post"),
        new("alt-svc", "clear"),
        new("authorization", string.Empty),
        new("content-security-policy", "script-src 'none'; object-src 'none'; base-uri 'none'"),
        new("early-data", "1"),
        new("expect-ct", string.Empty),
        new("forwarded", string.Empty),
        new("if-range", string.Empty),
        new("origin", string.Empty),
        new("purpose", "prefetch"),
        new("server", string.Empty),
        new("timing-allow-origin", "*"),
        new("upgrade-insecure-requests", "1"),
        new("user-agent", string.Empty),
        new("x-forwarded-for", string.Empty),
        new("x-frame-options", "deny"),
        new("x-frame-options", "sameorigin"),
    ];

    /// <summary>
    /// Gets the entry at an index, or throws <paramref name="error" /> when the index is
    /// past the table (RFC 9204 section 3.1).
    /// </summary>
    /// <param name="index">The index the peer sent.</param>
    /// <param name="error">The connection error an index past the table calls for.</param>
    /// <returns>The entry.</returns>
    /// <exception cref="QpackException">The index is 99 or more.</exception>
    public static HeaderField Get(long index, QpackErrorCode error)
    {
        if (index >= Count)
        {
            throw new QpackException(error, $"static table index {index} is past the table's {Count} entries");
        }

        return Entries[index];
    }

    /// <summary>
    /// Finds a field in the static table: the entry whose name and value both match if
    /// there is one, otherwise the first entry with the name.
    /// </summary>
    /// <param name="name">The field name.</param>
    /// <param name="value">The field value.</param>
    /// <returns>The index and whether the value matched too, or index -1 when no entry has the name.</returns>
    public static (int Index, bool IsExact) Find(string name, string value)
    {
        var firstWithName = -1;
        for (var index = 0; index < Count; index++)
        {
            var entry = Entries[index];
            if (entry.Name != name)
            {
                continue;
            }

            if (entry.Value == value)
            {
                return (index, true);
            }

            firstWithName = firstWithName < 0 ? index : firstWithName;
        }

        return (firstWithName, false);
    }
}
