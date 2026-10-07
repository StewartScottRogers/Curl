using System.Text;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <c>-h</c> / <c>--help</c> and <c>-M</c> / <c>--manual</c> as curl 8.21.0 parses them, measured with
/// the mingw build on Windows on 2026-09-27. Both end parsing where they stand, as <c>-V</c> does, so
/// nothing after them is read or refused and no URL is needed, and a refusal before them still wins.
/// <c>--help</c> reads its subject from the attached value or else the next argument, whatever it is
/// (<c>curl -h -v</c> prints <c>-v</c>'s manual section, <c>curl --help http://x</c> the unknown-category
/// page); <c>-h</c> in a bundle counts only as its last letter (<c>-vh</c> prints the usage page, while
/// <c>-hv</c> reports no URL and <c>-hv &lt;url&gt;</c> transfers without <c>-v</c>). <c>--no-help</c> is
/// refused as not reversible; <c>--no-manual</c> is accepted and does nothing. A <c>manual</c> line in a
/// <c>-K</c> file is ignored, while a <c>help</c> line there is reported in <see cref="CommandLineParseResult.ConfigFileHelpSubjects"/> and parsing carries on.
/// </summary>
[TestClass]
public sealed class CommandLineHelpAndManualOptionTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("-h", null)]
    [DataRow("--help", null)]
    [DataRow("--help=", null)]
    [DataRow("-h all", "all")]
    [DataRow("--help all", "all")]
    [DataRow("--help=all", "all")]
    [DataRow("--help all http", "all")]
    [DataRow("--help all --bogus", "all")]
    [DataRow("-h -v", "-v")]
    [DataRow("-h -M", "-M")]
    [DataRow("-h --bogus", "--bogus")]
    [DataRow("-h http://example.invalid", "http://example.invalid")]
    [DataRow("-vh", null)]
    [DataRow("-sh http", "http")]
    [DataRow("http://example.invalid --help", null)]
    [DataRow("-s --help -Q", "-Q")]
    public void Parse_Help_IsAcceptedAndAsksForHelpWithItsSubject(string arguments, string? subject)
    {
        CommandLineParseResult result = Parse(arguments.Split(' '));

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        AssertRequestDiagnostics(result, help: true, manual: false, version: false);
        Diagnostics.Assert("help subject", subject, result.Options?.HelpSubject);
        Diagnostics.Assert("warning lines after transfers", "[]", CommandLineParseDiagnostics.QuoteEach(result.WarningLinesAfterTransfers));
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.HelpRequested);
        Assert.AreEqual(subject, result.Options.HelpSubject);
        Assert.IsFalse(result.Options.ManualRequested);
        Assert.IsFalse(result.Options.VersionRequested);
        Assert.IsEmpty(result.WarningLinesAfterTransfers);
    }

    [TestMethod]
    public void Parse_HelpWithEmptyNextArgument_AsksForTheUsagePage()
    {
        CommandLineParseResult result = Parse(["--help", string.Empty, "-V"]);

        Diagnostics.Assert("help requested", true, result.Options?.HelpRequested);
        Diagnostics.Assert("help subject", null, result.Options?.HelpSubject);
        Diagnostics.Assert("version requested", false, result.Options?.VersionRequested);
        Assert.IsTrue(result.Options!.HelpRequested);
        Assert.IsNull(result.Options.HelpSubject);
        Assert.IsFalse(result.Options.VersionRequested);
    }

    [TestMethod]
    public void Parse_HelpSubjectThatLooksLikeAnOption_IsNotApplied()
    {
        CommandLineParseResult result = Parse(["-h", "-s"]);

        Diagnostics.Assert("silent", false, result.Options?.Silent);
        Assert.IsFalse(result.Options!.Silent);
    }

    [TestMethod]
    [DataRow("-hv")]
    [DataRow("-hs")]
    [DataRow("-hM")]
    [DataRow("-hZZ")]
    public void Parse_HelpLetterBeforeOtherLetters_IsReadAsNothing(string bundle)
    {
        CommandLineParseResult result = Parse([bundle]);

        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Diagnostics.Assert(
            "stderr lines",
            CommandLineParseDiagnostics.QuoteEach(["curl: (2) no URL specified", CommandLineRefusal.TryHelpLine]),
            CommandLineParseDiagnostics.QuoteEach(result.Refusal?.StandardErrorLines ?? []));
        Assert.IsFalse(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[] { "curl: (2) no URL specified", CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_HelpLetterBeforeOtherLettersWithUrl_TransfersWithoutThoseLetters()
    {
        CommandLineParseResult result = Parse(["-hs", "http://127.0.0.1:1"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("help requested", false, result.Options?.HelpRequested);
        Diagnostics.Assert("silent", false, result.Options?.Silent);
        Diagnostics.Assert("urls", "[\"http://127.0.0.1:1\"]", CommandLineParseDiagnostics.QuoteEach(result.Options?.Urls ?? []));
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.HelpRequested);
        Assert.IsFalse(result.Options.Silent);
        CollectionAssert.AreEqual(new[] { "http://127.0.0.1:1" }, result.Options.Urls.ToArray());
    }

    [TestMethod]
    [DataRow("--bogus -h", "curl: option --bogus: is unknown")]
    [DataRow("--bogus -M", "curl: option --bogus: is unknown")]
    [DataRow("--no-help", "curl: option --no-help: the given option cannot be reversed with a --no- prefix")]
    [DataRow("--no-manual", "curl: (2) no URL specified")]
    [DataRow("--data --help", "curl: (2) no URL specified")]
    public void Parse_RefusedHelpOrManualCommandLines_AreRefusedAsCurlRefusesThem(string arguments, string firstLine)
    {
        CommandLineParseResult result = Parse(arguments.Split(' '));

        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Diagnostics.Assert("first stderr line", firstLine, result.Refusal?.StandardErrorLines.FirstOrDefault());
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(firstLine, result.Refusal.StandardErrorLines[0]);
    }

    [TestMethod]
    [DataRow("-M")]
    [DataRow("--manual")]
    [DataRow("--manual=x")]
    [DataRow("--manual extra")]
    [DataRow("-M --bogus")]
    [DataRow("-M -h")]
    [DataRow("-Mh")]
    [DataRow("-Mv")]
    [DataRow("-vM")]
    [DataRow("-s -M --help")]
    public void Parse_Manual_IsAcceptedAndAsksForTheManual(string arguments)
    {
        CommandLineParseResult result = Parse(arguments.Split(' '));

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("manual requested", true, result.Options?.ManualRequested);
        Diagnostics.Assert("help requested", false, result.Options?.HelpRequested);
        Diagnostics.Assert("urls", "[]", CommandLineParseDiagnostics.QuoteEach(result.Options?.Urls ?? []));
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.ManualRequested);
        Assert.IsFalse(result.Options.HelpRequested);
        Assert.IsEmpty(result.Options.Urls);
    }

    [TestMethod]
    [DataRow("-V -M")]
    [DataRow("-V -h")]
    [DataRow("--no-manual -V")]
    public void Parse_VersionFirst_AsksForTheVersionOnly(string arguments)
    {
        CommandLineParseResult result = Parse(arguments.Split(' '));

        AssertRequestDiagnostics(result, help: false, manual: false, version: true);
        Assert.IsTrue(result.Options!.VersionRequested);
        Assert.IsFalse(result.Options.ManualRequested);
        Assert.IsFalse(result.Options.HelpRequested);
    }

    [TestMethod]
    public void Parse_NoHelpOrManualOption_AsksForNeither()
    {
        CommandLineParseResult result = Parse(["file:///nx"]);

        Diagnostics.Assert("help requested", false, result.Options?.HelpRequested);
        Diagnostics.Assert("help subject", null, result.Options?.HelpSubject);
        Diagnostics.Assert("manual requested", false, result.Options?.ManualRequested);
        Assert.IsFalse(result.Options!.HelpRequested);
        Assert.IsNull(result.Options.HelpSubject);
        Assert.IsFalse(result.Options.ManualRequested);
    }

    [TestMethod]
    [DataRow("manual")]
    [DataRow("-M")]
    [DataRow("version")]
    [DataRow("-V")]
    public void Parse_VersionOrManualInConfigFile_IsIgnored(string line)
    {
        CommandLineParseResult withUrl = ParseWithConfigFile(line, "file:///nx");
        CommandLineParseResult alone = ParseWithConfigFile(line);

        Diagnostics.Assert("with url: accepted", true, withUrl.IsAccepted);
        Diagnostics.Assert("with url: manual requested", false, withUrl.Options?.ManualRequested);
        Diagnostics.Assert("with url: version requested", false, withUrl.Options?.VersionRequested);
        Diagnostics.Assert("with url: config file help subjects", "[]", CommandLineParseDiagnostics.QuoteEach(withUrl.ConfigFileHelpSubjects));
        Diagnostics.Assert("alone: accepted", false, alone.IsAccepted);
        Diagnostics.Assert("alone: first stderr line", "curl: (2) no URL specified", alone.Refusal?.StandardErrorLines.FirstOrDefault());
        Assert.IsTrue(withUrl.IsAccepted);
        Assert.IsFalse(withUrl.Options.ManualRequested);
        Assert.IsFalse(withUrl.Options.VersionRequested);
        Assert.IsEmpty(withUrl.ConfigFileHelpSubjects);
        Assert.IsFalse(alone.IsAccepted);
        Assert.AreEqual("curl: (2) no URL specified", alone.Refusal.StandardErrorLines[0]);
    }

    [TestMethod]
    [DataRow("help", null)]
    [DataRow("--help", null)]
    [DataRow("-h", null)]
    [DataRow("help = \"all\"", "all")]
    [DataRow("help all", "all")]
    public void Parse_HelpInConfigFile_ReportsItsPageAndCarriesOn(string line, string? subject)
    {
        // curl -K cfg: the page on standard output, then curl: (2) no URL specified; exit 2 (2026-09-27).
        CommandLineParseResult withUrl = ParseWithConfigFile(line, "file:///nx");
        CommandLineParseResult alone = ParseWithConfigFile(line);

        string expectedSubjects = CommandLineParseDiagnostics.QuoteEach([subject]);
        Diagnostics.Assert("with url: accepted", true, withUrl.IsAccepted);
        Diagnostics.Assert("with url: help requested", false, withUrl.Options?.HelpRequested);
        Diagnostics.Assert("with url: help subject", null, withUrl.Options?.HelpSubject);
        Diagnostics.Assert("with url: config file help subjects", expectedSubjects, CommandLineParseDiagnostics.QuoteEach(withUrl.ConfigFileHelpSubjects));
        Diagnostics.Assert("alone: accepted", false, alone.IsAccepted);
        Diagnostics.Assert("alone: first stderr line", "curl: (2) no URL specified", alone.Refusal?.StandardErrorLines.FirstOrDefault());
        Diagnostics.Assert("alone: config file help subjects", expectedSubjects, CommandLineParseDiagnostics.QuoteEach(alone.ConfigFileHelpSubjects));
        Assert.IsTrue(withUrl.IsAccepted);
        Assert.IsFalse(withUrl.Options.HelpRequested);
        Assert.IsNull(withUrl.Options.HelpSubject);
        CollectionAssert.AreEqual(new[] { subject }, withUrl.ConfigFileHelpSubjects.ToArray());
        Assert.IsFalse(alone.IsAccepted);
        Assert.AreEqual("curl: (2) no URL specified", alone.Refusal.StandardErrorLines[0]);
        CollectionAssert.AreEqual(new[] { subject }, alone.ConfigFileHelpSubjects.ToArray());
    }

    [TestMethod]
    public void Parse_HelpInConfigFileThenVersion_ReportsThePageAndAsksForTheVersion()
    {
        CommandLineParseResult result = ParseWithConfigFile("--help", "-V");

        Diagnostics.Assert("version requested", true, result.Options?.VersionRequested);
        Diagnostics.Assert("config file help subjects", "[null]", CommandLineParseDiagnostics.QuoteEach(result.ConfigFileHelpSubjects));
        Assert.IsTrue(result.Options!.VersionRequested);
        CollectionAssert.AreEqual(new string?[] { null }, result.ConfigFileHelpSubjects.ToArray());
    }

    [TestMethod]
    public void Parse_HelpInConfigFileThenUnknownOption_ReportsThePageAndRefuses()
    {
        CommandLineParseResult result = ParseWithConfigFile("-h\nbogus-QRC", "file:///nx");

        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Diagnostics.Assert("config file help subjects", "[null]", CommandLineParseDiagnostics.QuoteEach(result.ConfigFileHelpSubjects));
        Assert.IsFalse(result.IsAccepted);
        CollectionAssert.AreEqual(new string?[] { null }, result.ConfigFileHelpSubjects.ToArray());
    }

    [TestMethod]
    public void Parse_HelpLinesInConfigFiles_AreReportedInTheOrderRead()
    {
        RecordingDataFileReader reader = new();
        reader.Files["a.cfg"] = Encoding.UTF8.GetBytes("help\nhelp = http\n");
        reader.Files["b.cfg"] = Encoding.UTF8.GetBytes("help all\n");
        Diagnostics.Bytes("a.cfg", reader.Files["a.cfg"]);
        Diagnostics.Bytes("b.cfg", reader.Files["b.cfg"]);

        CommandLineParseResult result = Parse(["-K", "a.cfg", "-K", "b.cfg", "file:///nx"], reader);

        Diagnostics.Assert("config file help subjects", "[null, \"http\", \"all\"]", CommandLineParseDiagnostics.QuoteEach(result.ConfigFileHelpSubjects));
        CollectionAssert.AreEqual(new[] { null, "http", "all" }, result.ConfigFileHelpSubjects.ToArray());
    }

    [TestMethod]
    public void Parse_NoConfigFileHelp_ReportsNoPages()
    {
        CommandLineParseResult result = Parse(["-h"]);

        Diagnostics.Assert("config file help subjects", "[]", CommandLineParseDiagnostics.QuoteEach(result.ConfigFileHelpSubjects));
        Assert.IsEmpty(result.ConfigFileHelpSubjects);
    }

    private void AssertRequestDiagnostics(CommandLineParseResult result, bool help, bool manual, bool version)
    {
        Diagnostics.Assert("help requested", help, result.Options?.HelpRequested);
        Diagnostics.Assert("manual requested", manual, result.Options?.ManualRequested);
        Diagnostics.Assert("version requested", version, result.Options?.VersionRequested);
    }

    private CommandLineParseResult ParseWithConfigFile(string lines, params string[] after)
    {
        RecordingDataFileReader reader = new();
        reader.Files["hk.txt"] = Encoding.UTF8.GetBytes(lines + "\n");
        Diagnostics.Bytes("hk.txt", reader.Files["hk.txt"]);
        return Parse(["-K", "hk.txt", .. after], reader);
    }

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        return result;
    }

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments, RecordingDataFileReader reader)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments, _ => false, new NoPasswordPrompt(), reader);
        Diagnostics.ActParse(result);
        return result;
    }

    private sealed class NoPasswordPrompt : IPasswordPrompt
    {
        public string ReadPassword(string prompt) => string.Empty;
    }
}
