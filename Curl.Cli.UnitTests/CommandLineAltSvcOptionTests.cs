using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Parse_NoAltSvc_LeavesItNotGiven()
    {
        CommandLineParseResult result = Parse([Url]);

        TestDiagnostics.For(TestContext).Assert("alt-svc file", null, result.Options?.AltSvcFile);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.AltSvcFile);
    }

    [TestMethod]
    public void Parse_AltSvc_RecordsTheFileVerbatim()
    {
        CommandLineParseResult result = Parse(["--alt-svc", "cache.txt", Url]);

        TestDiagnostics.For(TestContext).Assert("alt-svc file", "cache.txt", result.Options?.AltSvcFile);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("cache.txt", result.Options.AltSvcFile);
    }

    [TestMethod]
    public void Parse_AltSvcEmpty_AcceptsItAsNoFile()
    {
        CommandLineParseResult result = Parse(["--alt-svc", string.Empty, Url]);

        TestDiagnostics.For(TestContext).Assert("alt-svc file", string.Empty, result.Options?.AltSvcFile);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(string.Empty, result.Options.AltSvcFile);
    }

    [TestMethod]
    public void Parse_AltSvcTwice_KeepsTheLast()
    {
        CommandLineParseResult result = Parse(["--alt-svc=a.txt", "--alt-svc=b.txt", Url]);

        TestDiagnostics.For(TestContext).Assert("alt-svc file", "b.txt", result.Options?.AltSvcFile);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("b.txt", result.Options.AltSvcFile);
    }

    [TestMethod]
    public void Parse_AltSvcLookingLikeAFlag_WarnsNothing()
    {
        CommandLineParseResult result = Parse(["--alt-svc", "-abc", Url]);

        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Assert("alt-svc file", "-abc", result.Options?.AltSvcFile);
        diagnostics.Assert("warning lines", 0, result.WarningLines.Count);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("-abc", result.Options.AltSvcFile);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_AltSvcWithoutValue_IsRefusedAsMissingItsParameter()
    {
        CommandLineParseResult result = Parse(["--alt-svc"]);

        TestDiagnostics.For(TestContext).Assert("first stderr line", "curl: option --alt-svc: requires parameter", result.Refusal?.StandardErrorLines[0]);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual("curl: option --alt-svc: requires parameter", result.Refusal!.StandardErrorLines[0]);
    }

    [TestMethod]
    public void Parse_AltSvcBeforeNext_BelongsToItsGroupOnly()
    {
        CommandLineParseResult result = Parse(["--alt-svc", "a.txt", Url, "--next", Url]);

        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Act("groups' alt-svc files", CommandLineParseDiagnostics.QuoteEach(result.Groups.Select(group => group.AltSvcFile)));
        diagnostics.Assert("groups", 2, result.Groups.Count);
        Assert.HasCount(2, result.Groups);
        Assert.AreEqual("a.txt", result.Groups[0].AltSvcFile);
        Assert.IsNull(result.Groups[1].AltSvcFile);
    }

    private CommandLineParseResult Parse(string[] arguments)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments, NoPathExists);
        diagnostics.ActParse(result);
        return result;
    }
}
