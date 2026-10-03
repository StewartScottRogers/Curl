using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins <c>-C</c>/<c>--continue-at</c> beside <c>--no-clobber</c> as curl 8.21.0 reads them, measured with
/// the local curl 8.21.0 on 2026-10-02: the two are refused together, naming whichever came second, and a
/// <c>--clobber</c> after <c>--no-clobber</c> clears it (BL-1223).
/// </summary>
[TestClass]
public sealed class CommandLineContinueAtNoClobberTests
{
    private const string Url = "file:///nonexist";

    private const string MutuallyExclusive = "curl: --continue-at is mutually exclusive with --no-clobber";

    // curl -C 5 --no-clobber -o x URL, --no-clobber -C 5 -o x URL, and so on: exit 2, naming the second.
    [TestMethod]
    [DataRow(new[] { "-C", "5", "--no-clobber", "-o", "x", Url }, "--no-clobber")]
    [DataRow(new[] { "-C", "-", "--no-clobber", "-o", "x", Url }, "--no-clobber")]
    [DataRow(new[] { "--continue-at", "5", "--no-clobber", "-o", "x", Url }, "--no-clobber")]
    [DataRow(new[] { "--no-clobber", "-C", "5", "-o", "x", Url }, "-C")]
    [DataRow(new[] { "--no-clobber", "-C", "-", "-o", "x", Url }, "-C")]
    [DataRow(new[] { "--no-clobber", "--continue-at", "5", "-o", "x", Url }, "--continue-at")]
    [DataRow(new[] { "-s", "-S", "-C", "5", "--no-clobber", "-o", "x", Url }, "--no-clobber")]
    [DataRow(new[] { "-C", "5", "--no-clobber", "-s", "-o", "x", Url }, "--no-clobber")]
    public void Parse_NoClobberWithContinueAt_IsRefusedWithThreeLines(string[] arguments, string refusedOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse(arguments);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { MutuallyExclusive, $"curl: option {refusedOption}: is badly used here", CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }

    // curl -s -C 5 --no-clobber -o x URL: -s hides the error line but not the other two.
    [TestMethod]
    [DataRow(new[] { "-s", "-C", "5", "--no-clobber", "-o", "x", Url }, "--no-clobber")]
    [DataRow(new[] { "-s", "--no-clobber", "-C", "5", "-o", "x", Url }, "-C")]
    public void Parse_NoClobberWithContinueAtAfterSilent_IsRefusedWithoutTheErrorLine(string[] arguments, string refusedOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse(arguments);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { $"curl: option {refusedOption}: is badly used here", CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }

    // curl -C 5 --clobber URL and --no-clobber --clobber -C 5 URL: accepted.
    [TestMethod]
    [DataRow(new[] { "-C", "5", "--clobber", "-o", "x", Url })]
    [DataRow(new[] { "--clobber", "-C", "5", "-o", "x", Url })]
    [DataRow(new[] { "--no-clobber", "--clobber", "-C", "5", "-o", "x", Url })]
    public void Parse_ContinueAtWithClobberOn_IsAccepted(string[] arguments)
    {
        CommandLineParseResult result = CommandLineParser.Parse(arguments);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(5L, result.Options.ResumeFrom);
        Assert.IsTrue(result.Options.Clobber);
        Assert.IsEmpty(result.WarningLines);
    }

    // curl --no-clobber -o x URL with no -C: accepted, clobbering off.
    [TestMethod]
    public void Parse_NoClobberWithoutContinueAt_IsAccepted()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--no-clobber", "-o", "x", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.Clobber);
    }

    // curl -r 0-4 --no-clobber -C 5 URL and --remove-on-error --no-clobber -C 5 URL: the earlier checks win.
    [TestMethod]
    [DataRow(new[] { "-r", "0-4", "--no-clobber", "-C", "5", Url }, "curl: --continue-at is mutually exclusive with --range")]
    [DataRow(new[] { "--remove-on-error", "--no-clobber", "-C", "5", Url }, "curl: --continue-at is mutually exclusive with --remove-on-error")]
    public void Parse_EarlierExclusionAndNoClobberBeforeContinueAt_NamesTheEarlierExclusion(string[] arguments, string errorLine)
    {
        CommandLineParseResult result = CommandLineParser.Parse(arguments);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(errorLine, result.Refusal.StandardErrorLines[0]);
    }

    [TestMethod]
    public void ContinueAtExclusiveWithNoClobber_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineRefusal.ContinueAtExclusiveWithNoClobber(null!, errorsHidden: false));
    }
}
