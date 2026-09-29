using System.Text;

namespace Curl.Authentication;

/// <summary>
/// Turns the headers of a Signature Version 4 request into curl 8.21.0's canonical header lines
/// and signed header list (<c>make_headers</c> in <c>http_aws_sigv4.c</c>).
/// </summary>
internal static class AwsSigV4Headers
{
    /// <summary>
    /// Finds the first custom header named <paramref name="name" />, case-insensitively, followed
    /// by <c>:</c> or <c>;</c>, as curl's <c>Curl_checkheaders</c>.
    /// </summary>
    /// <param name="headers">The custom headers.</param>
    /// <param name="name">The header name.</param>
    /// <returns>The whole header as given, or <see langword="null" />.</returns>
    internal static string? FindCustomHeader(IReadOnlyList<string> headers, string name) =>
        headers.FirstOrDefault(header =>
            header.Length > name.Length
            && header.StartsWith(name, StringComparison.OrdinalIgnoreCase)
            && header[name.Length] is ':' or ';');

    /// <summary>
    /// Converts a custom header to the <c>name:value</c> line curl signs, or <see langword="null" />
    /// for one curl leaves out: no <c>:</c> or <c>;</c>, a bare <c>Name:</c> (which removes a
    /// header), or a value of blanks only. <c>Name;</c> signs as <c>Name:</c>.
    /// </summary>
    /// <param name="header">The custom header as given.</param>
    /// <returns>The line to sign, or <see langword="null" />.</returns>
    internal static string? ToSignedLine(string header)
    {
        int colon = header.IndexOf(':', StringComparison.Ordinal);
        int separator = colon < 0 ? header.IndexOf(';', StringComparison.Ordinal) : colon;
        return separator < 0 || IsLeftOut(header, separator)
            ? null
            : string.Concat(header.AsSpan(0, separator), ":", header.AsSpan(separator + 1));
    }

    /// <summary>
    /// Lowercases a line's name (ASCII only) and trims its value: leading blanks dropped, each
    /// run of blanks inside made one space, trailing blanks dropped (<c>trim_headers</c>).
    /// </summary>
    /// <param name="line">A <c>name:value</c> line.</param>
    /// <returns>The trimmed line.</returns>
    internal static string Trim(string line)
    {
        int colon = line.IndexOf(':', StringComparison.Ordinal);
        StringBuilder trimmed = new(AsciiCase.ToLower(line[..colon]));
        trimmed.Append(':');
        int index = SkipBlanks(line, colon + 1);
        while (index < line.Length)
        {
            int afterBlanks = SkipBlanks(line, index);
            if (afterBlanks > index)
            {
                trimmed.Append(afterBlanks < line.Length ? " " : string.Empty);
                index = afterBlanks;
            }
            else
            {
                trimmed.Append(line[index]);
                index++;
            }
        }

        return trimmed.ToString();
    }

    /// <summary>
    /// Sorts trimmed lines by name, bytewise and stably, and merges lines with the same name
    /// into one, their values joined with <c>,</c> in order.
    /// </summary>
    /// <param name="lines">Trimmed <c>name:value</c> lines.</param>
    /// <returns>The sorted, merged lines.</returns>
    internal static List<string> SortAndMerge(IEnumerable<string> lines)
    {
        List<string> merged = [];
        foreach (string line in lines.OrderBy(NameOf, StringComparer.Ordinal))
        {
            if (merged.Count > 0 && NameOf(merged[^1]) == NameOf(line))
            {
                merged[^1] += "," + line[(line.IndexOf(':', StringComparison.Ordinal) + 1)..];
            }
            else
            {
                merged.Add(line);
            }
        }

        return merged;
    }

    /// <summary>Gets a line's name: everything before its first <c>:</c>.</summary>
    /// <param name="line">A <c>name:value</c> line.</param>
    /// <returns>The name.</returns>
    internal static string NameOf(string line) => line[..line.IndexOf(':', StringComparison.Ordinal)];

    /// <summary>
    /// Gets a value indicating whether curl leaves a custom header out of the signature: a bare
    /// <c>Name:</c>, or a value of blanks only.
    /// </summary>
    /// <param name="header">The custom header as given.</param>
    /// <param name="separator">The index of its <c>:</c> or <c>;</c>.</param>
    /// <returns><see langword="true" /> when the header is not signed.</returns>
    private static bool IsLeftOut(string header, int separator)
    {
        bool removesAHeader = header[separator] == ':' && separator == header.Length - 1;
        int valueStart = SkipBlanks(header, separator + 1);
        return removesAHeader || (valueStart == header.Length && valueStart != separator + 1);
    }

    /// <summary>Gets the index of the first character at or after <paramref name="index" /> that is not a space or tab.</summary>
    /// <param name="text">The text.</param>
    /// <param name="index">Where to start.</param>
    /// <returns>That index, or the text's length.</returns>
    internal static int SkipBlanks(string text, int index)
    {
        while (index < text.Length && text[index] is ' ' or '\t')
        {
            index++;
        }

        return index;
    }
}
