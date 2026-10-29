namespace Curl.Extension.HttpCache;

/// <summary>Finds header field values in a header list by case-insensitive field name.</summary>
public static class HttpHeaderValues
{
    /// <summary>Returns the value of the first field named <paramref name="name"/>, or <see langword="null"/> when there is none.</summary>
    public static string? FindFirst(IReadOnlyList<KeyValuePair<string, string>> headers, string name)
    {
        foreach (var header in headers)
        {
            if (string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return header.Value;
            }
        }

        return null;
    }
}
