using System.Text;

namespace Curl.Cli;

/// <summary>
/// Appends the query a command line asks for to a URL, the way curl 8.21.0 does for <c>-G</c> /
/// <c>--get</c> and <c>--url-query</c>. Pure string work: the appended query's percent escapes are upper-cased, as
/// curl's URL API does, but the URL is neither parsed, validated nor otherwise normalised here; that is the URL layer's job (ADR-0004), so a query with a space in it is left
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

        string? query = QueryToAppend(options);
        return query is null ? url : AppendQuery(url, UpperCasePercentEscapes(query));
    }

    /// <summary>
    /// Returns whether <c>--path-as-is</c> still keeps the URL's dot segments once <see cref="Append" /> has run:
    /// only when <paramref name="options"/> asks for <c>--path-as-is</c> and there is no query to append.
    /// </summary>
    /// <remarks>
    /// curl 8.22.0 appends a <c>-G</c> / <c>--url-query</c> query by parsing the URL with its URL API, without
    /// <c>CURLU_PATH_AS_IS</c>, and reading it back, so the dot segments are gone before the transfer sees
    /// <c>--path-as-is</c>. Measured on Windows on 2026-10-26 (BL-3584 Notes): <c>-G -d a=b --path-as-is
    /// http://h/x/../y</c> and <c>--url-query a=b</c> both request <c>/y?a=b</c>, <c>-G -d ''</c> requests
    /// <c>/y</c>, and without a query <c>/x/../y</c> is requested as it is.
    /// </remarks>
    /// <param name="options">The parsed command line.</param>
    /// <returns><see langword="true"/> when the URL's path is sent as it is.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public static bool KeepsPathAsIs(CommandLineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.PathAsIs && QueryToAppend(options) is null;
    }

    private static string? QueryToAppend(CommandLineOptions options) =>
        options.DataInQuery && options.PostData is { } body
            ? Encoding.UTF8.GetString(body.Span)
            : options.UrlQuery;

    /// <summary>
    /// Upper-cases the hex digits of every complete percent escape in <paramref name="query"/>, as curl
    /// 8.21.0's URL API does to a query appended with <c>CURLU_APPENDQUERY</c>: upstream test1221's
    /// <c>--url-query "+%3d%3d"</c> requests <c>%3D%3D</c>. A <c>%</c> not followed by two hex digits
    /// is left as it is.
    /// </summary>
    private static string UpperCasePercentEscapes(string query)
    {
        StringBuilder normalised = new(query);
        for (int index = 0; index + 2 < normalised.Length; index++)
        {
            if (normalised[index] == '%' && char.IsAsciiHexDigit(normalised[index + 1]) && char.IsAsciiHexDigit(normalised[index + 2]))
            {
                normalised[index + 1] = char.ToUpperInvariant(normalised[index + 1]);
                normalised[index + 2] = char.ToUpperInvariant(normalised[index + 2]);
                index += 2;
            }
        }

        return normalised.ToString();
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
