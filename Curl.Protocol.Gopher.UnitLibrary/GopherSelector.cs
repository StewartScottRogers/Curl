using System.Globalization;
using System.Text;

namespace Curl.Protocol.Gopher;

/// <summary>
/// Builds the selector a gopher request sends from its URL, as curl 8.21.0's
/// <c>lib/gopher.c</c> does.
/// </summary>
/// <remarks>
/// <para>
/// The selector is the URL's path and query as written, still percent-encoded and with
/// its dot segments removed, less its first two bytes (the leading <c>/</c> and the
/// item-type character), then percent-decoded. A path and query of two bytes or
/// fewer gives an empty selector. The fragment is never sent.
/// </para>
/// <para>
/// The path is taken from <see cref="Uri.OriginalString" />, as UTF-8 bytes, rather than
/// <see cref="Uri.PathAndQuery" />, because .NET unescapes encoded unreserved characters
/// such as <c>%31</c> and has no query component for <c>gopher</c>, and either would move
/// which two characters are removed.
/// </para>
/// </remarks>
internal static class GopherSelector
{
    /// <summary>
    /// The number of bytes removed from the front of the path: the leading
    /// <c>/</c> and the item-type character.
    /// </summary>
    private const int PrefixLength = 2;

    private const string SchemeSeparator = "://";

    private static readonly char[] PathStarts = ['/', '?', '#'];

    /// <summary>
    /// Builds the selector for <paramref name="url" />, without its terminating CRLF.
    /// </summary>
    /// <param name="url">The transfer's URL.</param>
    /// <returns>
    /// The decoded selector bytes, or <see langword="null" /> when it decodes to a NUL
    /// byte, which curl refuses with exit 3.
    /// </returns>
    internal static byte[]? FromUrl(Uri url)
    {
        byte[] pathAndQuery = Encoding.UTF8.GetBytes(ReadPathAndQuery(url.OriginalString.Trim()));
        if (pathAndQuery.Length <= PrefixLength)
        {
            return [];
        }

        return PercentDecode(pathAndQuery[PrefixLength..]);
    }

    /// <summary>
    /// Reads the path and query from a URL as written, with the path's dot segments
    /// removed and an empty path read as <c>/</c>, as curl's URL parser leaves them.
    /// </summary>
    /// <param name="written">The URL as written.</param>
    /// <returns>The path, followed by <c>?</c> and the query when there is one.</returns>
    private static string ReadPathAndQuery(string written)
    {
        int authorityStart = written.IndexOf(SchemeSeparator, StringComparison.Ordinal) + SchemeSeparator.Length;
        int pathStart = written.IndexOfAny(PathStarts, authorityStart);
        string rest = pathStart < 0 ? string.Empty : written[pathStart..];
        int fragmentStart = rest.IndexOf('#', StringComparison.Ordinal);
        if (fragmentStart >= 0)
        {
            rest = rest[..fragmentStart];
        }

        int queryStart = rest.IndexOf('?', StringComparison.Ordinal);
        string path = queryStart < 0 ? rest : rest[..queryStart];
        string query = queryStart < 0 ? string.Empty : rest[queryStart..];

        return RemoveDotSegments(path) + query;
    }

    /// <summary>
    /// Removes the <c>.</c> and <c>..</c> segments from a path as RFC 3986 section 5.2.4
    /// does. Only literal dots count; <c>%2e</c> is left for the decoder.
    /// </summary>
    /// <param name="path">The path, empty or starting with <c>/</c>.</param>
    /// <returns>The path without dot segments, starting with <c>/</c>.</returns>
    private static string RemoveDotSegments(string path)
    {
        string[] segments = path.Split('/');
        List<string> kept = [];
        for (int index = 1; index < segments.Length; index++)
        {
            ApplySegment(kept, segments[index]);
        }

        string trailingSlash = IsDotSegment(segments[^1]) && kept.Count > 0 ? "/" : string.Empty;
        return "/" + string.Join('/', kept) + trailingSlash;
    }

    /// <summary>
    /// Applies one path segment to the segments kept so far: <c>..</c> drops the last one,
    /// <c>.</c> changes nothing, and any other segment is kept.
    /// </summary>
    /// <param name="kept">The segments kept so far.</param>
    /// <param name="segment">The next segment.</param>
    private static void ApplySegment(List<string> kept, string segment)
    {
        if (segment == "..")
        {
            if (kept.Count > 0)
            {
                kept.RemoveAt(kept.Count - 1);
            }
        }
        else if (segment != ".")
        {
            kept.Add(segment);
        }
    }

    private static bool IsDotSegment(string segment) => segment is "." or "..";

    /// <summary>
    /// Decodes each <c>%</c> followed by two hexadecimal digits into its byte and copies
    /// every other byte as it is, as <c>Curl_urldecode</c> does.
    /// </summary>
    /// <param name="encoded">The percent-encoded bytes.</param>
    /// <returns>The decoded bytes, or <see langword="null" /> when one of them is NUL.</returns>
    private static byte[]? PercentDecode(byte[] encoded)
    {
        List<byte> decoded = new(encoded.Length);
        int index = 0;
        while (index < encoded.Length)
        {
            byte value = DecodeAt(encoded, ref index);
            if (value == 0)
            {
                return null;
            }

            decoded.Add(value);
        }

        return [.. decoded];
    }

    private static byte DecodeAt(byte[] encoded, ref int index)
    {
        if (encoded[index] == '%'
            && index + 2 < encoded.Length
            && TryParseHexPair(encoded.AsSpan(index + 1, 2), out byte escaped))
        {
            index += 3;
            return escaped;
        }

        return encoded[index++];
    }

    private static bool TryParseHexPair(ReadOnlySpan<byte> pair, out byte value) =>
        byte.TryParse(pair, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value);
}
