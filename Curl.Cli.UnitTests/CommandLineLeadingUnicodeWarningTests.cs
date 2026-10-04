using System.Text;

namespace Curl.Cli;

/// <summary>
/// Pins curl 8.21.0's warning about an option value that starts with a character in U+2000-U+203F
/// (BL-1224): its OpenSSL build on Linux and macOS, reading arguments as UTF-8, prints
/// <c>Warning: The argument '&lt;value&gt;' starts with a Unicode character. Maybe ASCII was intended?</c>
/// (wrapped at 79 columns) before using the value as given (<c>has_leading_unicode</c> and
/// <c>getparameter</c> in <c>src/tool_getparam.c</c>); its Windows Schannel build reads arguments in the
/// ANSI code page and prints nothing (measured 2026-10-02 with <c>-o "–x"</c> and <c>-H "“X: y"</c>).
/// Both platforms are tested on every platform through the parser's <c>isWindows</c> switch.
/// </summary>
[TestClass]
public sealed class CommandLineLeadingUnicodeWarningTests
{
    private const string Url = "file:///nonexist";

    private const string EnDashX = "–x";

    private const string LeftQuoteHeader = "“X: y";

    private const string LeftSingleQuoteData = "‘a";

    private static IReadOnlyList<string> WarningFor(string value) =>
    [
        $"Warning: The argument '{value}' starts with a Unicode character. Maybe ASCII was ",
        "Warning: intended?",
    ];

    [TestMethod]
    public void OutputValueStartingWithEnDashIsWarnedAboutOffWindowsAndStillNamesTheFile()
    {
        CommandLineParseResult result = Parse(["-o", EnDashX, Url], isWindows: false);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(WarningFor(EnDashX).ToList(), result.WarningLines.ToList());
        CollectionAssert.AreEqual(new[] { EnDashX }, result.Options.OutputFiles.ToList());
    }

    [TestMethod]
    public void HeaderValueStartingWithLeftDoubleQuoteIsWarnedAboutOffWindowsAndStillSent()
    {
        CommandLineParseResult result = Parse(["-H", LeftQuoteHeader, Url], isWindows: false);

        Assert.IsTrue(result.IsAccepted);
        // Three bytes longer than the en dash value in UTF-8, so curl's 79-column wrap cuts one word earlier.
        CollectionAssert.AreEqual(
            new[]
            {
                $"Warning: The argument '{LeftQuoteHeader}' starts with a Unicode character. Maybe ASCII ",
                "Warning: was intended?",
            },
            result.WarningLines.ToList());
        CollectionAssert.AreEqual(new[] { LeftQuoteHeader }, result.Options.Headers.ToList());
    }

    [TestMethod]
    public void DataValueStartingWithLeftSingleQuoteIsWarnedAboutOffWindowsAndStillPosted()
    {
        CommandLineParseResult result = Parse(["--data", LeftSingleQuoteData, Url], isWindows: false);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(WarningFor(LeftSingleQuoteData).ToList(), result.WarningLines.ToList());
        Assert.AreEqual(LeftSingleQuoteData, Encoding.UTF8.GetString(result.Options.PostData!.Value.Span));
    }

    [TestMethod]
    public void AttachedLongValueStartingWithEnDashIsWarnedAboutOffWindows()
    {
        CommandLineParseResult result = Parse(["--output=" + EnDashX, Url], isWindows: false);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(WarningFor(EnDashX).ToList(), result.WarningLines.ToList());
    }

    [TestMethod]
    [DataRow("῿x", DisplayName = "U+1FFF, just below the range")]
    [DataRow("⁀x", DisplayName = "U+2040, just above the range")]
    [DataRow("x–", DisplayName = "en dash after the first character")]
    [DataRow("out", DisplayName = "plain ASCII")]
    public void OutputValueNotStartingInTheRangeIsNotWarnedAboutOffWindows(string value)
    {
        CommandLineParseResult result = Parse(["-o", value, Url], isWindows: false);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow(" x", DisplayName = "U+2000, the first in the range")]
    [DataRow("‿x", DisplayName = "U+203F, the last in the range")]
    public void OutputValueStartingAtEitherEndOfTheRangeIsWarnedAboutOffWindows(string value)
    {
        CommandLineParseResult result = Parse(["-o", value, Url], isWindows: false);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual($"Warning: The argument '{value}' starts with a Unicode character. Maybe ASCII was ", result.WarningLines[0]);
    }

    [TestMethod]
    public void PositionalUrlStartingWithEnDashIsNotWarnedAboutOffWindows()
    {
        CommandLineParseResult result = Parse(["–" + Url], isWindows: false);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void DeprecatedValueOptionStartingWithEnDashGetsOnlyItsDeprecationWarningOffWindows()
    {
        CommandLineParseResult result = Parse(["--egd-file", EnDashX, Url], isWindows: false);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "Warning: --egd-file is deprecated and has no function anymore" }, result.WarningLines.ToList());
    }

    [TestMethod]
    [DataRow("-o", EnDashX)]
    [DataRow("-H", LeftQuoteHeader)]
    [DataRow("--data", LeftSingleQuoteData)]
    public void ValueStartingInTheRangeIsNotWarnedAboutOnWindows(string option, string value)
    {
        CommandLineParseResult result = Parse([option, value, Url], isWindows: true);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void SilentBeforeTheOptionHidesTheWarningOffWindows()
    {
        CommandLineParseResult result = Parse(["-s", "-o", EnDashX, Url], isWindows: false);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void SilentAfterTheOptionKeepsTheWarningOffWindows()
    {
        CommandLineParseResult result = Parse(["-o", EnDashX, "-s", Url], isWindows: false);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(WarningFor(EnDashX).ToList(), result.WarningLines.ToList());
    }

    [TestMethod]
    public void ConfigFileHeaderValueStartingWithLeftDoubleQuoteIsWarnedAboutOnWindows()
    {
        // Measured with real curl 8.21.0 (Windows, Schannel, 2026-10-04): a -K file's lines are UTF-8 bytes
        // on every platform, so its Windows build warns about them as its other builds do (upstream test 470).
        CommandLineParseResult result = ParseConfigFile("-H “host:fake”\n", silentFirst: false);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: The argument '“host:fake”' starts with a Unicode character. Maybe ",
                "Warning: ASCII was intended?",
            },
            result.WarningLines.ToList());
        CollectionAssert.AreEqual(new[] { "“host:fake”" }, result.Options.Headers.ToList());
    }

    [TestMethod]
    public void CommandLineHeaderValueStartingWithLeftDoubleQuoteIsNotWarnedAboutOnWindows()
    {
        CommandLineParseResult result = Parse(["-H", "“host:fake”", Url], isWindows: true);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void SilentBeforeTheConfigFileHidesItsWarningOnWindows()
    {
        CommandLineParseResult result = ParseConfigFile("-H “host:fake”\n", silentFirst: true);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void ReadsArgumentsAsUtf8FollowsThePlatformSwitch()
    {
        Assert.IsTrue(Parse([Url], isWindows: false).Options!.ReadsArgumentsAsUtf8);
        Assert.IsFalse(Parse([Url], isWindows: true).Options!.ReadsArgumentsAsUtf8);
    }

    [TestMethod]
    public void ProcessPlatformParseReadsArgumentsAsUtf8OnlyOffWindows()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url]);

        Assert.AreEqual(!OperatingSystem.IsWindows(), result.Options!.ReadsArgumentsAsUtf8);
    }

    private static CommandLineParseResult Parse(IReadOnlyList<string> arguments, bool isWindows) =>
        CommandLineParser.Parse(arguments, _ => true, new UnexpectedPasswordPrompt(), new RecordingDataFileReader(), isWindows);

    private static CommandLineParseResult ParseConfigFile(string contents, bool silentFirst)
    {
        RecordingDataFileReader reader = new();
        reader.Files["config.txt"] = Encoding.UTF8.GetBytes(contents);
        string[] arguments = silentFirst ? ["-s", "-K", "config.txt", Url] : ["-K", "config.txt", Url];
        return CommandLineParser.Parse(arguments, _ => true, new UnexpectedPasswordPrompt(), reader, isWindows: true);
    }

    private sealed class UnexpectedPasswordPrompt : IPasswordPrompt
    {
        public string ReadPassword(string prompt) => throw new AssertFailedException("No password prompt was expected.");
    }
}
