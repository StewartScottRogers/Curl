using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins <c>--remove-on-error</c> / <c>--no-remove-on-error</c> as curl 8.21.0 reads them, measured with
/// the local curl 8.21.0 on 2026-09-28: both spellings are accepted, the last one wins, and
/// <c>--remove-on-error</c> beside any <c>-C</c> is refused, naming whichever came second (BL-494).
/// </summary>
[TestClass]
public sealed class CommandLineRemoveOnErrorTests
{
    private const string Url = "http://127.0.0.1:1/";

    private const string MutuallyExclusive = "curl: --continue-at is mutually exclusive with --remove-on-error";

    [TestMethod]
    public void Parse_NoSpelling_DoesNotRemoveOnError()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.RemoveOnError);
    }

    [TestMethod]
    [DataRow("--remove-on-error", true)]
    [DataRow("--no-remove-on-error", false)]
    public void Parse_OneSpelling_SetsRemoveOnError(string spelling, bool removeOnError)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelling, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(removeOnError, result.Options.RemoveOnError);
    }

    [TestMethod]
    [DataRow("--no-remove-on-error", "--remove-on-error", true)]
    [DataRow("--remove-on-error", "--no-remove-on-error", false)]
    public void Parse_TwoSpellings_LastOneWins(string first, string second, bool removeOnError)
    {
        CommandLineParseResult result = CommandLineParser.Parse([first, second, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(removeOnError, result.Options.RemoveOnError);
    }

    // curl --remove-on-error -C 5 URL, -C 5 --remove-on-error URL, and so on: exit 2, naming the second.
    [TestMethod]
    [DataRow(new[] { "--remove-on-error", "-C", "-", Url }, "-C")]
    [DataRow(new[] { "--remove-on-error", "-C", "5", Url }, "-C")]
    [DataRow(new[] { "--remove-on-error", "--continue-at", "5", Url }, "--continue-at")]
    [DataRow(new[] { "-C", "5", "--remove-on-error", Url }, "--remove-on-error")]
    [DataRow(new[] { "-C", "0", "--remove-on-error", Url }, "--remove-on-error")]
    [DataRow(new[] { "-C", "-", "--remove-on-error", Url }, "--remove-on-error")]
    [DataRow(new[] { "-C", "5", "--remove-on-error", "--no-remove-on-error", Url }, "--remove-on-error")]
    [DataRow(new[] { "-s", "-S", "-C", "5", "--remove-on-error", Url }, "--remove-on-error")]
    public void Parse_RemoveOnErrorWithContinueAt_IsRefusedWithThreeLines(string[] arguments, string refusedOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse(arguments);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { MutuallyExclusive, $"curl: option {refusedOption}: is badly used here", CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }

    // curl -s -C 5 --remove-on-error URL: -s hides the error line but not the other two.
    [TestMethod]
    public void Parse_RemoveOnErrorWithContinueAtAfterSilent_IsRefusedWithoutTheErrorLine()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-s", "-C", "5", "--remove-on-error", Url]);

        Assert.IsFalse(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[] { "curl: option --remove-on-error: is badly used here", CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }

    // curl -C 5 --no-remove-on-error URL and --remove-on-error --no-remove-on-error -C 5 URL: accepted.
    [TestMethod]
    [DataRow(new[] { "-C", "5", "--no-remove-on-error", Url })]
    [DataRow(new[] { "--no-remove-on-error", "-C", "5", Url })]
    [DataRow(new[] { "--remove-on-error", "--no-remove-on-error", "-C", "5", Url })]
    public void Parse_ContinueAtWithRemoveOnErrorOff_IsAccepted(string[] arguments)
    {
        CommandLineParseResult result = CommandLineParser.Parse(arguments);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(5L, result.Options.ResumeFrom);
        Assert.IsFalse(result.Options.RemoveOnError);
    }

    // curl -r 0-4 --remove-on-error -C 5 URL: the range is named, as it is checked first.
    [TestMethod]
    public void Parse_RangeAndRemoveOnErrorBeforeContinueAt_NamesTheRange()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-r", "0-4", "--remove-on-error", "-C", "5", Url]);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual("curl: --continue-at is mutually exclusive with --range", result.Refusal.StandardErrorLines[0]);
    }

    [TestMethod]
    public void ContinueAtExclusiveWithRemoveOnError_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineRefusal.ContinueAtExclusiveWithRemoveOnError(null!, errorsHidden: false));
    }
}
