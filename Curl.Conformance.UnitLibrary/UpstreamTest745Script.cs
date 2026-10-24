using System.Text;
using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// Interprets upstream's <c>tests/test745.pl</c> (curl 8.22.0), which case 745 runs as <c>%SRCDIR/test745.pl %SRCDIR/..</c>
/// to check that the options <c>include/curl/typecheck-gcc.h</c> type-checks are the options <c>curl.h</c> and
/// <c>multi.h</c> declare with a pointer type, without running Perl (BL-3392).
/// </summary>
/// <remarks>
/// Type-checked options are each <c>CURL...</c> name after <c>(option) ==</c> in <c>typecheck-gcc.h</c>. Declared options
/// are the first <c>(CURLOPT..., CURLOPTTYPE_...</c> pair of each <c>curl.h</c> line whose type name holds none of
/// <c>LONG</c>, <c>VALUES</c>, <c>BLOB</c> or <c>OFF_T</c>, four options the script adds itself, and the same from
/// <c>multi.h</c> for <c>CURLMOPT...</c> with only <c>LONG</c> and <c>OFF_T</c> left out. To standard output it writes,
/// in sorted order, each type-checked option no header declares, then each declared option not type-checked, then
/// <c>OK</c> when there was neither. The exit code is always 0. The first word, the source tree's root, defaults to the
/// current folder. Lines are read as text lines (Latin-1, as Perl reads bytes), and a missing file throws.
/// </remarks>
internal static class UpstreamTest745Script
{
    private static readonly Regex TypeChecked = new(@"\(option\) == (CURL[^ \)]*)", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex CurlOption = new(@"\((CURLOPT[^,]*), (CURLOPTTYPE_[^,]*)", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex MultiOption = new(@"\((CURLMOPT[^,]*), (CURLOPTTYPE_[^,]*)", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly string[] AddedOptions =
    [
        "CURLOPT_SOCKS5_GSSAPI_SERVICE",
        "CURLOPT_CONV_FROM_NETWORK_FUNCTION",
        "CURLOPT_CONV_FROM_UTF8_FUNCTION",
        "CURLOPT_CONV_TO_NETWORK_FUNCTION",
    ];

    /// <summary>The file name of the upstream script this class interprets.</summary>
    public const string ScriptName = "test745.pl";

    /// <summary>Runs the check over the source tree a case's command names, writing to the invocation's standard output.</summary>
    /// <param name="invocation">The command's words after the script: the source tree's root, if any.</param>
    /// <param name="runCurl">The in-process curl, which the script never runs.</param>
    /// <returns>The script's exit code, 0.</returns>
    public static async Task<int> RunAsync(UpstreamCurlInvocation invocation, Func<UpstreamCurlInvocation, Task<int>> runCurl)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        await invocation.StandardOutput.WriteAsync(Encoding.UTF8.GetBytes(Check(RootOf(invocation.Arguments))));
        return 0;
    }

    /// <summary>Reads the source tree's root from the command's words: the first word when it is not empty, otherwise the current folder (<c>$ARGV[0] || "."</c>).</summary>
    /// <param name="arguments">The command's words after the script.</param>
    /// <returns>The root folder.</returns>
    public static string RootOf(IReadOnlyList<string> arguments) => arguments.Count > 0 && arguments[0].Length > 0 ? arguments[0] : ".";

    /// <summary>Checks <c>typecheck-gcc.h</c> against the options <c>curl.h</c> and <c>multi.h</c> declare.</summary>
    /// <param name="root">The source tree's root, holding <c>include/curl</c>.</param>
    /// <returns>What the script writes to standard output.</returns>
    public static string Check(string root)
    {
        string headers = root + "/include/curl/";
        HashSet<string> typeChecked = [.. Matches(headers + "typecheck-gcc.h", TypeChecked, _ => true)];
        HashSet<string> declared = DeclaredOptions(headers);
        return string.Concat(
            typeChecked.Order(StringComparer.Ordinal).Where(option => !declared.Contains(option)).Select(option => $"{option} is not in curl.h\n")
                .Concat(declared.Order(StringComparer.Ordinal).Where(option => !typeChecked.Contains(option)).Select(option => $"{option} is not checked in typecheck-gcc-h\n"))
                .DefaultIfEmpty("OK\n"));
    }

    private static HashSet<string> DeclaredOptions(string headers)
    {
        HashSet<string> declared = [.. Matches(headers + "curl.h", CurlOption, type => !HasAny(type, "LONG", "VALUES", "BLOB", "OFF_T")), .. AddedOptions];
        declared.UnionWith(Matches(headers + "multi.h", MultiOption, type => !HasAny(type, "LONG", "OFF_T")));
        return declared;
    }
    private static bool HasAny(string type, params string[] words) => words.Any(word => type.Contains(word, StringComparison.Ordinal));

    // The first match of each line, by its first group, when its second group (if the pattern has one) passes the filter.
    private static IEnumerable<string> Matches(string path, Regex pattern, Func<string, bool> acceptsType) =>
        File.ReadLines(path, Encoding.Latin1)
            .Select(line => pattern.Match(line))
            .Where(match => match.Success && acceptsType(match.Groups.Count > 2 ? match.Groups[2].Value : string.Empty))
            .Select(match => match.Groups[1].Value);
}
