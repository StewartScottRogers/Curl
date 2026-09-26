using System.Buffers;
using System.Globalization;
using System.Text;

namespace Curl.Protocol.Http;

/// <summary>
/// Finds a 3xx response's redirect target, the source of <c>%{redirect_url}</c> and of the
/// next hop under <c>-L</c>: its first non-empty <c>Location</c> value, resolved against the
/// request URL as curl 8.21.0 resolves it (measured, BL-179 Notes).
/// </summary>
/// <remarks>
/// <para>
/// An absolute value (one with a scheme) keeps its bytes, spaces and percent escapes as
/// sent, with its scheme lowercased and, when it names an authority, its dot segments
/// removed and an empty path made <c>/</c>; one that is not a valid URL is kept whole.
/// </para>
/// <para>
/// Any other value is a reference resolved by RFC 3986 section 5.2: scheme-relative
/// (<c>//host/x</c>), absolute-path (<c>/x</c>), relative-path (<c>x</c>, <c>../x</c>),
/// query-only (<c>?q</c>) and fragment-only (<c>#f</c>). Before resolving, its spaces and
/// non-ASCII characters are percent-encoded and its percent escapes uppercased, as curl does
/// to a relative <c>Location</c> and not to an absolute one.
/// </para>
/// </remarks>
internal static class HttpRedirectLocation
{
    private const string HeaderName = "Location";

    private static readonly char[] AuthorityEnds = ['/', '?', '#'];

    private static readonly SearchValues<char> SchemeCharacters =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789+-.");

    /// <summary>
    /// Finds where a response redirects to.
    /// </summary>
    /// <param name="requestUrl">The URL the request was sent to.</param>
    /// <param name="head">The final response's head.</param>
    /// <returns>
    /// The resolved target, or <see langword="null" /> when the status is not 3xx or no
    /// <c>Location</c> header has a value.
    /// </returns>
    internal static string? Find(Uri requestUrl, HttpResponseHead head)
    {
        if (head.StatusLine.StatusCode is < 300 or >= 400)
        {
            return null;
        }

        HttpResponseHeader? location = head.Headers.FirstOrDefault(IsNonEmptyLocation);
        return location is null ? null : Resolve(requestUrl, location.Value);
    }

    /// <summary>
    /// Resolves a <c>Location</c> value against the request URL.
    /// </summary>
    /// <param name="requestUrl">The URL the request was sent to.</param>
    /// <param name="location">The non-empty <c>Location</c> value, without surrounding blanks.</param>
    /// <returns>The target URL as curl reports it.</returns>
    internal static string Resolve(Uri requestUrl, string location)
    {
        if (HasScheme(location))
        {
            return NormalizeAbsolute(location);
        }

        string reference = Encode(location);
        if (reference.StartsWith("//", StringComparison.Ordinal))
        {
            return NormalizeAbsolute(requestUrl.Scheme + ":" + reference);
        }

        (string path, string query, string fragment) = Split(reference);
        string origin = requestUrl.GetLeftPart(UriPartial.Authority);
        return origin + TargetPath(requestUrl.AbsolutePath, path) + TargetQuery(requestUrl.Query, path, query) + fragment;
    }

    private static bool IsNonEmptyLocation(HttpResponseHeader header) =>
        header.Value.Length > 0 && string.Equals(header.Name, HeaderName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether <paramref name="value" /> starts with an RFC 3986 scheme and a colon.
    /// </summary>
    private static bool HasScheme(string value)
    {
        int colon = value.IndexOf(':', StringComparison.Ordinal);
        return colon > 0 && char.IsAsciiLetter(value[0]) && !value.AsSpan(1, colon - 1).ContainsAnyExcept(SchemeCharacters);
    }

    /// <summary>
    /// Lowercases an absolute URL's scheme and, when it names an authority, removes its dot
    /// segments and makes an empty path <c>/</c>; a value that is not a valid URL is kept whole.
    /// </summary>
    private static string NormalizeAbsolute(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            return url;
        }

        int colon = url.IndexOf(':', StringComparison.Ordinal);
        string scheme = url[..colon].ToLowerInvariant();
        string rest = url[(colon + 1)..];
        if (!rest.StartsWith("//", StringComparison.Ordinal))
        {
            return scheme + ":" + rest;
        }

        int authorityEnd = rest.IndexOfAny(AuthorityEnds, 2);
        int pathStart = authorityEnd < 0 ? rest.Length : authorityEnd;
        (string path, string query, string fragment) = Split(rest[pathStart..]);
        return scheme + ":" + rest[..pathStart] + (path.Length == 0 ? "/" : RemoveDotSegments(path)) + query + fragment;
    }

    /// <summary>
    /// Splits a reference into its path, its query with the <c>?</c>, and its fragment with the
    /// <c>#</c>; a part that is absent is empty.
    /// </summary>
    private static (string Path, string Query, string Fragment) Split(string reference)
    {
        int hash = reference.IndexOf('#', StringComparison.Ordinal);
        string fragment = hash < 0 ? string.Empty : reference[hash..];
        string beforeFragment = hash < 0 ? reference : reference[..hash];
        int question = beforeFragment.IndexOf('?', StringComparison.Ordinal);
        return question < 0
            ? (beforeFragment, string.Empty, fragment)
            : (beforeFragment[..question], beforeFragment[question..], fragment);
    }

    /// <summary>
    /// The target path of RFC 3986 section 5.2.2: the base path for an empty reference path,
    /// the reference path when it is absolute, and the two merged otherwise, dot segments removed.
    /// </summary>
    private static string TargetPath(string basePath, string path)
    {
        if (path.Length == 0)
        {
            return basePath;
        }

        return RemoveDotSegments(path[0] == '/' ? path : basePath[..(basePath.LastIndexOf('/') + 1)] + path);
    }

    /// <summary>
    /// The target query: the reference's own, or the base's when the reference has neither a
    /// path nor a query.
    /// </summary>
    private static string TargetQuery(string baseQuery, string path, string query) =>
        path.Length == 0 && query.Length == 0 ? baseQuery : query;

    /// <summary>
    /// Removes <c>.</c> and <c>..</c> segments from a path that starts with <c>/</c>, as
    /// RFC 3986 section 5.2.4 does; a trailing dot segment leaves a trailing <c>/</c>.
    /// </summary>
    private static string RemoveDotSegments(string path)
    {
        string[] segments = path.Split('/');
        List<string> kept = [];
        for (int index = 1; index < segments.Length; index++)
        {
            string segment = segments[index];
            if (segment == "..")
            {
                RemoveLast(kept);
            }

            if (segment is "." or "..")
            {
                AddIfLast(kept, index == segments.Length - 1);
                continue;
            }

            kept.Add(segment);
        }

        return "/" + string.Join('/', kept);
    }

    private static void RemoveLast(List<string> kept)
    {
        if (kept.Count > 0)
        {
            kept.RemoveAt(kept.Count - 1);
        }
    }

    private static void AddIfLast(List<string> kept, bool isLast)
    {
        if (isLast)
        {
            kept.Add(string.Empty);
        }
    }

    /// <summary>
    /// Percent-encodes a relative reference's spaces and non-ASCII characters as UTF-8 and
    /// uppercases the hexadecimal digits of its percent escapes, as curl 8.21.0 does.
    /// </summary>
    private static string Encode(string reference)
    {
        StringBuilder encoded = new(reference.Length);
        for (int index = 0; index < reference.Length;)
        {
            index += AppendEncoded(encoded, reference, index);
        }

        return encoded.ToString();
    }

    /// <summary>
    /// Appends the character at <paramref name="index" />, encoded, and returns how many
    /// characters it took: three for a percent escape, two for a surrogate pair, one otherwise.
    /// </summary>
    private static int AppendEncoded(StringBuilder encoded, string value, int index)
    {
        char character = value[index];
        if (character == '%' && IsEscape(value, index))
        {
            encoded.Append('%').Append(char.ToUpperInvariant(value[index + 1])).Append(char.ToUpperInvariant(value[index + 2]));
            return 3;
        }

        if (character is ' ' or > '~')
        {
            return AppendEscaped(encoded, value, index);
        }

        encoded.Append(character);
        return 1;
    }

    private static bool IsEscape(string value, int percent) =>
        percent + 2 < value.Length && char.IsAsciiHexDigit(value[percent + 1]) && char.IsAsciiHexDigit(value[percent + 2]);

    /// <summary>
    /// Appends the UTF-8 percent escapes of the character at <paramref name="index" />, a
    /// surrogate pair taken whole, and returns how many characters it took.
    /// </summary>
    private static int AppendEscaped(StringBuilder encoded, string value, int index)
    {
        int length = char.IsSurrogatePair(value, index) ? 2 : 1;
        foreach (byte octet in Encoding.UTF8.GetBytes(value, index, length))
        {
            encoded.Append('%').Append(octet.ToString("X2", CultureInfo.InvariantCulture));
        }

        return length;
    }
}
