using System.Text;
using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// Interprets upstream's <c>tests/test1173.pl</c> (curl 8.22.0), which case 1173 runs as
/// <c>%SRCDIR/test1173.pl %SRCDIR/../docs/libcurl/symbols-in-versions</c> followed by the built <c>.1</c> and <c>.3</c>
/// pages, to scan the man pages for common formatting mistakes, without running Perl (BL-3411).
/// </summary>
/// <remarks>
/// The pages are not the command's page arguments (shell patterns no one expands) but the ones
/// <see cref="UpstreamCd2nroff"/> writes from the Markdown beside the <c>symbols-in-versions</c> file and its
/// <c>opts</c> folder, as a build would (ADR-0752, ADR-0803): the folder's pages and then the <c>opts</c> pages, each in
/// ordinal order. The <c>.1</c> pages (the command line tool's, made by a manual generator Curl does not have) are not
/// scanned; a reference to <c>curl.1</c> or <c>curl-config.1</c> is present, as in a build, and any other section 1
/// reference is broken. Each finding goes to standard error in the script's words, with the page's path and line; the
/// exit code is the count of those the script counts (some it only prints), and <c>ok</c> is written when there are
/// none. Where <c>symbols-in-versions</c> lacks <c>CURLALTSVC_H1</c> the script writes its complaint and exits 0. A
/// missing file throws.
/// </remarks>
internal static class UpstreamTest1173Script
{
    private const RegexOptions Options = RegexOptions.CultureInvariant;

    private const RegexOptions Insensitive = RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;

    private static readonly Regex SymbolLine = new("^([^ ]*) +(.*)", Options, UpstreamRegex.MatchTimeout);

    private static readonly Regex DeprecatedInfo = new("([0-9.]+) +([0-9.]+)", Options, UpstreamRegex.MatchTimeout);

    private static readonly Regex LibcurlPage = new(@"[/\\](CURL|curl_)([^/\\]*).3", Options, UpstreamRegex.MatchTimeout);

    private static readonly Regex Referral = new("^.so ", Options, UpstreamRegex.MatchTimeout);

    private static readonly Regex SynopsisHeading = new(@"^\.SH SYNOPSIS", Insensitive, UpstreamRegex.MatchTimeout);

    private static readonly Regex ExampleHeading = new(@"^\.SH EXAMPLE", Insensitive, UpstreamRegex.MatchTimeout);

    private static readonly Regex SeeAlsoHeading = new("^\\.SH \"SEE ALSO\"", Insensitive, UpstreamRegex.MatchTimeout);

    private static readonly Regex AnyHeading = new(@"^\.SH", Insensitive, UpstreamRegex.MatchTimeout);

    private static readonly Regex SeeAlsoLine = new(@"^\.BR (.*)", Insensitive, UpstreamRegex.MatchTimeout);

    private static readonly Regex CurlName = new("^(lib|)curl", Insensitive, UpstreamRegex.MatchTimeout);

    private static readonly Regex SeeAlsoReference = new(@"([a-z_0-9-]*) \(([13])\)([, ]*)", Insensitive, UpstreamRegex.MatchTimeout);

    private static readonly Regex Blank = new("^ *$", Options, UpstreamRegex.MatchTimeout);

    // The patterns that start with .* are anchored with ^, which finds the same matches in a line (no line holds a
    // line feed): unanchored, a line with no match is tried again from every position, quadratic in its length (BL-3420).
    private static readonly Regex TrailingComma = new(@"^.*(, *)\z", Options, UpstreamRegex.MatchTimeout);

    private static readonly Regex RawNewline = new(@"[^\\]\\n", Options, UpstreamRegex.MatchTimeout);

    private static readonly Regex SectionHeading = new(@"^\.SH ([^\r\n]*)", Insensitive, UpstreamRegex.MatchTimeout);

    private static readonly Regex Quoted = new("\"(.*)\"\\z", Options, UpstreamRegex.MatchTimeout);

    private static readonly Regex Bolded = new(@"\\f([BI])(.*)", Options, UpstreamRegex.MatchTimeout);

    private static readonly Regex FontReference = new(@"\\f([BI])((lib|)curl[a-z_0-9-]*)\(([13])\)", Insensitive, UpstreamRegex.MatchTimeout);

    private static readonly Regex BareReference = new(@"\\f([BI])((libcurl|CURLOPT_|CURLSHOPT_|CURLINFO_|CURLMOPT_|curl_easy_|curl_multi_|curl_url|curl_mime|curl_global|curl_share)[a-zA-Z_0-9-]+)(.)", Options, UpstreamRegex.MatchTimeout);

    private static readonly Regex OtherFont = new(@"^(.*)\\f([^BIP])", Options, UpstreamRegex.MatchTimeout);

    private static readonly Regex ProseSection = new("^(DESCRIPTION|RETURN VALUE|AVAILABILITY)", Insensitive, UpstreamRegex.MatchTimeout);

    private static readonly Regex UnreferencedCallPattern = new("^(.*)((curl_multi|curl_easy|curl_url|curl_global|curl_url|curl_share)[a-zA-Z_0-9-]+)", Options, UpstreamRegex.MatchTimeout);

    private static readonly Regex ExcludedSection = new("^(SYNOPSIS|EXAMPLE|NAME|SEE ALSO)", Insensitive, UpstreamRegex.MatchTimeout);

    private static readonly Regex OptionSymbol = new("^(.*)(CURL(OPT_|MOPT_|INFO_|SHOPT_)[A-Z0-9_]*)", Options, UpstreamRegex.MatchTimeout);

    private static readonly Regex TrailingSpace = new("[ \t]+$", Options, UpstreamRegex.MatchTimeout);

    private static readonly string[] OptionOrder = ["NAME", "SYNOPSIS", "DESCRIPTION", "PROTOCOLS", "EXAMPLE", "AVAILABILITY", "RETURN VALUE", "SEE ALSO"];

    private static readonly string[] FunctionOrder = ["NAME", "SYNOPSIS", "DESCRIPTION", "EXAMPLE", "AVAILABILITY", "RETURN VALUE", "SEE ALSO"];

    private static readonly string[] NotOptions =
    [
        "CURLINFO_TEXT", "CURLINFO_HEADER_IN", "CURLINFO_HEADER_OUT", "CURLINFO_DATA_IN", "CURLINFO_DATA_OUT",
        "CURLINFO_SSL_DATA_IN", "CURLINFO_SSL_DATA_OUT",
    ];

    /// <summary>The file name of the upstream script this class interprets.</summary>
    public const string ScriptName = "test1173.pl";

    /// <summary>Runs the check over the symbols file a case's command names, writing to the invocation's standard error.</summary>
    /// <param name="invocation">The command's words after the script: the <c>symbols-in-versions</c> path, then the page patterns.</param>
    /// <param name="runCurl">The in-process curl, which the script never runs.</param>
    /// <returns>The script's exit code: the count of the findings it counts.</returns>
    public static async Task<int> RunAsync(UpstreamCurlInvocation invocation, Func<UpstreamCurlInvocation, Task<int>> runCurl)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        (int exitCode, string standardError) = Check(invocation.Arguments[0]);
        await invocation.StandardError.WriteAsync(Encoding.UTF8.GetBytes(standardError));
        return exitCode;
    }

    /// <summary>Scans the libcurl man pages written from the Markdown beside a <c>symbols-in-versions</c> file.</summary>
    /// <param name="symbolsInVersions">The path of <c>docs/libcurl/symbols-in-versions</c>.</param>
    /// <returns>The script's exit code and what it writes to standard error.</returns>
    public static (int ExitCode, string StandardError) Check(string symbolsInVersions)
    {
        Dictionary<string, string> deprecated = new(StringComparer.Ordinal) { ["CURLOPT_EGDSOCKET"] = "1", ["CURLOPT_RANDOM_FILE"] = "1" };
        foreach (string notOption in NotOptions)
        {
            deprecated[notOption] = "1";
        }

        HashSet<string> symbols = ReadSymbols(symbolsInVersions, deprecated);
        if (!symbols.Contains("CURLALTSVC_H1"))
        {
            return (0, "did not get the symbols-in-version!\n");
        }

        string libcurlFolder = Path.GetDirectoryName(symbolsInVersions)!.Replace('\\', '/');
        return Scan(libcurlFolder, UpstreamCd2nroff.ConvertFolder(libcurlFolder), deprecated);
    }

    /// <summary>Scans man pages the way the script does.</summary>
    /// <param name="folder">The folder the pages' paths are shown under.</param>
    /// <param name="pages">Each page's lines by its path in the folder, <c>NAME.3</c> or <c>opts/NAME.3</c>.</param>
    /// <param name="deprecated">The symbols marked deprecated.</param>
    /// <returns>The script's exit code and what it writes to standard error.</returns>
    public static (int ExitCode, string StandardError) Scan(string folder, Dictionary<string, IReadOnlyList<string>> pages, Dictionary<string, string> deprecated)
    {
        ManPageScan scan = new(deprecated, [.. pages.Keys]);
        foreach (string page in pages.Keys.Where(page => !page.Contains('/', StringComparison.Ordinal)).Order(StringComparer.Ordinal)
            .Concat(pages.Keys.Where(page => page.Contains('/', StringComparison.Ordinal)).Order(StringComparer.Ordinal)))
        {
            scan.Page($"{folder}/{page}", pages[page]);
        }

        return scan.Errors == 0 ? (0, "ok\n") : (scan.Errors, scan.Output.ToString());
    }

    // allsymbols: every name, and a deprecated mark on one whose info holds two versions.
    private static HashSet<string> ReadSymbols(string path, Dictionary<string, string> deprecated)
    {
        HashSet<string> symbols = new(StringComparer.Ordinal);
        foreach (Match line in File.ReadLines(path).Select(line => SymbolLine.Match(line)).Where(line => line.Success))
        {
            symbols.Add(line.Groups[1].Value);
            if (DeprecatedInfo.IsMatch(line.Groups[2].Value))
            {
                deprecated[line.Groups[1].Value] = line.Groups[2].Value;
            }
        }

        return symbols;
    }

    // scanmanpage and its state shared across pages.
    private sealed class ManPageScan(Dictionary<string, string> deprecated, HashSet<string> pagePaths)
    {
        private readonly Dictionary<string, int> sectionLines = new(StringComparer.Ordinal);

        private readonly HashSet<string> knownReferences = new(StringComparer.Ordinal) { "curl.1", "curl-config.1" };

        public int Errors { get; private set; }

        public StringBuilder Output { get; } = new();

        public void Page(string file, IReadOnlyList<string> lines)
        {
            Match kind = LibcurlPage.Match(file);
            PageState state = new(file, kind.Success && !deprecated.ContainsKey("CURL" + kind.Groups[2].Value), kind.Success && kind.Groups[1].Value == "CURL");
            int line = 1;
            foreach (string text in lines)
            {
                if (Referral.IsMatch(text))
                {
                    return;
                }

                Line(state, text, line);
                line++;
            }

            Finish(state, line);
        }

        private void Line(PageState state, string text, int line)
        {
            Section(state, text, line);
            CountAll(
                [
                    text.StartsWith('\'') ? $"{state.File}:{line} line starts with single quote!\n" : null,
                    MissingTerminator(state, text, line),
                    .. BrokenReferences(state, text, line),
                    ReferenceWithoutSection(state, text, line),
                    SuspiciousFont(state, text, line),
                    UnreferencedCall(state, text, line),
                    UntaggedOption(state, text, line),
                    TrailingSpace.IsMatch(text) ? $"{state.File}:{line} trailing whitespace\n" : null,
                ]);
        }

        // The chain of heading and body checks.
        private void Section(PageState state, string text, int line)
        {
            if (state.Required && SynopsisHeading.IsMatch(text))
            {
                state.InSynopsis = true;
                state.InExample = false;
            }
            else if (ExampleHeading.IsMatch(text))
            {
                state.InSynopsis = false;
                state.InExample = true;
            }
            else if (SeeAlsoHeading.IsMatch(text))
            {
                state.InSeeAlso = true;
            }
            else if (AnyHeading.IsMatch(text))
            {
                state.InSynopsis = false;
                state.InExample = false;
            }
            else
            {
                Body(state, text, line);
            }

            RecordHeading(state, text, line);
        }

        private void RecordHeading(PageState state, string text, int line)
        {
            Match heading = SectionHeading.Match(text);
            if (heading.Success)
            {
                string name = Quoted.Replace(heading.Groups[1].Value, "$1", 1);
                state.Sections.Add(name);
                sectionLines[name] = line;
                state.Heading = name;
            }
        }

        // A line inside the see-also, example or synopsis section.
        private void Body(PageState state, string text, int line)
        {
            if (state.InSeeAlso)
            {
                SeeAlso(state, text, line);
            }
            else if (state.InExample)
            {
                Output.Append(ExampleLine(state, text, line));
            }
            else if (state.InSynopsis)
            {
                Output.Append(SynopsisLine(state, text, line));
            }
        }

        // Printed but not counted: a raw "\n" in an example.
        private static string ExampleLine(PageState state, string text, int line)
        {
            state.ExampleSize++;
            return RawNewline.IsMatch(text) ? $"{state.File}:{line} '\\n' need to be '\\\\n'!\n" : string.Empty;
        }

        // Printed but not counted: a synopsis that does not start with .nf.
        private static string SynopsisLine(PageState state, string text, int line)
        {
            state.SynopsisSize++;
            return state.SynopsisSize == 1 && !text.Contains(".nf", StringComparison.Ordinal) ? $"{state.File}:{line}:1:ERROR: be .nf for proper formatting\n" : string.Empty;
        }

        private void SeeAlso(PageState state, string text, int line)
        {
            Match entry = SeeAlsoLine.Match(text);
            if (!entry.Success)
            {
                return;
            }

            string rest = entry.Groups[1].Value;
            if (CurlName.IsMatch(rest))
            {
                CurlSeeAlso(state, rest, line);
            }
            else
            {
                Match comma = TrailingComma.Match(rest);
                state.Separators.Add((comma.Success ? comma.Groups[1].Value : " ", line));
            }
        }

        // A see-also entry naming a curl page: one "name (section)" then the separator, and nothing else.
        private void CurlSeeAlso(PageState state, string entry, int line)
        {
            Match reference = SeeAlsoReference.Match(entry);
            string rest = entry;
            if (reference.Success)
            {
                rest = entry.Remove(reference.Index, reference.Length);
                state.Separators.Add((reference.Groups[3].Value, line));
                CountAll([BrokenReference(reference.Groups[1].Value, reference.Groups[2].Value, state.File, line)]);
            }

            CountAll([Blank.IsMatch(rest) ? null : $"{state.File}:{line} bad SEE ALSO format\n"]);
        }

        private static string? MissingTerminator(PageState state, string text, int line)
        {
            Match bolded = Bolded.Match(text);
            return !bolded.Success || bolded.Groups[2].Value.Contains("\\fP", StringComparison.Ordinal)
                ? null
                : $"{state.File}:{line} missing \\f{bolded.Groups[1].Value} terminator!\n";
        }

        // Each \f-wrapped page reference in turn, taken out of the line as the script's substitution does.
        private IEnumerable<string?> BrokenReferences(PageState state, string text, int line)
        {
            string rest = text;
            for (Match reference = FontReference.Match(rest); reference.Success; reference = FontReference.Match(rest))
            {
                rest = rest.Remove(reference.Index, reference.Length);
                yield return BrokenReference(reference.Groups[2].Value, reference.Groups[4].Value, state.File, line);
            }
        }

        private string? BrokenReference(string name, string section, string file, int line) =>
            IsPresent($"{name}.{section}", section) ? null : $"{file}:{line} broken reference to {name}({section})\n";

        private bool IsPresent(string reference, string section) =>
            knownReferences.Contains(reference) || (section == "3" && (pagePaths.Contains(reference) || pagePaths.Contains("opts/" + reference)));

        private static string? ReferenceWithoutSection(PageState state, string text, int line)
        {
            Match bare = BareReference.Match(text);
            return !bare.Success || bare.Groups[4].Value == "(" || NotOptions.Contains(bare.Groups[2].Value)
                ? null
                : $"{state.File}:{line} curl ref to {bare.Groups[2].Value} without section\n";
        }

        private static string? SuspiciousFont(PageState state, string text, int line)
        {
            Match other = OtherFont.Match(text);
            return !other.Success || other.Groups[1].Value.EndsWith('\\') ? null : $"{state.File}:{line} suspicious \\f format!\n";
        }

        private static string? UnreferencedCall(PageState state, string text, int line)
        {
            Match call = UnreferencedCallPattern.Match(text);
            return state.Heading.Length == 0 || !ProseSection.IsMatch(state.Heading) || !call.Success || call.Groups[1].Value.EndsWith("\\fI", StringComparison.Ordinal)
                ? null
                : $"{state.File}:{line} unrefed curl call: {call.Groups[2].Value}\n";
        }

        private string? UntaggedOption(PageState state, string text, int line) =>
            state.OptionPage && state.Heading.Length > 0 && !ExcludedSection.IsMatch(state.Heading) ? UntaggedSymbol(state, text, line) : null;

        private string? UntaggedSymbol(PageState state, string text, int line)
        {
            Match option = OptionSymbol.Match(text);
            string symbol = option.Groups[2].Value;
            return !option.Success || deprecated.ContainsKey(symbol) || option.Groups[1].Value.EndsWith("\\fI", StringComparison.Ordinal)
                ? null
                : $"{state.File}:{line} option {symbol} missing \\fI tagging\n";
        }

        private void CountAll(IEnumerable<string?> messages)
        {
            foreach (string message in messages.OfType<string>())
            {
                Output.Append(message);
                Errors++;
            }
        }

        private void Finish(PageState state, int line)
        {
            List<(string Separator, int Line)> all = state.Separators;
            CountAll(
                all.Take(Math.Max(all.Count - 1, 0)).Where(entry => entry.Separator != ",")
                    .Select(entry => $"{state.File}:{entry.Line}: bad not-last SEE ALSO separator: '{entry.Separator}'\n"));
            CountAll(all.TakeLast(1).Where(entry => entry.Separator == ",").Select(entry => $"{state.File}:{entry.Line}: superfluous comma separator\n"));
            if (state.Required)
            {
                CheckRequiredSections(state, line);
            }
        }

        // Only for libcurl function and option pages: the example and the mandatory sections in their order.
        private void CheckRequiredSections(PageState state, int line)
        {
            CountAll([state.ExampleSize < 2 ? $"{state.File}:{line} missing EXAMPLE section\n" : null]);
            if (state.Sections.Count < 3)
            {
                CountAll([$"{state.File}:{line} too few man page sections!\n"]);
                return;
            }

            CountAll([FindSectionProblem(state, state.OptionPage ? OptionOrder : FunctionOrder, line)]);
        }

        // The first mandatory section out of order, else the first one missing at the end.
        private string? FindSectionProblem(PageState state, string[] order, int line)
        {
            int found = 0;
            int expected = 1;
            foreach (string section in state.Sections.TakeWhile(section => section.Length > 0).Where(order.Contains))
            {
                found = Array.IndexOf(order, section) + 1;
                if (found != expected)
                {
                    return $"{state.File}:{sectionLines[section]} Got {section}, when {order[expected - 1]} was expected\n";
                }

                expected++;
                if (found == order.Length)
                {
                    break;
                }
            }

            return found == order.Length ? null : MissingSection(state, order, found, line);
        }

        private static string MissingSection(PageState state, string[] order, int found, int line) =>
            $"{state.File}:{line} missing mandatory section: {order[found]}\n"
            + $"{state.File}:{line} section found at index {found}: '{state.Sections.ElementAtOrDefault(found)}'\n"
            + $" Found {state.Sections.Count} used sections\n";
    }

    private sealed class PageState(string file, bool required, bool optionPage)
    {
        public string File { get; } = file;

        public bool Required { get; } = required;

        public bool OptionPage { get; } = optionPage;

        public bool InSynopsis { get; set; }

        public bool InExample { get; set; }

        public bool InSeeAlso { get; set; }

        public int ExampleSize { get; set; }

        public int SynopsisSize { get; set; }

        public string Heading { get; set; } = string.Empty;

        public List<string> Sections { get; } = [];

        public List<(string Separator, int Line)> Separators { get; } = [];
    }
}
