using System.Text;
using Libcurl.Misc.Version;

namespace Curl.Conformance;

/// <summary>
/// Interprets upstream's <c>tests/test1177.pl</c> (curl 8.22.0), which case 1177 runs as
/// <c>%SRCDIR/test1177.pl CURL_VERSION_INFO.3 CURL.H VERSION.C</c> to check that the feature names and
/// <c>CURL_VERSION_*</c> bits in <c>curl_version_info.3</c>, <c>curl.h</c> and <c>lib/version.c</c> agree, without
/// running Perl (BL-2993).
/// </summary>
/// <remarks>
/// <c>curl_version_info.3</c> is made from <c>curl_version_info.md</c> when curl is built and is not in the release
/// tarball, so a <c>.3</c> path that names no file is read as the <c>.md</c> beside it: each <c>## </c> heading, like
/// the man page's <c>.IP</c> lines, is a feature name, less its backticks and quotes, and <c>no name</c> names none. Its bits are
/// the words after <c> mask bit: </c> that start <c>CURL_VERSION_</c>, ignoring case, and <c>curl.h</c>'s are its
/// <c>#define CURL_VERSION_*</c> lines. <c>lib/version.c</c> is not vendored: <see cref="CurlVersion.FeatureNames"/>,
/// Libcurl's copy of its features table, stands for its <c>FEATURE("name"</c> lines, and the path is used only in
/// the script's messages. To standard error it writes, in the script's words and sorted, a header bit the man page
/// lacks, a man page bit the header lacks, a source feature name the man page lacks and a man page feature name
/// the source lacks; the exit code is the number of problems, modulo 256 as Perl's <c>exit</c> takes it. Parsing is
/// by hand, not with <c>Regex</c>, as elsewhere in this library.
/// </remarks>
internal static class UpstreamTest1177Script
{
    private const int ExitCodeMask = 255;

    private const string BitPrefix = "CURL_VERSION_";

    private const string MaskBit = " mask bit: ";

    private const string Define = "#define ";

    private const string Heading = "## ";

    private const string ManPageItem = ".IP ";

    private const string NoName = "no name";

    /// <summary>The file name of the upstream script this class interprets.</summary>
    public const string ScriptName = "test1177.pl";

    /// <summary>Runs the check over the files a case's command names, writing to the invocation's standard error.</summary>
    /// <param name="invocation">The command's words after the script: curl_version_info.3, curl.h and version.c.</param>
    /// <param name="runCurl">The in-process curl, which the script never runs.</param>
    /// <returns>The script's exit code.</returns>
    public static async Task<int> RunAsync(UpstreamCurlInvocation invocation, Func<UpstreamCurlInvocation, Task<int>> runCurl)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        (int exitCode, string standardError) = Check(invocation.Arguments[0], invocation.Arguments[1], invocation.Arguments[2], CurlVersion.FeatureNames);
        await invocation.StandardError.WriteAsync(Encoding.UTF8.GetBytes(standardError));
        return exitCode;
    }

    /// <summary>Checks the man page's bits and feature names against the header's bits and the source's feature names.</summary>
    /// <param name="manPage">The path of <c>curl_version_info.3</c>, as the script prints it; read as the <c>.md</c> beside it when absent.</param>
    /// <param name="header">The path of <c>include/curl/curl.h</c>.</param>
    /// <param name="source">The path of <c>lib/version.c</c>, used only in messages.</param>
    /// <param name="sourceNames">The feature names <c>lib/version.c</c>'s table lists.</param>
    /// <returns>The script's exit code and what it writes to standard error.</returns>
    public static (int ExitCode, string StandardError) Check(string manPage, string header, string source, IEnumerable<string> sourceNames)
    {
        string[] manLines = File.ReadAllLines(ManPageFile(manPage));
        HashSet<string> manBits = [.. manLines.Select(ManPageBit).OfType<string>()];
        HashSet<string> manNames = [.. manLines.Select(ManPageName).OfType<string>()];
        HashSet<string> headerBits = [.. File.ReadLines(header).Select(HeaderBit).OfType<string>()];
        HashSet<string> sourceNameSet = [.. sourceNames];
        List<string> problems =
        [
            .. Missing(headerBits, manBits).Select(bit => $"{manPage}: missing {bit}"),
            .. Missing(manBits, headerBits).Select(bit => $"{manPage}: {bit} is not in the header!"),
            .. Missing(sourceNameSet, manNames).Select(name => $"{manPage}: missing feature name {name}"),
            .. Missing(manNames, sourceNameSet).Select(name => $"{source}: missing feature name {name}"),
        ];
        return (problems.Count & ExitCodeMask, string.Concat(problems.Select(problem => problem + "\n")));
    }

    private static IEnumerable<string> Missing(HashSet<string> names, HashSet<string> from) =>
        names.Where(name => !from.Contains(name)).Order(StringComparer.Ordinal);

    private static string ManPageFile(string manPage) =>
        File.Exists(manPage) || !manPage.EndsWith(".3", StringComparison.Ordinal) ? manPage : Path.ChangeExtension(manPage, ".md");

    // / mask bit: (CURL_VERSION_[A-Z0-9_]+)/i.
    private static string? ManPageBit(string line)
    {
        int start = line.IndexOf(MaskBit, StringComparison.OrdinalIgnoreCase);
        return start < 0 ? null : BitAt(line, start + MaskBit.Length);
    }

    // The .md's "## `name`" heading, the .3's /^\.ip (.*)/i; "no name" names none.
    private static string? ManPageName(string line)
    {
        string name = HeadingText(line).Trim('`', '"');
        return name.Length == 0 || name == NoName ? null : name;
    }

    private static string HeadingText(string line) =>
        line.StartsWith(Heading, StringComparison.Ordinal) ? line[Heading.Length..]
        : line.StartsWith(ManPageItem, StringComparison.OrdinalIgnoreCase) ? line[ManPageItem.Length..]
        : string.Empty;

    // /^\#define (CURL_VERSION_[A-Z0-9_]+)/i.
    private static string? HeaderBit(string line) =>
        line.StartsWith(Define, StringComparison.OrdinalIgnoreCase) ? BitAt(line, Define.Length) : null;

    private static string? BitAt(string line, int start)
    {
        string word = new([.. line.Skip(start).TakeWhile(character => char.IsAsciiLetterOrDigit(character) || character == '_')]);
        return word.Length > BitPrefix.Length && word.StartsWith(BitPrefix, StringComparison.OrdinalIgnoreCase) ? word : null;
    }
}
