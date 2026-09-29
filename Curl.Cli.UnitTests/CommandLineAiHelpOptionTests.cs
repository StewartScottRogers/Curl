using System.Text;

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
        CommandLineParseResult result = CommandLineParser.Parse(arguments.Split(' '));

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.AiHelpRequested);
        Assert.AreEqual(subject, result.Options.AiHelpSubject);
        Assert.IsFalse(result.Options.HelpRequested);
    }

    [TestMethod]
    public void Parse_NoAiHelp_IsRefusedAsNotReversible()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--no-ai-help"]);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual("curl: option --no-ai-help: the given option cannot be reversed with a --no- prefix", result.Refusal.StandardErrorLines[0]);
    }

    [TestMethod]
    public void Parse_NoAiHelpOption_AsksForNoAiHelp()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["file:///nx"]);

        Assert.IsFalse(result.Options!.AiHelpRequested);
        Assert.IsNull(result.Options.AiHelpSubject);
    }

    [TestMethod]
    [DataRow("ai-help")]
    [DataRow("ai-help all")]
    public void Parse_AiHelpInConfigFile_IsIgnored(string line)
    {
        RecordingDataFileReader reader = new();
        reader.Files["ai.cfg"] = Encoding.UTF8.GetBytes(line + "\n");

        CommandLineParseResult result = CommandLineParser.Parse(["-K", "ai.cfg", "file:///nx"], _ => false, new NoPasswordPrompt(), reader);

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
