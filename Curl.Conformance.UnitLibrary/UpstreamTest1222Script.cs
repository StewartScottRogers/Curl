using System.Text;
using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// Interprets upstream's <c>tests/test1222.pl</c> (curl 8.22.0), which case 1222 runs as
/// <c>%SRCDIR/test1222.pl %SRCDIR/.. %PWD/..</c> to check that headers, man pages and
/// <c>docs/libcurl/symbols-in-versions</c> agree on deprecations, without running Perl (BL-2992, BL-3000).
/// </summary>
/// <remarks>
/// It reads <c>ROOT/docs/libcurl/symbols-in-versions</c>, whose absence is the script's <c>die</c> (its message to
/// standard error and exit code 255), then the libcurl manual: the built pages <c>BUILDROOT/docs/libcurl/*.3</c>
/// and <c>opts/*.3</c> when there are any, else the pages <see cref="UpstreamCd2nroff"/> writes from
/// <c>ROOT/docs/libcurl</c>'s Markdown, as a build would (ADR-0752). With neither it writes the script's
/// <c>curl built without the libcurl manual. Skipping test 1222.</c> to standard output and exits 0. Otherwise it
/// carries out the script's check: <c>scan_header</c> over every <c>ROOT/include/curl/*.h</c> (in ordinal order,
/// where the script reads the folder's order; a missing folder is its <c>die</c>), <c>scan_man_page</c> over the
/// function pages and the option pages, following <c>.so man3/</c> includes, <c>scan_man_for_opts</c> over
/// <c>curl_easy_setopt.3</c> and <c>curl_easy_getinfo.3</c> (whose findings, as in the script, land in the function
/// pages' table), and the table of symbols the four sources disagree on, after the legend. Each problem goes to
/// standard output in the script's words, man page conflicts in ordinal order of their symbols where the script's
/// hash order is undefined, and the exit code is their count.
/// </remarks>
internal static class UpstreamTest1222Script
{
    private const int Die = 255;

    private const string NotDeprecated = "X";

    private const string UnknownVersion = "?";

    private const string NotListed = " ";

    private const string Legend =
        "Legend:\n<empty> Not listed\nX       Not deprecated\n?       Deprecated in unknown version\nx.yy.z  Deprecated in version x.yy.z\n\n"
        + "Symbol                                 symbols-in  func man  opt man   .h\n                                       -versions\n";

    private const RegexOptions Options = RegexOptions.CultureInvariant;

    private static readonly Regex SymbolsLine = new(@"^((?:CURL|LIBCURL)\S+)\s+\S+\s*(\S*)\s*(\S*)$", Options);

    private static readonly Regex Deprecation =
        new(@"(?:deprecated|obsoleted?)\b\s*(?:in\b|since\b)?\s*(?:version\b|curl\b|libcurl\b)?\s*(\d[0-9.]*\d)?\b\s*(.*)$", Options | RegexOptions.IgnoreCase);

    private static readonly Regex FontChange = new(@"\\f.", Options);

    private static readonly Regex Whitespace = new(@"\s+", Options);

    private static readonly Regex Inclusion = new(@"\.so\s+man3/(.*\.3\b)", Options);

    private static readonly Regex SectionStart = new(@"^\.SH\b\s*(\w*)", Options);

    private static readonly Regex SectionRead = new("DESCRIPTION|DEPRECATED", Options);

    private static readonly Regex SetoptItem = new(@"^\.IP\s+((?:CURLOPT)_\w+)", Options);

    private static readonly Regex GetinfoItem = new(@"^\.IP\s+((?:CURLINFO)_\w+)", Options);

    private static readonly Regex FunctionName = new(@"^(?:curl|curlx)_\w", Options);

    private static readonly Regex OptionName = new(@"^(?:CURLOPT|CURLINFO)_\w+$", Options);

    private static readonly Regex TabledName = new(@"^(?:CURLOPT|CURLINFO|curl|curlx)_\w", Options);

    private static readonly Regex UndeprecatedEverywhere = new("[X ]{3}", Options);

    /// <summary>The file name of the upstream script this class interprets.</summary>
    public const string ScriptName = "test1222.pl";

    /// <summary>Runs the check over the folders a case's command names, writing to the invocation's output streams.</summary>
    /// <param name="invocation">The command's words after the script: the release root and the build root.</param>
    /// <param name="runCurl">The in-process curl, which the script never runs.</param>
    /// <returns>The script's exit code.</returns>
    public static async Task<int> RunAsync(UpstreamCurlInvocation invocation, Func<UpstreamCurlInvocation, Task<int>> runCurl)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        (int exitCode, string standardOutput, string standardError) = Check(invocation.Arguments[0], invocation.Arguments[1]);
        await invocation.StandardOutput.WriteAsync(Encoding.UTF8.GetBytes(standardOutput));
        await invocation.StandardError.WriteAsync(Encoding.UTF8.GetBytes(standardError));
        return exitCode;
    }

    /// <summary>Checks that the headers, the libcurl manual and symbols-in-versions agree on deprecations.</summary>
    /// <param name="root">The release root, beside <c>docs</c> and <c>include</c>, as the script prints it.</param>
    /// <param name="buildRoot">The build root, whose <c>docs/libcurl</c> holds the built <c>.3</c> pages if any.</param>
    /// <returns>The script's exit code and what it writes to standard output and standard error.</returns>
    public static (int ExitCode, string StandardOutput, string StandardError) Check(string root, string buildRoot)
    {
        string symbolsInVersions = $"{root}/docs/libcurl/symbols-in-versions";
        if (!File.Exists(symbolsInVersions))
        {
            return (Die, string.Empty, symbolsInVersions + "\n");
        }

        Dictionary<string, string> symbols = ReadSymbols(symbolsInVersions);
        Func<string, IReadOnlyList<string>?>? readPage = ManualPages(root, buildRoot);
        if (readPage is null)
        {
            return (0, "curl built without the libcurl manual. Skipping test 1222.\n", string.Empty);
        }

        string includeFolder = $"{root}/include/curl";
        if (!Directory.Exists(includeFolder))
        {
            return (Die, string.Empty, $"Cannot opendir {includeFolder}\n");
        }

        DeprecationSurvey survey = new(symbols, readPage);
        survey.Run(includeFolder);
        return (survey.Problems, survey.Output.ToString(), string.Empty);
    }

    // The symbols neither removed nor aliased away: X, or the version deprecating them.
    private static Dictionary<string, string> ReadSymbols(string path)
    {
        Dictionary<string, string> symbols = new(StringComparer.Ordinal);
        foreach (Match line in File.ReadLines(path).Select(line => SymbolsLine.Match(line)).Where(line => line.Success && line.Groups[3].Length == 0))
        {
            string deprecated = line.Groups[2].Value;
            symbols[line.Groups[1].Value] = deprecated.Length == 0 || deprecated == "." ? NotDeprecated : deprecated;
        }

        return symbols;
    }

    // Reads a page by its path in the manual: built pages when there are any, else ones converted from the Markdown.
    private static Func<string, IReadOnlyList<string>?>? ManualPages(string root, string buildRoot)
    {
        string builtFolder = $"{buildRoot}/docs/libcurl";
        if (Directory.Exists(builtFolder) && Directory.EnumerateFiles(builtFolder, "*.3").Any())
        {
            return page => File.Exists($"{builtFolder}/{page}") ? File.ReadAllLines($"{builtFolder}/{page}") : null;
        }

        Dictionary<string, IReadOnlyList<string>> converted = UpstreamCd2nroff.ConvertFolder($"{root}/docs/libcurl");
        return converted.Keys.Any(page => !page.Contains('/', StringComparison.Ordinal)) ? converted.GetValueOrDefault : null;
    }

    // One line of text from a paragraph: font changes removed, whitespace runs as one space.
    private static string Paragraph(string text) => Whitespace.Replace(FontChange.Replace(text, string.Empty), " ");

    // The page an include names, beside the page including it.
    private static string Beside(string page, string name)
    {
        int slash = page.LastIndexOf('/');
        return slash < 0 ? name : page[..(slash + 1)] + name;
    }

    // The script's merge of one symbol's four statuses: "!" when they disagree.
    private static string Agreement(string[] statuses) => statuses.Aggregate(NotListed, Merge);

    private static string Merge(string agreed, string status)
    {
        agreed = agreed == NotListed ? status : agreed;
        if (status == NotListed || agreed == status)
        {
            return agreed;
        }

        return agreed == UnknownVersion ? Refined(status) : Disagreement(agreed, status);
    }

    // An unknown version takes a stated one and conflicts with "not deprecated".
    private static string Refined(string status) => status != NotDeprecated ? status : "!";

    private static string Disagreement(string agreed, string status) =>
        agreed == NotDeprecated || status != UnknownVersion ? "!" : agreed;

    private sealed class DeprecationSurvey(Dictionary<string, string> symbols, Func<string, IReadOnlyList<string>?> readPage)
    {
        private readonly Dictionary<string, string> header = new(StringComparer.Ordinal);

        private readonly Dictionary<string, string> functionPages = new(StringComparer.Ordinal);

        private readonly Dictionary<string, string> optionPages = new(StringComparer.Ordinal);

        public StringBuilder Output { get; } = new();

        public int Problems { get; private set; }

        public void Run(string includeFolder)
        {
            ScanHeaders(includeFolder);
            foreach (string function in header.Keys.Where(name => FunctionName.IsMatch(name)).Order(StringComparer.Ordinal).ToList())
            {
                ScanManPage($"{function}.3", function, functionPages);
            }

            ScanManForOptions("curl_easy_setopt.3", SetoptItem);
            ScanManForOptions("curl_easy_getinfo.3", GetinfoItem);
            foreach (string option in symbols.Keys.Where(name => OptionName.IsMatch(name)).Order(StringComparer.Ordinal))
            {
                ScanManPage($"opts/{option}.3", option, optionPages);
            }

            WriteTable();
        }

        private void ScanHeaders(string includeFolder)
        {
            foreach (string headerFile in Directory.EnumerateFiles(includeFolder, "*.h").Order(StringComparer.Ordinal))
            {
                HeaderScan scan = new(header);
                foreach (string line in File.ReadLines(headerFile))
                {
                    scan.Add(line);
                }
            }
        }

        // scan_man_page: the deprecation a page's DESCRIPTION and DEPRECATED sections state, or X.
        private void ScanManPage(string page, string symbol, Dictionary<string, string> table)
        {
            IReadOnlyList<string>? lines = readPage(page);
            if (lines is null)
            {
                return;
            }

            ManPageReading reading = new();
            foreach (string line in lines)
            {
                Match inclusion = Inclusion.Match(line);
                if (inclusion.Success)
                {
                    ScanManPage(Beside(page, inclusion.Groups[1].Value), symbol, table);
                    reading.Version = table.GetValueOrDefault(symbol, reading.Version);
                }
                else
                {
                    ReadManLine(reading, line, symbol);
                }
            }

            table[symbol] = reading.Version;
        }

        private void ReadManLine(ManPageReading reading, string line, string symbol)
        {
            Match start = SectionStart.Match(line);
            if (start.Success)
            {
                if (SectionRead.IsMatch(reading.Section))
                {
                    reading.Version = SectionVersion(Paragraph(Paragraph(reading.Text.ToString())), symbol, reading.Version);
                }

                reading.Section = start.Groups[1].Value;
                reading.Text.Clear();
            }
            else if (!line.StartsWith('.'))
            {
                reading.Text.Append(line).Append('\n');
            }
        }

        private string SectionVersion(string paragraph, string symbol, string version)
        {
            for (Match mention = Deprecation.Match(paragraph); mention.Success; mention = Deprecation.Match(mention.Groups[2].Value))
            {
                version = StatedVersion(mention.Groups[1].Value, symbol, version);
            }

            return version;
        }

        // A first statement sets the version; a later one stating another is a problem.
        private string StatedVersion(string stated, string symbol, string version)
        {
            if (version == NotDeprecated || version == UnknownVersion)
            {
                return stated.Length > 0 ? stated : UnknownVersion;
            }

            if (stated.Length > 0 && stated != version)
            {
                Output.Append($"error: {symbol} man page lists unmatching deprecation versions {version} and {stated}\n");
                Problems++;
            }

            return version;
        }

        // scan_man_for_opts: each ".IP OPTION" paragraph's deprecation, or X.
        private void ScanManForOptions(string page, Regex item)
        {
            string option = string.Empty;
            StringBuilder text = new();
            foreach (string line in readPage(page) ?? [])
            {
                if (!line.StartsWith('.'))
                {
                    text.Append(line).Append('\n');
                    continue;
                }

                string previous = option;
                Match start = item.Match(line);
                option = start.Success ? start.Groups[1].Value : string.Empty;
                string paragraph = Paragraph(text.ToString());
                text.Clear();
                if (previous.Length > 0)
                {
                    functionPages[previous] = OptionVersion(paragraph);
                }
            }
        }

        private static string OptionVersion(string paragraph)
        {
            string version = NotDeprecated;
            for (Match mention = Deprecation.Match(paragraph); mention.Success; mention = Deprecation.Match(mention.Groups[2].Value))
            {
                version = mention.Groups[1].Length > 0 ? mention.Groups[1].Value : UnknownVersion;
            }

            return version;
        }

        private void WriteTable()
        {
            string leader = Legend;
            IEnumerable<string> names = symbols.Keys.Concat(functionPages.Keys).Concat(optionPages.Keys).Concat(header.Keys).Distinct(StringComparer.Ordinal);
            foreach (string name in names.Where(candidate => TabledName.IsMatch(candidate)).Order(StringComparer.Ordinal))
            {
                string[] statuses = [.. new[] { symbols, functionPages, optionPages, header }.Select(table => table.GetValueOrDefault(name, NotListed))];
                if (UndeprecatedEverywhere.IsMatch(statuses[1] + statuses[2] + statuses[3]) || Agreement(statuses) != "!")
                {
                    continue;
                }

                Output.Append(leader).Append($"{name,-38} {statuses[0],-11} {statuses[1],-9} {statuses[2],-9} {statuses[3]}\n");
                leader = string.Empty;
                Problems++;
            }
        }
    }

    // Where scan_man_page is in one page: the version so far, the section and the section's text.
    private sealed class ManPageReading
    {
        public string Version { get; set; } = NotDeprecated;

        public string Section { get; set; } = string.Empty;

        public StringBuilder Text { get; } = new();
    }

    // scan_header: one header's public functions, CURLOPT options and enum values, each X or its deprecation version.
    private sealed class HeaderScan(Dictionary<string, string> header)
    {
        private static readonly Regex DeprecatedOption = new(@"CURLOPTDEPRECATED\(\s*(\S+)\s*,(?:.*?,){2}\s*(.*?)\s*,.*""\)", Options);

        private static readonly Regex Option = new(@"CURLOPT\(\s*(\S+)\s*(?:,.*?){2}\)", Options);

        private static readonly Regex Deprecated = new(@"^(.*)CURL_DEPRECATED\(\s*(\S+?)\s*,.*?""\)(.*)$", Options);

        private static readonly Regex ExternFunction = new(@"^CURL_EXTERN\s+.*\s+(\S+?)\s*\(", Options);

        private static readonly Regex EnumValue = new(@"(\w+)\s*[,=}]", Options);

        private static readonly Regex EnumKeyword = new(@"\benum\b", Options);

        private string folded = string.Empty;

        private bool inComment;

        private bool inEnum;

        public void Add(string line)
        {
            if (Statement(line) is not { } statement || Flag(statement) is not { } flagged)
            {
                return;
            }

            inEnum = !flagged.Contains('}', StringComparison.Ordinal) && (inEnum || EnumKeyword.IsMatch(flagged));
        }

        // The line trimmed, unfolded and without comments; null for a line the script goes past.
        private string? Statement(string line)
        {
            string statement = line.Trim();
            if (inComment)
            {
                int end = statement.IndexOf("*/", StringComparison.Ordinal);
                if (end < 0)
                {
                    return null;
                }

                statement = statement[(end + 2)..].TrimStart();
                inComment = false;
            }

            statement = WithoutComments(folded.Length > 0 ? $"{folded} {statement}" : statement).Trim();
            folded = string.Empty;
            return statement.Length == 0 || statement[0] == '#' ? null : Unfolded(statement);
        }

        private string WithoutComments(string statement)
        {
            for (int open = statement.IndexOf("/*", StringComparison.Ordinal); open >= 0; open = statement.IndexOf("/*", StringComparison.Ordinal))
            {
                int close = statement.IndexOf("*/", open + 2, StringComparison.Ordinal);
                if (close < 0)
                {
                    break;
                }

                statement = $"{statement[..open]} {statement[(close + 2)..]}";
            }

            int last = statement.LastIndexOf("/*", StringComparison.Ordinal);
            if (last < 0)
            {
                return statement;
            }

            inComment = true;
            return statement[..last] + " ";
        }

        // A statement not ending in ; , { or } continues on the next line.
        private string? Unfolded(string statement) => ";,{}".Contains(statement[^1], StringComparison.Ordinal) ? statement : Fold(statement);

        private string? Fold(string statement)
        {
            folded = statement;
            return null;
        }

        // Flags what the statement declares; null when it is folded to wait for its next line.
        private string? Flag(string statement)
        {
            if (statement.Contains("CURLOPTDEPRECATED(", StringComparison.Ordinal))
            {
                return Flagged(DeprecatedOption.Match(statement), statement, match => match.Groups[2].Value);
            }

            return statement.Contains("CURLOPT(", StringComparison.Ordinal)
                ? Flagged(Option.Match(statement), statement, _ => NotDeprecated)
                : FlagOther(statement);
        }

        private string? Flagged(Match match, string statement, Func<Match, string> version)
        {
            if (!match.Success)
            {
                return Fold(statement);
            }

            header[match.Groups[1].Value] = version(match);
            return statement;
        }

        private string? FlagOther(string statement)
        {
            string version = NotDeprecated;
            if (statement.Contains("CURL_DEPRECATED(", StringComparison.Ordinal))
            {
                Match deprecated = Deprecated.Match(statement);
                if (!deprecated.Success)
                {
                    return Fold(statement);
                }

                version = deprecated.Groups[2].Value;
                statement = $"{deprecated.Groups[1].Value} {deprecated.Groups[3].Value}";
            }

            Match flagged = ExternFunction.Match(statement);
            flagged = flagged.Success || !inEnum ? flagged : EnumValue.Match(statement);
            if (flagged.Success)
            {
                header[flagged.Groups[1].Value] = version;
            }

            return statement;
        }
    }
}
