namespace Curl.Original.Protocol.Http;

/// <summary>
/// Reads one raw header line, <c>Name: value</c>, the way curl's <c>lib/http.c</c> does:
/// <see cref="ListsToken" /> is <c>Curl_compareheader</c> and <see cref="CopyValue" /> is
/// <c>Curl_copy_header_value</c> (curl 8.22.0, upstream unit programs 1625 and 1626).
/// </summary>
/// <remarks>
/// Only the line's first line counts: the scan stops at the first carriage return or line feed.
/// </remarks>
public static class HttpHeaderLine
{
    private static readonly char[] LineEnds = ['\r', '\n'];

    private static readonly char[] TokenSeparators = [' ', '\t', ','];

    private static readonly char[] Whitespace = [' ', '\t', '\r', '\n', '\v', '\f'];

    /// <summary>
    /// Determines whether <paramref name="line" /> starts with the header name
    /// <paramref name="nameWithColon" /> and its value lists <paramref name="token" />: a word
    /// set apart by blanks or commas, compared without regard to case, as <c>Curl_compareheader</c> does.
    /// </summary>
    /// <param name="line">The raw header line, such as <c>Transfer-Encoding: gzip, chunked</c>.</param>
    /// <param name="nameWithColon">The header name with its colon, such as <c>Transfer-Encoding:</c>.</param>
    /// <param name="token">The token looked for, such as <c>chunked</c>.</param>
    /// <returns><see langword="true" /> when the header is <paramref name="nameWithColon" /> and lists <paramref name="token" />.</returns>
    public static bool ListsToken(string line, string nameWithColon, string token)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(nameWithColon);
        ArgumentNullException.ThrowIfNull(token);
        if (!line.StartsWith(nameWithColon, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return FirstLine(line[nameWithColon.Length..])
            .Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Any(word => word.Equals(token, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Copies the value of <paramref name="line" />, as <c>Curl_copy_header_value</c> does: what
    /// follows the first colon, up to the end of the first line, without leading or trailing whitespace.
    /// </summary>
    /// <param name="line">The raw header line, such as <c>Content-Type: text/html\r\n</c>.</param>
    /// <returns>The value, empty when the line has none or no colon.</returns>
    public static string CopyValue(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        int colon = line.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 ? string.Empty : FirstLine(line[(colon + 1)..]).Trim(Whitespace);
    }

    private static string FirstLine(string text)
    {
        int end = text.IndexOfAny(LineEnds);
        return end < 0 ? text : text[..end];
    }
}
