using System.Text;
using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// Interprets upstream's <c>tests/test1165.pl</c> (curl 8.22.0), which case 1165 runs as <c>%SRCDIR/test1165.pl %SRCDIR/..</c>
/// to check that the <c>CURL_DISABLE_*</c> options set by <c>configure.ac</c>, <c>CMakeLists.txt</c> and
/// <c>lib/curl_config-cmake.h.in</c>, used by the C sources and documented in <c>docs/CURL-DISABLE.md</c> agree, without
/// running Perl (BL-3409, ADR-0803).
/// </summary>
/// <remarks>
/// <c>configure.ac</c> and the <c>m4</c> files count the first option on each line, <c>CMakeLists.txt</c> and
/// <c>curl_config-cmake.h.in</c> likewise (leaving out <c>CURL_DISABLE_INSTALL</c>), the <c>.c</c> and <c>.h</c> files of
/// <c>include/curl</c>, <c>src</c>, <c>lib</c>, <c>lib/vtls</c> and <c>lib/vauth</c> every option but the two whitelisted
/// ones (the last file naming an option is the one reported), and the document each heading that names an option in
/// backticks. <c>CURL_DISABLE_TYPECHECK</c> is never counted. To standard output it writes one line per difference, in the
/// script's words and sorted order, and the exit code is the number of differences modulo 256, as a process exit code keeps
/// it. Lines are read as Latin-1 text lines, a missing folder throws, and a missing file counts as empty, as with the script.
/// </remarks>
internal static class UpstreamTest1165Script
{
    private const string Documents = "CURL-DISABLE.md";

    private static readonly Regex Option = new("CURL_DISABLE_[A-Z0-9_]+", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex DocumentedOption = new("^## `(CURL_DISABLE_[A-Z0-9_]+)`", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly HashSet<string> Whitelisted = new(["CURL_DISABLE_DEPRECATION", "CURL_DISABLE_TYPECHECK"], StringComparer.Ordinal);

    /// <summary>The file name of the upstream script this class interprets.</summary>
    public const string ScriptName = "test1165.pl";

    /// <summary>Runs the check over the source tree a case's command names, writing to the invocation's standard output.</summary>
    /// <param name="invocation">The command's words after the script: the source tree's root.</param>
    /// <param name="runCurl">The in-process curl, which the script never runs.</param>
    /// <returns>The script's exit code: the number of differences modulo 256.</returns>
    public static async Task<int> RunAsync(UpstreamCurlInvocation invocation, Func<UpstreamCurlInvocation, Task<int>> runCurl)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        (string output, int exitCode) = Check(invocation.Arguments[0]);
        await invocation.StandardOutput.WriteAsync(Encoding.UTF8.GetBytes(output));
        return exitCode;
    }

    /// <summary>Checks the <c>CURL_DISABLE_*</c> options of the build files, the sources and the document against each other.</summary>
    /// <param name="root">The source tree's root.</param>
    /// <returns>What the script writes to standard output and its exit code.</returns>
    public static (string Output, int ExitCode) Check(string root)
    {
        Tree tree = new(ScanConfigure(root), FirstOptions(root + "/CMakeLists.txt"), FirstOptions(root + "/lib/curl_config-cmake.h.in"), ScanSources(root), ScanDocuments(root));
        List<string> differences = [.. FromConfigure(tree), .. FromCMake(tree), .. FromCode(tree), .. FromDocuments(tree)];
        return (string.Concat(differences.Select(difference => difference + "\n")), differences.Count % 256);
    }

    // The options each place names, as the script collects them.
    private sealed record Tree(HashSet<string> Configure, HashSet<string> CMake, HashSet<string> CMakeConfig, Dictionary<string, string> Code, HashSet<string> Documented);

    // "Check the configure symbols for use in code".
    private static IEnumerable<string> FromConfigure(Tree tree) =>
        tree.Configure.Order(StringComparer.Ordinal).SelectMany(option => Missing(
            (tree.Code.ContainsKey(option), $"Present in configure.ac, not used by code: {option}"),
            (tree.Documented.Contains(option), $"Present in configure.ac, not documented in {Documents}: {option}")));

    // "Check the CMakeLists.txt symbols for use in code", then "... for use in curl_config-cmake.h.in".
    private static IEnumerable<string> FromCMake(Tree tree)
    {
        List<string> options = [.. tree.CMake.Order(StringComparer.Ordinal)];
        return options.SelectMany(option => Missing(
                (tree.Code.ContainsKey(option), $"Present in CMakeLists.txt, not used by code: {option}"),
                (tree.Documented.Contains(option), $"Present in CMakeLists.txt, not documented in {Documents}: {option}")))
            .Concat(options.SelectMany(option => Missing(
                (tree.CMakeConfig.Contains(option), $"Present in CMakeLists.txt, not propagated via curl_config-cmake.h.in: {option}"))));
    }

    // "Check the code symbols for use in configure".
    private static IEnumerable<string> FromCode(Tree tree) =>
        tree.Code.Keys.Order(StringComparer.Ordinal).SelectMany(option => Missing(
            (tree.Configure.Contains(option), $"Not set by configure: {option} ({tree.Code[option]})"),
            (tree.CMake.Contains(option), $"Not set by CMakeLists.txt: {option} ({tree.Code[option]})"),
            (tree.Documented.Contains(option), $"Used in code, not documented in {Documents}: {option}")));

    // "Check the documented symbols".
    private static IEnumerable<string> FromDocuments(Tree tree) =>
        tree.Documented.Order(StringComparer.Ordinal).SelectMany(option => Missing(
            (tree.Configure.Contains(option), $"Documented but not in configure: {option}"),
            (tree.CMake.Contains(option), $"Documented but not in CMakeLists.txt: {option}"),
            (tree.Code.ContainsKey(option), $"Documented, but not used by code: {option}")));

    // The differences of the checks that do not hold.
    private static IEnumerable<string> Missing(params (bool Holds, string Difference)[] checks) =>
        checks.Where(check => !check.Holds).Select(check => check.Difference);
    // scan_configure: configure.ac and every m4 file, the first option on each line, leaving out CURL_DISABLE_TYPECHECK.
    private static HashSet<string> ScanConfigure(string root)
    {
        HashSet<string> options = FirstOptions(root + "/configure.ac");
        foreach (string m4 in Directory.EnumerateFiles(root + "/m4").Where(path => path.EndsWith(".m4", StringComparison.Ordinal)))
        {
            options.UnionWith(FirstOptions(m4));
        }

        options.Remove("CURL_DISABLE_TYPECHECK");
        return options;
    }

    // scanconf_cmake: the first option on each line of a build file, leaving out CURL_DISABLE_INSTALL and CURL_DISABLE_TYPECHECK.
    private static HashSet<string> FirstOptions(string path) =>
        new(
            ReadLinesIfPresent(path).Select(line => Option.Match(line)).Where(match => match.Success).Select(match => match.Value)
                .Where(option => option != "CURL_DISABLE_INSTALL" && option != "CURL_DISABLE_TYPECHECK"),
            StringComparer.Ordinal);

    // scan_sources: every option in the C sources and headers, mapped to the last file that names it.
    private static Dictionary<string, string> ScanSources(string root)
    {
        Dictionary<string, string> files = new(StringComparer.Ordinal);
        foreach ((string option, string source) in new[] { "/include/curl", "/src", "/lib", "/lib/vtls", "/lib/vauth" }.SelectMany(directory => OptionsInFolder(root + directory)))
        {
            files[option] = source;
        }

        return files;
    }

    // scan_dir: the options of each .c and .h file of a folder in sorted file order, with the file naming them.
    private static IEnumerable<(string Option, string Source)> OptionsInFolder(string folder) =>
        Directory.EnumerateFiles(folder).Select(Path.GetFileName).OfType<string>().Where(IsCSource).Order(StringComparer.Ordinal)
            .SelectMany(name => ReadLinesIfPresent($"{folder}/{name}").SelectMany(line => Option.Matches(line)).Select(match => match.Value).Where(value => !Whitelisted.Contains(value)).Select(value => (value, $"{folder}/{name}")));

    private static bool IsCSource(string name) => name.EndsWith(".c", StringComparison.Ordinal) || name.EndsWith(".h", StringComparison.Ordinal);

    // scan_docs: every heading naming an option in backticks, leaving out CURL_DISABLE_TYPECHECK.
    private static HashSet<string> ScanDocuments(string root) =>
        new(
            ReadLinesIfPresent($"{root}/docs/{Documents}").Select(line => DocumentedOption.Match(line)).Where(match => match.Success).Select(match => match.Groups[1].Value)
                .Where(option => option != "CURL_DISABLE_TYPECHECK"),
            StringComparer.Ordinal);

    // open() failing without die(): the script then reads no lines.
    private static IEnumerable<string> ReadLinesIfPresent(string path) => File.Exists(path) ? File.ReadLines(path, Encoding.Latin1) : [];
}
