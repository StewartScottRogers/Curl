using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <see cref="CurlAiHelpText"/>, the Markdown <c>--ai-help</c> prints (BL-911, ADR-0224): the short
/// index by default, one category's option sections by name, and every category with the exit codes and
/// <c>--write-out</c> variables for <c>all</c>, built from the same tables and manual <c>--help</c> and
/// <c>--manual</c> print.
/// </summary>
[TestClass]
public sealed class CurlAiHelpTextTests
{
    private static readonly string AllMarkdown = Markdown("all");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void TryGetMarkdown_NoSubject_PrintsTheIndex(string? subject)
    {
        string index = WrittenMarkdown(subject);

        AssertHas("starts with the title", index.StartsWith("# curl --ai-help\n\nThis `curl` is curl 8.21.0 reimplemented in C#", StringComparison.Ordinal));
        AssertContains("\n## Categories\n", index);
        AssertContains("\n## Most-used options\n", index);
        AssertHas("ends with the run line", index.EndsWith("Run `curl --ai-help <category>` for one category's options in full, or `curl --ai-help all` for every option, the exit codes and the `--write-out` variables.\n", StringComparison.Ordinal));
        Assert.StartsWith("# curl --ai-help\n\nThis `curl` is curl 8.21.0 reimplemented in C#", index);
        Assert.Contains("\n## Categories\n", index);
        Assert.Contains("\n## Most-used options\n", index);
        Assert.EndsWith("Run `curl --ai-help <category>` for one category's options in full, or `curl --ai-help all` for every option, the exit codes and the `--write-out` variables.\n", index);
    }

    [TestMethod]
    public void TryGetMarkdown_NoSubject_ListsEveryCategoryWithTheCommandThatExpandsIt()
    {
        string index = WrittenMarkdown(null);

        foreach ((string name, string description) in Categories())
        {
            AssertContains($"\n- `{name}`: {description}. Run `curl --ai-help {name}`\n", index);
            Assert.Contains($"\n- `{name}`: {description}. Run `curl --ai-help {name}`\n", index);
        }
    }

    [TestMethod]
    public void TryGetMarkdown_NoSubject_ListsTwentyOptionsWithTheirShortFormArgumentAndSummary()
    {
        string index = WrittenMarkdown(null);
        string[] mostUsed = [.. SectionLines(index, "## Most-used options").Where(line => line.StartsWith("- `--", StringComparison.Ordinal))];
        Diagnostics.Act("most-used lines", CommandLineParseDiagnostics.QuoteEach(mostUsed));

        Diagnostics.Assert("most-used count", 20, mostUsed.Length);
        Diagnostics.Assert("first most-used line", "- `--request <method>` (`-X`): Specify request method to use", mostUsed.FirstOrDefault());
        AssertHas("lists --json", mostUsed.Contains("- `--json <data>`: HTTP POST JSON"));
        AssertHas("lists --user-agent", mostUsed.Contains("- `--user-agent <name>` (`-A`): Send User-Agent \\<name> to server"));
        Assert.HasCount(20, mostUsed);
        Assert.AreEqual("- `--request <method>` (`-X`): Specify request method to use", mostUsed[0]);
        CollectionAssert.Contains(mostUsed, "- `--json <data>`: HTTP POST JSON");
        CollectionAssert.Contains(mostUsed, "- `--user-agent <name>` (`-A`): Send User-Agent \\<name> to server");
    }

    [TestMethod]
    [DataRow("http")]
    [DataRow("HTTP")]
    public void TryGetMarkdown_Category_PrintsItsHeadingAndASectionForEachOfItsOptions(string subject)
    {
        string http = WrittenMarkdown(subject);

        AssertHas("starts with the http heading", http.StartsWith("# http: HTTP and HTTPS protocol\n\n## --", StringComparison.Ordinal));
        Assert.StartsWith("# http: HTTP and HTTPS protocol\n\n## --", http);
        foreach (string longName in HelpLongNames("http"))
        {
            AssertContains($"\n## --{longName}\n", http);
            Assert.Contains($"\n## --{longName}\n", http);
        }

        AssertHas("has no --ftp-pasv section", !http.Contains("\n## --ftp-pasv\n", StringComparison.Ordinal));
        Assert.DoesNotContain("\n## --ftp-pasv\n", http);
    }

    [TestMethod]
    public void TryGetMarkdown_OptionSection_GivesItsFactsSummaryAndManualText()
    {
        string section = WrittenOptionSection(WrittenMarkdown("http"), "data");

        const string ExpectedStart = "## --data\n\n- Short form: `-d`\n- Argument: `<data>`\n- Repeat: yes, each use adds to the ones before it\n\nHTTP POST data.\n\n"
            + "(HTTP MQTT) Send the specified data in a POST request to the HTTP server, in the same way that a browser does";
        Diagnostics.Diff("section start", ExpectedStart, section[..Math.Min(section.Length, ExpectedStart.Length)]);
        AssertContains("\n\nExamples:\n\n```\ncurl -d \"name=curl\" https://example.com\ncurl -d \"name=curl\" -d \"tool=cmdline\" https://example.com\ncurl -d @filename https://example.com\n```\n\n", section);
        Assert.StartsWith(
            "## --data\n\n- Short form: `-d`\n- Argument: `<data>`\n- Repeat: yes, each use adds to the ones before it\n\nHTTP POST data.\n\n"
            + "(HTTP MQTT) Send the specified data in a POST request to the HTTP server, in the same way that a browser does",
            section);
        Assert.Contains("\n\nExamples:\n\n```\ncurl -d \"name=curl\" https://example.com\ncurl -d \"name=curl\" -d \"tool=cmdline\" https://example.com\ncurl -d @filename https://example.com\n```\n\n", section);
    }

    [TestMethod]
    [DataRow("silent", "- Turn off with: `--no-silent`")]
    [DataRow("no-alpn", "- Turn back on with: `--alpn`")]
    [DataRow("show-headers", "- Also accepted as: `--include`")]
    [DataRow("ssl", "- Also accepted as: `--ftp-ssl`")]
    [DataRow("ssl-reqd", "- Also accepted as: `--ftp-ssl-reqd`")]
    [DataRow("krb", "- Also accepted as: `--krb4`")]
    [DataRow("disable-eprt", "- Opposite spelling: `--eprt`, the same as `--no-disable-eprt`")]
    [DataRow("disable-epsv", "- Opposite spelling: `--epsv`, the same as `--no-disable-epsv`")]
    [DataRow("silent", "- Repeat: yes, but a repeat has no extra effect")]
    [DataRow("max-time", "- Repeat: yes, the last one given is used")]
    [DataRow("output", "- Repeat: once per URL: give it once for each URL on the command line")]
    [DataRow("ai-help", "- Repeat: not stated; see the description")]
    [DataRow("ai-help", "- Argument: `[category]`")]
    [DataRow("verbose", "- Short form: `-v`")]
    [DataRow("verbose", "- Argument: none")]
    public void TryGetMarkdown_OptionSection_CarriesTheFact(string longName, string factLine)
    {
        string section = WrittenOptionSection(WrittenAllMarkdown(), longName);

        AssertContains($"\n{factLine}\n", section);
        Assert.Contains($"\n{factLine}\n", OptionSection(AllMarkdown, longName));
    }

    [TestMethod]
    [DataRow("log-level", "<level>", "Curl's own diagnostic log, not curl's -v: none (the default), error, warning, info or verbose, to standard error or the --log-file.")]
    [DataRow("log-file", "<file>", "Write Curl's own diagnostic log, not curl's -v, to this file instead of standard error; at info level unless --log-level says otherwise.")]
    public void TryGetMarkdown_CurlOwnDiagnosticLogOption_IsDocumentedUnderCurlButNotInHelp(string longName, string argument, string description)
    {
        string section = WrittenOptionSection(WrittenMarkdown("curl"), longName);

        AssertHas("starts with its facts", section.StartsWith($"## --{longName}\n\n- Short form: none\n- Argument: `{argument}`\n", StringComparison.Ordinal));
        AssertContains($"\n\n{description}\n", section);
        AssertHas("section is in --ai-help all", AllMarkdown.Contains(section, StringComparison.Ordinal));
        AssertHas("not listed by --help all", !HelpLongNames("all").Contains(longName));
        AssertHas("not in the manual", !string.Join('\n', CurlManual.Lines()).Contains($"--{longName}", StringComparison.Ordinal));
        Assert.StartsWith($"## --{longName}\n\n- Short form: none\n- Argument: `{argument}`\n", section);
        Assert.Contains($"\n\n{description}\n", section);
        Assert.Contains(section, AllMarkdown);
        Assert.DoesNotContain(longName, HelpLongNames("all").ToArray());
        Assert.DoesNotContain($"--{longName}", string.Join('\n', CurlManual.Lines()));
    }

    [TestMethod]
    [DataRow("data", "- Turn off with:")]
    [DataRow("data", "- Not supported by this build yet")]
    [DataRow("data", "- Also accepted as:")]
    public void TryGetMarkdown_OptionSection_LeavesOutAFactThatDoesNotApply(string longName, string factLine)
    {
        string section = WrittenOptionSection(WrittenAllMarkdown(), longName);

        AssertHas($"leaves out \"{factLine}\"", !section.Contains($"\n{factLine}", StringComparison.Ordinal));
        Assert.DoesNotContain($"\n{factLine}", OptionSection(AllMarkdown, longName));
    }

    [TestMethod]
    public void TryGetMarkdown_TlsEarlyData_DescribesItAsHonoured()
    {
        // BL-1105: the hand-built TLS client sends the request as 0-RTT early data on a resumed session.
        string section = WrittenOptionSection(WrittenAllMarkdown(), "tls-earlydata");

        AssertHas("does not say it is not supported", !section.Contains("\n- Not supported by this build yet", StringComparison.Ordinal));
        AssertContains("\n- Turn off with: `--no-tls-earlydata`\n", section);
        AssertContains("early data", section);
        Assert.DoesNotContain("\n- Not supported by this build yet", section);
        Assert.Contains("\n- Turn off with: `--no-tls-earlydata`\n", section);
        Assert.Contains("early data", section);
    }

    [TestMethod]
    public void TryGetMarkdown_OptionThisBuildDoesNotParse_SaysSo()
    {
        string unparsed = HelpLongNames("all")
            .First(name => !name.StartsWith("no-", StringComparison.Ordinal) && !CommandLineOptionTable.Rows.Any(row => row.LongName == name));
        Diagnostics.Arrange("first unparsed option", "--" + unparsed);
        string section = WrittenOptionSection(WrittenAllMarkdown(), unparsed);

        AssertContains("\n- Not supported by this build yet: curl refuses it with exit 2.\n", section);
        Assert.Contains("\n- Not supported by this build yet: curl refuses it with exit 2.\n", OptionSection(AllMarkdown, unparsed));
    }

    [TestMethod]
    public void TryGetMarkdown_All_PrintsEveryCategoryThenExitCodesAndWriteOutVariables()
    {
        string all = WrittenAllMarkdown();
        foreach ((string name, string description) in Categories())
        {
            AssertContains($"# {name}: {description}\n\n## --", all);
            Assert.Contains($"# {name}: {description}\n\n## --", AllMarkdown);
        }

        AssertContains("\n# Exit codes\n\nThere are a bunch of different error codes", all);
        AssertContains("\n- `0`\n\n  Success. The operation completed successfully according to the instructions.\n", all);
        AssertContains("\n- `100`\n\n  A value or data field grew larger than allowed.\n", all);
        AssertContains("\n# --write-out variables\n\nUse each as `%{name}` in the `--write-out` format.\n\n- `certs`\n\n  Output the certificate chain with details.", all);
        AssertHas("ends with xfer_id", all.EndsWith("\n- `xfer_id`\n\n  The numerical identifier of the last transfer done. -1 if no transfer has been started yet for the handle. The transfer id is unique among all transfers performed using the same connection cache. (Added in 8.2.0)\n", StringComparison.Ordinal));
        Diagnostics.Diff("ALL against all", all, WrittenMarkdown("ALL"));
        Assert.Contains("\n# Exit codes\n\nThere are a bunch of different error codes", AllMarkdown);
        Assert.Contains("\n- `0`\n\n  Success. The operation completed successfully according to the instructions.\n", AllMarkdown);
        Assert.Contains("\n- `100`\n\n  A value or data field grew larger than allowed.\n", AllMarkdown);
        Assert.Contains("\n# --write-out variables\n\nUse each as `%{name}` in the `--write-out` format.\n\n- `certs`\n\n  Output the certificate chain with details.", AllMarkdown);
        Assert.EndsWith("\n- `xfer_id`\n\n  The numerical identifier of the last transfer done. -1 if no transfer has been started yet for the handle. The transfer id is unique among all transfers performed using the same connection cache. (Added in 8.2.0)\n", AllMarkdown);
        Assert.AreEqual(AllMarkdown, Markdown("ALL"));
    }

    [TestMethod]
    public void TryGetMarkdown_All_PlacesEveryParsedOptionUnderACategory()
    {
        // An option added to CommandLineOptionTable fails here until --ai-help documents it.
        string all = WrittenAllMarkdown();
        Diagnostics.Arrange("parsed options", CommandLineOptionTable.Rows.Count());
        string[] unplaced = [.. CommandLineOptionTable.Rows
            .Where(row => !all.Contains($"\n## --{row.LongName}\n", StringComparison.Ordinal) && !all.Contains($"`--{row.LongName}`", StringComparison.Ordinal))
            .Select(row => "--" + row.LongName)];
        Diagnostics.Act("options with no place", CommandLineParseDiagnostics.QuoteEach(unplaced));
        Diagnostics.Assert("options with no place", CommandLineParseDiagnostics.QuoteEach([]), CommandLineParseDiagnostics.QuoteEach(unplaced));
        foreach (CommandLineOption row in CommandLineOptionTable.Rows)
        {
            Assert.IsTrue(
                AllMarkdown.Contains($"\n## --{row.LongName}\n", StringComparison.Ordinal) || AllMarkdown.Contains($"`--{row.LongName}`", StringComparison.Ordinal),
                $"--{row.LongName} has no place in --ai-help all.");
        }
    }

    [TestMethod]
    public void TryGetMarkdown_All_IsUnwrappedMarkdownWithLineFeedsOnly()
    {
        string[] lines = WrittenAllMarkdown().Split('\n');
        Diagnostics.Act("line count", lines.Length);
        Diagnostics.Assert("lines with a tab", 0, lines.Count(line => line.Contains('\t', StringComparison.Ordinal)));
        Diagnostics.Assert("lines with a carriage return", 0, lines.Count(line => line.Contains('\r', StringComparison.Ordinal)));
        Diagnostics.Assert("last line", "\"\"", "\"" + lines[^1] + "\"");
        bool inCode = false;
        foreach (string line in lines)
        {
            inCode ^= line == "```";
            Assert.DoesNotContain("\t", line);
            Assert.DoesNotContain("\r", line);
            Assert.IsTrue(inCode || line == "```" || !line.TrimStart().Contains("  ", StringComparison.Ordinal), line);
        }

        Diagnostics.Assert("code fence left open", false, inCode);
        Assert.IsFalse(inCode, "A code fence is left open.");
        Assert.AreEqual(string.Empty, lines[^1]);
    }

    [TestMethod]
    [DataRow("bogus")]
    [DataRow("category")]
    [DataRow("-v")]
    public void TryGetMarkdown_UnknownCategory_ReturnsFalse(string subject)
    {
        Diagnostics.Arrange("subject", "\"" + subject + "\"");
        bool found = CurlAiHelpText.TryGetMarkdown(subject, out string markdown);
        Diagnostics.Act("found", found);
        Diagnostics.Act("markdown", "\"" + markdown + "\"");

        Diagnostics.Assert("found", false, found);
        Diagnostics.Assert("markdown", "\"\"", "\"" + markdown + "\"");
        Assert.IsFalse(CurlAiHelpText.TryGetMarkdown(subject, out markdown));
        Assert.AreEqual(string.Empty, markdown);
    }

    [TestMethod]
    public void UnknownCategoryLines_AreWhatHelpPrintsForAnUnknownCategory()
    {
        Diagnostics.Arrange("help subject", "bogus");
        string[] help = [.. CurlHelpText.Lines("bogus", CurlHelpText.DefaultColumns)];
        string[] aiHelp = [.. CurlAiHelpText.UnknownCategoryLines()];
        Diagnostics.Act("unknown category lines", CommandLineParseDiagnostics.QuoteEach(aiHelp));

        Diagnostics.Assert("unknown category lines", CommandLineParseDiagnostics.QuoteEach(help), CommandLineParseDiagnostics.QuoteEach(aiHelp));
        CollectionAssert.AreEqual(CurlHelpText.Lines("bogus", CurlHelpText.DefaultColumns).ToArray(), CurlAiHelpText.UnknownCategoryLines().ToArray());
    }

    private static string Markdown(string? subject)
    {
        Assert.IsTrue(CurlAiHelpText.TryGetMarkdown(subject, out string markdown));
        return markdown;
    }

    /// <summary>The categories, as <c>--help category</c> lists them.</summary>
    private static IEnumerable<(string Name, string Description)> Categories() =>
        CurlHelpText.Lines("category", CurlHelpText.DefaultColumns).Select(line => (line[1..12].TrimEnd(), line[13..]));

    /// <summary>The long names of the options <c>--help &lt;subject&gt;</c> lists at 200 columns, one row a line.</summary>
    private static IEnumerable<string> HelpLongNames(string subject) =>
        CurlHelpText.Lines(subject, 200)
            .Where(line => line.StartsWith(' '))
            .Select(line => line[(line.IndexOf("--", StringComparison.Ordinal) + 2)..].Split(' ')[0]);

    private static IEnumerable<string> SectionLines(string markdown, string heading) =>
        markdown.Split('\n').SkipWhile(line => line != heading).Skip(1).TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal));

    /// <summary>The option's first section in <paramref name="markdown"/>, from its heading to the next heading.</summary>
    private static string OptionSection(string markdown, string longName)
    {
        int start = markdown.IndexOf($"## --{longName}\n", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, start, longName);
        int end = markdown.IndexOf("\n#", start + 1, StringComparison.Ordinal);
        return markdown[start..(end + 1)];
    }

    /// <summary>Returns <see cref="Markdown"/> for <paramref name="subject"/>, writing the subject and the Markdown as diagnostics.</summary>
    private string WrittenMarkdown(string? subject)
    {
        Diagnostics.Arrange("subject", subject is null ? "null" : "\"" + subject + "\"");
        string markdown = Markdown(subject);
        Diagnostics.Act("markdown length", markdown.Length);
        Diagnostics.Bytes("markdown", System.Text.Encoding.UTF8.GetBytes(markdown));
        return markdown;
    }

    /// <summary>Returns <c>--ai-help all</c>, built once for the class, writing it as diagnostics.</summary>
    private string WrittenAllMarkdown()
    {
        Diagnostics.Arrange("subject", "\"all\"");
        Diagnostics.Act("markdown length", AllMarkdown.Length);
        return AllMarkdown;
    }

    /// <summary>Returns <see cref="OptionSection"/>, writing the option and its section as diagnostics.</summary>
    private string WrittenOptionSection(string markdown, string longName)
    {
        Diagnostics.Arrange("option", "--" + longName);
        string section = OptionSection(markdown, longName);
        Diagnostics.Act("section", section);
        return section;
    }

    private void AssertContains(string expected, string text) =>
        Diagnostics.Assert($"contains \"{expected.Replace("\n", "\\n", StringComparison.Ordinal)}\"", true, text.Contains(expected, StringComparison.Ordinal));

    private void AssertHas(string label, bool actual) => Diagnostics.Assert(label, true, actual);
}
