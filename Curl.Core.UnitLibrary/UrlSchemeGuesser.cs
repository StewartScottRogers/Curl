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

        return HasScheme(url) ? url : GuessScheme(url) + "://" + url;
    }

    /// <summary>Tells whether <paramref name="url" /> starts with a scheme and <c>:/</c>.</summary>
    /// <param name="url">The URL as the command line gave it.</param>
    /// <returns><see langword="true" /> when the URL names its own scheme.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="url" /> is <see langword="null" />.</exception>
    public static bool HasScheme(string url)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (url.Length == 0 || !char.IsAsciiLetter(url[0]))
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

    /// <summary>Guesses the scheme for <paramref name="url" />, which names none.</summary>
    /// <param name="url">The URL as the command line gave it, without a scheme.</param>
    /// <returns>The scheme its host prefix implies, or <see cref="DefaultScheme" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="url" /> is <see langword="null" />.</exception>
    public static string GuessScheme(string url)
    {
        ArgumentNullException.ThrowIfNull(url);

        ReadOnlySpan<char> authority = url.AsSpan();
        int authorityEnd = authority.IndexOfAny('/', '?', '#');
        if (authorityEnd >= 0)
        {
            authority = authority[..authorityEnd];
        }

        ReadOnlySpan<char> host = authority[(authority.LastIndexOf('@') + 1)..];
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

    private static bool IsSchemeCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is '+' or '.' or '-';
}
