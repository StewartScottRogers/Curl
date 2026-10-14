using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Shows two outputs line by line where they differ: up to two lines of context before and
/// after, each differing expected line marked <c>-</c> and each differing produced line marked
/// <c>+</c>, every line numbered and quoted with its control characters escaped.
/// </summary>
internal static class UpstreamLineDifference
{
    private const int ContextLines = 2;

    private const int MaximumChangedLines = 5;

    /// <summary>Shows the lines that differ between what was expected and what was produced.</summary>
    /// <param name="expected">The expected bytes.</param>
    /// <param name="actual">The bytes produced.</param>
    /// <returns>One line per shown line, each ending in a line feed.</returns>
    public static string Show(byte[] expected, byte[] actual)
    {
        List<string> expectedLines = Lines(expected);
        List<string> actualLines = Lines(actual);
        int prefix = CommonPrefix(expectedLines, actualLines);
        int suffix = CommonSuffix(expectedLines, actualLines, prefix);
        int contextStart = Math.Max(0, prefix - ContextLines);
        StringBuilder shown = new();
        AppendLines(shown, ' ', expectedLines, contextStart, prefix - contextStart);
        AppendLines(shown, '-', expectedLines, prefix, expectedLines.Count - suffix - prefix);
        AppendLines(shown, '+', actualLines, prefix, actualLines.Count - suffix - prefix);
        AppendLines(shown, ' ', expectedLines, expectedLines.Count - suffix, Math.Min(suffix, ContextLines));
        return shown.ToString();
    }

    // Each line with its line feed; a last line without one is kept as it is.
    private static List<string> Lines(byte[] bytes)
    {
        List<string> lines = [];
        int start = 0;
        while (start < bytes.Length)
        {
            int lineFeed = bytes.AsSpan(start).IndexOf((byte)'\n');
            int end = lineFeed < 0 ? bytes.Length : start + lineFeed + 1;
            lines.Add(Encoding.Latin1.GetString(bytes, start, end - start));
            start = end;
        }

        return lines;
    }

    private static int CommonPrefix(List<string> expected, List<string> actual)
    {
        int count = 0;
        while (count < expected.Count && count < actual.Count && expected[count] == actual[count])
        {
            count++;
        }

        return count;
    }

    // Matching lines at the end, never reaching back into the common prefix.
    private static int CommonSuffix(List<string> expected, List<string> actual, int prefix)
    {
        int limit = Math.Min(expected.Count, actual.Count) - prefix;
        int count = 0;
        while (count < limit && expected[expected.Count - 1 - count] == actual[actual.Count - 1 - count])
        {
            count++;
        }

        return count;
    }

    private static void AppendLines(StringBuilder shown, char marker, List<string> lines, int start, int count)
    {
        int showing = marker == ' ' ? count : Math.Min(count, MaximumChangedLines);
        for (int index = start; index < start + showing; index++)
        {
            shown.Append(marker).Append(' ').Append(index + 1).Append(": ").Append(UpstreamFirstDifference.Show(lines[index])).Append('\n');
        }

        if (showing < count)
        {
            shown.Append(marker).Append(" ... ").Append(count - showing).Append(" more\n");
        }
    }
}
