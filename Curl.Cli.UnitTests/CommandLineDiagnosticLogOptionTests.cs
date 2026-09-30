using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser reads Curl's own diagnostic log options, <c>--log-level</c> and <c>--log-file</c>
/// (BL-918, ADR-0222 decision 1): the five levels in any case, anything else refused as badly used with
/// exit 2, <c>--log-file</c> alone meaning <c>info</c>, the last occurrence winning, both global across
/// <c>--next</c>, and both read from a <c>-K</c> file.
/// </summary>
[TestClass]
public sealed class CommandLineDiagnosticLogOptionTests
{
    private const string Url = "file:///nx";

    [TestMethod]
    [DataRow("none", DiagnosticLogLevel.None)]
    [DataRow("error", DiagnosticLogLevel.Error)]
    [DataRow("warning", DiagnosticLogLevel.Warning)]
    [DataRow("info", DiagnosticLogLevel.Info)]
    [DataRow("verbose", DiagnosticLogLevel.Verbose)]
    [DataRow("NoNe", DiagnosticLogLevel.None)]
    [DataRow("ERROR", DiagnosticLogLevel.Error)]
    [DataRow("Warning", DiagnosticLogLevel.Warning)]
    [DataRow("iNFO", DiagnosticLogLevel.Info)]
    [DataRow("VerBose", DiagnosticLogLevel.Verbose)]
    public void Parse_LogLevel_SetsThatLevel(string value, DiagnosticLogLevel level)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--log-level", value, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(level, result.Options.DiagnosticLogLevel);
        Assert.IsNull(result.Options.DiagnosticLogFile);
    }

    [TestMethod]
    public void Parse_NoLogOption_LogsNothing()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url]);

        Assert.AreEqual(DiagnosticLogLevel.None, result.Options!.DiagnosticLogLevel);
        Assert.IsNull(result.Options.DiagnosticLogFile);
    }

    [TestMethod]
    [DataRow("bogus")]
    [DataRow("")]
    [DataRow("3")]
    public void Parse_LogLevelNotALevel_IsRefusedAsBadlyUsed(string value)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--log-level", value, Url]);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "curl: option --log-level: is badly used here", "curl: try 'curl --help' or 'curl --manual' for more information" },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_LogFileAlone_LogsAtInfoToThatFile()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--log-file", "x.log", Url]);

        Assert.AreEqual(DiagnosticLogLevel.Info, result.Options!.DiagnosticLogLevel);
        Assert.AreEqual("x.log", result.Options.DiagnosticLogFile);
    }

    [TestMethod]
    [DataRow("--log-file x.log --log-level error")]
    [DataRow("--log-level error --log-file x.log")]
    public void Parse_LogFileWithLogLevel_TheLevelWins(string arguments)
    {
        CommandLineParseResult result = CommandLineParser.Parse([.. arguments.Split(' '), Url]);

        Assert.AreEqual(DiagnosticLogLevel.Error, result.Options!.DiagnosticLogLevel);
        Assert.AreEqual("x.log", result.Options.DiagnosticLogFile);
    }

    [TestMethod]
    public void Parse_LogFileWithLogLevelNone_LogsNothing()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--log-file", "x.log", "--log-level", "none", Url]);

        Assert.AreEqual(DiagnosticLogLevel.None, result.Options!.DiagnosticLogLevel);
    }

    [TestMethod]
    public void Parse_RepeatedLogOptions_TheLastOneWins()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--log-level", "verbose", "--log-file", "a.log", "--log-level", "warning", "--log-file", "b.log", Url]);

        Assert.AreEqual(DiagnosticLogLevel.Warning, result.Options!.DiagnosticLogLevel);
        Assert.AreEqual("b.log", result.Options.DiagnosticLogFile);
    }

    [TestMethod]
    public void Parse_LogOptionsBeforeNext_HoldForEveryGroup()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--log-level", "verbose", "--log-file", "x.log", Url, "--next", Url]);

        Assert.HasCount(2, result.Groups);
        foreach (CommandLineOptions group in result.Groups)
        {
            Assert.AreEqual(DiagnosticLogLevel.Verbose, group.DiagnosticLogLevel);
            Assert.AreEqual("x.log", group.DiagnosticLogFile);
        }
    }

    [TestMethod]
    public void Parse_LogLevelAfterNext_HoldsForTheFirstGroupToo()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url, "--next", "--log-level", "error", Url]);

        Assert.AreEqual(DiagnosticLogLevel.Error, result.Groups[0].DiagnosticLogLevel);
    }

    [TestMethod]
    public void Parse_NoLogLevel_IsRefusedAsNotReversible()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--no-log-level", "info", Url]);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual("curl: option --no-log-level: the given option cannot be reversed with a --no- prefix", result.Refusal.StandardErrorLines[0]);
    }

    [TestMethod]
    public void Parse_LogOptionsInConfigFile_AreRead()
    {
        RecordingDataFileReader reader = new();
        reader.Files["log.cfg"] = Encoding.UTF8.GetBytes("log-level = verbose\nlog-file = x.log\n");

        CommandLineParseResult result = CommandLineParser.Parse(["-K", "log.cfg", Url], _ => false, new NoPasswordPrompt(), reader);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(DiagnosticLogLevel.Verbose, result.Options.DiagnosticLogLevel);
        Assert.AreEqual("x.log", result.Options.DiagnosticLogFile);
    }

    private sealed class NoPasswordPrompt : IPasswordPrompt
    {
        public string ReadPassword(string prompt) => string.Empty;
    }
}
