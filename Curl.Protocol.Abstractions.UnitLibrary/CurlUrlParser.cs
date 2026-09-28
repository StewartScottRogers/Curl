using System.Text;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Parses the URL of a transfer as curl 8.21.0's <c>parseurl</c> in <c>lib/urlapi.c</c>
/// does with the flags libcurl uses for a transfer: schemes are guessed, unknown schemes
/// are accepted, and dot segments are removed unless path-as-is is set.
/// </summary>
internal static class CurlUrlParser
{
    /// <summary>The longest URL curl accepts, in UTF-8 bytes (<c>CURL_MAX_INPUT_LENGTH</c>).</summary>
    private const int MaximumLength = 8_000_000;

    /// <summary>The most slashes curl accepts between a scheme and its host.</summary>
    private const int MaximumSlashes = 3;

    private const string FileScheme = "file";

    /// <summary>The length of <c>localhost</c> and of <c>127.0.0.1</c>, the hosts a <c>file</c> URL accepts.</summary>
    private const int LocalHostLength = 9;

    /// <summary>
    /// Parses <paramref name="text" />, or returns <see langword="null" /> when curl rejects
    /// it, with <paramref name="rejection" /> saying why.
    /// </summary>
    public static CurlUrl? Parse(string text, bool pathAsIs, bool driveLetters, out CurlUrlRejection rejection)
    {
        if (Encoding.UTF8.GetByteCount(text) > MaximumLength || text.Any(IsControlOrSpace))
        {
            rejection = CurlUrlRejection.MalformedInput;

            return null;
        }

        string? scheme = CurlUrlScheme.Read(text, driveLetters);
        if (scheme != FileScheme)
        {
            return ParseWithAuthority(text, scheme, pathAsIs, out rejection);
        }

        CurlUrl? url = ParseFile(text, pathAsIs, driveLetters);
        rejection = url is null ? CurlUrlRejection.BadFileUrl : CurlUrlRejection.None;

        return url;
    }

    private static bool IsControlOrSpace(char character) => character <= ' ' || character == '\x7f';

    /// <summary>
    /// Parses a URL whose scheme is not <c>file</c>, or that has no scheme: one to three
    /// slashes, then an authority that must name a host.
    /// </summary>
    private static CurlUrl? ParseWithAuthority(string text, string? scheme, bool pathAsIs, out CurlUrlRejection rejection)
    {
        string? rest = scheme is null ? text : SkipSchemeAndSlashes(text, scheme);
        if (rest is null)
        {
            rejection = CurlUrlRejection.BadSlashes;

            return null;
        }

        int hostEnd = AuthorityLength(rest);
        if (hostEnd == 0)
        {
            rejection = CurlUrlRejection.NoHost;

            return null;
        }

        CurlUrlAuthority? authority = CurlUrlAuthority.Parse(rest[..hostEnd], scheme, out rejection);

        return authority is null
            ? null
            : Build(text, scheme ?? CurlUrlScheme.Guess(authority.Host), authority, rest[hostEnd..], pathAsIs);
    }

    /// <summary>
    /// Returns what follows the scheme and the one to three slashes after it, or
    /// <see langword="null" /> when there are more than three.
    /// </summary>
    private static string? SkipSchemeAndSlashes(string text, string scheme)
    {
        string rest = ConvertBackslashesAfterDoubleSlash(text[(scheme.Length + 1)..]);
        int slashes = rest.AsSpan().IndexOfAnyExcept('/');
        if (slashes < 0)
        {
            slashes = rest.Length;
        }

        return slashes > MaximumSlashes ? null : rest[slashes..];
    }

    /// <summary>The length of the authority: up to the first <c>/</c>, <c>?</c> or <c>#</c>.</summary>
    private static int AuthorityLength(string rest)
    {
        int end = rest.AsSpan().IndexOfAny('/', '?', '#');

        return end < 0 ? rest.Length : end;
    }

    /// <summary>
    /// Parses a <c>file</c> URL as curl does on the platform chosen: an authority, when
    /// there is one, must be empty, a drive letter, <c>localhost</c> or
    /// <c>127.0.0.1</c>; a slash before a drive letter is dropped on Windows, and a drive
    /// letter is rejected elsewhere.
    /// </summary>
    private static CurlUrl? ParseFile(string text, bool pathAsIs, bool driveLetters)
    {
        string? rest = SkipFileAuthority(text[(FileScheme.Length + 1)..]);
        string? path = rest is null ? null : ApplyDriveLetterRule(rest, driveLetters);

        return path is null ? null : Build(text, FileScheme, CurlUrlAuthority.None, path, pathAsIs);
    }

    /// <summary>
    /// Drops the slash before a drive letter when drive letters are allowed, and rejects
    /// a path with a drive letter when they are not.
    /// </summary>
    private static string? ApplyDriveLetterRule(string path, bool driveLetters)
    {
        bool slashBeforeDrive = path.StartsWith('/') && StartsWithDriveLetter(path, 1);
        if (driveLetters)
        {
            return slashBeforeDrive ? path[1..] : path;
        }

        return slashBeforeDrive || StartsWithDriveLetter(path, 0) ? null : path;
    }

    /// <summary>
    /// Returns what follows <c>file:</c> with its backslashes converted and its
    /// authority, if any, skipped, or <see langword="null" /> when the authority is not
    /// one curl accepts.
    /// </summary>
    private static string? SkipFileAuthority(string rest)
    {
        rest = ConvertBackslashesAfterDoubleSlash(rest);
        bool hasAuthority = rest.StartsWith("//", StringComparison.Ordinal);
        if (hasAuthority)
        {
            rest = rest[2..];
        }

        if (!hasAuthority || rest.StartsWith('/') || StartsWithDriveLetter(rest, 0))
        {
            return rest;
        }

        return StartsWithLocalHost(rest) ? rest[LocalHostLength..] : null;
    }

    private static bool StartsWithLocalHost(string text) =>
        text.StartsWith("localhost/", StringComparison.OrdinalIgnoreCase)
        || text.StartsWith("127.0.0.1/", StringComparison.Ordinal);

    /// <summary>
    /// A drive letter as curl's <c>STARTS_WITH_URL_DRIVE_PREFIX</c> sees one: a letter,
    /// then <c>:</c> or <c>|</c>, then <c>/</c>, <c>\</c> or the end of the text.
    /// </summary>
    private static bool StartsWithDriveLetter(string text, int index)
    {
        ReadOnlySpan<char> rest = text.AsSpan(index);

        return rest.Length >= 2
            && char.IsAsciiLetter(rest[0])
            && IsDriveSeparator(rest[1])
            && (rest.Length == 2 || IsSlash(rest[2]));
    }

    private static bool IsDriveSeparator(char character) => character == ':' || character == '|';

    private static bool IsSlash(char character) => character == '/' || character == '\\';

    /// <summary>
    /// Turns each <c>\</c> before the first <c>?</c> or <c>#</c> into <c>/</c> when the
    /// text after the scheme's colon starts with <c>//</c>, as curl 8.21.0 on Windows was
    /// measured to do for every scheme; after a single slash they stay as typed.
    /// </summary>
    private static string ConvertBackslashesAfterDoubleSlash(string text)
    {
        if (!text.StartsWith("//", StringComparison.Ordinal))
        {
            return text;
        }

        int end = text.AsSpan().IndexOfAny('?', '#');
        if (end < 0)
        {
            end = text.Length;
        }

        return string.Concat(text[..end].Replace('\\', '/'), text.AsSpan(end));
    }

    /// <summary>
    /// Splits the fragment at the first <c>#</c> and the query at the first <c>?</c>
    /// before it, then settles the path: <c>/</c> when it is empty or one character, and
    /// without dot segments unless <paramref name="pathAsIs" /> is set.
    /// </summary>
    private static CurlUrl Build(string text, string scheme, CurlUrlAuthority authority, string tail, bool pathAsIs)
    {
        string? fragment = null;
        int hash = tail.IndexOf('#');
        if (hash >= 0)
        {
            fragment = tail[(hash + 1)..];
            tail = tail[..hash];
        }

        string? query = null;
        int question = tail.IndexOf('?');
        if (question >= 0)
        {
            query = tail[(question + 1)..];
            tail = tail[..question];
        }

        string path = tail.Length <= 1 ? "/" : tail;
        if (!pathAsIs)
        {
            path = CurlUrlDotSegments.Remove(path);
        }

        return new CurlUrl(text, scheme, authority, path, query, fragment);
    }
}
