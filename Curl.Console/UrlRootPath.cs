namespace Curl.Console;

/// <summary>
/// Gives a URL whose path is empty the root path <c>/</c>, as curl 8.21.0 does to the URL
/// <c>%{url_effective}</c> prints.
/// </summary>
/// <remarks>
/// Measured on Windows with curl 8.21.0 on 2026-09-27 (BL-371 Notes): <c>localhost:1</c> and
/// <c>http://localhost:1</c> print <c>http://localhost:1/</c>, <c>http://localhost:1?q=1</c>
/// prints <c>http://localhost:1/?q=1</c>, <c>http://localhost:1#f</c> prints
/// <c>http://localhost:1/#f</c>, and <c>http://localhost:1/a</c> prints as it is.
/// </remarks>
internal static class UrlRootPath
{
    private const string SchemeSeparator = "://";

    private static readonly char[] AuthorityEnds = ['/', '?', '#'];

    /// <summary>
    /// Puts <c>/</c> right after the authority of <paramref name="url" /> when no path follows it.
    /// </summary>
    /// <param name="url">The URL, with its scheme.</param>
    /// <returns>
    /// <paramref name="url" /> with <c>/</c> inserted before its query or fragment or at its end
    /// when its path is empty; otherwise, and when it has no <c>://</c>, <paramref name="url" />.
    /// </returns>
    internal static string AddToEmptyPath(string url)
    {
        int schemeSeparator = url.IndexOf(SchemeSeparator, StringComparison.Ordinal);
        if (schemeSeparator < 0)
        {
            return url;
        }

        int authorityEnd = url.IndexOfAny(AuthorityEnds, schemeSeparator + SchemeSeparator.Length);
        if (authorityEnd < 0)
        {
            return url + "/";
        }

        return url[authorityEnd] == '/' ? url : url.Insert(authorityEnd, "/");
    }
}
