using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Interprets upstream's <c>tests/test1139.pl</c> (curl 8.22.0), which case 1139 runs as
/// <c>%SRCDIR/test1139.pl %SRCDIR/.. %PWD/..</c> to check that every libcurl option in
/// <c>docs/libcurl/symbols-in-versions</c> has its man page and its mention in its index page, and that
/// <c>src/tool_getparam.c</c>, <c>curl.1</c> and <c>src/tool_listhelp.c</c> name the same command-line options,
/// without running Perl (BL-2992).
/// </summary>
/// <remarks>
/// It reads what the script reads: the <c>#define CURLOPT_/CURLINFO_/CURLMOPT_</c> aliases of
/// <c>ROOT/include/curl/curl.h</c>; the <c>CURLOPT_</c>, <c>CURLINFO_</c> and <c>CURLMOPT_</c> lines of
/// <c>symbols-in-versions</c>, each neither an alias nor removed needing <c>docs/libcurl/opts/NAME.md</c>; the
/// <c>## </c> headings of <c>curl_easy_setopt.md</c>, <c>curl_easy_getinfo.md</c> and <c>curl_multi_setopt.md</c>,
/// which must be in order and mention each such option; the <c>aliases</c> table of <c>tool_getparam.c</c>; the
/// <c>.IP "-o, --option"</c> lines of <c>curl.1</c>; and the <c>  { "</c> lines of <c>tool_listhelp.c</c>.
/// <c>curl.1</c> is generated at build time and is in no release tarball, so when neither
/// <c>BUILDROOT/docs/cmdline-opts/curl.1</c> nor <c>ROOT/docs/cmdline-opts/curl.1</c> exists it stands in for the
/// page with the <c>.IP</c> line upstream's <c>managen</c> writes for each <c>docs/cmdline-opts/*.md</c> file with a
/// <c>Long:</c> line in its front matter (ADR-0750). To standard error it writes, in the script's words, each
/// problem it finds and then their count; the options not in all three places are written in ordinal order, where the
/// script's hash order is undefined. A missing file throws, as the script's <c>die</c>. Parsing is by hand, not with
/// <c>Regex</c>, as elsewhere in this library.
/// </remarks>
internal static class UpstreamTest1139Script
{
    private const int InAllThree = 7;

    private const string HeadingPrefix = "## ";

    private const string DefinePrefix = "#define ";

    private static readonly string[] OptionKinds = ["OPT", "INFO", "MOPT"];

    // The CURLINFO_* defines of the debug callback, which share the getinfo prefix by mistake.
    private static readonly string[] DebugCallbackInfos =
        ["CURLINFO_DATA_IN", "CURLINFO_DATA_OUT", "CURLINFO_END", "CURLINFO_HEADER_IN", "CURLINFO_HEADER_OUT", "CURLINFO_LASTONE", "CURLINFO_NONE", "CURLINFO_SSL_DATA_IN", "CURLINFO_SSL_DATA_OUT", "CURLINFO_TEXT"];

    // The script's %opts: options pretended to be in tool_getparam.c (1) or in curl.1 and tool_listhelp.c (6).
    private static readonly (string Option, int Where)[] PretendedOptions =
    [
        ("--no-alpn", 1), ("--no-npn", 1), ("-N, --no-buffer", 1), ("--no-sessionid", 1), ("--no-keepalive", 1), ("--no-progress-meter", 1), ("--no-clobber", 1),
        ("--alpn", 6), ("--npn", 6), ("--eprt", 6), ("--epsv", 6), ("--keepalive", 6), ("-N, --buffer", 6), ("--sessionid", 6), ("--progress-meter", 6), ("--clobber", 6),
        ("--krb4", 6), ("--ftp-ssl", 6), ("--ftp-ssl-reqd", 6), ("--include", 6),
        ("--test-duphandle", 6), ("--test-event", 6), ("--wdebug", 6),
    ];

    /// <summary>The file name of the upstream script this class interprets.</summary>
    public const string ScriptName = "test1139.pl";

    /// <summary>Runs the check over the folders a case's command names, writing to the invocation's standard error.</summary>
    /// <param name="invocation">The command's words after the script: the release root and the build root.</param>
    /// <param name="runCurl">The in-process curl, which the script never runs.</param>
    /// <returns>The script's exit code, always 0: the case reads the count it writes.</returns>
    public static async Task<int> RunAsync(UpstreamCurlInvocation invocation, Func<UpstreamCurlInvocation, Task<int>> runCurl)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        await invocation.StandardError.WriteAsync(Encoding.UTF8.GetBytes(Check(invocation.Arguments[0], invocation.Arguments[1])));
        return 0;
    }

    /// <summary>Checks the libcurl option pages and the command-line option lists.</summary>
    /// <param name="root">The release root, beside <c>docs</c>, <c>include</c> and <c>src</c>, as the script prints it.</param>
    /// <param name="buildRoot">The build root, where a built <c>docs/cmdline-opts/curl.1</c> is looked for first.</param>
    /// <returns>What the script writes to standard error.</returns>
    public static string Check(string root, string buildRoot)
    {
        List<string> problems = LibcurlProblems(root);
        int errors = problems.Count;
        List<string> misplaced = CommandLineProblems(root, buildRoot, problems);
        return string.Concat(problems.Select(problem => problem + "\n")) + $"{errors + misplaced.Count}\n";
    }

    // The option pages symbols-in-versions needs and the index pages' order and mentions.
    private static List<string> LibcurlProblems(string root)
    {
        HashSet<string> aliases = Aliases($"{root}/include/curl/curl.h");
        List<(string Option, string Kind)> symbols = [.. File.ReadLines($"{root}/docs/libcurl/symbols-in-versions").Select(SymbolLine).OfType<(string, string)>().Where(symbol => !aliases.Contains(symbol.Item1))];
        List<string> problems = [.. symbols.Where(symbol => !File.Exists($"{root}/docs/libcurl/opts/{symbol.Option}.md")).Select(symbol => $"Missing {symbol.Option}.md")];
        problems.AddRange(IndexPageProblems($"{root}/docs/libcurl/curl_easy_setopt.md", symbols, "OPT"));
        problems.AddRange(IndexPageProblems($"{root}/docs/libcurl/curl_easy_getinfo.md", symbols, "INFO"));
        problems.AddRange(IndexPageProblems($"{root}/docs/libcurl/curl_multi_setopt.md", symbols, "MOPT"));
        return problems;
    }

    // Adds the alpha-order notes and the options not in all three places to the problems; returns the latter.
    private static List<string> CommandLineProblems(string root, string buildRoot, List<string> problems)
    {
        Dictionary<string, int> where = new(StringComparer.Ordinal);
        Array.ForEach(PretendedOptions, pretended => where[pretended.Option] = pretended.Where);
        problems.AddRange(GetParamProblems(File.ReadLines($"{root}/src/tool_getparam.c"), where));
        Mark(where, ManPageLines(root, buildRoot).Select(line => line.Replace("\\-", "-", StringComparison.Ordinal)).Select(ManPageOption), 2);
        Mark(where, File.ReadLines($"{root}/src/tool_listhelp.c").Select(HelpListOption), 4);
        List<string> misplaced = [.. where.Where(entry => entry.Value != InAllThree).Select(entry => entry.Key).Order(StringComparer.Ordinal)];
        problems.AddRange(misplaced.Select(option => $"{option} is not in{Places(~where[option])} (but in{Places(where[option])})"));
        return misplaced;
    }

    // /^\#define (CURL(OPT|INFO|MOPT)_\w+) (.*)/ with a true value, beside the debug callback's CURLINFO_* names.
    private static HashSet<string> Aliases(string curlHeader) =>
        [.. DebugCallbackInfos.Concat(File.ReadLines(curlHeader).Select(AliasName).OfType<string>())];

    private static string? AliasName(string line)
    {
        int start = DefinePrefix.Length;
        if (!line.StartsWith(DefinePrefix, StringComparison.Ordinal) || OptionKindOf(line, start) is null)
        {
            return null;
        }

        int end = start + WordLength(line, start);
        return HasTrueValueAfter(line, end) ? line[start..end] : null;
    }

    // " (.*)" with a true value: a blank right after the name, then a value Perl reads as true.
    private static bool HasTrueValueAfter(string line, int end) =>
        end < line.Length && line[end] == ' ' && IsTrue(line[(end + 1)..]);

    // /(CURL(OPT|INFO|MOPT)_\w+) *([0-9.]*) *([0-9.-]*) *([0-9.]*)/, anywhere on the line; null for a removed option.
    private static (string Option, string Kind)? SymbolLine(string line)
    {
        int start = Enumerable.Range(0, line.Length).FirstOrDefault(index => OptionKindOf(line, index) is not null, -1);
        if (start < 0)
        {
            return null;
        }

        int end = start + WordLength(line, start);
        (_, int afterAdded) = FieldAt(line, end, "0123456789.");
        (_, int afterDeprecated) = FieldAt(line, afterAdded, "0123456789.-");
        (string removed, _) = FieldAt(line, afterDeprecated, "0123456789.");
        return IsTrue(removed) ? null : (line[start..end], OptionKindOf(line, start)!);
    }

    // " *([CHARACTERS]*)" from the position: the field and where it ends.
    private static (string Field, int End) FieldAt(string line, int position, string characters)
    {
        int start = position + line.Skip(position).TakeWhile(character => character == ' ').Count();
        string field = new([.. line.Skip(start).TakeWhile(character => characters.Contains(character, StringComparison.Ordinal))]);
        return (field, start + field.Length);
    }

    // CURL, then OPT_, INFO_ or MOPT_ and at least one word character, at the index; its kind, or null.
    private static string? OptionKindOf(string text, int index) =>
        text.AsSpan(index).StartsWith("CURL", StringComparison.Ordinal)
            ? OptionKinds.FirstOrDefault(kind => text.AsSpan(index + 4).StartsWith(kind + "_", StringComparison.Ordinal) && index + 5 + kind.Length < text.Length && IsWordCharacter(text[index + 5 + kind.Length]))
            : null;

    // scanmdpage: the headings in order, then a heading mentioning each option as a whole word.
    private static IEnumerable<string> IndexPageProblems(string page, List<(string Option, string Kind)> symbols, string kind)
    {
        IEnumerable<string> options = symbols.Where(symbol => symbol.Kind == kind).Select(symbol => symbol.Option);
        List<string> headings = [.. File.ReadLines(page).Where(line => line.StartsWith(HeadingPrefix, StringComparison.Ordinal)).Select(line => line[HeadingPrefix.Length..].Replace("\\-", "-", StringComparison.Ordinal))];
        List<string> sorted = [.. headings.Order(StringComparer.Ordinal)];
        int unsorted = Enumerable.Range(0, headings.Count).FirstOrDefault(index => headings[index] != sorted[index], -1);
        IEnumerable<string> order = unsorted < 0 ? [] : [$"{page}:1:ERROR: {headings[unsorted]} is not alphabetical (expected {sorted[unsorted]})"];
        return order.Concat(options.Where(option => !headings.Any(heading => MentionsWord(heading, option))).Select(option => $"Missing mention of {option} in {page}"));
    }

    // /$m\b/: the option, followed by the end or a character that is not a word character.
    private static bool MentionsWord(string heading, string option) =>
        Enumerable.Range(0, heading.Length).Any(index => heading.AsSpan(index).StartsWith(option, StringComparison.Ordinal)
            && (index + option.Length == heading.Length || !IsWordCharacter(heading[index + option.Length])));

    // The aliases table: /^  \{(\"[^,]*\").*\'(.)\',/ after the "struct LongShort aliases" line.
    private static List<string> GetParamProblems(IEnumerable<string> source, Dictionary<string, int> where)
    {
        List<string> problems = [];
        string previous = string.Empty;
        bool inList = false;
        int number = 0;
        foreach (string line in source)
        {
            number++;
            if (inList && GetParamEntry(line) is { } entry)
            {
                problems.AddRange(GetParamEntryProblems(where, entry.Quoted, entry.Short, previous, number));
                previous = entry.Quoted;
            }

            // The table's own line is never an entry: it does not start with '  {"'.
            inList |= line.Contains("struct LongShort aliases", StringComparison.Ordinal);
        }

        return problems;
    }

    // Marks the entry's "-s, --long" title as in tool_getparam.c, noting an entry out of alphabetical order.
    private static IEnumerable<string> GetParamEntryProblems(Dictionary<string, int> where, string quoted, char shortOption, string previous, int number)
    {
        string title = $"--{quoted[1..^1]}";
        Mark(where, [shortOption == ' ' ? title : $"-{shortOption}, {title}"], 1);
        return string.CompareOrdinal(quoted, previous) < 0 ? [$"tool_getparam.c:{number}: '{quoted}' is NOT placed in alpha-order"] : [];
    }

    private static (string Quoted, char Short)? GetParamEntry(string line)
    {
        if (!line.StartsWith("  {\"", StringComparison.Ordinal))
        {
            return null;
        }

        int comma = line.IndexOf(',', StringComparison.Ordinal);
        int close = line.LastIndexOf('"', (comma < 0 ? line.Length : comma) - 1);
        int quote = close > 3 ? line.LastIndexOf("',", StringComparison.Ordinal) : -1;
        return quote - 2 > close && line[quote - 2] == '\'' ? (line[3..(close + 1)], line[quote - 1]) : null;
    }

    // The built curl.1, else the .IP line managen writes for each option file (ADR-0750).
    private static IEnumerable<string> ManPageLines(string root, string buildRoot)
    {
        string? built = new[] { $"{buildRoot}/docs/cmdline-opts/curl.1", $"{root}/docs/cmdline-opts/curl.1" }.FirstOrDefault(File.Exists);
        return built is not null
            ? File.ReadLines(built)
            : Directory.EnumerateFiles($"{root}/docs/cmdline-opts", "*.md").Order(StringComparer.Ordinal).Select(ManagenOptionLine).OfType<string>();
    }

    private static string? ManagenOptionLine(string optionFile)
    {
        List<string> lines = [.. File.ReadLines(optionFile)];
        List<string> frontMatter = lines.FirstOrDefault() == "---" ? [.. lines.Skip(1).TakeWhile(line => line != "---")] : [];
        string? longName = FrontMatterValue(frontMatter, "Long: ");
        string? shortName = FrontMatterValue(frontMatter, "Short: ");
        return longName is null ? null : $".IP \"{(shortName is null ? string.Empty : $"-{shortName}, ")}--{longName}\"";
    }

    private static string? FrontMatterValue(List<string> frontMatter, string prefix) =>
        frontMatter.Where(line => line.StartsWith(prefix, StringComparison.Ordinal)).Select(line => line[prefix.Length..].Trim()).FirstOrDefault();

    // /^\.IP \"(-[^\"]*)\"/, then the option in it.
    private static string? ManPageOption(string line)
    {
        const string Prefix = ".IP \"-";
        int close = line.StartsWith(Prefix, StringComparison.Ordinal) ? line.IndexOf('"', Prefix.Length) : -1;
        return close < 0 ? null : OptionCombination(line[(Prefix.Length - 1)..close]);
    }

    // /^  \{ \" *(.*)/, then the option in it.
    private static string? HelpListOption(string line) =>
        line.StartsWith("  { \"", StringComparison.Ordinal) ? OptionCombination(line[5..].TrimStart(' ')) : null;

    // /^-(.), --([a-z0-9.-]*)/ gives "-o, --option", else /^--([a-z0-9.-]*)/ gives "--option".
    private static string? OptionCombination(string text)
    {
        if (text.Length >= 6 && text[0] == '-' && text.AsSpan(2).StartsWith(", --", StringComparison.Ordinal))
        {
            return $"-{text[1]}, --{OptionName(text, 6)}";
        }

        return text.StartsWith("--", StringComparison.Ordinal) ? $"--{OptionName(text, 2)}" : null;
    }

    private static string OptionName(string text, int start) =>
        new([.. text.Skip(start).TakeWhile(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character is '.' or '-')]);

    private static void Mark(Dictionary<string, int> where, IEnumerable<string?> options, int place)
    {
        foreach (string option in options.OfType<string>())
        {
            where[option] = where.GetValueOrDefault(option) | place;
        }
    }

    private static string Places(int where) =>
        string.Concat(new[] { (1, " tool_getparam.c"), (2, " curl.1"), (4, " tool_listhelp.c") }.Where(place => (where & place.Item1) != 0).Select(place => place.Item2));

    // The one place word characters are counted, so the delegate the method group makes is cached once.
    private static int WordLength(string text, int start) => text.Skip(start).TakeWhile(IsWordCharacter).Count();

    private static bool IsWordCharacter(char character) => char.IsAsciiLetterOrDigit(character) || character == '_';

    // Perl's truth: not empty and not "0".
    private static bool IsTrue(string text) => text.Length > 0 && text != "0";
}
