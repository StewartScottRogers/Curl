namespace Curl.Core.Hsts;

/// <summary>
/// Reads one line of curl's HSTS file as libcurl 8.21.0's <c>hsts_load</c> and <c>hsts_add</c>
/// do, measured on 2026-09-29 UTC (BL-620's notes, ADR-0177).
/// </summary>
/// <remarks>
/// A line is <c>host "expiry"</c>: blanks before the host are skipped, the host runs to the
/// first space and is at most 2048 characters, exactly one space follows, and the expiry is
/// in double quotes, at most 17 characters, a backslash keeping the character after it (a
/// quote included) inside. The line must end straight after the closing quote, or at a CR.
/// A comment (<c>#</c> after the blanks), an empty line, a tab for the space, a second space
/// or anything after the quote is not an entry.
/// </remarks>
public static class HstsFileLineParser
{
    /// <summary>The longest host curl reads from the file: libcurl's <c>MAX_HSTS_HOSTLEN</c>.</summary>
    public const int MaxHostLength = 2048;

    /// <summary>The longest expiry curl reads between the quotes: libcurl's <c>MAX_HSTS_DATELEN</c>.</summary>
    public const int MaxExpiryLength = 17;

    /// <summary>Reads <paramref name="line" />.</summary>
    /// <param name="line">One line of the file, without its LF.</param>
    /// <returns>The fields the line holds, or <see langword="null" /> when it holds no entry.</returns>
    public static HstsFileLine? Parse(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        ReadOnlySpan<char> text = line.AsSpan().TrimStart(" \t");
        int space = text.IndexOf(' ');
        return space is < 0 or > MaxHostLength || text[0] == '#' ? null : ParseExpiry(text[..space].ToString(), text[(space + 1)..]);
    }

    /// <summary>Reads the quoted expiry that follows the host's space, and checks the line ends after it.</summary>
    private static HstsFileLine? ParseExpiry(string host, ReadOnlySpan<char> quoted)
    {
        int closingQuote = quoted.StartsWith('"') ? FindClosingQuote(quoted) : -1;
        return closingQuote is > 0 and <= MaxExpiryLength + 1 && EndsLine(quoted[(closingQuote + 1)..])
            ? new HstsFileLine(host, quoted[1..closingQuote].ToString())
            : null;
    }

    /// <summary>
    /// Finds the quote that closes the expiry, as libcurl's <c>curlx_str_quotedword</c> does: a
    /// backslash keeps the character after it inside.
    /// </summary>
    /// <returns>Its index, or -1 when there is none.</returns>
    private static int FindClosingQuote(ReadOnlySpan<char> text)
    {
        int index = 1;
        while (index < text.Length && text[index] != '"')
        {
            index += IsEscape(text, index) ? 2 : 1;
        }

        return index < text.Length ? index : -1;
    }

    /// <summary>Whether what follows the closing quote is the end of the line: nothing, or a CR, which curl takes as one.</summary>
    private static bool EndsLine(ReadOnlySpan<char> rest) => rest.IsEmpty || rest[0] == '\r';

    private static bool IsEscape(ReadOnlySpan<char> text, int index) => text[index] == '\\' && index + 1 < text.Length;
}
