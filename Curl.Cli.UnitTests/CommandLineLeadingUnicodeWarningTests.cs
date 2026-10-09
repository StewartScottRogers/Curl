using System.Text;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private static IReadOnlyList<string> WarningFor(string value) =>
    [
        $"Warning: The argument '{value}' starts with a Unicode character. Maybe ASCII was ",
        "Warning: intended?",
    ];

    [TestMethod]
    public void OutputValueStartingWithEnDashIsWarnedAboutOffWindowsAndStillNamesTheFile()
    {
        CommandLineParseResult result = Parse(["-o", EnDashX, Url], isWindows: false);

        AssertAcceptedWithWarnings(result, WarningFor(EnDashX));
        Diagnostics.Assert("output files", CommandLineParseDiagnostics.QuoteEach([EnDashX]), CommandLineParseDiagnostics.QuoteEach(result.Options?.OutputFiles ?? []));
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(WarningFor(EnDashX).ToList(), result.WarningLines.ToList());
        CollectionAssert.AreEqual(new[] { EnDashX }, result.Options.OutputFiles.ToList());
    }

    [TestMethod]
    public void HeaderValueStartingWithLeftDoubleQuoteIsWarnedAboutOffWindowsAndStillSent()
    {
        CommandLineParseResult result = Parse(["-H", LeftQuoteHeader, Url], isWindows: false);

        AssertAcceptedWithWarnings(
            result,
            [
                $"Warning: The argument '{LeftQuoteHeader}' starts with a Unicode character. Maybe ASCII ",
                "Warning: was intended?",
            ]);
        Diagnostics.Assert("headers", CommandLineParseDiagnostics.QuoteEach([LeftQuoteHeader]), CommandLineParseDiagnostics.QuoteEach(result.Options?.Headers ?? []));
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

        AssertAcceptedWithWarnings(result, WarningFor(LeftSingleQuoteData));
        if (result.Options?.PostData is { } postData)
        {
            Diagnostics.Bytes("post data", postData.ToArray());
        }

        Diagnostics.Assert("post data", LeftSingleQuoteData, result.Options?.PostData is { } data ? Encoding.UTF8.GetString(data.Span) : null);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(WarningFor(LeftSingleQuoteData).ToList(), result.WarningLines.ToList());
        Assert.AreEqual(LeftSingleQuoteData, Encoding.UTF8.GetString(result.Options.PostData!.Value.Span));
    }

    [TestMethod]
    public void AttachedLongValueStartingWithEnDashIsWarnedAboutOffWindows()
    {
        CommandLineParseResult result = Parse(["--output=" + EnDashX, Url], isWindows: false);

        AssertAcceptedWithWarnings(result, WarningFor(EnDashX));
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

        AssertAcceptedWithWarnings(result, []);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow(" x", DisplayName = "U+2000, the first in the range")]
    [DataRow("‿x", DisplayName = "U+203F, the last in the range")]
    public void OutputValueStartingAtEitherEndOfTheRangeIsWarnedAboutOffWindows(string value)
    {
        CommandLineParseResult result = Parse(["-o", value, Url], isWindows: false);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert(
            "first warning line",
            $"Warning: The argument '{value}' starts with a Unicode character. Maybe ASCII was ",
            result.WarningLines.Count > 0 ? result.WarningLines[0] : null);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual($"Warning: The argument '{value}' starts with a Unicode character. Maybe ASCII was ", result.WarningLines[0]);
    }

    [TestMethod]
    public void PositionalUrlStartingWithEnDashIsNotWarnedAboutOffWindows()
    {
        CommandLineParseResult result = Parse(["–" + Url], isWindows: false);

        AssertAcceptedWithWarnings(result, []);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void DeprecatedValueOptionStartingWithEnDashGetsOnlyItsDeprecationWarningOffWindows()
    {
        CommandLineParseResult result = Parse(["--egd-file", EnDashX, Url], isWindows: false);

        AssertAcceptedWithWarnings(result, ["Warning: --egd-file is deprecated and has no function anymore"]);
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

        AssertAcceptedWithWarnings(result, []);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void SilentBeforeTheOptionHidesTheWarningOffWindows()
    {
        CommandLineParseResult result = Parse(["-s", "-o", EnDashX, Url], isWindows: false);

        AssertAcceptedWithWarnings(result, []);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void SilentAfterTheOptionKeepsTheWarningOffWindows()
    {
        CommandLineParseResult result = Parse(["-o", EnDashX, "-s", Url], isWindows: false);

        AssertAcceptedWithWarnings(result, WarningFor(EnDashX));
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(WarningFor(EnDashX).ToList(), result.WarningLines.ToList());
    }

    [TestMethod]
    public void ConfigFileHeaderValueStartingWithLeftDoubleQuoteIsWarnedAboutOnWindows()
    {
        // Measured with real curl 8.21.0 (Windows, Schannel, 2026-10-04): a -K file's lines are UTF-8 bytes
        // on every platform, so its Windows build warns about them as its other builds do (upstream test 470).
        CommandLineParseResult result = ParseConfigFile("-H “host:fake”\n", silentFirst: false);

        AssertAcceptedWithWarnings(
            result,
            [
                "Warning: The argument '“host:fake”' starts with a Unicode character. Maybe ",
                "Warning: ASCII was intended?",
            ]);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: The argument '“host:fake”' starts with a Unicode character. Maybe ",
                "Warning: ASCII was intended?",
            },
            result.WarningLines.ToList());
        Assert.HasCount(1, result.Options.Headers);
    }

    [TestMethod]
    public void ConfigFileHeaderUserAgentAndRefererGoOutAsTheFileUtf8BytesOnWindows()
    {
        // Measured with real curl 8.21.0 (Windows, Schannel, 2026-10-08): a -K file's “quoted” values are
        // sent as their UTF-8 bytes E2 80 9C ... E2 80 9D, not the ANSI code page's 93 ... 94 (upstream
        // test 470), so each is held re-spelled for the ANSI encoder the request side uses (BL-1848).
        CommandLineParseResult result = ParseConfigFile("-H \"X-A: “quoted”\"\nuser-agent = “agent”\nreferer = “ref”\n", silentFirst: false);
        Encoding wireEncoding = result.Options!.ConfigFileWireTextEncoding!;
        string[] sent = [.. result.Options.Headers.Select(wireEncoding.GetBytes).Select(Convert.ToHexString)];
        string userAgent = Convert.ToHexString(wireEncoding.GetBytes(result.Options.UserAgent!));
        string referer = Convert.ToHexString(wireEncoding.GetBytes(result.Options.Referer!));

        string[] expected = [Convert.ToHexString(Encoding.UTF8.GetBytes("X-A: “quoted”"))];
        Diagnostics.Assert("header bytes", string.Join(",", expected), string.Join(",", sent));
        Diagnostics.Assert("user-agent bytes", Convert.ToHexString(Encoding.UTF8.GetBytes("“agent”")), userAgent);
        Diagnostics.Assert("referer bytes", Convert.ToHexString(Encoding.UTF8.GetBytes("“ref”")), referer);
        CollectionAssert.AreEqual(expected, sent);
        Assert.AreEqual(Convert.ToHexString(Encoding.UTF8.GetBytes("“agent”")), userAgent);
        Assert.AreEqual(Convert.ToHexString(Encoding.UTF8.GetBytes("“ref”")), referer);
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: The argument '“agent”' starts with a Unicode character. Maybe ",
                "Warning: ASCII was intended?",
                "Warning: The argument '“ref”' starts with a Unicode character. Maybe ASCII ",
                "Warning: was intended?",
            },
            result.WarningLines.ToList());
    }

    [TestMethod]
    public void CommandLineHeaderAndUserAgentAreHeldUnchangedOnWindows()
    {
        CommandLineParseResult result = Parse(["-H", "X-A: “quoted”", "-A", "x“agent”", Url], isWindows: true);

        Diagnostics.Assert("headers", CommandLineParseDiagnostics.QuoteEach(["X-A: “quoted”"]), CommandLineParseDiagnostics.QuoteEach(result.Options?.Headers ?? []));
        CollectionAssert.AreEqual(new[] { "X-A: “quoted”" }, result.Options!.Headers.ToList());
        Assert.AreEqual("x“agent”", result.Options.UserAgent);
    }

    [TestMethod]
    public void ConfigFileHeaderIsHeldUnchangedOffWindows()
    {
        RecordingDataFileReader reader = new();
        reader.Files["config.txt"] = Encoding.UTF8.GetBytes("-H \"X-A: “quoted”\"\n");
        Diagnostics.ArrangeArguments(["-K", "config.txt", Url]);

        CommandLineParseResult result = CommandLineParser.Parse(["-K", "config.txt", Url], _ => true, new UnexpectedPasswordPrompt(), reader, isWindows: false);
        Diagnostics.ActParse(result);

        Diagnostics.Assert("headers", CommandLineParseDiagnostics.QuoteEach(["X-A: “quoted”"]), CommandLineParseDiagnostics.QuoteEach(result.Options?.Headers ?? []));
        CollectionAssert.AreEqual(new[] { "X-A: “quoted”" }, result.Options!.Headers.ToList());
        Assert.IsNull(result.Options.ConfigFileWireTextEncoding);
    }

    [TestMethod]
    public void CommandLineHeaderValueStartingWithLeftDoubleQuoteIsNotWarnedAboutOnWindows()
    {
        CommandLineParseResult result = Parse(["-H", "“host:fake”", Url], isWindows: true);

        AssertAcceptedWithWarnings(result, []);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void SilentBeforeTheConfigFileHidesItsWarningOnWindows()
    {
        CommandLineParseResult result = ParseConfigFile("-H “host:fake”\n", silentFirst: true);

        AssertAcceptedWithWarnings(result, []);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void ReadsArgumentsAsUtf8FollowsThePlatformSwitch()
    {
        bool offWindows = Parse([Url], isWindows: false).Options!.ReadsArgumentsAsUtf8;
        bool onWindows = Parse([Url], isWindows: true).Options!.ReadsArgumentsAsUtf8;

        Diagnostics.Assert("reads arguments as UTF-8 off Windows", true, offWindows);
        Diagnostics.Assert("reads arguments as UTF-8 on Windows", false, onWindows);
        Assert.IsTrue(offWindows);
        Assert.IsFalse(onWindows);
    }

    [TestMethod]
    public void ProcessPlatformParseReadsArgumentsAsUtf8OnlyOffWindows()
    {
        Diagnostics.ArrangeArguments([Url]);
        CommandLineParseResult result = CommandLineParser.Parse([Url]);
        Diagnostics.ActParse(result);

        // Printed as a match, not the platform's own value, so the line reads the same on every platform.
        Diagnostics.Assert("reads arguments as UTF-8 exactly when off Windows", true, result.Options?.ReadsArgumentsAsUtf8 == !OperatingSystem.IsWindows());
        Assert.AreEqual(!OperatingSystem.IsWindows(), result.Options!.ReadsArgumentsAsUtf8);
    }

    [TestMethod]
    public void ExpandedDataLedByVariableFileBytesStartingWithLeftDoubleQuoteIsWarnedAboutOnWindows()
    {
        // Upstream test268, measured with curl 8.21.0 (Schannel, Windows) on 2026-10-08: the file's
        // E2 80 9C bytes lead the expanded value, so the warning names it although typed arguments never warn.
        CommandLineParseResult result = ParseWithVariableFile(["--variable", "hello@junk", "--expand-data", "{{hello:json}}", Url]);

        AssertAcceptedWithWarnings(result, WarningFor("“"));
        Diagnostics.Bytes("post data", result.Options?.PostData?.ToArray() ?? []);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(WarningFor("“").ToList(), result.WarningLines.ToList());
        CollectionAssert.AreEqual(new byte[] { 0xE2, 0x80, 0x9C }, result.Options.PostData!.Value.ToArray());
    }

    [TestMethod]
    [DataRow("“{{hello}}", DisplayName = "typed text leads the reference")]
    [DataRow("x{{hello}}", DisplayName = "ASCII leads the reference")]
    [DataRow("“x", DisplayName = "no reference at all")]
    public void ExpandedDataLedByTypedTextIsNotWarnedAboutOnWindows(string template)
    {
        CommandLineParseResult result = ParseWithVariableFile(["--variable", "hello@junk", "--expand-data", template, Url]);

        AssertAcceptedWithWarnings(result, []);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void ValueAfterAnExpandedValueLedByVariableBytesIsNotWarnedAboutOnWindows()
    {
        CommandLineParseResult result = ParseWithVariableFile(["--variable", "hello@junk", "--expand-data", "{{hello}}", "-H", LeftQuoteHeader, Url]);

        AssertAcceptedWithWarnings(result, WarningFor("“"));
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(WarningFor("“").ToList(), result.WarningLines.ToList());
    }

    private void AssertAcceptedWithWarnings(CommandLineParseResult result, IReadOnlyList<string> warningLines)
    {
        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("warning lines", CommandLineParseDiagnostics.QuoteEach(warningLines), CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
    }

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments, bool isWindows)
    {
        Diagnostics.ArrangeArguments(arguments);
        Diagnostics.Arrange("parse as Windows", isWindows);
        CommandLineParseResult result = CommandLineParser.Parse(arguments, _ => true, new UnexpectedPasswordPrompt(), new RecordingDataFileReader(), isWindows);
        Diagnostics.ActParse(result);
        return result;
    }

    /// <summary>Parses as the Windows build, with the file <c>junk</c> holding U+201C as UTF-8, as upstream test268 writes it.</summary>
    private CommandLineParseResult ParseWithVariableFile(IReadOnlyList<string> arguments)
    {
        RecordingDataFileReader reader = new();
        reader.Files["junk"] = [0xE2, 0x80, 0x9C];
        Diagnostics.Bytes("file junk", reader.Files["junk"]);
        Diagnostics.ArrangeArguments(arguments);
        Diagnostics.Arrange("parse as Windows", true);
        CommandLineParseResult result = CommandLineParser.Parse(arguments, _ => true, new UnexpectedPasswordPrompt(), reader, isWindows: true);
        Diagnostics.ActParse(result);
        return result;
    }

    private CommandLineParseResult ParseConfigFile(string contents, bool silentFirst)
    {
        RecordingDataFileReader reader = new();
        reader.Files["config.txt"] = Encoding.UTF8.GetBytes(contents);
        string[] arguments = silentFirst ? ["-s", "-K", "config.txt", Url] : ["-K", "config.txt", Url];
        Diagnostics.Bytes("config file config.txt", reader.Files["config.txt"]);
        Diagnostics.ArrangeArguments(arguments);
        Diagnostics.Arrange("parse as Windows", true);
        CommandLineParseResult result = CommandLineParser.Parse(arguments, _ => true, new UnexpectedPasswordPrompt(), reader, isWindows: true);
        Diagnostics.ActParse(result);
        return result;
    }

    private sealed class UnexpectedPasswordPrompt : IPasswordPrompt
    {
        public string ReadPassword(string prompt) => throw new AssertFailedException("No password prompt was expected.");
    }
}
