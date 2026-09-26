using System.Text;

namespace Curl.Cli;

/// <summary>
/// Splits a message into the lines curl 8.21.0's <c>errorf</c> and <c>warnf</c> print when standard
/// error is not a terminal: each line starts with the prefix, and the text is cut so a line holds at
/// most 79 bytes, at the last space or tab that fits (kept at the end of the line), or at the width
/// itself when there is none. Ported from <c>voutf</c> in curl's <c>src/tool_msgs.c</c>; widths are
/// counted in UTF-8 bytes, as curl counts them.
/// </summary>
internal static class WrappedMessage
{
    /// <summary>The width curl wraps to when it cannot ask a terminal for its width.</summary>
    private const int TerminalColumns = 79;

    /// <summary>Splits <paramref name="message"/> into prefixed lines, without line terminators.</summary>
    /// <param name="prefix">The prefix of every line: <c>curl: </c> for an error, <c>Warning: </c> for a warning.</param>
    /// <param name="message">The message, not empty.</param>
    /// <returns>One or more lines.</returns>
    internal static IReadOnlyList<string> Lines(string prefix, string message)
    {
        int width = TerminalColumns - prefix.Length;
        List<string> lines = [];
        ReadOnlySpan<byte> rest = Encoding.UTF8.GetBytes(message);
        while (rest.Length > width)
        {
            int cut = LastBlankAtOrBefore(rest, width - 1);
            lines.Add(prefix + Encoding.UTF8.GetString(rest[..(cut + 1)]));
            rest = rest[(cut + 1)..];
        }

        lines.Add(prefix + Encoding.UTF8.GetString(rest));
        return lines;
    }

    private static int LastBlankAtOrBefore(ReadOnlySpan<byte> text, int start)
    {
        for (int cut = start; cut > 0; cut--)
        {
            if (text[cut] is (byte)' ' or (byte)'\t')
            {
                return cut;
            }
        }

        return start;
    }
}
