using System.Text;
using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// Interprets upstream's <c>tests/test1276.pl</c> (curl 8.22.0), which case 1276 runs as <c>%SRCDIR/test1276.pl %SRCDIR/..</c>
/// to check that <c>lib/easyoptions.c</c> is what <c>lib/optiontable.pl</c> prints for <c>include/curl/curl.h</c>,
/// without running Perl (BL-3395).
/// </summary>
/// <remarks>
/// <c>optiontable.pl</c> is interpreted here, not run: its header and footer text are read from the vendored script
/// (its two here-documents, with <c>\\</c>, <c>\@</c> and <c>\$</c> read as the character after the backslash and <c>$lastnum</c> as the last option number), and its
/// table is built from <c>curl.h</c> by the same line rules, in the script's order, including the quirk that an
/// obsolete option skips the rest of that line's rules. A number lower than the one before it makes optiontable.pl exit
/// 2 after its header, which leaves the generated text short, as it does upstream. When the generated text and
/// <c>easyoptions.c</c> are byte for byte equal nothing is written and the exit code is 0; otherwise the script writes
/// its three summary lines and up to eleven differing lines, and exits 1 only if one differed after line ends were
/// stripped. Files are read as Latin-1 (Perl's <c>binmode</c>), and a missing file throws.
/// </remarks>
internal static class UpstreamTest1276Script
{
    private static readonly Regex ContinuationEnd = new(@"(.*)\),", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex DeprecatedStart = new(@"^ *CURLOPTDEPRECATED\((.*)", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex OptionLine = new(@"^ *CURLOPT\(([^,]*), ([^,]*), (\d+)\)", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex AliasLine = new(@"^#define (CURLOPT_[^ ]*) *(CURLOPT_\S*)", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex HeredocEscape = new(@"\\([\\@$])", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex FieldSeparator = new(", *", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    /// <summary>The file name of the upstream script this class interprets.</summary>
    public const string ScriptName = "test1276.pl";

    /// <summary>What the script wrote to standard output and standard error, and its exit code.</summary>
    /// <param name="Output">The script's standard output.</param>
    /// <param name="Error">What optiontable.pl wrote to standard error (the script's stderr is not read).</param>
    /// <param name="ExitCode">The script's exit code: 0, or 1 when a line differed.</param>
    public readonly record struct Result(string Output, string Error, int ExitCode);

    /// <summary>Runs the check over the source tree a case's command names, writing to the invocation's streams.</summary>
    /// <param name="invocation">The command's words after the script: the source tree's root.</param>
    /// <param name="runCurl">The in-process curl, which the script never runs.</param>
    /// <returns>The script's exit code.</returns>
    public static async Task<int> RunAsync(UpstreamCurlInvocation invocation, Func<UpstreamCurlInvocation, Task<int>> runCurl)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        Result result = Check(invocation.Arguments[0]);
        await invocation.StandardOutput.WriteAsync(Encoding.UTF8.GetBytes(result.Output));
        await invocation.StandardError.WriteAsync(Encoding.UTF8.GetBytes(result.Error));
        return result.ExitCode;
    }

    /// <summary>Compares <c>lib/easyoptions.c</c> with what <c>lib/optiontable.pl</c> prints for <c>include/curl/curl.h</c>.</summary>
    /// <param name="root">The source tree's root.</param>
    /// <returns>What the script writes and its exit code.</returns>
    public static Result Check(string root)
    {
        (string generated, string error) = Generate(File.ReadAllLines(root + "/lib/optiontable.pl", Encoding.Latin1), File.ReadAllText(root + "/include/curl/curl.h", Encoding.Latin1));
        string existing = File.ReadAllText(root + "/lib/easyoptions.c", Encoding.Latin1);
        if (generated == existing)
        {
            return new Result("", error, 0);
        }

        List<string> generatedLines = SplitLines(generated);
        List<string> existingLines = SplitLines(existing);
        StringBuilder output = new();
        output.Append("easyoptions.c need to be regenerated!\n");
        output.Append($"easyoptions.c is {existingLines.Count} lines\n");
        output.Append($"generated file is {generatedLines.Count} lines\n");
        int differing = AppendDifferingLines(generatedLines, existingLines, output);
        return new Result(output.ToString(), error, differing > 0 ? 1 : 0);
    }

    // The first eleven lines that differ once line ends are stripped, as File: and Gen: pairs; how many were shown.
    private static int AppendDifferingLines(List<string> generatedLines, List<string> existingLines, StringBuilder output)
    {
        int differing = 0;
        for (int index = 0; index < generatedLines.Count && differing <= 10; index++)
        {
            string generatedLine = StripLineEnds(generatedLines[index]);
            string existingLine = index < existingLines.Count ? StripLineEnds(existingLines[index]) : "";
            if (generatedLine != existingLine)
            {
                output.Append($"File: {index + 1}:{ShowLine(existingLine)}\nGen:  {index + 1}:{ShowLine(generatedLine)}\n");
                differing++;
            }
        }

        return differing;
    }

    // What optiontable.pl prints for the header, and what it wrote to standard error.
    private static (string Text, string Error) Generate(string[] script, string curlHeader)
    {
        string header = Heredoc(script, "print <<HEAD", "HEAD");
        OptionTable table = new();
        foreach (string line in SplitLines(curlHeader))
        {
            if (table.Read(line) == ReadOutcome.BadNumber)
            {
                return (header, table.Error);
            }
        }

        StringBuilder text = new(header);
        text.Append(table.Entries());
        text.Append(Heredoc(script, "print <<FOOT", "FOOT").Replace("$lastnum", table.LastNumber.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal));
        return (text.ToString(), "");
    }

    // The lines between the introducer and the terminator, each ending in a line feed, with \\ read as \.
    private static string Heredoc(string[] script, string introducer, string terminator) =>
        string.Concat(script.SkipWhile(line => line != introducer).Skip(1).TakeWhile(line => line != terminator).Select(line => HeredocEscape.Replace(line, "$1") + "\n"));

    private static List<string> SplitLines(string text)
    {
        List<string> lines = [];
        int start = 0;
        while (start < text.Length)
        {
            int end = text.IndexOf('\n', start);
            end = end < 0 ? text.Length : end + 1;
            lines.Add(text[start..end]);
            start = end;
        }

        return lines;
    }

    private static string StripLineEnds(string line) => line.Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal);

    // showline: every character outside 0x20 to 0x7f becomes %xx.
    private static string ShowLine(string line) => string.Concat(line.Select(character => character is >= ' ' and <= '\u007f' ? character.ToString() : $"%{(int)character:x2}"));

    private static string Chomp(string line) => line.EndsWith('\n') ? line[..^1] : line;

    private enum ReadOutcome
    {
        Read,
        BadNumber,
    }

    // optiontable.pl's state: the options by name, their types, the names in the order met, the aliases and the last number.
    private sealed class OptionTable
    {
        private readonly Dictionary<string, string> options = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> types = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> aliases = new(StringComparer.Ordinal);
        private readonly List<string> names = [];
        private string continued = "";

        public long LastNumber { get; private set; }

        public string Error { get; private set; } = "";

        // The while loop's body for one line; an obsolete option's "next" inside add() ends the line's rules at once.
        public ReadOutcome Read(string line)
        {
            if (continued.Length > 0)
            {
                Match end = ContinuationEnd.Match(line);
                if (end.Success)
                {
                    continued += end.Groups[1].Value;
                    string[] fields = FieldSeparator.Split(continued);
                    AddOutcome added = Add(Field(fields, 0), Field(fields, 1), Field(fields, 2));
                    if (added != AddOutcome.Added)
                    {
                        return added == AddOutcome.BadNumber ? ReadOutcome.BadNumber : ReadOutcome.Read;
                    }

                    continued = "";
                }
                else
                {
                    continued += Chomp(line);
                }
            }

            return ReadRules(line);
        }

        public string Entries()
        {
            StringBuilder entries = new();
            foreach (string name in names.Order(StringComparer.Ordinal))
            {
                string alias = aliases.GetValueOrDefault(name, "");
                string target = alias.Length > 0 ? alias : name;
                string flag = alias.Length > 0 ? "CURLOT_FLAG_ALIAS" : "0";
                string entry = $"  {{ \"{name}\", {options.GetValueOrDefault(target, "")}, {types.GetValueOrDefault(target, "")}, {flag} }},\n";
                entries.Append(entry.Length < 80
                    ? entry
                    : $"  {{ \"{name}\", {options.GetValueOrDefault(target, "")},\n    {types.GetValueOrDefault(target, "")}, {flag} }},\n");
            }

            return entries.ToString();
        }

        private static string Field(string[] fields, int index) => index < fields.Length ? fields[index] : "";

        private ReadOutcome ReadRules(string line)
        {
            Match deprecated = DeprecatedStart.Match(line);
            if (deprecated.Success)
            {
                continued = Chomp(deprecated.Groups[1].Value);
            }

            Match option = OptionLine.Match(line);
            if (option.Success && Add(option.Groups[1].Value, option.Groups[2].Value, option.Groups[3].Value) == AddOutcome.BadNumber)
            {
                return ReadOutcome.BadNumber;
            }

            ReadAlias(AliasLine.Match(line));
            return ReadOutcome.Read;
        }

        // "#define CURLOPT_OLD CURLOPT_NEW", unless either name is obsolete.
        private void ReadAlias(Match alias)
        {
            if (alias.Success && !alias.Groups[1].Value.Contains("OBSOLETE", StringComparison.Ordinal) && !alias.Groups[2].Value.Contains("OBSOLETE", StringComparison.Ordinal))
            {
                string old = alias.Groups[1].Value["CURLOPT_".Length..];
                aliases[old] = alias.Groups[2].Value["CURLOPT_".Length..];
                names.Add(old);
            }
        }

        private AddOutcome Add(string optionName, string typeText, string number)
        {
            if (optionName.Contains("OBSOLETE", StringComparison.Ordinal))
            {
                return AddOutcome.Skipped;
            }

            string name = optionName.StartsWith("CURLOPT_", StringComparison.Ordinal) ? optionName["CURLOPT_".Length..] : "";
            string extension = ReplaceFirst(ReplaceFirst(typeText.Replace(" ", "", StringComparison.Ordinal), "CURLOPTTYPE_", ""), "CBPOINT", "CBPTR");
            extension = extension.EndsWith("POINT", StringComparison.Ordinal) ? extension[..^"POINT".Length] : extension;
            options[name] = optionName;
            types[name] = "CURLOT_" + extension;
            names.Add(name);
            long.TryParse(number, out long parsed);
            if (parsed < LastNumber)
            {
                Error = $"ERROR: {optionName} has bad number: {parsed} < {LastNumber}\n";
                return AddOutcome.BadNumber;
            }

            LastNumber = parsed;
            return AddOutcome.Added;
        }

        private static string ReplaceFirst(string text, string old, string replacement)
        {
            int index = text.IndexOf(old, StringComparison.Ordinal);
            return index < 0 ? text : string.Concat(text.AsSpan(0, index), replacement, text.AsSpan(index + old.Length));
        }

        private enum AddOutcome
        {
            Added,
            Skipped,
            BadNumber,
        }
    }
}
