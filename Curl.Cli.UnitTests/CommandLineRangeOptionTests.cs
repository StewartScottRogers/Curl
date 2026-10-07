using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser records <c>-r</c>/<c>--range</c>, <c>-C</c>/<c>--continue-at</c> and
/// <c>--max-filesize</c>, the warnings curl prints for an odd range, and the refusal of
/// <c>-r</c> together with <c>-C</c>, each measured against the local curl 8.21.0 on
/// 2026-09-26.
/// </summary>
[TestClass]
public sealed class CommandLineRangeOptionTests
{
    private const string Url = "file:///C:/rp13/f.txt";

    private const string TryHelp = "curl: try 'curl --help' or 'curl --manual' for more information";

    private const string MutuallyExclusive = "curl: --continue-at is mutually exclusive with --range";

    private const string MissingDashWarning = "Warning: A specified range MUST include at least one dash (-). Appending one for you";

    private const string InvalidCharacterWarning = "Warning: Invalid character is found in given range. A specified range MUST have only digits in 'start'-'stop'. The server's response to this request is uncertain.";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_NoneOfTheOptions_LeavesThemNotGiven()
    {
        CommandLineParseResult result = Parse([Url]);

        AssertRange(null, result);
        AssertResumeFrom(null, false, result);
        Diagnostics.Assert("max file size", "null", result.Options?.MaxFileSize?.ToString() ?? "null");
        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.Range);
        Assert.IsNull(result.Options.ResumeFrom);
        Assert.IsFalse(result.Options.ResumeFromOutputSize);
        Assert.IsNull(result.Options.MaxFileSize);
    }

    [TestMethod]
    [DataRow("-r", "0-4", "0-4")]
    [DataRow("--range", "-3", "-3")]
    [DataRow("-r", "2-3,5-6", "2-3,5-6")]
    [DataRow("-r", "3-1", "3-1")]
    [DataRow("-r", "-0", "-0")]
    [DataRow("-r", "-", "-")]
    [DataRow("-r", "99999999999999999999", "99999999999999999999")]
    public void Parse_RangeWithOnlyRangeCharactersOrUnreadableNumber_KeepsItVerbatimWithoutWarning(string spelledOption, string value, string expected)
    {
        CommandLineParseResult result = Parse([spelledOption, value, Url]);

        AssertRange(expected, result);
        AssertWarnings([], result);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.Range);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow("-r0-4", "0-4")]
    [DataRow("--range=5-", "5-")]
    public void Parse_RangeAttached_KeepsTheValue(string argument, string expected)
    {
        CommandLineParseResult result = Parse([argument, Url]);

        AssertRange(expected, result);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.Range);
    }

    [TestMethod]
    [DataRow("5", "5-")]
    [DataRow("0", "0-")]
    [DataRow("007", "7-")]
    [DataRow("5abc", "5-")]
    [DataRow("5,6", "5-")]
    [DataRow("5 ", "5-")]
    public void Parse_RangeStartingWithADigitAndNoDash_AppendsADashAndWarns(string value, string expected)
    {
        CommandLineParseResult result = Parse(["-r", value, Url]);

        AssertRange(expected, result);
        AssertWarnings([MissingDashWarning], result);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.Range);
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: A specified range MUST include at least one dash (-). Appending one for you",
            },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow("abc")]
    [DataRow("1-2abc")]
    [DataRow("a-3")]
    [DataRow(" 1-2")]
    [DataRow("+1-2")]
    public void Parse_RangeWithAnInvalidCharacter_KeepsItVerbatimAndWarns(string value)
    {
        CommandLineParseResult result = Parse(["-r", value, Url]);

        AssertRange(value, result);
        AssertWarnings([InvalidCharacterWarning], result);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(value, result.Options.Range);
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: Invalid character is found in given range. A specified range MUST have only digits in 'start'-'stop'. The server's response to this request is uncertain.",
            },
            result.WarningLines.ToArray());
    }

    // curl -s -r abc prints nothing; curl -s -S -r abc prints nothing either; curl -r abc -s
    // prints the warning, because -s had not been read yet.
    [TestMethod]
    [DataRow(new[] { "-s", "-r", "abc", Url }, 0)]
    [DataRow(new[] { "-sS", "-r", "5", Url }, 0)]
    [DataRow(new[] { "-r", "abc", "-s", Url }, 1)]
    [DataRow(new[] { "-r", "5", "-s", Url }, 1)]
    public void Parse_RangeWarning_IsHiddenOnlyBySilentReadBeforeIt(string[] arguments, int warningLineCount)
    {
        CommandLineParseResult result = Parse(arguments);

        Diagnostics.Assert("warning count", warningLineCount, result.WarningLines.Count);
        Assert.IsTrue(result.IsAccepted);
        Assert.HasCount(warningLineCount, result.WarningLines);
    }

    [TestMethod]
    public void Parse_RangeGivenTwice_KeepsTheLast()
    {
        CommandLineParseResult result = Parse(["-r", "1-2", "-r", "3-4", Url]);

        AssertRange("3-4", result);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("3-4", result.Options.Range);
    }

    [TestMethod]
    public void Parse_EmptyRange_IsRefusedAsBlank()
    {
        CommandLineParseResult result = Parse(["-r", string.Empty, Url]);

        AssertStandardError(["curl: option -r: blank argument where content is expected", TryHelp], result);
        Assert.IsFalse(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[] { "curl: option -r: blank argument where content is expected", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    // The three range texts the task names, taken from the command line to the transfer:
    // each is recorded, then ByteRangeParser turns it into exit 33 before any ByteRange exists.
    [TestMethod]
    [DataRow("3-1")]
    [DataRow("abc")]
    [DataRow("-0")]
    public void Parse_RangeNamingNoRange_ReachesTheTransferAsExit33(string value)
    {
        CommandLineParseResult result = Parse(["-r", value, Url]);

        AssertRange(value, result);
        bool parsed = result.Options?.Range is { } range && ByteRangeParser.TryParse(range, out _);
        Diagnostics.Act("byte range parsed", parsed);
        Diagnostics.Assert("byte range parsed", false, parsed);
        Diagnostics.Assert("not delivered exit code", CurlExitCode.RangeError, ByteRangeParser.NotDeliveredFailure.ExitCode);
        Diagnostics.Assert(
            "not delivered error message",
            "Requested range was not delivered by the server",
            ByteRangeParser.NotDeliveredFailure.ErrorMessage);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(ByteRangeParser.TryParse(result.Options.Range!, out _));
        Assert.AreEqual(CurlExitCode.RangeError, ByteRangeParser.NotDeliveredFailure.ExitCode);
        Assert.AreEqual("Requested range was not delivered by the server", ByteRangeParser.NotDeliveredFailure.ErrorMessage);
    }

    [TestMethod]
    [DataRow("-C", "5", 5L)]
    [DataRow("--continue-at", "0", 0L)]
    [DataRow("-C", "9223372036854775807", long.MaxValue)]
    public void Parse_ContinueAtOffset_RecordsIt(string spelledOption, string value, long expected)
    {
        CommandLineParseResult result = Parse([spelledOption, value, Url]);

        AssertResumeFrom(expected, false, result);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.ResumeFrom);
        Assert.IsFalse(result.Options.ResumeFromOutputSize);
    }

    [TestMethod]
    public void Parse_ContinueAtDash_ResumesFromTheOutputSize()
    {
        CommandLineParseResult result = Parse(["-C", "5", "-C", "-", Url]);

        AssertResumeFrom(null, true, result);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.ResumeFrom);
        Assert.IsTrue(result.Options.ResumeFromOutputSize);
    }

    [TestMethod]
    public void Parse_ContinueAtOffsetAfterDash_KeepsTheOffset()
    {
        CommandLineParseResult result = Parse(["-C", "-", "-C", "7", Url]);

        AssertResumeFrom(7L, false, result);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(7L, result.Options.ResumeFrom);
        Assert.IsFalse(result.Options.ResumeFromOutputSize);
    }

    [TestMethod]
    [DataRow("-C", "abc")]
    [DataRow("-C", "-5")]
    [DataRow("-C", "-1")]
    [DataRow("-C", "-0")]
    [DataRow("-C", "")]
    [DataRow("-C", "1k")]
    [DataRow("-C", "1e3")]
    [DataRow("-C", "99999999999999999999")]
    [DataRow("-C", " 5")]
    [DataRow("-C", "+5")]
    [DataRow("-C", "5x")]
    [DataRow("--continue-at", "-5")]
    public void Parse_ContinueAtNotAnOffset_IsRefused(string spelledOption, string value)
    {
        CommandLineParseResult result = Parse([spelledOption, value, Url]);

        AssertExitCode(CurlExitCode.FailedInit, result);
        AssertStandardError([$"curl: option {spelledOption}: expected a proper numerical parameter", TryHelp], result);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { $"curl: option {spelledOption}: expected a proper numerical parameter", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    // curl -C -5 <bad URL> and curl -C -5 with no URL: exit 2 on the -C value, measured on curl
    // 8.21.0 on 2026-09-25. The option parser refuses it before any URL is looked at, so a
    // malformed URL never gets the chance to make it exit 3.
    [TestMethod]
    [DataRow("-C", "file://[bad")]
    [DataRow("--continue-at", "htp:/%zz")]
    public void Parse_NegativeContinueAtWithAMalformedUrl_IsRefusedOnTheOptionWithExit2(
        string spelledOption,
        string malformedUrl)
    {
        CommandLineParseResult result = Parse([spelledOption, "-5", malformedUrl]);

        AssertExitCode(CurlExitCode.FailedInit, result);
        AssertStandardError([$"curl: option {spelledOption}: expected a proper numerical parameter", TryHelp], result);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        Assert.AreEqual(2, (int)result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { $"curl: option {spelledOption}: expected a proper numerical parameter", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_NegativeContinueAtWithNoUrl_IsRefusedOnTheOptionNotForTheMissingUrl()
    {
        CommandLineParseResult result = Parse(["-C", "-5"]);

        AssertStandardError(["curl: option -C: expected a proper numerical parameter", TryHelp], result);
        Assert.IsFalse(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[] { "curl: option -C: expected a proper numerical parameter", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    // curl -r 0-4 -C 5 URL: exit 2, the three lines, naming whichever option came second as typed.
    [TestMethod]
    [DataRow(new[] { "-r", "0-4", "-C", "5", Url }, "-C")]
    [DataRow(new[] { "-C", "5", "-r", "0-4", Url }, "-r")]
    [DataRow(new[] { "-C", "-", "--range=0-4", Url }, "--range=0-4")]
    [DataRow(new[] { "--continue-at=5", "-r0-4", Url }, "-r0-4")]
    [DataRow(new[] { "-r", "0-4", "-C", "abc", Url }, "-C")]
    [DataRow(new[] { "-r", "0-4", "-C", "", Url }, "-C")]
    [DataRow(new[] { "-C", "5", "-r", "", Url }, "-r")]
    [DataRow(new[] { "-C", "0", "-r", "0-4", Url }, "-r")]
    [DataRow(new[] { "-r", "0-4", "-C", "5", "-s", Url }, "-C")]
    [DataRow(new[] { "-s", "-S", "-r", "0-4", "-C", "5", Url }, "-C")]
    public void Parse_RangeWithContinueAt_IsRefusedWithThreeLines(string[] arguments, string refusedOption)
    {
        CommandLineParseResult result = Parse(arguments);

        AssertExitCode(CurlExitCode.FailedInit, result);
        AssertStandardError([MutuallyExclusive, $"curl: option {refusedOption}: is badly used here", TryHelp], result);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { MutuallyExclusive, $"curl: option {refusedOption}: is badly used here", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    // curl -s -r 0-4 -C 5 URL: -s hides the error line but not the other two.
    [TestMethod]
    public void Parse_RangeWithContinueAtAfterSilent_IsRefusedWithoutTheErrorLine()
    {
        CommandLineParseResult result = Parse(["-s", "-r", "0-4", "-C", "5", Url]);

        AssertStandardError(["curl: option -C: is badly used here", TryHelp], result);
        Assert.IsFalse(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[] { "curl: option -C: is badly used here", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [DataRow("9", 9L)]
    [DataRow("0", 0L)]
    [DataRow("1k", 1024L)]
    [DataRow("1.5k", 1536L)]
    [DataRow("3b", 3L)]
    public void Parse_MaxFileSize_RecordsTheSizeInBytes(string value, long expected)
    {
        CommandLineParseResult result = Parse(["--max-filesize", value, Url]);

        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("max file size", expected, result.Options.MaxFileSize);
        Assert.AreEqual(expected, result.Options.MaxFileSize);
    }

    [TestMethod]
    public void Parse_MaxFileSizeGivenTwice_KeepsTheLast()
    {
        CommandLineParseResult result = Parse(["--max-filesize", "1", "--max-filesize=0", Url]);

        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("max file size", 0L, result.Options.MaxFileSize);
        Assert.AreEqual(0L, result.Options.MaxFileSize);
    }

    [TestMethod]
    [DataRow("1x", "is badly used here")]
    [DataRow("-1", "expected a proper numerical parameter")]
    [DataRow("99999999999999999999", "too large number")]
    public void Parse_MaxFileSizeUnreadable_IsRefused(string value, string reason)
    {
        CommandLineParseResult result = Parse(["--max-filesize", value, Url]);

        AssertStandardError([$"curl: option --max-filesize: {reason}", TryHelp], result);
        Assert.IsFalse(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[] { $"curl: option --max-filesize: {reason}", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        return result;
    }

    private void AssertRange(string? expected, CommandLineParseResult result) =>
        Diagnostics.Assert("range", Quote(expected), Quote(result.Options?.Range));

    private static string Quote(string? value) => value is null ? "null" : "\"" + value + "\"";

    private void AssertResumeFrom(long? expectedOffset, bool expectedFromOutputSize, CommandLineParseResult result)
    {
        Diagnostics.Assert("resume from", expectedOffset?.ToString() ?? "null", result.Options?.ResumeFrom?.ToString() ?? "null");
        Diagnostics.Assert("resume from output size", expectedFromOutputSize, result.Options?.ResumeFromOutputSize);
    }

    private void AssertWarnings(string[] expected, CommandLineParseResult result) =>
        Diagnostics.Assert(
            "warnings",
            CommandLineParseDiagnostics.QuoteEach(expected),
            CommandLineParseDiagnostics.QuoteEach(result.WarningLines));

    private void AssertExitCode(CurlExitCode expected, CommandLineParseResult result) =>
        Diagnostics.Assert("exit code", expected, CommandLineParseDiagnostics.Peek(result.Refusal)?.ExitCode);

    private void AssertStandardError(string[] expected, CommandLineParseResult result) =>
        Diagnostics.Assert(
            "stderr",
            CommandLineParseDiagnostics.QuoteEach(expected),
            CommandLineParseDiagnostics.QuoteEach(CommandLineParseDiagnostics.Peek(result.Refusal)?.StandardErrorLines ?? []));
}
