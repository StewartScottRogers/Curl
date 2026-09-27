using System.Text;

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
/// <c>-K</c> file is ignored; so, for now, is a <c>help</c> line (task BL-369).
/// </summary>
[TestClass]
public sealed class CommandLineHelpAndManualOptionTests
{
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
        CommandLineParseResult result = CommandLineParser.Parse(arguments.Split(' '));

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
        CommandLineParseResult result = CommandLineParser.Parse(["--help", string.Empty, "-V"]);

        Assert.IsTrue(result.Options!.HelpRequested);
        Assert.IsNull(result.Options.HelpSubject);
        Assert.IsFalse(result.Options.VersionRequested);
    }

    [TestMethod]
    public void Parse_HelpSubjectThatLooksLikeAnOption_IsNotApplied()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-h", "-s"]);

        Assert.IsFalse(result.Options!.Silent);
    }

    [TestMethod]
    [DataRow("-hv")]
    [DataRow("-hs")]
    [DataRow("-hM")]
    [DataRow("-hZZ")]
    public void Parse_HelpLetterBeforeOtherLetters_IsReadAsNothing(string bundle)
    {
        CommandLineParseResult result = CommandLineParser.Parse([bundle]);

        Assert.IsFalse(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[] { "curl: (2) no URL specified", CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_HelpLetterBeforeOtherLettersWithUrl_TransfersWithoutThoseLetters()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-hs", "http://127.0.0.1:1"]);

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
        CommandLineParseResult result = CommandLineParser.Parse(arguments.Split(' '));

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
        CommandLineParseResult result = CommandLineParser.Parse(arguments.Split(' '));

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
        CommandLineParseResult result = CommandLineParser.Parse(arguments.Split(' '));

        Assert.IsTrue(result.Options!.VersionRequested);
        Assert.IsFalse(result.Options.ManualRequested);
        Assert.IsFalse(result.Options.HelpRequested);
    }

    [TestMethod]
    public void Parse_NoHelpOrManualOption_AsksForNeither()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["file:///nx"]);

        Assert.IsFalse(result.Options!.HelpRequested);
        Assert.IsNull(result.Options.HelpSubject);
        Assert.IsFalse(result.Options.ManualRequested);
    }

    [TestMethod]
    [DataRow("manual")]
    [DataRow("-M")]
    [DataRow("help")]
    [DataRow("help = \"all\"")]
    [DataRow("-h")]
    public void Parse_HelpOrManualInConfigFile_IsIgnored(string line)
    {
        RecordingDataFileReader reader = new();
        reader.Files["hk.txt"] = Encoding.UTF8.GetBytes(line + "\n");

        CommandLineParseResult withUrl = CommandLineParser.Parse(["-K", "hk.txt", "file:///nx"], _ => false, new NoPasswordPrompt(), reader);
        CommandLineParseResult alone = CommandLineParser.Parse(["-K", "hk.txt"], _ => false, new NoPasswordPrompt(), reader);

        Assert.IsTrue(withUrl.IsAccepted);
        Assert.IsFalse(withUrl.Options.HelpRequested);
        Assert.IsNull(withUrl.Options.HelpSubject);
        Assert.IsFalse(withUrl.Options.ManualRequested);
        Assert.IsFalse(alone.IsAccepted);
        Assert.AreEqual("curl: (2) no URL specified", alone.Refusal.StandardErrorLines[0]);
    }

    private sealed class NoPasswordPrompt : IPasswordPrompt
    {
        public string ReadPassword(string prompt) => string.Empty;
    }
}
