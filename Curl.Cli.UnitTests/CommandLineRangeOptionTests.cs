using Curl.Core;
using Curl.Protocol.Abstractions;

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

    [TestMethod]
    public void Parse_NoneOfTheOptions_LeavesThemNotGiven()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url]);

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
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, value, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.Range);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow("-r0-4", "0-4")]
    [DataRow("--range=5-", "5-")]
    public void Parse_RangeAttached_KeepsTheValue(string argument, string expected)
    {
        CommandLineParseResult result = CommandLineParser.Parse([argument, Url]);

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
        CommandLineParseResult result = CommandLineParser.Parse(["-r", value, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.Range);
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: A specified range MUST include at least one dash (-). Appending one ",
                "Warning: for you",
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
        CommandLineParseResult result = CommandLineParser.Parse(["-r", value, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(value, result.Options.Range);
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: Invalid character is found in given range. A specified range MUST ",
                "Warning: have only digits in 'start'-'stop'. The server's response to this ",
                "Warning: request is uncertain.",
            },
            result.WarningLines.ToArray());
    }

    // curl -s -r abc prints nothing; curl -s -S -r abc prints nothing either; curl -r abc -s
    // prints the warning, because -s had not been read yet.
    [TestMethod]
    [DataRow(new[] { "-s", "-r", "abc", Url }, 0)]
    [DataRow(new[] { "-sS", "-r", "5", Url }, 0)]
    [DataRow(new[] { "-r", "abc", "-s", Url }, 3)]
    [DataRow(new[] { "-r", "5", "-s", Url }, 2)]
    public void Parse_RangeWarning_IsHiddenOnlyBySilentReadBeforeIt(string[] arguments, int warningLineCount)
    {
        CommandLineParseResult result = CommandLineParser.Parse(arguments);

        Assert.IsTrue(result.IsAccepted);
        Assert.HasCount(warningLineCount, result.WarningLines);
    }

    [TestMethod]
    public void Parse_RangeGivenTwice_KeepsTheLast()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-r", "1-2", "-r", "3-4", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("3-4", result.Options.Range);
    }

    [TestMethod]
    public void Parse_EmptyRange_IsRefusedAsBlank()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-r", string.Empty, Url]);

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
        CommandLineParseResult result = CommandLineParser.Parse(["-r", value, Url]);

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
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, value, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.ResumeFrom);
        Assert.IsFalse(result.Options.ResumeFromOutputSize);
    }

    [TestMethod]
    public void Parse_ContinueAtDash_ResumesFromTheOutputSize()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-C", "5", "-C", "-", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.ResumeFrom);
        Assert.IsTrue(result.Options.ResumeFromOutputSize);
    }

    [TestMethod]
    public void Parse_ContinueAtOffsetAfterDash_KeepsTheOffset()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-C", "-", "-C", "7", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(7L, result.Options.ResumeFrom);
        Assert.IsFalse(result.Options.ResumeFromOutputSize);
    }

    [TestMethod]
    [DataRow("-C", "abc")]
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
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, value, Url]);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { $"curl: option {spelledOption}: expected a proper numerical parameter", TryHelp },
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
        CommandLineParseResult result = CommandLineParser.Parse(arguments);

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
        CommandLineParseResult result = CommandLineParser.Parse(["-s", "-r", "0-4", "-C", "5", Url]);

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
        CommandLineParseResult result = CommandLineParser.Parse(["--max-filesize", value, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.MaxFileSize);
    }

    [TestMethod]
    public void Parse_MaxFileSizeGivenTwice_KeepsTheLast()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--max-filesize", "1", "--max-filesize=0", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(0L, result.Options.MaxFileSize);
    }

    [TestMethod]
    [DataRow("1x", "is badly used here")]
    [DataRow("-1", "expected a proper numerical parameter")]
    [DataRow("99999999999999999999", "too large number")]
    public void Parse_MaxFileSizeUnreadable_IsRefused(string value, string reason)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--max-filesize", value, Url]);

        Assert.IsFalse(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[] { $"curl: option --max-filesize: {reason}", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }
}
