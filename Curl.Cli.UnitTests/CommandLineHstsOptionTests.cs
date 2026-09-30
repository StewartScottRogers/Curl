namespace Curl.Cli;

/// <summary>
/// Pins how the parser reads <c>--hsts</c>, measured against the local curl 8.21.0 on 2026-09-29
/// (BL-621 Notes): the file is taken verbatim, an empty value is accepted, a value that looks like a
/// flag draws no warning (curl wrote the file <c>-abc</c>), and each <c>--next</c> group has its own.
/// </summary>
[TestClass]
public sealed class CommandLineHstsOptionTests
{
    private const string Url = "https://localhost:18443/";

    private static readonly Func<string, bool> NoPathExists = _ => false;

    [TestMethod]
    public void Parse_NoHsts_LeavesItNotGiven()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.HstsFile);
    }

    [TestMethod]
    public void Parse_Hsts_RecordsTheFileVerbatim()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--hsts", "cache.txt", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("cache.txt", result.Options.HstsFile);
    }

    [TestMethod]
    public void Parse_HstsEmpty_AcceptsItAsNoFile()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--hsts", string.Empty, Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(string.Empty, result.Options.HstsFile);
    }

    [TestMethod]
    public void Parse_HstsTwice_KeepsTheLast()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--hsts=a.txt", "--hsts=b.txt", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("b.txt", result.Options.HstsFile);
    }

    [TestMethod]
    public void Parse_HstsLookingLikeAFlag_WarnsNothing()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--hsts", "-abc", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("-abc", result.Options.HstsFile);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_HstsWithoutValue_IsRefusedAsMissingItsParameter()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--hsts"], NoPathExists);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual("curl: option --hsts: requires parameter", result.Refusal!.StandardErrorLines[0]);
    }

    [TestMethod]
    public void Parse_HstsBeforeNext_BelongsToItsGroupOnly()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--hsts", "a.txt", Url, "--next", Url], NoPathExists);

        Assert.HasCount(2, result.Groups);
        Assert.AreEqual("a.txt", result.Groups[0].HstsFile);
        Assert.IsNull(result.Groups[1].HstsFile);
    }
}
