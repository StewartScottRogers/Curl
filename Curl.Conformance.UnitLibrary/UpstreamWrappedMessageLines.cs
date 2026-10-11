using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Joins the lines curl 8.21.0's <c>warnf</c> and <c>notef</c> (<c>voutf</c> in <c>src/tool_msgs.c</c>)
/// wrap a <c>Warning: </c> or <c>Note: </c> message into, so a message naming the harness's long
/// absolute <c>%LOGDIR</c> compares equal to the one line <c>runtests.pl</c>'s short <c>log</c> keeps
/// it on (ADR-0475).
/// </summary>
/// <remarks>
/// <c>voutf</c> cuts a message longer than <c>COLUMNS</c> (79 under <c>runtests.pl</c>) after its
/// last blank, or at the full width when there is none, and starts the rest on a new line with the
/// prefix again. A line is joined to the next when both start with the same prefix and the first
/// ends in a blank or is at least <see cref="DefaultColumns"/> characters long; the next line's
/// prefix is dropped. Line ends, LF or CRLF, are kept as the joined message's last line has them.
/// </remarks>
internal static class UpstreamWrappedMessageLines
{
    /// <summary>The terminal width <c>runtests.pl</c> sets, <c>COLUMNS=79</c>.</summary>
    public const int DefaultColumns = 79;

    private static readonly string[] Prefixes = ["Warning: ", "Note: "];

    /// <summary>Joins every wrapped <c>Warning: </c> or <c>Note: </c> message in <paramref name="body"/> back onto one line.</summary>
    /// <param name="body">Standard error bytes, read as Latin-1.</param>
    /// <returns>The bytes with each wrapped message on one line.</returns>
    public static byte[] Unwrap(byte[] body)
    {
        StringBuilder result = new();
        string? pending = null;
        foreach (string line in UpstreamTestLines.SplitAfterLineFeeds(Encoding.Latin1.GetString(body)))
        {
            string joined = pending is not null && ContinuationPrefix(pending, line) is { } prefix
                ? WithoutLineEnd(pending) + line[prefix.Length..]
                : Flushed(result, pending) + line;
            pending = joined;
        }

        Flushed(result, pending);
        return Encoding.Latin1.GetBytes(result.ToString());
    }

    // Appends the line held back, if any, and returns the empty start of the next one.
    private static string Flushed(StringBuilder result, string? pending)
    {
        result.Append(pending);
        return "";
    }

    // The prefix the next line repeats when line was cut by voutf, else null.
    private static string? ContinuationPrefix(string line, string next) =>
        Prefixes.FirstOrDefault(prefix => line.StartsWith(prefix, StringComparison.Ordinal) && next.StartsWith(prefix, StringComparison.Ordinal))
            is { } prefix && IsCut(WithoutLineEnd(line))
            ? prefix
            : null;

    private static bool IsCut(string content) =>
        content.EndsWith(' ') || content.EndsWith('\t') || content.Length >= DefaultColumns;

    private static string WithoutLineEnd(string line) => line.TrimEnd('\n').TrimEnd('\r');
}
