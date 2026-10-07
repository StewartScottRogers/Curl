using System.Text;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins how <c>--ai-help</c> is parsed (BL-911, ADR-0224): as <c>--help</c> is, it ends parsing where it
/// stands, so nothing after it is read and no URL is needed, and it reads its subject from the attached
/// value or else the next argument. <c>--no-ai-help</c> is refused as not reversible, and an
/// <c>ai-help</c> line in a <c>-K</c> file is ignored.
/// </summary>
[TestClass]
public sealed class CommandLineAiHelpOptionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("--ai-help", null)]
    [DataRow("--ai-help=", null)]
    [DataRow("--ai-help http", "http")]
    [DataRow("--ai-help=all", "all")]
    [DataRow("--ai-help all --bogus", "all")]
    [DataRow("--ai-help -v", "-v")]
    [DataRow("-s --ai-help", null)]
    public void Parse_AiHelp_IsAcceptedAndAsksForAiHelpWithItsSubject(string arguments, string? subject)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        string[] split = arguments.Split(' ');
        diagnostics.ArrangeArguments(split);

        CommandLineParseResult result = CommandLineParser.Parse(split);
        diagnostics.ActParse(result);
        diagnostics.Act("ai help requested", result.Options?.AiHelpRequested);
        diagnostics.Act("help requested", result.Options?.HelpRequested);

        diagnostics.Assert("ai help subject", subject, result.Options?.AiHelpSubject);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.AiHelpRequested);
        Assert.AreEqual(subject, result.Options.AiHelpSubject);
        Assert.IsFalse(result.Options.HelpRequested);
    }

    [TestMethod]
    public void Parse_NoAiHelp_IsRefusedAsNotReversible()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        string[] arguments = ["--no-ai-help"];
        diagnostics.ArrangeArguments(arguments);

        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        diagnostics.ActParse(result);

        const string Expected = "curl: option --no-ai-help: the given option cannot be reversed with a --no- prefix";
        diagnostics.Assert("first stderr line", Expected, result.Refusal?.StandardErrorLines[0]);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(Expected, result.Refusal.StandardErrorLines[0]);
    }

    [TestMethod]
    public void Parse_NoAiHelpOption_AsksForNoAiHelp()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        string[] arguments = ["file:///nx"];
        diagnostics.ArrangeArguments(arguments);

        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        diagnostics.ActParse(result);

        diagnostics.Assert("ai help requested", false, result.Options?.AiHelpRequested);
        diagnostics.Assert("ai help subject", null, result.Options?.AiHelpSubject);
        Assert.IsFalse(result.Options!.AiHelpRequested);
        Assert.IsNull(result.Options.AiHelpSubject);
    }

    [TestMethod]
    [DataRow("ai-help")]
    [DataRow("ai-help all")]
    public void Parse_AiHelpInConfigFile_IsIgnored(string line)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        RecordingDataFileReader reader = new();
        reader.Files["ai.cfg"] = Encoding.UTF8.GetBytes(line + "\n");
        string[] arguments = ["-K", "ai.cfg", "file:///nx"];
        diagnostics.Arrange("ai.cfg", line + "\\n");
        diagnostics.ArrangeArguments(arguments);

        CommandLineParseResult result = CommandLineParser.Parse(arguments, _ => false, new NoPasswordPrompt(), reader);
        diagnostics.ActParse(result);
        diagnostics.Act("config file help subjects", result.ConfigFileHelpSubjects.Count);

        diagnostics.Assert("ai help requested", false, result.Options?.AiHelpRequested);
        diagnostics.Assert("ai help subject", null, result.Options?.AiHelpSubject);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.AiHelpRequested);
        Assert.IsNull(result.Options.AiHelpSubject);
        Assert.IsEmpty(result.ConfigFileHelpSubjects);
    }

    private sealed class NoPasswordPrompt : IPasswordPrompt
    {
        public string ReadPassword(string prompt) => string.Empty;
    }
}
