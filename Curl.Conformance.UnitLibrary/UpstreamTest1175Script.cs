using System.Text;
using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// Interprets upstream's <c>tests/test1175.pl</c> (curl 8.22.0), which case 1175 runs as
/// <c>%SRCDIR/test1175.pl %SRCDIR/..</c> to check that <c>docs/libcurl/symbols-in-versions</c> and
/// <c>docs/libcurl/libcurl-errors.md</c> name the same <c>CURLE_*</c> and <c>CURLM_*</c> codes, without running
/// Perl (BL-2991).
/// </summary>
/// <remarks>
/// A code counts from <c>symbols-in-versions</c> unless the column after its first version starts with a version
/// (the script's "removed!"), and from <c>libcurl-errors.md</c> for each <c>## CURLE_</c> or <c>## CURLM_</c> heading
/// that does not name an <c>OBSOLETE</c> code. To standard output it writes, in the script's words and in sorted
/// order, each code the documentation lacks and then each documented code <c>symbols-in-versions</c> lacks; the exit
/// code is always 0. Lines are read as text lines, and a missing file throws.
/// </remarks>
internal static class UpstreamTest1175Script
{
    private static readonly Regex SymbolLine = new(@"^(CURL[EM]_[^ \t]*)[ \t]*([0-9.]+)[ \t]*(.*)", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex DocumentedCode = new("^## (CURL[EM]_[^ ]*)", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    /// <summary>The file name of the upstream script this class interprets.</summary>
    public const string ScriptName = "test1175.pl";

    /// <summary>Runs the check over the source tree a case's command names, writing to the invocation's standard output.</summary>
    /// <param name="invocation">The command's words after the script: the source tree's root.</param>
    /// <param name="runCurl">The in-process curl, which the script never runs.</param>
    /// <returns>The script's exit code, 0.</returns>
    public static async Task<int> RunAsync(UpstreamCurlInvocation invocation, Func<UpstreamCurlInvocation, Task<int>> runCurl)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        await invocation.StandardOutput.WriteAsync(Encoding.UTF8.GetBytes(Check(invocation.Arguments[0])));
        return 0;
    }

    /// <summary>Checks the error codes of <c>symbols-in-versions</c> against <c>libcurl-errors.md</c>.</summary>
    /// <param name="root">The source tree's root, holding <c>docs/libcurl</c>.</param>
    /// <returns>What the script writes to standard output.</returns>
    public static string Check(string root)
    {
        string libcurlDocs = Path.Combine(root, "docs", "libcurl");
        HashSet<string> listed = [.. File.ReadLines(Path.Combine(libcurlDocs, "symbols-in-versions")).Select(ListedCode).OfType<string>()];
        HashSet<string> documented = [.. File.ReadLines(Path.Combine(libcurlDocs, "libcurl-errors.md")).Select(DocumentedCodeOf).OfType<string>()];
        return string.Concat(
            listed.Except(documented).Order(StringComparer.Ordinal).Select(code => $"{code} is not in libcurl-errors.md\n")
                .Concat(documented.Except(listed).Order(StringComparer.Ordinal).Select(code => $"{code} is not in symbols-in-versions\n")));
    }

    // getincludeerrors: a code whose rest starts with a version was removed.
    private static string? ListedCode(string line)
    {
        Match match = SymbolLine.Match(line);
        return match.Success && !StartsWithVersion(match.Groups[3].Value) ? match.Groups[1].Value : null;
    }

    private static bool StartsWithVersion(string rest) => rest.Length > 0 && (char.IsAsciiDigit(rest[0]) || rest[0] == '.');

    // getdocserrors: an OBSOLETE heading names no code.
    private static string? DocumentedCodeOf(string line)
    {
        Match match = DocumentedCode.Match(line);
        return match.Success && !match.Groups[1].Value.Contains("OBSOLETE", StringComparison.Ordinal) ? match.Groups[1].Value : null;
    }
}
