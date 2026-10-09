using System.Globalization;
using System.Text;

namespace Curl.Cli;

/// <summary>
/// Splits a message into the lines curl 8.21.0's <c>errorf</c> and <c>warnf</c> print when standard
/// error is not a terminal: each line starts with the prefix, and the text is cut so a line holds at
/// most the terminal width (79 bytes unless <c>COLUMNS</c> says otherwise), at the last space or tab that fits (kept at the end of the line), or at the width
/// itself when there is none. Ported from <c>voutf</c> in curl's <c>src/tool_msgs.c</c>; widths are
/// counted in UTF-8 bytes, as curl counts them.
/// </summary>
public static class WrappedMessage
{
    /// <summary>The width curl wraps to when <c>COLUMNS</c> does not give one.</summary>
    public const int DefaultTerminalColumns = 79;

    /// <summary>
    /// Splits <paramref name="message"/> into prefixed lines, without line terminators, at the width
    /// this process's <c>COLUMNS</c> environment variable gives (see <see cref="TerminalColumns"/>).
    /// Measured with curl 8.21.0 (Windows, 2026-10-08): <c>-K</c> naming a missing 73-byte path wraps
    /// its <c>cannot read config from</c> line at 79 columns with no <c>COLUMNS</c>, at 40 with
    /// <c>COLUMNS=40</c>, and not at all with <c>COLUMNS=200</c>.
    /// </summary>
    /// <param name="prefix">The prefix of every line: <c>curl: </c> for an error, <c>Warning: </c> for a warning.</param>
    /// <param name="message">The message, not empty.</param>
    /// <returns>One or more lines.</returns>
    public static IReadOnlyList<string> Lines(string prefix, string message) =>
        Lines(prefix, message, TerminalColumns(Environment.GetEnvironmentVariable("COLUMNS")));

    /// <summary>
    /// The width curl 8.21.0's <c>get_terminal_columns</c> takes from a <c>COLUMNS</c> value: the value
    /// when it reads as a whole number from 21 to 9999, otherwise <see cref="DefaultTerminalColumns"/>.
    /// </summary>
    /// <param name="columnsVariable">The <c>COLUMNS</c> value, or <see langword="null"/> when it is not set.</param>
    /// <returns>The terminal width.</returns>
    public static int TerminalColumns(string? columnsVariable) =>
        int.TryParse(
            columnsVariable,
            NumberStyles.AllowLeadingWhite | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out int columns)
        && columns is > 20 and < 10000
            ? columns
            : DefaultTerminalColumns;

    /// <summary>Splits <paramref name="message"/> into prefixed lines at <paramref name="columns"/>, without line terminators.</summary>
    /// <param name="prefix">The prefix of every line: <c>curl: </c> for an error, <c>Warning: </c> for a warning.</param>
    /// <param name="message">The message, not empty.</param>
    /// <param name="columns">The terminal width.</param>
    /// <returns>One or more lines.</returns>
    public static IReadOnlyList<string> Lines(string prefix, string message, int columns)
    {
        int width = columns - prefix.Length;
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
