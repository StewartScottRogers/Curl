namespace Curl.Cli;

/// <summary>
/// What <c>--ai-help [subject]</c> prints on standard output: Markdown for AI agents, where <c>--help</c>
/// is for people (BL-911, ADR-0224). With no subject it is a short index: what this curl is, the
/// categories of <see cref="CurlHelpText.Categories"/> with the command that expands each, and the 20
/// most-used options. A category name in any case gives a <c>#</c> heading for the category and a
/// <c>##</c> section for each of its options; <c>all</c> gives every category that way, then the exit
/// codes and the <c>--write-out</c> variables. Each option section is built from the rows of
/// <see cref="CurlHelpTable"/> (plus Curl's own <c>--log-level</c> and <c>--log-file</c>), <see cref="CurlOptionAliasTable"/>,
/// <see cref="CommandLineOptionTable"/> and the option's section of <see cref="CurlManual"/>, so it agrees
/// with <c>--help</c> and <c>--manual</c>. The Markdown is not wrapped to any width and every line ends in
/// <c>\n</c> alone, so it is the same bytes on every platform.
/// </summary>
public static class CurlAiHelpText
{
    private const string AllSubject = "all";

    /// <summary>
    /// The subject whose Markdown the console builds from Curl's extensions (ADR-1008); the index lists it
    /// beside the categories, and <see cref="IsExtensionsSubject"/> recognises it in any case.
    /// </summary>
    public const string ExtensionsSubject = "extensions";

    /// <summary>Reports whether <paramref name="subject"/> asks for the extensions' Markdown, in any case.</summary>
    /// <param name="subject">The <c>--ai-help</c> subject, <see langword="null"/> for none.</param>
    /// <returns><see langword="true"/> for <c>extensions</c> in any case.</returns>
    public static bool IsExtensionsSubject(string? subject) => string.Equals(subject, ExtensionsSubject, StringComparison.OrdinalIgnoreCase);

    /// <summary>The line an option's section carries when curl's Windows Schannel build refuses it (ADR-0397).</summary>
    private const string SchannelBuildRefusalLine =
        "- On Windows: refused with exit 2 (`the installed libcurl version does not support this`), as curl's Schannel build refuses it; accepted on Linux and macOS.";

    /// <summary>The line an option's section carries when every reference build refuses it for want of a QUIC library (ADR-0540).</summary>
    private const string NoQuicRefusalLine =
        "- On every platform: refused with exit 2 (`the installed libcurl version does not support this`), as curl's reference builds (Schannel on Windows, OpenSSL on Linux and macOS) have no QUIC library.";

    /// <summary>The line an option's section carries when every reference build refuses it for want of ECH (ADR-0918).</summary>
    private const string NoEchRefusalLine =
        "- On every platform: refused with exit 2 (`the installed libcurl version does not support this`), as curl's reference builds (Schannel on Windows, OpenSSL on Linux and macOS) are built without ECH.";

    /// <summary>The line an option's section carries when every reference build refuses it for want of c-ares (ADR-0841).</summary>
    private const string NoCAresRefusalLine =
        "- On every platform: refused with exit 2 (`the installed libcurl version does not support this`), as curl's reference builds (Schannel on Windows, OpenSSL on Linux and macOS) are built without c-ares.";

    /// <summary>
    /// The rows <c>--ai-help</c> documents that curl's <c>--help</c> does not: Curl's own diagnostic-log
    /// options and <c>--extensions</c>, which <c>--help</c> leaves out so curl's help pages stay byte for byte (ADR-1008). <c>--ai-help</c> itself is in <see cref="CurlHelpTable"/>, so <c>--help</c> names it (BL-2985).
    /// </summary>
    private static readonly CurlHelpEntry[] CurlOnlyEntries =
    [
        new("    --log-level <level>", "Curl's own diagnostic log, not curl's -v: none (the default), error, warning, info or verbose, to standard error or the --log-file", CurlHelpCategories.Curl),
        new("    --extensions", "List Curl's own extensions beyond upstream curl, one per line: each one's schemes and options (ADR-1008); `--ai-help extensions` documents them", CurlHelpCategories.Curl),
        new("    --log-file <file>", "Write Curl's own diagnostic log, not curl's -v, to this file instead of standard error; at info level unless --log-level says otherwise", CurlHelpCategories.Curl),
    ];

    /// <summary>The options the index lists as most used, by long name, in the order it lists them.</summary>
    private static readonly string[] MostUsedLongNames =
    [
        "request", "header", "data", "json", "form", "user", "output", "remote-name", "upload-file", "location",
        "silent", "show-error", "fail", "show-headers", "verbose", "write-out", "max-time", "insecure", "cookie", "user-agent",
    ];

    /// <summary>
    /// Names <see cref="CommandLineOptionTable"/> parses that curl's help lists under another name, as the
    /// line the other name's section carries for them.
    /// </summary>
    private static readonly Dictionary<string, string> OtherSpellingLines = new(StringComparer.Ordinal)
    {
        ["show-headers"] = "- Also accepted as: `--include`",
        ["ssl"] = "- Also accepted as: `--ftp-ssl`",
        ["ssl-reqd"] = "- Also accepted as: `--ftp-ssl-reqd`",
        ["krb"] = "- Also accepted as: `--krb4`",
        ["disable-eprt"] = "- Opposite spelling: `--eprt`, the same as `--no-disable-eprt`",
        ["disable-epsv"] = "- Opposite spelling: `--epsv`, the same as `--no-disable-epsv`",
    };

    /// <summary>The environment variables an option's section names, by long name, as the line it carries for them (ADR-0682).</summary>
    private static readonly Dictionary<string, string> EnvironmentLines = new(StringComparer.Ordinal)
    {
        ["cacert"] = CaCertificateEnvironmentLine,
        ["capath"] = CaCertificateEnvironmentLine,
    };

    private const string CaCertificateEnvironmentLine =
        "- Environment: with neither `--cacert` nor `--capath` given and the certificate checked, `CURL_CA_BUNDLE` names the CA file; without it, `SSL_CERT_DIR` names the CA folder and `SSL_CERT_FILE` the CA file. Read on Linux and macOS; on Windows none of the three is read, as curl's Schannel build reads none.";

    /// <summary>The sentences curl's manual uses to say how a repeated option behaves, with what each means.</summary>
    private static readonly (string Sentence, string Meaning)[] RepeatSentences =
    [
        ("can be used several times in a command line", "yes, each use adds to the ones before it"),
        ("is provided several times, the last set value is used", "yes, the last one given is used"),
        ("multiple times has no extra effect", "yes, but a repeat has no extra effect"),
        ("is associated with a single URL", "once per URL: give it once for each URL on the command line"),
    ];

    /// <summary>
    /// Every option <c>--ai-help</c> documents: the rows of <see cref="CurlHelpTable"/> and Curl's
    /// own, ordered by long name.
    /// </summary>
    internal static IReadOnlyList<CurlHelpEntry> Entries { get; } =
        [.. CurlHelpTable.Entries.Concat(CurlOnlyEntries).OrderBy(LongName, StringComparer.Ordinal)];

    /// <summary>Builds the Markdown <c>--ai-help</c> prints for <paramref name="subject"/>.</summary>
    /// <param name="subject">
    /// <see langword="null"/> or empty for the index; <c>all</c> or a category name, in any case.
    /// </param>
    /// <param name="markdown">The Markdown, every line ending in <c>\n</c>; empty when the method returns <see langword="false"/>.</param>
    /// <returns>
    /// <see langword="false"/> when <paramref name="subject"/> names no category; curl then prints
    /// <see cref="UnknownCategoryLines"/>, as <c>--help</c> does.
    /// </returns>
    public static bool TryGetMarkdown(string? subject, out string markdown)
    {
        IReadOnlyList<string>? lines = SubjectLines(subject);
        markdown = lines is null ? string.Empty : string.Concat(lines.Select(line => line + "\n"));
        return lines is not null;
    }

    /// <summary>
    /// The lines <c>--help</c> prints for a subject that names no category, which <c>--ai-help</c> prints for
    /// one too: <c>Unknown category provided, ...</c>, a blank line and the category list.
    /// </summary>
    /// <returns>The lines, without line terminators.</returns>
    public static IReadOnlyList<string> UnknownCategoryLines() =>
        ["Unknown category provided, here is a list of all categories:", string.Empty, .. CurlHelpText.Lines("category", CurlHelpText.DefaultColumns)];

    private static IReadOnlyList<string>? SubjectLines(string? subject)
    {
        if (string.IsNullOrEmpty(subject))
        {
            return IndexLines();
        }

        if (subject.Equals(AllSubject, StringComparison.OrdinalIgnoreCase))
        {
            return AllLines();
        }

        (string Name, string Description, CurlHelpCategories Category)[] matches =
            [.. CurlHelpText.Categories.Where(category => category.Name.Equals(subject, StringComparison.OrdinalIgnoreCase))];
        return matches.Length == 0 ? null : CategoryLines(matches[0]);
    }

    private static List<string> IndexLines()
    {
        List<string> lines =
        [
            "# curl --ai-help",
            string.Empty,
            "This `curl` is curl 8.22.0 reimplemented in C#, a drop-in replacement: the same options, exit codes and output bytes. Run it as `curl [options...] <url>`. This page is the Markdown option reference for AI agents; `curl --help` is the one for people.",
            string.Empty,
            "## Categories",
            string.Empty,
            "Every option is filed under one or more of these categories. Expand one with the command after it.",
            string.Empty,
        ];
        lines.AddRange(CurlHelpText.Categories.Select(category => $"- `{category.Name}`: {category.Description}. Run `curl --ai-help {category.Name}`"));
        lines.Add($"- `{ExtensionsSubject}`: Curl's own extensions beyond upstream curl, which `curl --extensions` lists. Run `curl --ai-help {ExtensionsSubject}`");
        lines.AddRange([string.Empty, "## Most-used options", string.Empty]);
        lines.AddRange(MostUsedLongNames.Select(name => MostUsedLine(Entries.First(entry => LongName(entry) == name))));
        lines.AddRange(
        [
            string.Empty,
            "## More",
            string.Empty,
            "Run `curl --ai-help <category>` for one category's options in full, or `curl --ai-help all` for every option, the exit codes and the `--write-out` variables.",
        ]);
        return lines;
    }

    private static string MostUsedLine(CurlHelpEntry entry)
    {
        string? shortName = ShortName(entry);
        string shortForm = shortName is null ? string.Empty : $" (`{shortName}`)";
        return $"- `{entry.Option[4..]}`{shortForm}: {CurlManualMarkdown.Escape(entry.Description)}";
    }

    private static List<string> AllLines()
    {
        List<string> lines = [.. CurlHelpText.Categories.SelectMany(CategoryLines)];
        lines.AddRange(["# Exit codes", string.Empty, .. CurlManualMarkdown.ExitCodesMarkdown(), string.Empty]);
        lines.AddRange(["# --write-out variables", string.Empty, "Use each as `%{name}` in the `--write-out` format.", string.Empty, .. CurlManualMarkdown.WriteOutVariablesMarkdown()]);
        return lines;
    }

    private static List<string> CategoryLines((string Name, string Description, CurlHelpCategories Category) category)
    {
        List<string> lines = [$"# {category.Name}: {category.Description}", string.Empty];
        foreach (CurlHelpEntry entry in Entries.Where(entry => (entry.Categories & category.Category) != 0))
        {
            lines.AddRange(OptionLines(entry));
            lines.Add(string.Empty);
        }

        return lines;
    }

    private static List<string> OptionLines(CurlHelpEntry entry)
    {
        string longName = LongName(entry);
        List<string> lines =
        [
            $"## --{longName}",
            string.Empty,
            $"- Short form: {CodeOrNone(ShortName(entry))}",
            $"- Argument: {CodeOrNone(Argument(entry))}",
        ];
        lines.AddRange(OnOffLines(longName));
        lines.Add($"- Repeat: {RepeatMeaning(longName)}");
        lines.AddRange(OtherSpellingAndEnvironmentLines(longName));
        lines.AddRange(CommandLineOptionTable.Rows.Any(row => row.LongName == RowName(longName) && !row.RefusedWhenTurnedOn) ? [] : ["- Not supported by this build yet: curl refuses it with exit 2."]);
        lines.AddRange(ReferenceBuildRefusalLines(longName));
        lines.AddRange([string.Empty, CurlManualMarkdown.Escape(entry.Description) + "."]);
        lines.AddRange(CurlManualMarkdown.TryGetOptionMarkdown(longName, out IReadOnlyList<string> manual) ? [string.Empty, .. manual] : []);
        return lines;
    }

    /// <summary>The lines saying which reference builds refuse the option for want of a feature, when any do.</summary>
    private static IEnumerable<string> ReferenceBuildRefusalLines(string longName)
    {
        CommandLineOption[] rows = [.. CommandLineOptionTable.Rows.Where(row => row.LongName == RowName(longName))];
        return ReferenceBuildRefusals.Where(refusal => rows.Any(refusal.Refuses)).Select(refusal => refusal.Line);
    }

    /// <summary>Each way a reference build refuses an option for want of a feature, with the line its section then carries, in the order the lines are written.</summary>
    private static readonly (Func<CommandLineOption, bool> Refuses, string Line)[] ReferenceBuildRefusals =
    [
        (row => row.RefusedByWindowsSchannelBuild, SchannelBuildRefusalLine),
        (row => row.RefusedByEveryReferenceBuild, NoQuicRefusalLine),
        (row => row.RefusedByBuildsWithoutEch, NoEchRefusalLine),
        (row => row.RefusedByBuildsWithoutCAres, NoCAresRefusalLine),
    ];

    /// <summary>The lines naming the option's other spelling and the environment variables it stands in for, when it has them.</summary>
    private static IEnumerable<string> OtherSpellingAndEnvironmentLines(string longName)
    {
        IEnumerable<string> otherSpelling = OtherSpellingLines.TryGetValue(RowName(longName), out string? spellingLine) ? [spellingLine] : [];
        return otherSpelling.Concat(EnvironmentLines.TryGetValue(longName, out string? environmentLine) ? [environmentLine] : []);
    }

    /// <summary>The line naming the spelling that reverses the option, when curl has one.</summary>
    private static IEnumerable<string> OnOffLines(string longName)
    {
        if (longName.StartsWith("no-", StringComparison.Ordinal))
        {
            return [$"- Turn back on with: `--{longName[3..]}`"];
        }

        CurlOptionAlias? alias = CurlOptionAliasTable.Aliases.FirstOrDefault(candidate => candidate.Name == longName);
        return alias is null || alias.NoPrefix == CurlOptionNoPrefix.NotAccepted ? [] : [$"- Turn off with: `--no-{longName}`"];
    }

    private static string RepeatMeaning(string longName)
    {
        string text = CurlManualMarkdown.OptionPlainText(longName);
        return RepeatSentences.FirstOrDefault(repeat => text.Contains(repeat.Sentence, StringComparison.Ordinal)).Meaning
            ?? "not stated; see the description";
    }

    private static string CodeOrNone(string? text) => text is null ? "none" : $"`{text}`";

    /// <summary>The name <see cref="CommandLineOptionTable"/> parses the option as: <c>no-alpn</c> is the row <c>alpn</c>.</summary>
    private static string RowName(string longName) => longName.StartsWith("no-", StringComparison.Ordinal) ? longName[3..] : longName;

    /// <summary>The short form in a row's option text, <c>-d</c> in <c>-d, --data &lt;data&gt;</c>.</summary>
    private static string? ShortName(CurlHelpEntry entry) => entry.Option[0] == '-' ? entry.Option[..2] : null;

    /// <summary>The long name in a row's option text, <c>data</c> in <c>-d, --data &lt;data&gt;</c>.</summary>
    private static string LongName(CurlHelpEntry entry) => entry.Option[6..].Split(' ')[0];

    /// <summary>The argument placeholder in a row's option text, <c>&lt;data&gt;</c> in <c>-d, --data &lt;data&gt;</c>.</summary>
    private static string? Argument(CurlHelpEntry entry)
    {
        int space = entry.Option.IndexOf(' ', 6);
        return space < 0 ? null : entry.Option[(space + 1)..];
    }
}
