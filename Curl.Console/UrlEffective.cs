using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Normalises a transfer's URL into the one curl 8.21.0 prints for <c>%{url_effective}</c>:
/// the scheme in lower case, the authority as typed, and the path as curl sends it.
/// </summary>
/// <remarks>
/// Measured on Windows with curl 8.21.0 on 2026-09-27 (BL-371 and BL-444 Notes):
/// <c>HTTP://LocalHost:1</c> prints <c>http://LocalHost:1/</c>, <c>http://localhost:1/a/../b</c>
/// prints <c>http://localhost:1/b</c> (<c>/a/../b</c> under <c>--path-as-is</c>),
/// <c>http://localhost:1?q=1</c> prints <c>http://localhost:1/?q=1</c>, the query and fragment
/// keep their dot segments, <c>file://localhost/C:/x</c> prints <c>file://C:/x</c>, and a URL
/// curl rejects, such as <c>http://local host</c>, prints as it is.
/// </remarks>
internal static class UrlEffective
{
    private const string FileScheme = "file";

    private static readonly char[] AuthorityEnds = ['/', '\\', '?', '#'];

    private static readonly char[] Slashes = ['/', '\\'];

    /// <summary>
    /// Rebuilds <paramref name="url" /> from its lower-case scheme, its authority as typed, and
    /// its parsed path, query and fragment.
    /// </summary>
    /// <param name="url">The URL transferred, with its query.</param>
    /// <param name="pathAsIs">Whether <c>--path-as-is</c> keeps the path's dot segments.</param>
    /// <returns>The URL <c>%{url_effective}</c> prints; <paramref name="url" /> when curl rejects it.</returns>
    internal static string Normalize(string url, bool pathAsIs)
    {
        if (!CurlUrl.TryParse(url, pathAsIs, out CurlUrl? parsed))
        {
            return url;
        }

        string authority = parsed.Scheme == FileScheme ? string.Empty : Authority(url, parsed.Scheme);
        string query = parsed.Query is null ? string.Empty : "?" + parsed.Query;
        string fragment = parsed.Fragment is null ? string.Empty : "#" + parsed.Fragment;

        return parsed.Scheme + "://" + authority + parsed.AbsolutePath + query + fragment;
    }

    /// <summary>
    /// Returns the authority of <paramref name="url" /> as typed: the text after its scheme,
    /// when it was typed, and the slashes after that, up to the path, query or fragment.
    /// </summary>
    private static string Authority(string url, string scheme)
    {
        int start = url.StartsWith(scheme + ":", StringComparison.OrdinalIgnoreCase) ? scheme.Length + 1 : 0;
        string rest = url[start..].TrimStart(Slashes);
        int end = rest.IndexOfAny(AuthorityEnds);

        return end < 0 ? rest : rest[..end];
    }
}
