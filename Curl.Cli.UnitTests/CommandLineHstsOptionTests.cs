using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_NoHsts_LeavesItNotGiven()
    {
        CommandLineParseResult result = Parse([Url], NoPathExists);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("hsts file", null, result.Options?.HstsFile);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.HstsFile);
    }

    [TestMethod]
    public void Parse_Hsts_RecordsTheFileVerbatim()
    {
        CommandLineParseResult result = Parse(["--hsts", "cache.txt", Url], NoPathExists);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("hsts file", Quote("cache.txt"), Quote(result.Options?.HstsFile));
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("cache.txt", result.Options.HstsFile);
    }

    [TestMethod]
    public void Parse_HstsEmpty_AcceptsItAsNoFile()
    {
        CommandLineParseResult result = Parse(["--hsts", string.Empty, Url], NoPathExists);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("hsts file", Quote(string.Empty), Quote(result.Options?.HstsFile));
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(string.Empty, result.Options.HstsFile);
    }

    [TestMethod]
    public void Parse_HstsTwice_KeepsTheLast()
    {
        CommandLineParseResult result = Parse(["--hsts=a.txt", "--hsts=b.txt", Url], NoPathExists);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("hsts file", Quote("b.txt"), Quote(result.Options?.HstsFile));
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("b.txt", result.Options.HstsFile);
    }

    [TestMethod]
    public void Parse_HstsLookingLikeAFlag_WarnsNothing()
    {
        CommandLineParseResult result = Parse(["--hsts", "-abc", Url], NoPathExists);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("hsts file", Quote("-abc"), Quote(result.Options?.HstsFile));
        Diagnostics.Assert("warning lines", "[]", CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("-abc", result.Options.HstsFile);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_HstsWithoutValue_IsRefusedAsMissingItsParameter()
    {
        CommandLineParseResult result = Parse(["--hsts"], NoPathExists);

        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Diagnostics.Assert("first stderr line", "curl: option --hsts: requires parameter", result.Refusal?.StandardErrorLines.FirstOrDefault());
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual("curl: option --hsts: requires parameter", result.Refusal!.StandardErrorLines[0]);
    }

    [TestMethod]
    public void Parse_HstsBeforeNext_BelongsToItsGroupOnly()
    {
        CommandLineParseResult result = Parse(["--hsts", "a.txt", Url, "--next", Url], NoPathExists);

        Diagnostics.Assert("group count", 2, result.Groups.Count);
        Diagnostics.Assert("group 0 hsts file", Quote("a.txt"), Quote(result.Groups.ElementAtOrDefault(0)?.HstsFile));
        Diagnostics.Assert("group 1 hsts file", null, result.Groups.ElementAtOrDefault(1)?.HstsFile);
        Assert.HasCount(2, result.Groups);
        Assert.AreEqual("a.txt", result.Groups[0].HstsFile);
        Assert.IsNull(result.Groups[1].HstsFile);
    }

    private static string Quote(string? value) => value is null ? "null" : "\"" + value + "\"";

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments, Func<string, bool> pathExists)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments, pathExists);
        Diagnostics.ActParse(result);
        return result;
    }
}
