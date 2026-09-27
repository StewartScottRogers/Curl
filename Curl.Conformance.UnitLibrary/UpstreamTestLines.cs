namespace Curl.Conformance;

/// <summary>Splits test-file text into lines the way Perl reads them: each line keeps its line feed.</summary>
internal static class UpstreamTestLines
{
    /// <summary>Splits text after each line feed.</summary>
    /// <param name="text">The text, one character per byte.</param>
    /// <returns>Each line with its line feed; the last line has none when the text does not end in one.</returns>
    public static IEnumerable<string> SplitAfterLineFeeds(string text)
    {
        int start = 0;
        while (start < text.Length)
        {
            int lineFeed = text.IndexOf('\n', start);
            int end = lineFeed < 0 ? text.Length : lineFeed + 1;
            yield return text[start..end];
            start = end;
        }
    }
}
