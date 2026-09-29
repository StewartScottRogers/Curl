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

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void TryGetMarkdown_NoSubject_PrintsTheIndex(string? subject)
    {
        string index = Markdown(subject);

        Assert.StartsWith("# curl --ai-help\n\nThis `curl` is curl 8.21.0 reimplemented in C#", index);
        Assert.Contains("\n## Categories\n", index);
        Assert.Contains("\n## Most-used options\n", index);
        Assert.EndsWith("Run `curl --ai-help <category>` for one category's options in full, or `curl --ai-help all` for every option, the exit codes and the `--write-out` variables.\n", index);
    }

    [TestMethod]
    public void TryGetMarkdown_NoSubject_ListsEveryCategoryWithTheCommandThatExpandsIt()
    {
        string index = Markdown(null);

        foreach ((string name, string description) in Categories())
        {
            Assert.Contains($"\n- `{name}`: {description}. Run `curl --ai-help {name}`\n", index);
        }
    }

    [TestMethod]
    public void TryGetMarkdown_NoSubject_ListsTwentyOptionsWithTheirShortFormArgumentAndSummary()
    {
        string index = Markdown(null);
        string[] mostUsed = [.. SectionLines(index, "## Most-used options").Where(line => line.StartsWith("- `--", StringComparison.Ordinal))];

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
        string http = Markdown(subject);

        Assert.StartsWith("# http: HTTP and HTTPS protocol\n\n## --", http);
        foreach (string longName in HelpLongNames("http"))
        {
            Assert.Contains($"\n## --{longName}\n", http);
        }

        Assert.DoesNotContain("\n## --ftp-pasv\n", http);
    }

    [TestMethod]
    public void TryGetMarkdown_OptionSection_GivesItsFactsSummaryAndManualText()
    {
        string section = OptionSection(Markdown("http"), "data");

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
        Assert.Contains($"\n{factLine}\n", OptionSection(AllMarkdown, longName));
    }

    [TestMethod]
    [DataRow("data", "- Turn off with:")]
    [DataRow("data", "- Not supported by this build yet")]
    [DataRow("data", "- Also accepted as:")]
    public void TryGetMarkdown_OptionSection_LeavesOutAFactThatDoesNotApply(string longName, string factLine)
    {
        Assert.DoesNotContain($"\n{factLine}", OptionSection(AllMarkdown, longName));
    }

    [TestMethod]
    public void TryGetMarkdown_OptionThisBuildDoesNotParse_SaysSo()
    {
        string unparsed = HelpLongNames("all")
            .First(name => !name.StartsWith("no-", StringComparison.Ordinal) && !CommandLineOptionTable.Rows.Any(row => row.LongName == name));

        Assert.Contains("\n- Not supported by this build yet: curl refuses it with exit 2.\n", OptionSection(AllMarkdown, unparsed));
    }

    [TestMethod]
    public void TryGetMarkdown_All_PrintsEveryCategoryThenExitCodesAndWriteOutVariables()
    {
        foreach ((string name, string description) in Categories())
        {
            Assert.Contains($"# {name}: {description}\n\n## --", AllMarkdown);
        }

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
        string[] lines = AllMarkdown.Split('\n');
        bool inCode = false;
        foreach (string line in lines)
        {
            inCode ^= line == "```";
            Assert.DoesNotContain("\t", line);
            Assert.DoesNotContain("\r", line);
            Assert.IsTrue(inCode || line == "```" || !line.TrimStart().Contains("  ", StringComparison.Ordinal), line);
        }

        Assert.IsFalse(inCode, "A code fence is left open.");
        Assert.AreEqual(string.Empty, lines[^1]);
    }

    [TestMethod]
    [DataRow("bogus")]
    [DataRow("category")]
    [DataRow("-v")]
    public void TryGetMarkdown_UnknownCategory_ReturnsFalse(string subject)
    {
        Assert.IsFalse(CurlAiHelpText.TryGetMarkdown(subject, out string markdown));
        Assert.AreEqual(string.Empty, markdown);
    }

    [TestMethod]
    public void UnknownCategoryLines_AreWhatHelpPrintsForAnUnknownCategory()
    {
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
}
