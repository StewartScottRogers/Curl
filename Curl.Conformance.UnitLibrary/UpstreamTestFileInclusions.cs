using System.Text;

namespace Curl.Conformance;

/// <summary>
/// The <c>%includetext &lt;path&gt;%</c> and <c>%include &lt;path&gt;%</c> instructions of a test
/// file line, carried out as <c>subtextfile</c> and the end of <c>subbase64</c> in upstream's
/// <c>testutil.pm</c> carry them out at <c>curl-8_21_0</c>.
/// </summary>
/// <remarks>
/// Each matches <c>%include ([^%]*)%[\n\r]+</c> case-sensitively, every match left to right in
/// one pass, so bytes an instruction brings in are not searched again, and the line break after
/// the closing <c>%</c> is consumed with it; an instruction with no line break after it is left as
/// written. A file that cannot be read is included as nothing, as Perl's failed <c>open</c> reads
/// nothing.
/// </remarks>
internal static class UpstreamTestFileInclusions
{
    private const string TextMarker = "%includetext ";

    private const string RawMarker = "%include ";

    /// <summary>Replaces every <c>%includetext</c> with the named file, its CRLF line endings turned into LF.</summary>
    /// <param name="line">The line, one character per byte.</param>
    /// <param name="readFile">Reads a file by path, or returns <see langword="null"/> when it cannot.</param>
    /// <param name="replaced">Whether any instruction was replaced, so the line's variables are substituted again.</param>
    /// <returns>The line with every instruction replaced.</returns>
    public static string ReplaceTextIncludes(string line, Func<string, byte[]?> readFile, out bool replaced) =>
        ReplaceEach(line, TextMarker, path => Read(readFile, path).Replace("\r\n", "\n", StringComparison.Ordinal), out replaced);

    /// <summary>Replaces every <c>%include</c> with the named file's bytes, unchanged.</summary>
    /// <param name="line">The line, one character per byte.</param>
    /// <param name="readFile">Reads a file by path, or returns <see langword="null"/> when it cannot.</param>
    /// <returns>The line with every instruction replaced.</returns>
    public static string ReplaceRawIncludes(string line, Func<string, byte[]?> readFile) =>
        ReplaceEach(line, RawMarker, path => Read(readFile, path), out _);

    /// <summary>
    /// Adds <c>%includetext</c> and <c>%include</c> to the list, once each, when the line holds
    /// one, for an expansion given no way to read files.
    /// </summary>
    /// <param name="line">The expanded line.</param>
    /// <param name="unsupported">The names found so far.</param>
    public static void AddUnread(string line, List<string> unsupported)
    {
        foreach (string marker in new[] { RawMarker, TextMarker })
        {
            string name = marker.TrimEnd();
            if (line.Contains(marker, StringComparison.Ordinal) && !unsupported.Contains(name))
            {
                unsupported.Add(name);
            }
        }
    }

    // The path is carried one character per byte; variables were inserted as UTF-8.
    private static string Read(Func<string, byte[]?> readFile, string path) =>
        Encoding.Latin1.GetString(readFile(Encoding.UTF8.GetString(Encoding.Latin1.GetBytes(path))) ?? []);

    private static string ReplaceEach(string text, string marker, Func<string, string> contents, out bool replaced)
    {
        replaced = false;
        int start = text.IndexOf(marker, StringComparison.Ordinal);
        while (start >= 0)
        {
            int close = text.IndexOf('%', start + marker.Length);
            int end = close < 0 ? -1 : AfterLineBreaks(text, close + 1);
            if (end < 0)
            {
                return text;
            }

            int searchFrom = start + 1;
            if (end > close + 1)
            {
                string included = contents(text[(start + marker.Length)..close]);
                text = string.Concat(text.AsSpan(0, start), included, text.AsSpan(end));
                searchFrom = start + included.Length;
                replaced = true;
            }

            start = text.IndexOf(marker, searchFrom, StringComparison.Ordinal);
        }

        return text;
    }

    private static int AfterLineBreaks(string text, int index)
    {
        while (index < text.Length && text[index] is '\r' or '\n')
        {
            index++;
        }

        return index;
    }
}
