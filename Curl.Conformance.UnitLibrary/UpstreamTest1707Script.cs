using System.Text;
using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// Interprets upstream's <c>tests/test1707.pl</c> (curl 8.22.0), which cases 1707, 1708 and 1710 run as
/// <c>%SRCDIR/test1707.pl %CURL &lt;option&gt; ../docs/cmdline-opts/curl.txt</c>, without running Perl
/// (ADR-0714, BL-2866): it checks that <c>curl -h &lt;option&gt;</c> prints the option's section of the manual.
/// </summary>
/// <remarks>
/// <para>The arguments are <c>%CURL</c>, the option and the manual text's path, relative to upstream's
/// <c>tests</c> folder, which is <c>tests</c> under <paramref name="sourceTreeFolder"/>. <c>%CURL</c> is the
/// runner's in-process curl. The script runs <c>curl -h &lt;option&gt;</c>, then finds the option's other name
/// in <c>curl -h all</c> (the long name after a short option's <c>-F, </c>; the short name before a long
/// option's line), and takes the manual's section from the line starting with four spaces and
/// <c>-F, --form</c> (or the long name alone) up to the next line starting with four spaces and a dash.</para>
/// <para>When the two differ in line count it prints both counts; otherwise it compares the lines with their
/// carriage returns and line feeds removed and prints each line that differs, and, for lines of one length,
/// each differing column the way the script's <c>print</c> does: its format text with the column and the
/// character's code appended, unformatted. It exits with the number of differences.</para>
/// </remarks>
/// <param name="sourceTreeFolder">The folder standing in for upstream's source tree, holding <c>docs</c>.</param>
public sealed class UpstreamTest1707Script(string sourceTreeFolder)
{
    /// <summary>The file name of the upstream script this class interprets.</summary>
    public const string ScriptName = "test1707.pl";

    private const string ManualHeadingIndent = "    ";

    /// <summary>Runs the script with the command's words after it, calling <paramref name="runCurl"/> for <c>%CURL</c>.</summary>
    /// <param name="invocation">The script's arguments (<c>%CURL</c>, the option, the manual's path) and the case's streams.</param>
    /// <param name="runCurl">The in-process curl.</param>
    /// <returns>The number of differences: 0 when the help text matches the manual.</returns>
    public async Task<int> RunAsync(UpstreamCurlInvocation invocation, Func<UpstreamCurlInvocation, Task<int>> runCurl)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(runCurl);
        string option = invocation.Arguments[1];
        string manualPath = invocation.Arguments[2];
        List<string> helpLines = SplitLines(await RunCurlAsync(invocation, runCurl, option));
        string helpAll = await RunCurlAsync(invocation, runCurl, "all");
        List<string> manualLines = ManualSection(
            SplitLines(Encoding.Latin1.GetString(await File.ReadAllBytesAsync(Path.GetFullPath(Path.Combine(sourceTreeFolder, "tests", manualPath))))),
            FullOptionName(option, helpAll));
        StringBuilder report = new();
        int differences = Compare(helpLines, manualLines, option, manualPath, report);
        await invocation.StandardOutput.WriteAsync(Encoding.Latin1.GetBytes(report.ToString()));
        return differences;
    }

    /// <summary>The option's names as the manual's heading gives them: <c>-F, --form</c>, or the long name alone.</summary>
    /// <param name="option">The option the script was given, short (<c>-F</c>) or long (<c>--form</c>).</param>
    /// <param name="helpAll">What <c>curl -h all</c> printed.</param>
    /// <returns>The heading's names.</returns>
    public static string FullOptionName(string option, string helpAll)
    {
        ArgumentNullException.ThrowIfNull(option);
        string escaped = Regex.Escape(option);
        if (!option.StartsWith("--", StringComparison.Ordinal))
        {
            Match longName = Regex.Match(helpAll, $"^ +{escaped}, ([^ \n]*)", RegexOptions.Multiline | RegexOptions.CultureInvariant);
            return $"{option}, {longName.Groups[1].Value}";
        }

        string? line = SplitLines(helpAll).Find(line => line.Contains($" {option} ", StringComparison.Ordinal));
        Match shortName = Regex.Match(line ?? "", $"^ *(-(.)), {escaped}", RegexOptions.CultureInvariant);
        return shortName.Success ? $"{shortName.Groups[1].Value}, {option}" : option;
    }

    /// <summary>The manual's lines from the heading starting with <paramref name="fullOptionName"/> up to the next heading.</summary>
    /// <param name="manualLines">The manual's lines.</param>
    /// <param name="fullOptionName">The heading's names, from <see cref="FullOptionName"/>.</param>
    /// <returns>The option's section, heading included; empty when no heading matches.</returns>
    public static List<string> ManualSection(IReadOnlyList<string> manualLines, string fullOptionName)
    {
        ArgumentNullException.ThrowIfNull(manualLines);
        string heading = ManualHeadingIndent + fullOptionName;
        List<string> section = [];
        foreach (string line in manualLines)
        {
            if (section.Count > 0 && line.StartsWith(ManualHeadingIndent + "-", StringComparison.Ordinal))
            {
                break;
            }

            if (section.Count > 0 || line.StartsWith(heading, StringComparison.Ordinal))
            {
                section.Add(line);
            }
        }

        return section;
    }

    /// <summary>Compares the help text's lines with the manual's, writing the script's report of each difference.</summary>
    /// <param name="helpLines">What <c>curl -h &lt;option&gt;</c> printed, by line.</param>
    /// <param name="manualLines">The option's section of the manual.</param>
    /// <param name="option">The option, for the line-count report.</param>
    /// <param name="manualPath">The manual's path as given, for the line-count report.</param>
    /// <param name="report">Where the report is written.</param>
    /// <returns>The number of differences.</returns>
    public static int Compare(IReadOnlyList<string> helpLines, IReadOnlyList<string> manualLines, string option, string manualPath, StringBuilder report)
    {
        ArgumentNullException.ThrowIfNull(helpLines);
        ArgumentNullException.ThrowIfNull(manualLines);
        ArgumentNullException.ThrowIfNull(report);
        if (helpLines.Count != manualLines.Count)
        {
            report.Append($"curl -h {option} is {helpLines.Count} lines, {manualPath} says {manualLines.Count} lines\n");
            return 1;
        }

        int differences = 0;
        for (int index = 0; index < helpLines.Count; index++)
        {
            differences += CompareLine(index, TrimLineEnd(helpLines[index]), TrimLineEnd(manualLines[index]), report);
        }

        return differences;
    }

    private static int CompareLine(int index, string helpLine, string manualLine, StringBuilder report)
    {
        if (helpLine == manualLine)
        {
            return 0;
        }

        report.Append($"Line {index}\n-h   : {helpLine} ({helpLine.Length} bytes)\nfile : {manualLine} ({manualLine.Length} bytes)\n");
        if (helpLine.Length == manualLine.Length)
        {
            ReportColumns(helpLine, manualLine, report);
        }

        return 1;
    }

    // The script prints these with print, not printf, so its format text comes out as written, the values after it.
    private static void ReportColumns(string helpLine, string manualLine, StringBuilder report)
    {
        for (int column = 0; column < helpLine.Length; column++)
        {
            if (helpLine[column] != manualLine[column])
            {
                report.Append($"-h   col %d: %02x\n{column}{(int)helpLine[column]}");
                report.Append($"file col %d: %02x\n{column}{(int)manualLine[column]}");
            }
        }
    }

    private static string TrimLineEnd(string line) => line.Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal);

    // Perl's <FILE> lines: each keeps its line feed, and text after the last one is a line of its own.
    private static List<string> SplitLines(string text)
    {
        List<string> lines = [];
        int start = 0;
        for (int end = text.IndexOf('\n', start); end >= 0; end = text.IndexOf('\n', start))
        {
            lines.Add(text[start..(end + 1)]);
            start = end + 1;
        }

        if (start < text.Length)
        {
            lines.Add(text[start..]);
        }

        return lines;
    }

    private static async Task<string> RunCurlAsync(UpstreamCurlInvocation invocation, Func<UpstreamCurlInvocation, Task<int>> runCurl, string helpTopic)
    {
        using MemoryStream output = new();
        await runCurl(new UpstreamCurlInvocation(["-h", helpTopic], output, invocation.StandardError, invocation.StandardInput, invocation.Connector, invocation.DatagramConnector));
        return Encoding.Latin1.GetString(output.ToArray());
    }
}
