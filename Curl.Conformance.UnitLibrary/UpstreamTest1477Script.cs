using System.Text;
using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// Interprets upstream's <c>tests/test1477.pl</c> (curl 8.22.0), which case 1477 runs as
/// <c>%SRCDIR/test1477.pl %SRCDIR/.. %PWD/..</c> to check that the public headers and the
/// <c>libcurl-errors</c> man page name the same error codes, without running Perl (BL-2991).
/// </summary>
/// <remarks>
/// It reads every <c>.h</c> file of <c>include/curl</c> under the first root in sorted order, taking each line that
/// starts with two blanks and a <c>CURLE_</c>, <c>CURLUE_</c>, <c>CURLSHE_</c>, <c>CURLHE_</c> or <c>CURLM_</c> name
/// that is not <c>OBSOLETE</c>, <c>CURLE_RESERVED</c> or <c>_LAST</c>, and writes <c>double: NAME</c> to standard
/// error for a name seen twice. The man page is <c>docs/libcurl/libcurl-errors.3</c> under the second root, each
/// <c>.IP "NAME</c> line but <c>CURLM_CALL_MULTI_SOCKET</c>; the release tarball has no <c>.3</c> file, which
/// upstream's build generates from <c>libcurl-errors.md</c> by turning each <c>## NAME</c> heading into
/// <c>.IP "NAME</c>, so without one the headings of <c>docs/libcurl/libcurl-errors.md</c> under the first root stand
/// for it (ADR-0749). To standard output it writes <c>Result</c>, each header name the man page lacks and each man
/// page name no header has, in sorted order and the script's words; the exit code is always 0.
/// </remarks>
internal static class UpstreamTest1477Script
{
    private const string CodeName = "(CURL(E|UE|SHE|HE|M)_[A-Z0-9_]*)";

    private static readonly Regex HeaderCode = new("^  " + CodeName, RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex ManPageCode = new("^\\.IP \"" + CodeName, RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex MarkdownCode = new("^## " + CodeName, RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    /// <summary>The file name of the upstream script this class interprets.</summary>
    public const string ScriptName = "test1477.pl";

    /// <summary>Runs the check over the two roots a case's command names, writing to the invocation's standard output and error.</summary>
    /// <param name="invocation">The command's words after the script: the source root and the build root.</param>
    /// <param name="runCurl">The in-process curl, which the script never runs.</param>
    /// <returns>The script's exit code, 0.</returns>
    public static async Task<int> RunAsync(UpstreamCurlInvocation invocation, Func<UpstreamCurlInvocation, Task<int>> runCurl)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        (string standardOutput, string standardError) = Check(invocation.Arguments[0], invocation.Arguments[1]);
        await invocation.StandardError.WriteAsync(Encoding.UTF8.GetBytes(standardError));
        await invocation.StandardOutput.WriteAsync(Encoding.UTF8.GetBytes(standardOutput));
        return 0;
    }

    /// <summary>Checks the headers' error codes against the man page's.</summary>
    /// <param name="root">The source root, holding <c>include/curl</c> (and <c>docs/libcurl/libcurl-errors.md</c>).</param>
    /// <param name="buildRoot">The build root, which may hold <c>docs/libcurl/libcurl-errors.3</c>.</param>
    /// <returns>What the script writes to standard output and standard error.</returns>
    public static (string StandardOutput, string StandardError) Check(string root, string buildRoot)
    {
        (List<string> headerNames, Dictionary<string, string> headerPlaces, string standardError) = ScanHeaders(root + "/include/curl");
        List<(string Name, string Place)> manPageNames = ScanManPage(root, buildRoot);
        Dictionary<string, string> manPagePlaces = PlacesByName(manPageNames);
        return (
            "Result\n"
            + Unmatched(headerNames, headerPlaces, manPagePlaces, "not in man page")
            + Unmatched(manPageNames.Select(entry => entry.Name), manPagePlaces, headerPlaces, "not in any header"),
            standardError);
    }

    // Each name the other side lacks, in sorted order, with where it was last seen.
    private static string Unmatched(IEnumerable<string> names, Dictionary<string, string> places, Dictionary<string, string> otherPlaces, string missingFrom) =>
        string.Concat(names.Order(StringComparer.Ordinal).Where(name => !otherPlaces.ContainsKey(name)).Select(name => $"{name} from {places[name]}, {missingFrom}\n"));

    private static Dictionary<string, string> PlacesByName(List<(string Name, string Place)> entries) =>
        entries.GroupBy(entry => entry.Name, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Last().Place, StringComparer.Ordinal);

    private static (List<string> Names, Dictionary<string, string> Places, string StandardError) ScanHeaders(string headerFolder)
    {
        List<string> names = [];
        Dictionary<string, string> places = new(StringComparer.Ordinal);
        StringBuilder standardError = new();
        foreach (string header in HeaderFiles(headerFolder))
        {
            ScanHeader(headerFolder + "/" + header, names, places, standardError);
        }

        return (names, places, standardError.ToString());
    }

    private static IEnumerable<string> HeaderFiles(string headerFolder) =>
        Directory.EnumerateFiles(headerFolder).Select(path => Path.GetFileName(path)).Where(name => name.EndsWith(".h", StringComparison.Ordinal)).Order(StringComparer.Ordinal);

    private static void ScanHeader(string file, List<string> names, Dictionary<string, string> places, StringBuilder standardError)
    {
        int lineNumber = 0;
        foreach (string line in File.ReadLines(file))
        {
            lineNumber++;
            Match match = HeaderCode.Match(line);
            if (match.Success && IsCountedHeaderName(match.Groups[1].Value))
            {
                string name = match.Groups[1].Value;
                names.Add(name);
                if (!places.TryAdd(name, $"{file}:{lineNumber}"))
                {
                    standardError.Append($"double: {name}\n");
                    places[name] = $"{file}:{lineNumber}";
                }
            }
        }
    }

    private static bool IsCountedHeaderName(string name) =>
        !name.Contains("OBSOLETE", StringComparison.Ordinal) && !name.Contains("CURLE_RESERVED", StringComparison.Ordinal) && !name.EndsWith("_LAST", StringComparison.Ordinal);

    private static List<(string Name, string Place)> ScanManPage(string root, string buildRoot)
    {
        string manPage = buildRoot + "/docs/libcurl/libcurl-errors.3";
        (string file, Regex pattern) = File.Exists(manPage) ? (manPage, ManPageCode) : (root + "/docs/libcurl/libcurl-errors.md", MarkdownCode);
        return [.. File.ReadLines(file)
            .Select((line, index) => (Match: pattern.Match(line), Place: $"{file}:{index + 1}"))
            .Where(entry => entry.Match.Success && !entry.Match.Groups[1].Value.Contains("CURLM_CALL_MULTI_SOCKET", StringComparison.Ordinal))
            .Select(entry => (entry.Match.Groups[1].Value, entry.Place))];
    }
}
