namespace Curl.Console;

/// <summary>
/// Reads the file name a <c>Location</c> header line points at: the last segment of its path,
/// still percent-encoded, which curl 8.21.0 names a <c>-J -L</c> file after when no
/// <c>Content-Disposition</c> names it (upstream tests 1642 and 1643, BL-1809).
/// </summary>
internal static class RedirectLocationFileName
{
    /// <summary>The header's name and colon, matched without regard to case.</summary>
    private const string LocationPrefix = "Location:";

    /// <summary>
    /// Finds the file name in one header line.
    /// </summary>
    /// <param name="line">One header line, its line ending included or not.</param>
    /// <returns>
    /// The last path segment of the <c>Location</c> URL, its query and fragment dropped; or
    /// <see langword="null" /> when the line is not a <c>Location</c> line or its path ends
    /// without a segment.
    /// </returns>
    internal static string? Find(ReadOnlySpan<byte> line)
    {
        string text = System.Text.Encoding.Latin1.GetString(line).Trim();
        if (!text.StartsWith(LocationPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string path = PathOf(text[LocationPrefix.Length..].Trim());
        string segment = path[(path.LastIndexOf('/') + 1)..];

        return segment.Length == 0 ? null : segment;
    }

    /// <summary>
    /// Gives a URL's path: the text before its query or fragment, past the authority when the
    /// URL is absolute (an absolute URL with no path gives an empty one).
    /// </summary>
    /// <param name="url">The <c>Location</c> value.</param>
    /// <returns>The path, relative or absolute.</returns>
    private static string PathOf(string url)
    {
        int end = url.IndexOfAny(['?', '#']);
        string beforeQuery = end < 0 ? url : url[..end];
        int scheme = beforeQuery.IndexOf("://", StringComparison.Ordinal);
        if (scheme < 0)
        {
            return beforeQuery;
        }

        int pathStart = beforeQuery.IndexOf('/', scheme + 3);

        return pathStart < 0 ? string.Empty : beforeQuery[pathStart..];
    }
}
