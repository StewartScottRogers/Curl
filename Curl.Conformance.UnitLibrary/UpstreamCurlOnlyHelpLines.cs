using System.Text;

namespace Curl.Conformance;

/// <summary>
/// The lines Curl's <c>--help</c> prints that curl's does not, so an AI agent finds <c>--ai-help</c>
/// (BL-2985, ADR-0224): the usage page's last line and <c>--ai-help</c>'s row in <c>--help all</c> and
/// <c>--help curl</c>. <see cref="UpstreamCaseVerification"/> removes them, and only them, from a run's
/// stdout before comparing it with <c>&lt;verify&gt;&lt;stdout&gt;</c>, so cases 1461 to 1464 still
/// compare curl's own lines byte for byte. The text is a copy of <c>Curl.Cli</c>'s, which this library
/// does not reference.
/// </summary>
internal static class UpstreamCurlOnlyHelpLines
{
    private const string UsageLine = "Use \"--ai-help\" for Markdown help written for AI agents (Curl only)";
    private const string RowStart = "     --ai-help <category>";
    private const string RowEnd = "  Markdown help for AI agents (Curl only)";

    /// <summary>Returns <paramref name="output"/> without the Curl-only help lines, line ends kept on the rest.</summary>
    /// <param name="output">A run's stdout.</param>
    /// <returns>The output with every Curl-only help line, and its line end, removed.</returns>
    public static byte[] Remove(byte[] output)
    {
        IEnumerable<string> kept = UpstreamTestLines.SplitAfterLineFeeds(Encoding.Latin1.GetString(output))
            .Where(line => !IsCurlOnly(line.TrimEnd('\r', '\n')));
        return Encoding.Latin1.GetBytes(string.Concat(kept));
    }

    /// <summary>Whether <paramref name="line"/>, without its line end, is one of the Curl-only help lines.</summary>
    /// <param name="line">One line, without its line end.</param>
    /// <returns><see langword="true"/> for the usage line or <c>--ai-help</c>'s row at any column width.</returns>
    internal static bool IsCurlOnly(string line) =>
        line == UsageLine || line.StartsWith(RowStart, StringComparison.Ordinal) && line.EndsWith(RowEnd, StringComparison.Ordinal);
}
