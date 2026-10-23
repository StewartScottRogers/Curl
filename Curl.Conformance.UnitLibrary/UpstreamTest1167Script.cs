using System.Text;
using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// Interprets upstream's <c>tests/test1167.pl</c> (curl 8.22.0), which case 1167 runs as <c>%SRCDIR/test1167.pl %SRCDIR/..</c>
/// to check that every public symbol of the headers in <c>include/curl</c> carries the <c>curl</c> prefix, without running
/// Perl or a C preprocessor (BL-3394).
/// </summary>
/// <remarks>
/// The symbols are, from every <c>.h</c> file of <c>include/curl</c> under the root in sorted order, each <c>enum</c> member
/// and then each <c>#define</c> name (a function-like macro with its parameter list, as the script reads it). Where the
/// script reads the enums from the C preprocessor's output, this reads the header with its comments and preprocessor lines
/// removed and a line that starts with a call of one of the headers' function-like macros (<c>CURLOPT(CURLOPT_URL, ...)</c>)
/// read from its first argument, which is what <c>CURLOPT</c> expands to first; every <c>#if</c> branch is kept (ADR-0749),
/// so the count of fine symbols can differ from a run of <c>cpp</c>. A symbol not starting <c>curl</c> or <c>libcurl</c>
/// in any case is reported under the script's heading with exit code 1; otherwise the count is written and the exit code
/// is 0. The verbose <c>-v</c> mode and the include folder for the preprocessor are not read. Lines are read as text lines
/// (Latin-1, as Perl reads bytes), and a missing folder throws.
/// </remarks>
internal static class UpstreamTest1167Script
{
    private static readonly Regex EnumStart = new(@"enum\s+(\S+\s+)?\{", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex Define = new(@"^ *# *define +([^ \n]*)", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex MacroCall = new(@"^([A-Za-z_][A-Za-z0-9_]*)\(\s*", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex Comment = new(@"/\*.*?\*/|//[^\n]*", RegexOptions.CultureInvariant | RegexOptions.Singleline, UpstreamRegex.MatchTimeout);

    private static readonly Regex Deprecated = new("CURL_DEPRECATED\\((?:\"[^\"]*\"|[^\")])*\\)", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly Regex CurlPrefix = new("^(lib|)curl", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, UpstreamRegex.MatchTimeout);

    private static readonly HashSet<string> NotSymbols = new(["", "typedef", "enum", "="], StringComparer.Ordinal);

    private static readonly Regex Digits = new(@"^\d+$", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    /// <summary>The file name of the upstream script this class interprets.</summary>
    public const string ScriptName = "test1167.pl";

    /// <summary>Runs the check over the source tree a case's command names, writing to the invocation's standard output.</summary>
    /// <param name="invocation">The command's words after the script: the source tree's root.</param>
    /// <param name="runCurl">The in-process curl, which the script never runs.</param>
    /// <returns>The script's exit code: 0, or 1 when a symbol lacks the prefix.</returns>
    public static async Task<int> RunAsync(UpstreamCurlInvocation invocation, Func<UpstreamCurlInvocation, Task<int>> runCurl)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        (string output, int exitCode) = Check(invocation.Arguments[0]);
        await invocation.StandardOutput.WriteAsync(Encoding.UTF8.GetBytes(output));
        return exitCode;
    }

    /// <summary>Checks the prefix of every public symbol of the headers.</summary>
    /// <param name="root">The source tree's root, holding <c>include/curl</c>.</param>
    /// <returns>What the script writes to standard output and its exit code.</returns>
    public static (string Output, int ExitCode) Check(string root)
    {
        List<string> headers = [.. Directory.EnumerateFiles(root + "/include/curl").Where(path => path.EndsWith(".h", StringComparison.Ordinal)).Order(StringComparer.Ordinal)];
        HashSet<string> macros = FunctionLikeMacros(headers);
        List<string> symbols = [.. headers.SelectMany(header => EnumSymbols(File.ReadAllText(header, Encoding.Latin1), macros).Concat(DefinedSymbols(header)))];
        List<string> bad = [.. symbols.Where(symbol => !CurlPrefix.IsMatch(symbol))];
        return bad.Count > 0
            ? ("Bad symbols in public header files:\n" + string.Concat(bad.Select(symbol => $"  {symbol}\n")), 1)
            : ($"{symbols.Count} fine symbols found\n", 0);
    }

    // The names of the headers' function-like macros: a defined name with a parameter list.
    private static HashSet<string> FunctionLikeMacros(List<string> headers) =>
        new(
            headers.SelectMany(DefinedSymbols).Where(symbol => symbol.Contains('(', StringComparison.Ordinal)).Select(symbol => symbol[..symbol.IndexOf('(', StringComparison.Ordinal)]),
            StringComparer.Ordinal);

    // scanheader: each "# define NAME" name, up to the first blank.
    private static IEnumerable<string> DefinedSymbols(string header) =>
        File.ReadLines(header, Encoding.Latin1).Select(line => Define.Match(line)).Where(match => match.Success).Select(match => match.Groups[1].Value);

    // scanenums: every line from one matching /enum\s+(\S+\s+)?{/ through the next holding "}".
    private static List<string> EnumSymbols(string header, HashSet<string> macros)
    {
        List<string> symbols = [];
        bool inEnum = false;
        foreach (string line in LogicalLines(header))
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
        string uncommented = Comment.Replace(header, match => new string('\n', match.Value.Count(character => character == '\n')));
        foreach (string line in Deprecated.Replace(uncommented, string.Empty).Split('\n'))
        {
            bool directive = continuesDirective || line.TrimStart().StartsWith('#');
            continuesDirective = directive && line.TrimEnd().EndsWith('\\');
            if (!directive)
            {
                yield return line;
            }
        }
    }

    // A macro call wrapped over several lines is one line to the preprocessor: lines are joined while a parenthesis is open.
    private static IEnumerable<string> LogicalLines(string header)
    {
        StringBuilder logical = new();
        int depth = 0;
        foreach (string line in PreprocessedLines(header))
        {
            logical.Append(line).Append(' ');
            depth += line.Count(character => character == '(') - line.Count(character => character == ')');
            if (depth <= 0)
            {
                yield return logical.ToString();
                logical.Clear();
                depth = 0;
            }
        }
    }

    // s/^\s+//; s/[,\s].*//; then not a closing brace, "typedef", "enum", "=", digits or empty; a leading macro call is read from its first argument.
    private static IEnumerable<string> EnumMember(string line, HashSet<string> macros)
    {
        string member = line.TrimStart();
        Match call = MacroCall.Match(member);
        if (call.Success && macros.Contains(call.Groups[1].Value))
        {
            member = member[call.Length..];
        }

        int end = member.IndexOfAny([',', ' ', '\t', '\r', '\f', '\v']);
        string symbol = end < 0 ? member : member[..end];
        if (IsSymbol(symbol))
        {
            yield return symbol;
        }
    }

    private static bool IsSymbol(string word) =>
        !word.Contains('}', StringComparison.Ordinal) && !NotSymbols.Contains(word) && !Digits.IsMatch(word);
}
