using System.Buffers;
using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Finds a response's redirect target, the source of <c>%{redirect_url}</c> and of the
/// next hop under <c>-L</c>: its first non-empty <c>Location</c> value, resolved against the
/// request URL as curl 8.21.0 resolves it (measured, BL-179 Notes).
/// </summary>
/// <remarks>
/// <para>
/// An absolute value (one with a scheme) keeps its bytes, spaces and percent escapes as
/// sent, with its scheme lowercased and, when it names an authority, its dot segments
/// removed and an empty path made <c>/</c>; one that is not a valid URL is kept whole. Its bytes from
/// 0x80 are marked (<see cref="HeaderByteText" />), so <c>%{redirect_url}</c> and, once followed,
/// <c>%{url_effective}</c> write each back as that one byte, as curl 8.22.0 does (CGR-062, ADR-0923).
/// </para>
/// <para>
/// Any other value is a reference resolved by RFC 3986 section 5.2: scheme-relative
/// (<c>//host/x</c>), absolute-path (<c>/x</c>), relative-path (<c>x</c>, <c>../x</c>),
/// query-only (<c>?q</c>) and fragment-only (<c>#f</c>). Before resolving, its spaces (<c>+</c> in
/// the query, <c>%20</c> elsewhere), control bytes and non-ASCII bytes are percent-encoded and its percent escapes uppercased, as curl does
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
    /// The resolved target, or <see langword="null" /> when no <c>Location</c> header has a
    /// value. Any status counts: curl 8.22.0 writes <c>%{redirect_url}</c> for a <c>Location</c>
    /// on a 200, 201 or 404 too, with or without <c>-L</c>, and follows only a 3xx (BL-2212).
    /// </returns>
    internal static string? Find(CurlUrl requestUrl, HttpResponseHead head) => Find(requestUrl, head, Encoding.Latin1);

    /// <summary>
    /// Finds where a response redirects to, a relative target keeping the request URL's path as its
    /// bytes in <paramref name="encoding" />.
    /// </summary>
    /// <param name="requestUrl">The URL the request was sent to.</param>
    /// <param name="head">The final response's head.</param>
    /// <param name="encoding">The command-line text encoding the request URL was typed in.</param>
    /// <returns>The resolved target, or <see langword="null" /> when no <c>Location</c> header has a value.</returns>
    internal static string? Find(CurlUrl requestUrl, HttpResponseHead head, Encoding encoding)
    {
        string? location = head.Headers.FirstOrDefault(IsNonEmptyLocation)?.Value ?? InterimLocation(head);
        return location is null ? null : Resolve(requestUrl, location, encoding, OperatingSystem.IsWindows());
    }

    /// <summary>
    /// Finds the last non-empty <c>Location</c> value among the heads read, which the final head
    /// leaves in place when it has none: curl 8.22.0 reports a <c>Location</c> from a 1xx head in
    /// <c>%{redirect_url}</c> (CGR-208).
    /// </summary>
    private static string? InterimLocation(HttpResponseHead head)
    {
        string? found = null;
        foreach (string line in Encoding.Latin1.GetString(head.HeadBytes.Span).Split('\n'))
        {
            int colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0 && string.Equals(line[..colon], HeaderName, StringComparison.OrdinalIgnoreCase))
            {
                string value = line[(colon + 1)..].Trim(' ', '\t', '\r');
                found = value.Length > 0 ? value : found;
            }
        }

        return found;
    }

    /// <summary>
    /// Resolves a <c>Location</c> value against the request URL.
    /// </summary>
    /// <param name="requestUrl">The URL the request was sent to.</param>
    /// <param name="location">The non-empty <c>Location</c> value, without surrounding blanks.</param>
    /// <returns>The target URL as curl reports it.</returns>
    internal static string Resolve(CurlUrl requestUrl, string location) => Resolve(requestUrl, location, Encoding.Latin1, OperatingSystem.IsWindows());

    /// <summary>
    /// Resolves a <c>Location</c> value against the request URL with the drive-letter rules of
    /// the chosen platform, so both can be tested on either.
    /// </summary>
    /// <param name="requestUrl">The URL the request was sent to.</param>
    /// <param name="location">The non-empty <c>Location</c> value, without surrounding blanks.</param>
    /// <param name="driveLetters">Whether a <c>file</c> path may start with a drive letter, as on Windows.</param>
    /// <returns>The target URL as curl reports it.</returns>
    internal static string Resolve(CurlUrl requestUrl, string location, bool driveLetters) => Resolve(requestUrl, location, Encoding.Latin1, driveLetters);

    /// <summary>
    /// Resolves a <c>Location</c> value against the request URL, a relative one keeping the request
    /// URL's path as its bytes in <paramref name="encoding" /> (<c>/é/</c> in cp1252 is <c>/%E9/</c>).
    /// </summary>
    /// <param name="requestUrl">The URL the request was sent to.</param>
    /// <param name="location">The non-empty <c>Location</c> value, without surrounding blanks.</param>
    /// <param name="encoding">The command-line text encoding the request URL was typed in.</param>
    /// <param name="driveLetters">Whether a <c>file</c> path may start with a drive letter, as on Windows.</param>
    /// <returns>The target URL as curl reports it.</returns>
    internal static string Resolve(CurlUrl requestUrl, string location, Encoding encoding, bool driveLetters)
    {
        if (HasScheme(location))
        {
            return HeaderByteText.Mark(NormalizeAbsolute(location, driveLetters));
        }

        string reference = Encode(location);
        if (reference.StartsWith("//", StringComparison.Ordinal))
        {
            return NormalizeAbsolute(requestUrl.Scheme + ":" + reference, driveLetters);
        }

        (string path, string query, string fragment) = Split(reference);
        return HttpUrlText.Origin(requestUrl) + TargetPath(HttpUrlText.Path(requestUrl, encoding), path) + TargetQuery(HttpUrlText.Query(requestUrl), path, query) + fragment;
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
    /// As curl 8.22.0 does (CGR-062, ADR-0829): a scheme other than <c>file</c> followed by one or
    /// three slashes names its host after them (<c>http:/b</c> is <c>http://b/</c>), and on
    /// Windows a <c>file</c> URL whose path starts with a drive letter loses the slash before it
    /// and its authority (<c>file:///c:/x</c> is <c>file://c:/x</c>).
    /// </summary>
    private static string NormalizeAbsolute(string url, bool driveLetters)
    {
        // curl follows with CURLU_ALLOW_SPACE, so a space does not make the target invalid.
        if (!CurlUrl.TryParse(url.Replace(" ", "%20", StringComparison.Ordinal), pathAsIs: false, out _))
        {
            return url;
        }

        int colon = url.IndexOf(':', StringComparison.Ordinal);
        string scheme = url[..colon].ToLowerInvariant();
        string rest = WithHostAfterSlashes(scheme, url[(colon + 1)..]);
        if (!rest.StartsWith("//", StringComparison.Ordinal))
        {
            return scheme + ":" + rest;
        }

        int authorityEnd = rest.IndexOfAny(AuthorityEnds, 2);
        int pathStart = authorityEnd < 0 ? rest.Length : authorityEnd;
        (string path, string query, string fragment) = Split(rest[pathStart..]);
        string normalizedPath = path.Length == 0 ? "/" : RemoveDotSegments(path);
        return scheme + ":" + (StartsWithDrive(scheme, normalizedPath, driveLetters) ? "//" + normalizedPath[1..] : rest[..pathStart] + normalizedPath) + query + fragment;
    }

    /// <summary>
    /// The text after a URL's scheme and colon with the slashes before its host made two, for a
    /// scheme other than <c>file</c> that has one or more; <paramref name="rest" /> otherwise.
    /// </summary>
    private static string WithHostAfterSlashes(string scheme, string rest) =>
        scheme != "file" && rest.StartsWith('/') ? "//" + rest.TrimStart('/') : rest;

    /// <summary>
    /// Whether <paramref name="path" /> is a Windows <c>file</c> path naming a drive, <c>/c:/x</c>.
    /// </summary>
    private static bool StartsWithDrive(string scheme, string path, bool driveLetters) =>
        driveLetters && scheme == "file" && path.Length > 2 && char.IsAsciiLetter(path[1]) && path[2] == ':';

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
    /// RFC 3986 section 5.2.4 does; a trailing dot segment leaves a trailing <c>/</c>. A dot
    /// written <c>%2E</c> (any case) counts as one, as curl 8.22.0 sends <c>/a/%2e%2e/b</c> as
    /// <c>/b</c> (CGR-062).
    /// </summary>
    private static string RemoveDotSegments(string path)
    {
        string[] segments = path.Split('/');
        List<string> kept = [];
        for (int index = 1; index < segments.Length; index++)
        {
            string segment = segments[index].Replace("%2E", ".", StringComparison.OrdinalIgnoreCase);
            if (segment == "..")
            {
                RemoveLast(kept);
            }

            if (segment is "." or "..")
            {
                AddIfLast(kept, index == segments.Length - 1);
                continue;
            }

            kept.Add(segments[index]);
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
    /// Percent-encodes a relative reference's spaces (<c>+</c> in the query), its control bytes and its bytes above 0x7E, each as itself, and
    /// uppercases the hexadecimal digits of its percent escapes, as curl 8.21.0 does.
    /// </summary>
    private static string Encode(string reference)
    {
        StringBuilder encoded = new(reference.Length);
        int hash = reference.IndexOf('#', StringComparison.Ordinal);
        int question = reference.IndexOf('?', StringComparison.Ordinal);
        int queryEnd = hash < 0 ? reference.Length : hash;
        for (int index = 0; index < reference.Length;)
        {
            index += AppendEncoded(encoded, reference, index, question >= 0 && index > question && index < queryEnd);
        }

        return encoded.ToString();
    }

    /// <summary>
    /// Appends the character at <paramref name="index" />, encoded, and returns how many
    /// characters it took: three for a percent escape, two for a surrogate pair, one otherwise.
    /// </summary>
    private static int AppendEncoded(StringBuilder encoded, string value, int index, bool inQuery)
    {
        char character = value[index];
        if (character == '%' && IsEscape(value, index))
        {
            encoded.Append('%').Append(char.ToUpperInvariant(value[index + 1])).Append(char.ToUpperInvariant(value[index + 2]));
            return 3;
        }

        return AppendUnescaped(encoded, value, index, inQuery);
    }

    /// <summary>
    /// Appends the character at <paramref name="index" />, which does not start a percent
    /// escape: a space as <c>+</c> in the query, a space, control byte (TAB, 0x01-0x1F) or byte above
    /// 0x7E escaped, as curl 8.22.0 escapes them (CGR-061), else itself.
    /// Returns how many characters it took.
    /// </summary>
    private static int AppendUnescaped(StringBuilder encoded, string value, int index, bool inQuery)
    {
        char character = value[index];
        if (character == ' ' && inQuery)
        {
            encoded.Append('+');
            return 1;
        }

        if (character is <= ' ' or > '~')
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
        // A header value is read as Latin-1, one character per byte: each byte is escaped as itself.
        if (value[index] <= 'ÿ')
        {
            encoded.Append('%').Append(((int)value[index]).ToString("X2", CultureInfo.InvariantCulture));
            return 1;
        }

        int length = char.IsSurrogatePair(value, index) ? 2 : 1;
        foreach (byte octet in Encoding.UTF8.GetBytes(value, index, length))
        {
            encoded.Append('%').Append(octet.ToString("X2", CultureInfo.InvariantCulture));
        }

        return length;
    }
}
