using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Interprets upstream's <c>tests/test971.pl</c> (curl 8.22.0), which case 971 runs as
/// <c>%SRCDIR/test971.pl OPTIONS-IN-VERSIONS CMDLINE-OPTS-FOLDER VERSIONS.md</c> to check that
/// <c>docs/options-in-versions</c> and the <c>docs/cmdline-opts/*.md</c> files name the same options at the
/// same versions, without running Perl (BL-2865).
/// </summary>
/// <remarks>
/// It reads the three files the command names, as the script does: the past releases, the <c>- VERSION: DATE</c>
/// lines after <c>## Past releases</c> in <c>VERSIONS.md</c> (upstream's <c>allversions.pm</c>); the option files,
/// every file in the folder whose name ends in <c>.md</c> and has a lowercase letter or digit before that; and the
/// <c>--option (-o)  VERSION</c> lines of <c>options-in-versions</c>. To standard error it writes, in the script's
/// order and words, an option file the list lacks, a listed version that is not a past release, a file whose first
/// <c>Added:</c> line names another version, and a listed option with no file, then <c>ok</c> when it found none;
/// the exit code is the number of problems, modulo 256 as Perl's <c>exit</c> takes it. Fewer than 100 past
/// releases is <c>allversions.pm</c>'s <c>die</c>: its message and exit code 255. Lines are read as text lines,
/// and a missing file or folder throws. Parsing is by hand, not with <c>Regex</c>, as elsewhere in this library.
/// </remarks>
internal static class UpstreamTest971Script
{
    private const int MinimumReleaseCount = 100;

    private const int Die = 255;

    private const string MarkdownExtension = ".md";

    /// <summary>The file name of the upstream script this class interprets.</summary>
    public const string ScriptName = "test971.pl";

    /// <summary>Runs the check over the files a case's command names, writing to the invocation's standard error.</summary>
    /// <param name="invocation">The command's words after the script: options-in-versions, the cmdline-opts folder and VERSIONS.md.</param>
    /// <param name="runCurl">The in-process curl, which the script never runs.</param>
    /// <returns>The script's exit code.</returns>
    public static async Task<int> RunAsync(UpstreamCurlInvocation invocation, Func<UpstreamCurlInvocation, Task<int>> runCurl)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        (int exitCode, string standardError) = Check(invocation.Arguments[0], invocation.Arguments[1], invocation.Arguments[2]);
        await invocation.StandardError.WriteAsync(Encoding.UTF8.GetBytes(standardError));
        return exitCode;
    }

    /// <summary>Checks options-in-versions against the cmdline-opts folder and the past releases.</summary>
    /// <param name="optionsInVersions">The path of <c>docs/options-in-versions</c>, as the script prints it.</param>
    /// <param name="optionFolder">The path of <c>docs/cmdline-opts</c>.</param>
    /// <param name="versions">The path of <c>docs/VERSIONS.md</c>.</param>
    /// <returns>The script's exit code and what it writes to standard error.</returns>
    public static (int ExitCode, string StandardError) Check(string optionsInVersions, string optionFolder, string versions)
    {
        HashSet<string> pastReleases = PastReleases(versions);
        if (pastReleases.Count < MinimumReleaseCount)
        {
            return (Die, $"too few releases ({pastReleases.Count}) found in {versions}\n");
        }

        HashSet<string> files = OptionFiles(optionFolder);
        (List<string> listed, Dictionary<string, string> listedVersions) = ListedOptions(optionsInVersions);
        List<string> problems = [.. files.Order(StringComparer.Ordinal).SelectMany(file => OptionFileProblems(file, listedVersions, pastReleases, optionsInVersions, optionFolder))];
        problems.AddRange(listed.Order(StringComparer.Ordinal).Where(option => !files.Contains(option)).Select(option => $"{option} is in the doc but NOT as a file!"));
        return (problems.Count & Die, problems.Count == 0 ? "ok\n" : string.Concat(problems.Select(problem => problem + "\n")));
    }

    private static IEnumerable<string> OptionFileProblems(string file, Dictionary<string, string> listedVersions, HashSet<string> pastReleases, string optionsInVersions, string optionFolder) =>
        listedVersions.TryGetValue(file, out string? version)
            ? FileProblems(file, version, pastReleases, optionFolder)
            : [$"--{file} is in the option directory but not in {optionsInVersions}!"];

    private static IEnumerable<string> FileProblems(string file, string version, HashSet<string> pastReleases, string optionFolder)
    {
        if (!pastReleases.Contains(version))
        {
            yield return $"{file}: {version} is not a proper release";
        }

        string? added = File.ReadLines(Path.Combine(optionFolder, file + MarkdownExtension)).Select(AddedVersion).FirstOrDefault(found => found is not null);
        if (added is not null && added != version)
        {
            yield return $"{file} lists {version} in doc but {added} in file";
        }
    }

    // versioncheck's /^Added: ([0-9.]+)/.
    private static string? AddedVersion(string line) =>
        line.StartsWith("Added: ", StringComparison.Ordinal) ? NonEmpty(VersionAt(line, "Added: ".Length)) : null;

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

    // cmdfiles' /[a-z0-9].*\.md$/: a lowercase letter or digit somewhere before the closing ".md".
    private static HashSet<string> OptionFiles(string optionFolder) =>
        [.. Directory.EnumerateFiles(optionFolder)
            .Select(path => Path.GetFileName(path))
            .Where(name => name.EndsWith(MarkdownExtension, StringComparison.Ordinal) && name[..^MarkdownExtension.Length].Any(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character)))
            .Select(name => name[..^MarkdownExtension.Length])];

    // mentions: each line /(.*) +([0-9.]+)/ gives the option, less its leading dashes, " (-o)" and trailing blanks.
    private static (List<string> Listed, Dictionary<string, string> Versions) ListedOptions(string optionsInVersions)
    {
        List<string> listed = [];
        Dictionary<string, string> versions = new(StringComparer.Ordinal);
        foreach (string line in File.ReadLines(optionsInVersions))
        {
            int blank = VersionBlank(line);
            if (blank >= 0)
            {
                string option = WithoutShortOption(WithoutLeadingDashes(line[..blank])).TrimEnd(' ');
                listed.Add(option);
                versions[option] = VersionAt(line, blank + 1);
            }
        }

        return (listed, versions);
    }

    // The greedy (.*) ends at the last blank followed by a version character; -1 when there is none.
    private static int VersionBlank(string line) =>
        Enumerable.Range(0, Math.Max(0, line.Length - 1)).LastOrDefault(index => line[index] == ' ' && IsVersionCharacter(line[index + 1]), -1);

    // s/^--//.
    private static string WithoutLeadingDashes(string flag) => flag.StartsWith("--", StringComparison.Ordinal) ? flag[2..] : flag;

    // s/ \(-.\)//: the first blank, "(-", any one character and ")".
    private static string WithoutShortOption(string flag)
    {
        int start = Enumerable.Range(0, Math.Max(0, flag.Length - 4)).FirstOrDefault(index => flag.AsSpan(index).StartsWith(" (-", StringComparison.Ordinal) && flag[index + 4] == ')', -1);
        return start < 0 ? flag : flag.Remove(start, 5);
    }

    private static string VersionAt(string line, int start) =>
        new([.. line.Skip(start).TakeWhile(IsVersionCharacter)]);

    private static string? NonEmpty(string text) => text.Length > 0 ? text : null;

    private static bool IsVersionCharacter(char character) => char.IsAsciiDigit(character) || character == '.';
}
