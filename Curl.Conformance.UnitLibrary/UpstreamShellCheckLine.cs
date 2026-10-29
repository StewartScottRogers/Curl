using System.Text;
using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// Interprets the shell check lines case 1182 runs (BL-3412, ADR-0882): a precheck made only of <c>mkdir DIR</c>,
/// <c>cp FILE DEST</c> and <c>echo 'TEXT' &gt; FILE</c> commands joined by <c>;</c>, and a postcheck
/// <c>grep -q "TEXT" FILE</c>, without a shell.
/// </summary>
/// <remarks>
/// Every path a command creates, writes or greps must lie inside the case's log directory: one outside it ends the line
/// with exit code 1 and a message, so a precheck skips the case with it and a postcheck fails the case. <c>cp</c> reads
/// its source from anywhere; a source <c>…/data/testN</c> that is not there is the vendored <c>testN.curltest</c> beside
/// that <c>data</c> folder, where the case files of upstream's <c>tests/data</c> are kept. <c>grep -q</c> is interpreted
/// only for a pattern with no regular-expression character, which it finds as plain text: exit code 0 when the file holds
/// it, 1 when not, and 2 when the file is not there.
/// </remarks>
internal static class UpstreamShellCheckLine
{
    private static readonly Regex Command = new(
        @"\G\s*(?:(?<name>mkdir)\s+(?<path>[^\s;]+)|(?<name>cp)\s+(?<source>[^\s;]+)\s+(?<path>[^\s;]+)|(?<name>echo)\s+'(?<text>[^']*)'\s*>\s*(?<path>[^\s;]+)|(?<name>grep)\s+-q\s+""(?<text>[^""\\.\[\]*^$]*)""\s+(?<path>[^\s;]+))\s*(?:;|$)",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        UpstreamRegex.MatchTimeout);

    private static readonly Regex VendoredCaseFile = new(@"[/\\]data[/\\](?<name>test\d+)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, UpstreamRegex.MatchTimeout);

    /// <summary>Whether the line is a precheck made only of <c>mkdir</c>, <c>cp</c> and <c>echo '…' &gt; FILE</c> commands.</summary>
    /// <param name="line">The precheck line, its variables substituted.</param>
    /// <returns><see langword="true"/> when this class runs the line as a precheck.</returns>
    public static bool InterpretsPrecheck(string line) => Commands(line) is { } commands && commands.All(command => command.Groups["name"].Value != "grep");

    /// <summary>Whether the line is a postcheck <c>grep -q "TEXT" FILE</c>.</summary>
    /// <param name="line">The postcheck line, its variables substituted.</param>
    /// <returns><see langword="true"/> when this class runs the line as a postcheck.</returns>
    public static bool InterpretsPostcheck(string line) => Commands(line) is [{ } command] && command.Groups["name"].Value == "grep";

    /// <summary>Runs the line's commands in order, stopping at the first that fails.</summary>
    /// <param name="line">The check line, its variables substituted.</param>
    /// <param name="logDirectory">The case's log directory, which every path the line writes or greps must lie inside.</param>
    /// <returns>The line's exit code and what it printed, or <see langword="null"/> when this class does not interpret it.</returns>
    public static UpstreamPerlOneLinerResult? RunLine(string line, string logDirectory)
    {
        ArgumentNullException.ThrowIfNull(logDirectory);
        if (Commands(line) is not { } commands)
        {
            return null;
        }

        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(logDirectory)) + Path.DirectorySeparatorChar;
        return commands.Select(command => RunCommand(command, root)).FirstOrDefault(result => result.ExitCode != 0) ?? new UpstreamPerlOneLinerResult(0, string.Empty);
    }

    // The line's commands, or null when any part of it is not one of the four.
    private static List<Match>? Commands(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        List<Match> commands = Command.Matches(line.Trim()).ToList();
        return commands.Count > 0 && commands[^1].Index + commands[^1].Length == line.Trim().Length ? commands : null;
    }

    private static UpstreamPerlOneLinerResult RunCommand(Match command, string root)
    {
        string path = command.Groups["path"].Value;
        if (!Path.GetFullPath(path).StartsWith(root, StringComparison.Ordinal))
        {
            return new UpstreamPerlOneLinerResult(1, $"the check path {path} is outside the log directory\n");
        }

        switch (command.Groups["name"].Value)
        {
            case "mkdir":
                Directory.CreateDirectory(path);
                return new UpstreamPerlOneLinerResult(0, string.Empty);
            case "cp":
                return Copy(command.Groups["source"].Value, path);
            case "echo":
                File.WriteAllText(path, command.Groups["text"].Value + "\n", Encoding.Latin1);
                return new UpstreamPerlOneLinerResult(0, string.Empty);
            default:
                return Grep(command.Groups["text"].Value, path);
        }
    }

    private static UpstreamPerlOneLinerResult Copy(string source, string destination)
    {
        string file = File.Exists(source) || VendoredCaseFile.Match(source) is not { Success: true } vendored
            ? source
            : Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(source))!, vendored.Groups["name"].Value + ".curltest");
        if (!File.Exists(file))
        {
            return new UpstreamPerlOneLinerResult(1, string.Empty);
        }

        File.Copy(file, Directory.Exists(destination) ? Path.Combine(destination, Path.GetFileName(source)) : destination, overwrite: true);
        return new UpstreamPerlOneLinerResult(0, string.Empty);
    }

    private static UpstreamPerlOneLinerResult Grep(string text, string file) =>
        !File.Exists(file)
            ? new UpstreamPerlOneLinerResult(2, string.Empty)
            : new UpstreamPerlOneLinerResult(File.ReadAllText(file, Encoding.Latin1).Contains(text, StringComparison.Ordinal) ? 0 : 1, string.Empty);
}
