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

    /// <summary>The slashes before an authority that may be empty (<c>CURLU_NO_AUTHORITY</c>).</summary>
    private const int AuthoritySlashes = 2;

    private const string FileScheme = "file";

    /// <summary>The length of <c>localhost</c> and of <c>127.0.0.1</c>, the hosts a <c>file</c> URL accepts.</summary>
    private const int LocalHostLength = 9;

    /// <summary>
    /// Parses <paramref name="text" />, or returns <see langword="null" /> when curl rejects
    /// it, with <paramref name="rejection" /> saying why. With <paramref name="disallowUser" />,
    /// user information in the authority is rejected before its host and port are checked. With
    /// <paramref name="allowEmptyHost" />, as libcurl's <c>CURLU_NO_AUTHORITY</c>, at most two
    /// slashes follow the scheme and an empty authority is an empty host. With
    /// <paramref name="keepFileBackslashes" />, as libcurl's own <c>parse_file</c>, a <c>file</c> URL's
    /// backslashes stay as typed; without it they become slashes after <c>file://</c>, as the curl tool's do.
    /// With <paramref name="fileSchemeWithoutSlash" />, as <c>Curl_is_absolute_url</c> when it does not guess
    /// schemes, text starting <c>file:</c> is a <c>file</c> URL even with no <c>/</c> after the colon.
    /// </summary>
    public static CurlUrl? Parse(
        string text,
        bool pathAsIs,
        bool driveLetters,
        bool disallowUser,
        out CurlUrlRejection rejection,
        bool allowEmptyHost = false,
        bool keepFileBackslashes = false,
        bool fileSchemeWithoutSlash = false)
    {
        if (Encoding.UTF8.GetByteCount(text) > MaximumLength || text.Any(IsControlOrSpace))
        {
            rejection = CurlUrlRejection.MalformedInput;

            return null;
        }

        string? scheme = ReadScheme(text, driveLetters, fileSchemeWithoutSlash);
        if (scheme != FileScheme)
        {
            return ParseWithAuthority(text, scheme, new AuthorityRules(pathAsIs, disallowUser, allowEmptyHost), out rejection);
        }

        CurlUrl? url = ParseFile(text, pathAsIs, driveLetters, keepFileBackslashes);
        rejection = url is null ? CurlUrlRejection.BadFileUrl : CurlUrlRejection.None;

        return url;
    }

    private static bool IsControlOrSpace(char character) => character <= ' ' || character == '\x7f';

    /// <summary>The scheme <see cref="CurlUrlScheme.Read" /> finds, else <see cref="FileSchemeWithoutSlash" />'s.</summary>
    private static string? ReadScheme(string text, bool driveLetters, bool fileSchemeWithoutSlash) =>
        CurlUrlScheme.Read(text, driveLetters) ?? FileSchemeWithoutSlash(text, fileSchemeWithoutSlash);

    /// <summary><c>file</c> when <paramref name="allowed" /> and the text starts <c>file:</c> in any case, else <see langword="null" />.</summary>
    private static string? FileSchemeWithoutSlash(string text, bool allowed) =>
        allowed && text.StartsWith(FileScheme + ":", StringComparison.OrdinalIgnoreCase) ? FileScheme : null;

    /// <summary>
    /// Parses a URL whose scheme is not <c>file</c>, or that has no scheme: one to three
    /// slashes, then an authority that must name a host unless
    /// <see cref="AuthorityRules.AllowEmptyHost" /> lets it be empty.
    /// </summary>
    private static CurlUrl? ParseWithAuthority(string text, string? scheme, AuthorityRules rules, out CurlUrlRejection rejection)
    {
        string? rest = scheme is null ? text : SkipSchemeAndSlashes(text, scheme, rules.AllowEmptyHost);
        if (rest is null)
        {
            rejection = CurlUrlRejection.BadSlashes;

            return null;
        }

        int hostEnd = AuthorityLength(rest);
        CurlUrlAuthority? authority = hostEnd == 0
            ? EmptyAuthority(rules.AllowEmptyHost, out rejection)
            : CurlUrlAuthority.Parse(rest[..hostEnd], scheme, rules.DisallowUser, out rejection);

        return authority is null
            ? null
            : Build(text, scheme ?? CurlUrlScheme.Guess(authority.Host), authority, rest[hostEnd..], rules.PathAsIs);
    }

    /// <summary>
    /// An authority with nothing in it: an empty host when <paramref name="allowEmptyHost" />, as upstream's
    /// <c>parseurl</c> accepts under <c>CURLU_NO_AUTHORITY</c>, else <see cref="CurlUrlRejection.NoHost" />.
    /// </summary>
    private static CurlUrlAuthority? EmptyAuthority(bool allowEmptyHost, out CurlUrlRejection rejection)
    {
        rejection = allowEmptyHost ? CurlUrlRejection.None : CurlUrlRejection.NoHost;

        return allowEmptyHost ? CurlUrlAuthority.None : null;
    }

    /// <summary>
    /// Returns what follows the scheme and the one to three slashes after it, or
    /// <see langword="null" /> when there are more than three. With
    /// <paramref name="allowEmptyHost" /> at most two are skipped and any after them start the
    /// path, as upstream's <c>parse_scheme</c> does under <c>CURLU_NO_AUTHORITY</c>. A backslash
    /// stays as typed: curl 8.21.0 on Windows was measured to send <c>http://h/a\b</c> as
    /// <c>GET /a\b</c> and to reject a backslash in the host (BL-1996).
    /// </summary>
    private static string? SkipSchemeAndSlashes(string text, string scheme, bool allowEmptyHost)
    {
        string rest = text[(scheme.Length + 1)..];
        int slashes = rest.AsSpan().IndexOfAnyExcept('/');
        if (slashes < 0)
        {
            slashes = rest.Length;
        }

        if (allowEmptyHost)
        {
            return rest[Math.Min(slashes, AuthoritySlashes)..];
        }

        return slashes > MaximumSlashes ? null : rest[slashes..];
    }

    /// <summary>How a URL's authority is read: path-as-is, user information refused, an empty host allowed.</summary>
    private readonly record struct AuthorityRules(bool PathAsIs, bool DisallowUser, bool AllowEmptyHost);

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
    /// letter is rejected elsewhere. As curl 8.22.0's <c>parse_file</c>, <c>file:/</c> is too short
    /// to be a file URL and the path must start with a slash or a drive letter, so <c>file:foo</c>,
    /// <c>file:./</c>, <c>file:?q</c> and <c>file:#f</c> are rejected (lib1560; the curl tool,
    /// measured, exits 3 for each).
    /// </summary>
    private static CurlUrl? ParseFile(string text, bool pathAsIs, bool driveLetters, bool keepBackslashes)
    {
        string afterScheme = text[(FileScheme.Length + 1)..];
        string? rest = afterScheme.Length < 2 ? null : SkipFileAuthority(afterScheme, keepBackslashes);
        string? path = rest is null || !StartsAsFilePath(rest) ? null : ApplyDriveLetterRule(rest, driveLetters);

        return path is null ? null : Build(text, FileScheme, CurlUrlAuthority.None, path, pathAsIs);
    }

    private static bool StartsAsFilePath(string rest) => rest.StartsWith('/') || StartsWithDriveLetter(rest, 0);

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
    /// one curl accepts. With <paramref name="keepBackslashes" /> the backslashes stay as typed.
    /// </summary>
    private static string? SkipFileAuthority(string rest, bool keepBackslashes)
    {
        rest = ConvertBackslashesAfterDoubleSlash(rest, keepBackslashes);
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
    /// text after <c>file:</c> starts with <c>//</c>, as curl 8.21.0 on Windows was
    /// measured to do for a <c>file</c> URL; after a single slash, or with <paramref name="keepBackslashes" />,
    /// they stay as typed.
    /// </summary>
    private static string ConvertBackslashesAfterDoubleSlash(string text, bool keepBackslashes)
    {
        if (keepBackslashes || !text.StartsWith("//", StringComparison.Ordinal))
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
