using System.Text;
using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// Interprets upstream's <c>tests/test1135.pl</c> (curl 8.22.0), which cases 1135 and 1279 run as
/// <c>%SRCDIR/test1135.pl %SRCDIR/..</c> and <c>%SRCDIR/test1135.pl --heading=EXPORTS --sort %SRCDIR/..</c> to list the
/// functions the public headers declare with <c>CURL_EXTERN</c>, in declaration order or sorted, without running Perl (BL-3393).
/// </summary>
/// <remarks>
/// The headers are the <c>.h</c> files of <c>include/curl</c> under the root, in sorted order and read as Latin-1 text
/// lines, as Perl reads bytes. A <c>CURL_EXTERN</c> line gives the name that ends the text before its first <c>(</c>; one
/// without a <c>(</c> takes the name from the text before the last <c>(</c> of the next line. <c>--heading=&lt;text&gt;</c>
/// writes the text as a first line, <c>--sort</c> sorts the names, and the root defaults to <c>.</c>. The exit code is
/// always 0; a missing header folder or file throws.
/// </remarks>
internal static class UpstreamTest1135Script
{
    private static readonly Regex Deprecated = new(@"CURL_DEPRECATED\(.*""\)", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex Spaces = new(" {2,}", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex Name = new("[a-z_]+$", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    /// <summary>The file name of the upstream script this class interprets.</summary>
    public const string ScriptName = "test1135.pl";

    /// <summary>Runs the listing over the source tree a case's command names, writing to the invocation's standard output.</summary>
    /// <param name="invocation">The command's words after the script: options, then the source tree's root.</param>
    /// <param name="runCurl">The in-process curl, which the script never runs.</param>
    /// <returns>The script's exit code, 0.</returns>
    public static async Task<int> RunAsync(UpstreamCurlInvocation invocation, Func<UpstreamCurlInvocation, Task<int>> runCurl)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        StringBuilder output = new();
        bool sort = false;
        string root = ".";
        foreach (string argument in invocation.Arguments)
        {
            if (argument.StartsWith("--heading=", StringComparison.Ordinal))
            {
                output.Append(argument["--heading=".Length..]).Append('\n');
            }
            else if (argument.Contains("--sort", StringComparison.Ordinal))
            {
                sort = true;
            }
            else
            {
                root = argument;
                break;
            }
        }

        output.Append(string.Concat(List(root, sort).Select(name => name + "\n")));
        await invocation.StandardOutput.WriteAsync(Encoding.UTF8.GetBytes(output.ToString()));
        return 0;
    }

    /// <summary>Lists the <c>CURL_EXTERN</c> function names of the headers under a source tree's root.</summary>
    /// <param name="root">The source tree's root, holding <c>include/curl</c>.</param>
    /// <param name="sort">Whether to sort the names instead of keeping declaration order.</param>
    /// <returns>The names.</returns>
    public static List<string> List(string root, bool sort)
    {
        string folder = root + "/include/curl";
        List<string> names = [.. Directory.EnumerateFiles(folder)
            .Where(path => path.EndsWith(".h", StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path))
            .Order(StringComparer.Ordinal)
            .SelectMany(name => HeaderNames(folder + "/" + name))];
        return sort ? [.. names.Order(StringComparer.Ordinal)] : names;
    }

    private static IEnumerable<string> HeaderNames(string header)
    {
        string first = string.Empty;
        foreach (string raw in File.ReadLines(header, Encoding.Latin1))
        {
            (string? name, first) = ReadLine(Spaces.Replace(Deprecated.Replace(raw, string.Empty, 1), " "), first);
            if (name is not null)
            {
                yield return name;
            }
        }
    }

    // One header line: the name it completes, if any, and the first line of a declaration still waiting for its second.
    private static (string? Name, string First) ReadLine(string line, string first)
    {
        if (line.StartsWith("CURL_EXTERN ", StringComparison.Ordinal))
        {
            int open = line.IndexOf('(', StringComparison.Ordinal);
            return open < 0 ? (null, line) : (NameEnding(line[..open]), string.Empty);
        }

        if (first.Length == 0)
        {
            return (null, string.Empty);
        }

        int last = line.LastIndexOf('(');
        return (last < 0 ? null : NameEnding(first + line[..last].TrimStart(' ')), string.Empty);
    }

    private static string? NameEnding(string text) => Name.Match(text) is { Success: true } match ? match.Value : null;
}
