using System.Text;
using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// Interprets upstream's <c>tests/test1119.pl</c> (curl 8.22.0), which case 1119 runs as
/// <c>%SRCDIR/test1119.pl %SRCDIR/.. ../include/curl</c> to check that the public headers, the libcurl man pages and
/// <c>docs/libcurl/symbols-in-versions</c> name the same symbols, without running Perl or a C preprocessor (BL-2991).
/// </summary>
/// <remarks>
/// The symbols are, from every <c>.h</c> file of <c>include/curl</c> under the root, each <c>#define CURL...</c> or
/// <c>#define LIBCURL...</c> name and each <c>CURL</c> member of an <c>enum</c> block. Where the script reads the
/// enums from the C preprocessor's output, this reads the header with its comments and preprocessor lines removed
/// and a line that starts with a call of one of the headers' function-like macros (<c>CURLOPT(CURLOPT_URL, ...)</c>)
/// read from its first argument, which is what <c>CURLOPT</c> and <c>CURLOPTDEPRECATED</c> expand to first; every
/// <c>#if</c> branch is kept (ADR-0749). The man pages are the <c>.md</c> files of <c>docs/libcurl</c> and
/// <c>docs/libcurl/opts</c>, read for <c>CURL&lt;group&gt;_</c> names as the script reads them. To standard output it
/// writes, in the script's words and sorted order, a symbol <c>symbols-in-versions</c> lists twice, each header symbol
/// it lacks, each symbol it lists as present that no header has, and each man page name it lacks; then <c>OK</c> when
/// only the man page names were reported or nothing was. The exit code is always 0. The second word, the include
/// folder for the preprocessor, is not read. Lines are read as text lines (Latin-1, as Perl reads bytes), and a missing
/// file throws.
/// </remarks>
internal static class UpstreamTest1119Script
{
    private static readonly Regex EnumStart = new(@"enum\s+(\S+\s+)?\{", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex Define = new("^#define ((LIB|)CURL[A-Za-z0-9_]*)", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex FunctionLikeMacro = new(@"^\s*#\s*define\s+([A-Za-z_][A-Za-z0-9_]*)\(", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex MacroCall = new(@"^([A-Za-z_][A-Za-z0-9_]*)\(\s*", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex Comment = new(@"/\*.*?\*/|//[^\n]*", RegexOptions.CultureInvariant | RegexOptions.Singleline, UpstreamRegex.MatchTimeout);

    private static readonly Regex Formatting = new("(^|[^A-Z0-9])[*_]+", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex GlobalSymbol = new(
        @"\W(CURL(AUTH|E|H|MOPT|OPT|SHOPT|UE|M|SSH|SSLBACKEND|HEADER|FORM|FTP|PIPE|MIMEOPT|GSSAPI|ALTSVC|PROTO|PROXY|UPART|USESSL|_READFUNC|_WRITEFUNC|_CSELECT|_FORMADD|_IPRESOLVE|_REDIR|_RTSPREQ|_TIMECOND|_VERSION)_[a-zA-Z0-9_]+)",
        RegexOptions.ECMAScript,
        UpstreamRegex.MatchTimeout);

    private static readonly string[] IgnoredInfixes = ["OBSOLETE", "CURLE_RESERVED"];

    private static readonly string[] IgnoredPrefixes = ["CURL_EXTERN", "CURLINC_", "CURL_TEMP_"];

    private static readonly string[] IgnoredSuffixes = ["_LAST", "_LASTENTRY"];

    /// <summary>The file name of the upstream script this class interprets.</summary>
    public const string ScriptName = "test1119.pl";

    /// <summary>Runs the check over the source tree a case's command names, writing to the invocation's standard output.</summary>
    /// <param name="invocation">The command's words after the script: the source tree's root and the include folder.</param>
    /// <param name="runCurl">The in-process curl, which the script never runs.</param>
    /// <returns>The script's exit code, 0.</returns>
    public static async Task<int> RunAsync(UpstreamCurlInvocation invocation, Func<UpstreamCurlInvocation, Task<int>> runCurl)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        await invocation.StandardOutput.WriteAsync(Encoding.UTF8.GetBytes(Check(invocation.Arguments[0])));
        return 0;
    }

    /// <summary>Checks the headers and man pages against <c>symbols-in-versions</c>.</summary>
    /// <param name="root">The source tree's root, holding <c>include/curl</c> and <c>docs/libcurl</c>.</param>
    /// <returns>What the script writes to standard output.</returns>
    public static string Check(string root)
    {
        StringBuilder output = new();
        int misses = 0;
        (HashSet<string> listed, HashSet<string> removed) = ListedSymbols(root + "/docs/libcurl/symbols-in-versions", output, ref misses);
        HashSet<string> used = new(StringComparer.Ordinal);
        misses += HeaderSymbols(root + "/include/curl").Order(StringComparer.Ordinal).Where(symbol => !IsIgnored(symbol)).Sum(symbol => Use(symbol, listed, used, output));
        misses += AppendMissing(listed, used, removed, output);
        AppendManPageReferences(root, listed, output);
        return misses > 0 ? output.ToString() : output.Append("OK\n").ToString();
    }

    // Each symbol listed as present that no header has, under the script's two-line heading.
    private static int AppendMissing(HashSet<string> listed, HashSet<string> used, HashSet<string> removed, StringBuilder output)
    {
        List<string> missing = [.. listed.Order(StringComparer.Ordinal).Where(symbol => !used.Contains(symbol) && !removed.Contains(symbol))];
        if (missing.Count > 0)
        {
            output.Append("Missing symbols mentioned in symbols-in-versions\nAdd them to a header, or mark them as removed.\n");
            output.Append(string.Concat(missing.Select(symbol => symbol + "\n")));
        }

        return missing.Count;
    }

    // Each man page name the list lacks, once per place; these are not misses.
    private static void AppendManPageReferences(string root, HashSet<string> listed, StringBuilder output)
    {
        HashSet<(string, string)> warned = [];
        output.Append(string.Concat(
            ManPageReferences(root + "/docs/libcurl").Concat(ManPageReferences(root + "/docs/libcurl/opts"))
                .Where(reference => !listed.Contains(reference.Symbol) && warned.Add(reference))
                .Select(reference => $"{reference.Place}: {reference.Symbol} is not a public symbol\n")));
    }

    private static int Use(string symbol, HashSet<string> listed, HashSet<string> used, StringBuilder output)
    {
        if (listed.Contains(symbol))
        {
            used.Add(symbol);
            return 0;
        }

        output.Append(symbol).Append('\n');
        return 1;
    }

    private static bool IsIgnored(string symbol) =>
        IgnoredInfixes.Any(infix => symbol.Contains(infix, StringComparison.Ordinal))
        || IgnoredPrefixes.Any(prefix => symbol.StartsWith(prefix, StringComparison.Ordinal))
        || IgnoredSuffixes.Any(suffix => symbol.EndsWith(suffix, StringComparison.Ordinal));

    private static List<string> HeaderSymbols(string headerFolder)
    {
        List<string> headers = [.. Directory.EnumerateFiles(headerFolder).Where(path => path.EndsWith(".h", StringComparison.Ordinal)).Order(StringComparer.Ordinal)];
        HashSet<string> macros = new(headers.SelectMany(File.ReadLines).Select(line => FunctionLikeMacro.Match(line)).Where(match => match.Success).Select(match => match.Groups[1].Value), StringComparer.Ordinal);
        return [.. headers.SelectMany(header => EnumSymbols(File.ReadAllText(header, Encoding.Latin1), macros).Concat(DefinedSymbols(header)))];
    }

    // scanheader: each "#define CURL..." or "#define LIBCURL..." name.
    private static IEnumerable<string> DefinedSymbols(string header) =>
        File.ReadLines(header, Encoding.Latin1).Select(line => Define.Match(line)).Where(match => match.Success).Select(match => match.Groups[1].Value);

    // scanenum: every line from one matching /enum\s+(\S+\s+)?{/ through the next holding "}".
    private static List<string> EnumSymbols(string header, HashSet<string> macros)
    {
        List<string> symbols = [];
        bool inEnum = false;
        foreach (string line in PreprocessedLines(header))
        {
            inEnum = inEnum || EnumStart.IsMatch(line);
            if (inEnum)
            {
                symbols.AddRange(EnumMember(line, macros));
                inEnum = !line.Contains('}', StringComparison.Ordinal);
            }
        }

        return symbols;
    }

    // The preprocessor's view: comments gone and directives (with their continuation lines) dropped.
    private static IEnumerable<string> PreprocessedLines(string header)
    {
        bool continuesDirective = false;
        foreach (string line in Comment.Replace(header, match => new string('\n', match.Value.Count(character => character == '\n'))).Split('\n'))
        {
            bool directive = continuesDirective || line.TrimStart().StartsWith('#');
            continuesDirective = directive && line.TrimEnd().EndsWith('\\');
            if (!directive)
            {
                yield return line;
            }
        }
    }

    // s/^\s+//; next unless /^CURL/; s/[,\s].*//; with a leading macro call read from its first argument.
    private static IEnumerable<string> EnumMember(string line, HashSet<string> macros)
    {
        string member = line.TrimStart();
        Match call = MacroCall.Match(member);
        if (call.Success && macros.Contains(call.Groups[1].Value))
        {
            member = member[call.Length..];
        }

        if (member.StartsWith("CURL", StringComparison.Ordinal))
        {
            int end = member.IndexOfAny([',', ' ', '\t', '\r', '\f', '\v']);
            yield return end < 0 ? member : member[..end];
        }
    }

    // symbols-in-versions' /(^[^ \n]+) +(.*)/ lines: a symbol listed twice is reported, one with a third column removed.
    private static (HashSet<string> Listed, HashSet<string> Removed) ListedSymbols(string symbolsInVersions, StringBuilder output, ref int misses)
    {
        HashSet<string> listed = new(StringComparer.Ordinal);
        HashSet<string> removed = new(StringComparer.Ordinal);
        foreach (string line in File.ReadLines(symbolsInVersions, Encoding.Latin1))
        {
            int blank = line.IndexOf(' ', StringComparison.Ordinal);
            if (blank <= 0)
            {
                continue;
            }

            string symbol = line[..blank];
            if (!listed.Add(symbol))
            {
                output.Append($"Detected duplicate symbol: {symbol}\n");
                misses++;
            }
            else if (line[blank..].Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 2)
            {
                removed.Add(symbol);
            }
        }

        return (listed, removed);
    }

    // checkmanpage over every .md file of the folder, in sorted order.
    private static IEnumerable<(string Symbol, string Place)> ManPageReferences(string folder) =>
        Directory.EnumerateFiles(folder)
            .Select(path => Path.GetFileName(path))
            .Where(name => name.EndsWith(".md", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .SelectMany(name => File.ReadLines(folder + "/" + name, Encoding.Latin1).SelectMany((line, index) => LineReferences(line, $"{folder}/{name}:{index + 1}")));

    // The first formatting run becomes a blank, then each /\W(CURL<group>_...)/ is taken out in turn; a name
    // starting CURLE_OBSOLETE or CURLOPT_TEMPLATE, or ending in "_", is skipped.
    private static IEnumerable<(string Symbol, string Place)> LineReferences(string line, string place)
    {
        string rest = Formatting.Replace(line, " ", 1);
        for (Match match = GlobalSymbol.Match(rest); match.Success; match = GlobalSymbol.Match(rest))
        {
            string symbol = match.Groups[1].Value;
            rest = rest.Remove(match.Index, match.Length);
            if (!symbol.StartsWith("CURLE_OBSOLETE", StringComparison.Ordinal) && !symbol.StartsWith("CURLOPT_TEMPLATE", StringComparison.Ordinal) && !symbol.EndsWith('_'))
            {
                yield return (symbol, place);
            }
        }
    }
}
