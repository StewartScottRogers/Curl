using System.Text;
using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// Interprets upstream's <c>tests/runtests.pl</c> (curl 8.22.0) as case 1169 runs it,
/// <c>%SRCDIR/runtests.pl -L %PWD/%LOGDIR/test%TESTNUMBER-library.pl</c>, to check that the runner accepts <c>-L</c> and
/// exits with the code the library's <c>exit</c> statement gives, and as case 1182 runs it, <c>-E FILE 1</c>, to check that it
/// skips the tests an exclude file names, without running Perl (BL-3410, BL-3412, ADR-0882).
/// </summary>
/// <remarks>
/// <c>runtests.pl</c> <c>require</c>s each <c>-L</c> file as it reads its options, so a library holding a line
/// <c>exit N</c> ends the run there with exit code <c>N</c> (modulo 256, as a process exit code is). Only the command lines
/// <c>-L FILE</c> and <c>[-o NAME=VALUE]... -E FILE TEST...</c> are interpreted, the first only for a library holding such a line and the second only when the
/// exclude file's <c>test:N:reason</c> lines skip every test named (each printed as <c>test NNNN SKIPPED: reason</c>, exit code 1): any other command line, or a library with no
/// <c>exit</c> line (after which <c>runtests.pl</c> would go on to run test cases), throws
/// <see cref="NotSupportedException"/>. The library is read as text lines (Latin-1, as Perl reads bytes); a missing file
/// throws.
/// </remarks>
internal static class UpstreamRunTestsScript
{
    private static readonly Regex ExcludeFieldSeparator = new(@"\s*:\s*", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex ExitStatement = new(@"^\s*exit\s*\(?\s*(\d+)\s*\)?\s*;?\s*$", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    /// <summary>The file name of the upstream script this class interprets.</summary>
    public const string ScriptName = "runtests.pl";

    /// <summary>Runs <c>runtests.pl -L FILE</c>, ending with the library's exit code, or <c>runtests.pl [-o NAME=VALUE]... -E FILE TEST...</c>, printing each test's skip reason.</summary>
    /// <param name="invocation">The command's words after the script, and the stdout an excluded test's reason goes to.</param>
    /// <param name="runCurl">The in-process curl, which neither command line reaches.</param>
    /// <returns>The exit code of the library's <c>exit</c> line, or 1 when every test named was excluded.</returns>
    public static async Task<int> RunAsync(UpstreamCurlInvocation invocation, Func<UpstreamCurlInvocation, Task<int>> runCurl)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        StringBuilder output = new();
        int exitCode = Run(invocation.Arguments, output);
        await invocation.StandardOutput.WriteAsync(Encoding.Latin1.GetBytes(output.ToString())).ConfigureAwait(false);
        return exitCode;
    }

    /// <summary>Carries out a <c>runtests.pl</c> command line of the form <c>-L FILE</c> or <c>[-o NAME=VALUE]... -E FILE TEST...</c>, discarding the <c>test NNNN SKIPPED: reason</c> lines the second prints.</summary>
    /// <param name="arguments">The words after the script's name.</param>
    /// <returns>The exit code of the library's first <c>exit</c> line, or 1 when every test named was excluded.</returns>
    public static int Run(IReadOnlyList<string> arguments) => Run(arguments, new StringBuilder());

    /// <summary>Carries out a <c>runtests.pl</c> command line of the form <c>-L FILE</c> or <c>[-o NAME=VALUE]... -E FILE TEST...</c>.</summary>
    /// <param name="arguments">The words after the script's name.</param>
    /// <param name="output">Where an excluded test's <c>test NNNN SKIPPED: reason</c> line goes.</param>
    /// <returns>The exit code of the library's first <c>exit</c> line, or 1 when every test named was excluded.</returns>
    public static int Run(IReadOnlyList<string> arguments, StringBuilder output)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(output);
        string[] words = [.. arguments];
        int first = OptionWordCount(words);
        bool isLibraryRun = first == 0 && words.Length == 2 && words[0] == "-L";
        bool isExcludedRun = words.Length > first + 2 && words[first] == "-E";
        return isLibraryRun ? LibraryExitCode(words[1])
            : isExcludedRun ? ExcludedRun(words[first + 1], words[(first + 2)..], output)
            : throw new NotSupportedException($"runtests.pl is interpreted only as -L FILE or -E FILE TEST..., not as: {string.Join(' ', arguments)}");
    }

    // The number of leading words that are -o NAME=VALUE pairs.
    private static int OptionWordCount(string[] words)
    {
        int first = 0;
        while (words.Length > first + 1 && words[first] == "-o")
        {
            first += 2;
        }

        return first;
    }

    // runtests.pl's -E file holds TYPE:PATTERNS:REASON lines; a test line skips each test number it lists with the
    // reason. Every test named must be skipped so, as a run of none of them ends with exit code 1; one that would run
    // is not interpreted.
    private static int ExcludedRun(string excludeFile, string[] tests, StringBuilder output)
    {
        Dictionary<int, string> reasons = ExcludedTestReasons(excludeFile);
        foreach (int test in tests.Select(ParseTestNumber))
        {
            output.Append(System.Globalization.CultureInfo.InvariantCulture, $"test {test:D4} SKIPPED: {SkipReason(reasons, test)}\n");
        }

        return 1;
    }

    // Each test number the exclude file's test lines list, with the reason its line gives.
    private static Dictionary<int, string> ExcludedTestReasons(string excludeFile)
    {
        Dictionary<int, string> reasons = [];
        IEnumerable<string[]> testLines = File.ReadLines(excludeFile, Encoding.Latin1)
            .Where(line => !line.StartsWith('#'))
            .Select(line => ExcludeFieldSeparator.Split(line, 3))
            .Where(IsTestLine);
        foreach (string[] fields in testLines)
        {
            foreach (string pattern in fields[1].Split(','))
            {
                reasons[ParseTestNumber(pattern)] = fields[2];
            }
        }

        return reasons;
    }

    private static bool IsTestLine(string[] fields) => fields.Length == 3 && fields[0] == "test";

    private static int ParseTestNumber(string text) =>
        int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int number)
            ? number
            : throw new NotSupportedException($"the test word {text} is not a test number, so runtests.pl would not run it as one");

    private static string SkipReason(Dictionary<int, string> reasons, int test) =>
        reasons.TryGetValue(test, out string? reason) ? reason : throw new NotSupportedException($"test {test} is not excluded, so runtests.pl would run it");

    // The first `exit N` line of the library ends runtests.pl with N, as require runs it.
    private static int LibraryExitCode(string library)
    {
        Match? exit = File.ReadLines(library, Encoding.Latin1).Select(line => ExitStatement.Match(line)).FirstOrDefault(match => match.Success);
        return exit is null
            ? throw new NotSupportedException($"the library {library} holds no exit line, so runtests.pl would go on to run test cases")
            : (int)(long.Parse(exit.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) % 256);
    }
}
