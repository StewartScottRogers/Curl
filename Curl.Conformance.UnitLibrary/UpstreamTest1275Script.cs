using System.Text;
using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// Interprets upstream's <c>tests/test1275.pl</c> (curl 8.22.0), which case 1275 runs as <c>%SRCDIR/test1275.pl %SRCDIR/..</c>
/// to check that no sentence in the Markdown documents starts with a lowercase word after a period, without running Perl (BL-3414).
/// </summary>
/// <remarks>
/// The script lists the tree with <c>git ls-files</c>; here every <c>*.md</c> file under the root is read in ordinal path order,
/// except the root's own <c>README.md</c>, which is the vendored tree's description and not an upstream file. Files are read as
/// Latin-1 and split at line feeds. A leading <c>---</c> block (front matter), lines starting with four spaces and fenced code
/// blocks are skipped. Each offending line writes the script's three standard error lines; the script then exits 1, or writes
/// <c>ok</c> and exits 0.
/// </remarks>
internal static class UpstreamTest1275Script
{
    private static readonly string[] AcceptedWords = ["curl", "libcurl", "macOS", "wolfSSL", "mbedTLS", "rustls", "c-ares"];

    private static readonly Regex LowercaseStart = new(@"^( *)([a-z][A-Za-z-]*)", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex PeriodThenWord = new(@"^(.*)\. +([a-z-]+)", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex Abbreviation = new(@"(\.\.|[0-9]|e.g|i.e|E.g|etc)\z", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    /// <summary>The file name of the upstream script this class interprets.</summary>
    public const string ScriptName = "test1275.pl";

    /// <summary>What the script wrote to standard output and standard error, and its exit code.</summary>
    /// <param name="Output">The script's standard output.</param>
    /// <param name="Error">The script's standard error.</param>
    /// <param name="ExitCode">The script's exit code: 0, or 1 when a line was offending.</param>
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

    /// <summary>Checks every Markdown file under a root.</summary>
    /// <param name="root">The source tree's root.</param>
    /// <returns>What the script writes and its exit code.</returns>
    public static Result Check(string root)
    {
        string rootReadme = Path.GetFullPath(Path.Combine(root, "README.md"));
        StringBuilder error = new();
        int errors = 0;
        foreach (string file in Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            if (Path.GetFullPath(file) != rootReadme)
            {
                errors += CheckFile(root.TrimEnd('/', '\\') + "/" + Path.GetRelativePath(root, file).Replace('\\', '/'), File.ReadAllText(file, Encoding.Latin1), error);
            }
        }

        return errors > 0 ? new Result("", error.ToString(), 1) : new Result("ok\n", "", 0);
    }

    // checkfile: the number of offending lines, each written to the error text.
    private static int CheckFile(string name, string text, StringBuilder error)
    {
        List<string> lines = SplitLines(text);
        int errors = 0;
        string previous = "";
        bool ignore = false;
        MarkdownSkipper skipper = new();
        for (int index = 0; index < lines.Count; index++)
        {
            string line = lines[index];
            if (skipper.Skips(index + 1, line))
            {
                previous = skipper.ResetsPrevious ? "" : previous;
                continue;
            }

            ignore ^= IsFence(line);
            errors += ignore ? 0 : Report(name, index + 1, previous, line, error);
            previous = line;
        }

        return errors;
    }

    // <$fh> reads: lines split at line feeds, with no empty line after a final one.
    private static List<string> SplitLines(string text)
    {
        List<string> lines = [.. text.Split('\n')];
        if (lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    private static bool IsFence(string line) => line.StartsWith("```", StringComparison.Ordinal) || line.StartsWith("~~~", StringComparison.Ordinal);

    // Writes the script's three lines for a lowercase word that wrongly follows a period; 1 if there was one.
    private static int Report(string name, int number, string previous, string line, StringBuilder error)
    {
        (int column, string word) = FindLowercaseWord(previous, line);
        if (word.Length == 0)
        {
            return 0;
        }

        error.Append($"{name}:{number}:{column}:error: lowercase {word} after period\n{line}\n{new string(' ', column)}^\n");
        return 1;
    }

    // The column and text of a lowercase word that wrongly follows a period, or an empty word.
    private static (int Column, string Word) FindLowercaseWord(string previous, string line)
    {
        Match start = LowercaseStart.Match(line);
        return previous.EndsWith('.') && start.Success
            ? (IsAccepted(start.Groups[2].Value) ? (0, "") : (start.Groups[1].Length, start.Groups[2].Value))
            : FindWordInsideLine(line);
    }

    private static (int Column, string Word) FindWordInsideLine(string line)
    {
        Match inside = PeriodThenWord.Match(line);
        if (!inside.Success)
        {
            return (0, "");
        }

        string prefix = inside.Groups[1].Value;
        string word = inside.Groups[2].Value;
        bool exempt = Abbreviation.IsMatch(prefix) || word[0] is not (>= 'a' and <= 'z') || IsAccepted(word);
        return exempt ? (0, "") : (prefix.Length + 2, word);
    }

    private static bool IsAccepted(string word) => AcceptedWords.Contains(word, StringComparer.Ordinal);

    // The lines checkfile skips: a leading --- block and lines indented by four spaces (which also clear the previous line).
    private sealed class MarkdownSkipper
    {
        private bool inMetadata;

        public bool ResetsPrevious { get; private set; }

        public bool Skips(int number, string line)
        {
            ResetsPrevious = false;
            bool isDivider = line.StartsWith("---", StringComparison.Ordinal);
            if (inMetadata)
            {
                inMetadata = !isDivider;
                return true;
            }

            if (number == 1 && isDivider)
            {
                inMetadata = true;
                return true;
            }

            ResetsPrevious = line.StartsWith("    ", StringComparison.Ordinal);
            return ResetsPrevious;
        }
    }
}
