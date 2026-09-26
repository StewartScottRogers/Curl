using System.Text;

namespace Curl.Cli;

/// <summary>
/// Appends the query a command line asks for to a URL, the way curl 8.21.0 does for <c>-G</c> /
/// <c>--get</c> and <c>--url-query</c>. Pure string work: the URL is neither parsed, validated nor
/// normalised here; that is the URL layer's job (ADR-0004), so a query with a space in it is left
/// for the URL layer to reject, as curl 8.21.0 rejects it with exit 3.
/// </summary>
public static class QueryUrl
{
    /// <summary>
    /// Returns <paramref name="url"/> with the query <paramref name="options"/> asks for appended.
    /// With <see cref="CommandLineOptions.DataInQuery"/> and <see cref="CommandLineOptions.PostData"/>
    /// given, even empty, the query is the body read as UTF-8 and <see cref="CommandLineOptions.UrlQuery"/>
    /// is ignored; otherwise it is <see cref="CommandLineOptions.UrlQuery"/>.
    /// </summary>
    /// <remarks>
    /// Measured with the local curl 8.21.0 on 2026-09-26 against a loopback server, reading the
    /// request line. The query goes after the URL's own query and a <c>&amp;</c> when that query is
    /// not empty, even when the appended query is empty (<c>/p?x</c> with <c>-G -d ''</c> requests
    /// <c>/p?x&amp;</c>); otherwise after a single <c>?</c> (<c>/p?</c> with <c>-G -d a</c> requests
    /// <c>/p?a</c>), and not at all when it is empty (<c>/p</c> with <c>-G -d ''</c> requests
    /// <c>/p</c>). A fragment stays after the query. <c>--url-query a -G -d c</c> requests
    /// <c>/p?c</c>: curl 8.21.0 drops the <c>--url-query</c> values when <c>-G</c> has data to move,
    /// and uses them when it has none (<c>-G --url-query b</c> requests <c>/p?b</c>).
    /// </remarks>
    /// <param name="url">One URL from the command line.</param>
    /// <param name="options">The parsed command line.</param>
    /// <returns>The URL with the query appended, or <paramref name="url"/> when there is none to append.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="url"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    public static string Append(string url, CommandLineOptions options)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(options);

        string? query = options.DataInQuery && options.PostData is { } body
            ? Encoding.UTF8.GetString(body.Span)
            : options.UrlQuery;
        return query is null ? url : AppendQuery(url, query);
    }

    private static string AppendQuery(string url, string query)
    {
        (string beforeFragment, string fragment) = SplitBefore(url, '#');
        (string withoutQuery, string ownQuery) = SplitBefore(beforeFragment, '?');

        if (ownQuery.Length > 1)
        {
            return $"{withoutQuery}{ownQuery}&{query}{fragment}";
        }

        return query.Length == 0 ? url : $"{withoutQuery}?{query}{fragment}";
    }

    /// <summary>
    /// Splits <paramref name="text"/> before the first <paramref name="separator"/>: the second part
    /// starts with the separator, and is empty when there is none.
    /// </summary>
    private static (string Before, string FromSeparator) SplitBefore(string text, char separator)
    {
        int index = text.IndexOf(separator, StringComparison.Ordinal);
        return index < 0 ? (text, string.Empty) : (text[..index], text[index..]);
    }
}
