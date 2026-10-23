namespace Curl.Core;

/// <summary>
/// Gives a URL typed without a scheme the scheme curl 8.21.0 guesses for it: the scheme its
/// host name's prefix implies (<c>ftp.</c>, <c>dict.</c>, <c>ldap.</c>, <c>imap.</c>,
/// <c>smtp.</c>, <c>pop3.</c>, compared without regard to case), otherwise <c>http</c>.
/// </summary>
/// <remarks>
/// <para>
/// A URL has a scheme when it starts with an ASCII letter followed by letters, digits,
/// <c>+</c>, <c>.</c> or <c>-</c>, then <c>:/</c>. So <c>foo:/x</c> and
/// <c>ftp.example.com:/x</c> already name a scheme (one curl does not support), while
/// <c>example.com:80</c> does not.
/// </para>
/// <para>
/// The host is the authority, the text before the first <c>/</c>, <c>?</c> or <c>#</c>,
/// after the last <c>@</c>; so <c>ftp.x@dict.example.com</c> is <c>dict</c> and
/// <c>u@ftp.example.com/p@imap.x</c> is <c>ftp</c>. Only the scheme is added: the rest of
/// the text is kept byte for byte, and rejecting a malformed URL is the URL parser's job.
/// Every case was measured against curl 8.21.0 on 2026-09-26 by reading
/// <c>%{scheme}</c> and <c>%{url_effective}</c> from <c>-w</c>.
/// </para>
/// </remarks>
public static class UrlSchemeGuesser
{
    /// <summary>The scheme guessed when no host prefix implies another.</summary>
    public const string DefaultScheme = "http";

    /// <summary>The scheme whose URL has no authority to hold a port, so an empty "port" is kept.</summary>
    private const string FileScheme = "file";

    private static readonly string[] HostPrefixSchemes = ["ftp", "dict", "ldap", "imap", "smtp", "pop3"];

    /// <summary>
    /// Returns <paramref name="url" /> unchanged when it names a scheme, otherwise the guessed
    /// scheme and <c>://</c> followed by <paramref name="url" />.
    /// </summary>
    /// <param name="url">The URL as the command line gave it.</param>
    /// <returns>The URL with a scheme.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="url" /> is <see langword="null" />.</exception>
    public static string AddGuessedScheme(string url)
    {
        ArgumentNullException.ThrowIfNull(url);

        return AddScheme(url, defaultScheme: null);
    }

    /// <summary>
    /// Returns <paramref name="url" /> unchanged when it names a scheme or, for a scheme other than
    /// <c>file</c>, its authority ends in an empty port, otherwise <paramref name="defaultScheme" /> (the <c>--proto-default</c> scheme) or,
    /// when that is <see langword="null" />, the guessed scheme, followed by <c>://</c> and <paramref name="url" />.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A default scheme replaces the guess outright, as curl 8.21.0 does: with
    /// <c>--proto-default dict</c>, <c>ftp.localhost:1/</c> becomes <c>dict://ftp.localhost:1/</c>
    /// (measured on Windows, 2026-09-28).
    /// </para>
    /// <para>
    /// curl 8.22.0's <c>Curl_parse_port</c> accepts an empty port only in a URL that named its own
    /// scheme, so <c>http:</c>, <c>1http://127.0.0.1:9/</c> and, on Windows, <c>h://127.0.0.1:9/x</c>
    /// (authorities <c>http:</c>, <c>1http:</c> and <c>h:</c>) are exit 3, <c>URL rejected: Port number
    /// was not a decimal number between 0 and 65535</c>, while <c>http://127.0.0.1:/</c> goes to port 80
    /// (BL-3175 Notes). Such a URL is left without a scheme, so the transfer URL parser reads it as one
    /// with no scheme and rejects it as curl does, before anything connects (ADR-0767).
    /// </para>
    /// </remarks>
    /// <param name="url">The URL as the command line gave it.</param>
    /// <param name="defaultScheme">The scheme for a URL without one, lowercase; <see langword="null" /> to guess.</param>
    /// <returns>The URL with a scheme.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="url" /> is <see langword="null" />.</exception>
    public static string AddScheme(string url, string? defaultScheme)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (HasScheme(url))
        {
            return url;
        }

        string scheme = defaultScheme ?? GuessScheme(url);

        return scheme != FileScheme && HostAndPort(url).EndsWith(':') ? url : scheme + "://" + url;
    }

    /// <summary>Tells whether <paramref name="url" /> starts with a scheme and <c>:/</c>.</summary>
    /// <remarks>
    /// On Windows a single ASCII letter and a colon is a drive prefix, not a scheme, as in
    /// curl 8.21.0's <c>Curl_is_absolute_url</c>: so <c>Z:/dir/file</c> is given the
    /// <c>--proto-default</c> (or guessed) scheme, and <c>file://Z:/dir/file</c> names the
    /// local file <c>Z:/dir/file</c> (upstream test1146).
    /// </remarks>
    /// <param name="url">The URL as the command line gave it.</param>
    /// <returns><see langword="true" /> when the URL names its own scheme.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="url" /> is <see langword="null" />.</exception>
    public static bool HasScheme(string url) => HasScheme(url, OperatingSystem.IsWindows());

    /// <summary>
    /// Tells whether <paramref name="url" /> starts with a scheme and <c>:/</c>, reading a single
    /// ASCII letter and a colon as a drive prefix rather than a scheme when
    /// <paramref name="runsOnWindows" /> is set.
    /// </summary>
    /// <param name="url">The URL as the command line gave it.</param>
    /// <param name="runsOnWindows"><see langword="true" /> to apply curl's Windows drive-prefix rule.</param>
    /// <returns><see langword="true" /> when the URL names its own scheme.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="url" /> is <see langword="null" />.</exception>
    public static bool HasScheme(string url, bool runsOnWindows)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (url.Length == 0 || !char.IsAsciiLetter(url[0]) || IsWindowsDrivePrefix(url, runsOnWindows))
        {
            return false;
        }

        int index = 1;
        while (index < url.Length && IsSchemeCharacter(url[index]))
        {
            index++;
        }

        return url.AsSpan(index).StartsWith(":/", StringComparison.Ordinal);
    }

    private static bool IsWindowsDrivePrefix(string url, bool runsOnWindows) =>
        runsOnWindows && url.Length > 1 && url[1] == ':';

    /// <summary>Guesses the scheme for <paramref name="url" />, which names none.</summary>
    /// <param name="url">The URL as the command line gave it, without a scheme.</param>
    /// <returns>The scheme its host prefix implies, or <see cref="DefaultScheme" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="url" /> is <see langword="null" />.</exception>
    public static string GuessScheme(string url)
    {
        ArgumentNullException.ThrowIfNull(url);

        ReadOnlySpan<char> host = HostAndPort(url);
        foreach (string scheme in HostPrefixSchemes)
        {
            if (host.Length > scheme.Length
                && host.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)
                && host[scheme.Length] == '.')
            {
                return scheme;
            }
        }

        return DefaultScheme;
    }

    /// <summary>The authority of <paramref name="url" />, read as one with no scheme, after its last <c>@</c>.</summary>
    private static ReadOnlySpan<char> HostAndPort(string url)
    {
        ReadOnlySpan<char> authority = url.AsSpan();
        int authorityEnd = authority.IndexOfAny('/', '?', '#');
        if (authorityEnd >= 0)
        {
            authority = authority[..authorityEnd];
        }

        return authority[(authority.LastIndexOf('@') + 1)..];
    }

    private static bool IsSchemeCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is '+' or '.' or '-';
}
