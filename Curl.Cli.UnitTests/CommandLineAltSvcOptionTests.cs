namespace Curl.Cli;

/// <summary>
/// Pins how the parser reads <c>--alt-svc</c>, measured against the local curl 8.21.0 on 2026-09-29
/// (BL-623 Notes): the file is taken verbatim, an empty value is accepted, a value that looks like a
/// flag draws no warning, and each <c>--next</c> group has its own.
/// </summary>
[TestClass]
public sealed class CommandLineAltSvcOptionTests
{
    private const string Url = "https://localhost:18499/";

    private static readonly Func<string, bool> NoPathExists = _ => false;

    [TestMethod]
    public void Parse_NoAltSvc_LeavesItNotGiven()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.AltSvcFile);
    }

    [TestMethod]
    public void Parse_AltSvc_RecordsTheFileVerbatim()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--alt-svc", "cache.txt", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("cache.txt", result.Options.AltSvcFile);
    }

    [TestMethod]
    public void Parse_AltSvcEmpty_AcceptsItAsNoFile()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--alt-svc", string.Empty, Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(string.Empty, result.Options.AltSvcFile);
    }

    [TestMethod]
    public void Parse_AltSvcTwice_KeepsTheLast()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--alt-svc=a.txt", "--alt-svc=b.txt", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("b.txt", result.Options.AltSvcFile);
    }

    [TestMethod]
    public void Parse_AltSvcLookingLikeAFlag_WarnsNothing()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--alt-svc", "-abc", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("-abc", result.Options.AltSvcFile);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_AltSvcWithoutValue_IsRefusedAsMissingItsParameter()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--alt-svc"], NoPathExists);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual("curl: option --alt-svc: requires parameter", result.Refusal!.StandardErrorLines[0]);
    }

    [TestMethod]
    public void Parse_AltSvcBeforeNext_BelongsToItsGroupOnly()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--alt-svc", "a.txt", Url, "--next", Url], NoPathExists);

        Assert.HasCount(2, result.Groups);
        Assert.AreEqual("a.txt", result.Groups[0].AltSvcFile);
        Assert.IsNull(result.Groups[1].AltSvcFile);
    }
}
