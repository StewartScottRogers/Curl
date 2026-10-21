using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Interprets upstream's <c>tests/test1488.pl</c> (curl 8.22.0), which case 1488 runs as
/// <c>%SRCDIR/test1488.pl %SRCDIR/.. ../include/curl %SRCDIR/../docs/VERSIONS.md</c> to check that
/// <c>docs/libcurl/symbols-in-versions</c> and the libcurl man pages agree on the version each symbol was added in,
/// without running Perl (BL-2992).
/// </summary>
/// <remarks>
/// It reads what the script reads: the past releases, the <c>- VERSION: DATE</c> lines after <c>## Past releases</c>
/// in <c>VERSIONS.md</c> (upstream's <c>allversions.pm</c>); every file whose name ends in a character and <c>md</c>
/// in <c>ROOT/docs/libcurl</c> and then <c>ROOT/docs/libcurl/opts</c>, in ordinal order, each up to the first line by
/// which both its <c>Title:</c> and its <c>Added-in:</c> line (either matched case-insensitively) are seen; and the
/// <c>SYMBOL VERSION</c> lines of <c>symbols-in-versions</c>. To standard output it writes, in the script's order and
/// words, a man page added in a version never released, a title two man pages share, a symbol added in a version
/// never released, and a symbol whose man page names another version (in ordinal order), then <c>OK</c> when it found
/// none; the exit code is the number of problems, modulo 256 as Perl's <c>exit</c> takes it. Fewer than 100 past
/// releases is <c>allversions.pm</c>'s <c>die</c>: its message to standard error and exit code 255. The include
/// folder the command names is unused, as in the script. A missing file or folder throws. Parsing is by hand, not
/// with <c>Regex</c>, as elsewhere in this library.
/// </remarks>
internal static class UpstreamTest1488Script
{
    private const int MinimumReleaseCount = 100;

    private const int Die = 255;

    private const string TitlePrefix = "Title: ";

    private const string AddedInPrefix = "Added-in: ";

    private const string NotApplicable = "n/a";

    /// <summary>The file name of the upstream script this class interprets.</summary>
    public const string ScriptName = "test1488.pl";

    /// <summary>Runs the check over the folders a case's command names, writing to the invocation's standard output.</summary>
    /// <param name="invocation">The command's words after the script: the release root, the include folder and VERSIONS.md.</param>
    /// <param name="runCurl">The in-process curl, which the script never runs.</param>
    /// <returns>The script's exit code.</returns>
    public static async Task<int> RunAsync(UpstreamCurlInvocation invocation, Func<UpstreamCurlInvocation, Task<int>> runCurl)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        (int exitCode, string standardOutput, string standardError) = Check(invocation.Arguments[0], invocation.Arguments[2]);
        await invocation.StandardOutput.WriteAsync(Encoding.UTF8.GetBytes(standardOutput));
        await invocation.StandardError.WriteAsync(Encoding.UTF8.GetBytes(standardError));
        return exitCode;
    }

    /// <summary>Checks symbols-in-versions against the libcurl man pages and the past releases.</summary>
    /// <param name="root">The release root, beside <c>docs</c>, as the script prints it.</param>
    /// <param name="versions">The path of <c>docs/VERSIONS.md</c>.</param>
    /// <returns>The script's exit code and what it writes to standard output and standard error.</returns>
    public static (int ExitCode, string StandardOutput, string StandardError) Check(string root, string versions)
    {
        HashSet<string> pastReleases = PastReleases(versions);
        if (pastReleases.Count < MinimumReleaseCount)
        {
            return (Die, string.Empty, $"too few releases ({pastReleases.Count}) found in {versions}\n");
        }

        List<string> problems = [];
        Dictionary<string, string> manPages = new(StringComparer.Ordinal);
        foreach (string page in ManPages($"{root}/docs/libcurl").Concat(ManPages($"{root}/docs/libcurl/opts")))
        {
            problems.AddRange(ManPageProblems(page, manPages, pastReleases));
        }

        problems.AddRange(SymbolProblems($"{root}/docs/libcurl/symbols-in-versions", manPages, pastReleases));
        return (problems.Count & Die, string.Concat(problems.Select(problem => problem + "\n")) + (problems.Count == 0 ? "OK\n" : string.Empty), string.Empty);
    }

    // Each symbol added in a version never released, then each whose man page names another version.
    private static IEnumerable<string> SymbolProblems(string symbolsInVersions, Dictionary<string, string> manPages, HashSet<string> pastReleases)
    {
        List<(string Symbol, string Version)> symbols = [.. File.ReadLines(symbolsInVersions).Select(SymbolLine).OfType<(string, string)>()];
        Dictionary<string, string> added = new(StringComparer.Ordinal);
        symbols.ForEach(symbol => added[symbol.Symbol] = symbol.Version);
        return symbols.Where(symbol => !pastReleases.Contains(symbol.Version)).Select(symbol => $"SIV: says {symbol.Symbol} was added in non-existing {symbol.Version}")
            .Concat(symbols.Select(symbol => symbol.Symbol).Order(StringComparer.Ordinal)
                .Where(symbol => manPages.TryGetValue(symbol, out string? version) && version != added[symbol])
                .Select(symbol => $"{symbol}.md says version {manPages[symbol]}, but SIV says {added[symbol]}"));
    }

    // scanman_md_dir's /.md\z/: any one character, then "md" at the end of the name.
    private static IEnumerable<string> ManPages(string folder) =>
        Directory.EnumerateFiles(folder)
            .Select(path => Path.GetFileName(path))
            .Where(name => name.Length > 2 && name.EndsWith("md", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(name => $"{folder}/{name}");

    // checkmanpage: the Title: and Added-in: lines, until both are seen.
    private static List<string> ManPageProblems(string page, Dictionary<string, string> manPages, HashSet<string> pastReleases)
    {
        List<string> problems = [];
        string? title = null;
        string? addedIn = null;
        foreach (string line in File.ReadLines(page))
        {
            title = line.StartsWith(TitlePrefix, StringComparison.OrdinalIgnoreCase) ? line[TitlePrefix.Length..] : title;
            addedIn = AddedInLine(line, page, pastReleases, problems) ?? addedIn;
            if (BothTrue(title, addedIn))
            {
                problems.AddRange(RecordManPage(manPages, title!, addedIn!, page));
                break;
            }
        }

        return problems;
    }

    private static bool BothTrue(string? title, string? addedIn) => IsTrue(addedIn) && IsTrue(title);

    // Only a true Added-in value is ever stored, so a stored title is always the script's true $manpage{$title}.
    private static IEnumerable<string> RecordManPage(Dictionary<string, string> manPages, string title, string addedIn, string page)
    {
        IEnumerable<string> problems = manPages.ContainsKey(title) ? [$"{title} is a duplicate symbol in file {page}"] : [];
        manPages[title] = addedIn;
        return problems;
    }

    // /^Added-in: ([an0-9.\/]*)/i, noting a version never released; null for any other line.
    private static string? AddedInLine(string line, string page, HashSet<string> pastReleases, List<string> problems)
    {
        if (!line.StartsWith(AddedInPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string addedIn = new([.. line.Skip(AddedInPrefix.Length).TakeWhile(character => "an0123456789./".Contains(character, StringComparison.Ordinal))]);
        problems.AddRange(addedIn != NotApplicable && !pastReleases.Contains(addedIn) ? [$"{page}: was added in a never released version: {addedIn}"] : []);
        return addedIn;
    }

    // /^(\S+) +([0-9.]*)/: a symbol from the start of the line, one or more blanks, and its version, perhaps empty.
    private static (string Symbol, string Version)? SymbolLine(string line)
    {
        int end = line.TakeWhile(character => !char.IsWhiteSpace(character)).Count();
        int version = end + line.Skip(end).TakeWhile(character => character == ' ').Count();
        return end > 0 && version > end ? (line[..end], VersionAt(line, version)) : null;
    }

    // allversions.pm's /^- ([0-9.]+): (.*)/ after the "## Past releases" line.
    private static HashSet<string> PastReleases(string versions) =>
        [.. File.ReadLines(versions)
            .SkipWhile(line => !line.StartsWith("## Past releases", StringComparison.Ordinal))
            .Select(PastRelease)
            .OfType<string>()];

    private static string? PastRelease(string line)
    {
        if (!line.StartsWith("- ", StringComparison.Ordinal))
        {
            return null;
        }

        string version = VersionAt(line, 2);
        return version.Length > 0 && line.AsSpan(2 + version.Length).StartsWith(": ", StringComparison.Ordinal) ? version : null;
    }

    private static string VersionAt(string line, int start) =>
        new([.. line.Skip(start).TakeWhile(character => char.IsAsciiDigit(character) || character == '.')]);

    // Perl's truth: defined, not empty and not "0".
    private static bool IsTrue(string? text) => text is { Length: > 0 } and not "0";
}
