using System.Text;
using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// Interprets upstream's <c>tests/test1544.pl</c> (curl 8.22.0), which case 1544 runs as <c>%SRCDIR/test1544.pl %SRCDIR/..</c>
/// to check that the OS/400 wrapper (<c>projects/OS400/ccsidcurl.c</c>) translates every string option that
/// <c>include/curl/curl.h</c> declares, without running Perl (BL-3396).
/// </summary>
/// <remarks>
/// The header's string options are the first arguments of the <c>CURLOPT</c> and <c>CURLOPTDEPRECATED</c> declarations
/// whose type is <c>CURLOPTTYPE_STRINGPOINT</c>, read from the header with its comments removed, backslash continuations
/// joined and any line that does not end a declaration folded into the next. The wrapper's are the <c>case</c> labels
/// between its <c>BEGIN TRANSLATABLE STRING OPTIONS</c> and <c>END TRANSLATABLE STRING OPTIONS</c> lines. To standard
/// output it writes, in sorted order (the script writes them in hash order), each header option the wrapper lacks as
/// <c>NAME is not translated</c>, each wrapper option the header lacks as <c>translated option NAME does not exist</c>,
/// then, for the first label that sorts before the one above it, the script's "not sorted" line (which, as in the script,
/// never looks at the last label). The exit code is 1 when anything was written and 0 otherwise. The first word of the
/// command is the source tree's root, <c>.</c> when absent. Lines are read as Latin-1, as Perl reads bytes, and a
/// missing file throws.
/// </remarks>
internal static class UpstreamTest1544Script
{
    private static readonly Regex AfterComment = new(@"^.*?\*/\s*(.*)$", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex Continued = new(@"^(.*)\\$", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex WholeComment = new(@"^(.*?)/\*.*?\*/(.*)$", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex CommentStart = new(@"^(.*)/\*", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex EndsDeclaration = new(@"[;,{}]$", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex EndsWithBareComma = new(@"[^)],$", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex StringOption = new(
        @"CURLOPT(?:DEPRECATED)?\s*\(\s*([^, \t]+)\s*,\s*CURLOPTTYPE_STRINGPOINT",
        RegexOptions.CultureInvariant,
        UpstreamRegex.MatchTimeout);

    private static readonly Regex Armor = new("(BEGIN|END) TRANSLATABLE STRING OPTIONS", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex CaseLabel = new(@"case\s+([^:]+):", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    /// <summary>The file name of the upstream script this class interprets.</summary>
    public const string ScriptName = "test1544.pl";

    /// <summary>Runs the check over the source tree a case's command names, writing to the invocation's standard output.</summary>
    /// <param name="invocation">The command's words after the script: the source tree's root.</param>
    /// <param name="runCurl">The in-process curl, which the script never runs.</param>
    /// <returns>The script's exit code: 1 when it reported anything, otherwise 0.</returns>
    public static async Task<int> RunAsync(UpstreamCurlInvocation invocation, Func<UpstreamCurlInvocation, Task<int>> runCurl)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        (string output, int errors) = Check(invocation.Arguments.Count > 0 ? invocation.Arguments[0] : ".");
        await invocation.StandardOutput.WriteAsync(Encoding.UTF8.GetBytes(output));
        return errors > 0 ? 1 : 0;
    }

    /// <summary>Compares the header's string options with the wrapper's translated ones.</summary>
    /// <param name="root">The source tree's root, holding <c>include/curl/curl.h</c> and <c>projects/OS400/ccsidcurl.c</c>.</param>
    /// <returns>What the script writes to standard output and how many problems it counted.</returns>
    public static (string Output, int Errors) Check(string root)
    {
        List<string> defined = HeaderStringOptions(root + "/include/curl/curl.h");
        List<string> translated = WrapperStringOptions(root + "/projects/OS400/ccsidcurl.c");
        List<string> lines =
        [
            .. defined.Except(translated, StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(option => $"{option} is not translated\n"),
            .. translated.Except(defined, StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(option => $"translated option {option} does not exist\n"),
        ];
        for (int i = 1; i < translated.Count - 1; i++)
        {
            if (string.CompareOrdinal(translated[i], translated[i - 1]) < 0)
            {
                lines.Add($"Translated string options are not sorted ({translated[i - 1]}/{translated[i]})\n");
                break;
            }
        }

        return (string.Concat(lines), lines.Count);
    }

    // scan_header: the first argument of each CURLOPT declaration of type CURLOPTTYPE_STRINGPOINT.
    private static List<string> HeaderStringOptions(string header)
    {
        List<string> options = [];
        string folded = string.Empty;
        bool inComment = false;
        foreach (string raw in File.ReadLines(header, Encoding.Latin1))
        {
            string? line = Unfold(raw, ref folded, ref inComment);
            if (line is null)
            {
                continue;
            }

            if (!EndsDeclaration.IsMatch(line) || EndsWithBareComma.IsMatch(line))
            {
                folded = line;
                continue;
            }

            Match option = StringOption.Match(line);
            if (option.Success)
            {
                options.Add(option.Groups[1].Value);
            }
        }

        return options;
    }

    // One header line with the open comment's trail, the pending fold and its comments dealt with; null when nothing is left to look at.
    private static string? Unfold(string raw, ref string folded, ref bool inComment)
    {
        string? line = AfterOpenComment(raw.Trim(), ref inComment);
        if (line is null)
        {
            return null;
        }

        // The pending fold, empty when there is none, goes in front. The space this puts before an
        // unfolded line is harmless: WithoutComments trims every line this returns (AF-0419).
        line = folded + " " + line;
        folded = string.Empty;
        Match continuation = Continued.Match(line);
        if (continuation.Success)
        {
            folded = continuation.Groups[1].Value + " ";
            return null;
        }

        (line, inComment) = WithoutComments(line);
        return line.Length == 0 || line[0] == '#' ? null : line;
    }

    // The text after the "*/" that closes the comment a previous line left open, which ends it; null while it stays open.
    private static string? AfterOpenComment(string line, ref bool inComment)
    {
        if (!inComment)
        {
            return line;
        }

        Match after = AfterComment.Match(line);
        inComment = !after.Success;
        return after.Success ? after.Groups[1].Value : null;
    }

    // Whole comments become a blank each; an unterminated one cuts the line and leaves the comment open. The line is trimmed.
    private static (string Line, bool InComment) WithoutComments(string line)
    {
        for (Match whole = WholeComment.Match(line); whole.Success; whole = WholeComment.Match(line))
        {
            line = whole.Groups[1].Value + " " + whole.Groups[2].Value;
        }

        Match start = CommentStart.Match(line);
        return start.Success ? ((start.Groups[1].Value + " ").Trim(), true) : (line.Trim(), false);
    }

    // scan_wrapper_for_strings: the case labels between the BEGIN and END armor lines.
    private static List<string> WrapperStringOptions(string wrapper)
    {
        List<string> options = [];
        bool inArmor = false;
        foreach (string line in File.ReadLines(wrapper, Encoding.Latin1))
        {
            Match armor = Armor.Match(line);
            if (armor.Success)
            {
                inArmor = armor.Groups[1].Value == "BEGIN";
                continue;
            }

            Match label = inArmor ? CaseLabel.Match(line) : Match.Empty;
            if (label.Success)
            {
                options.Add(label.Groups[1].Value);
            }
        }

        return options;
    }
}
