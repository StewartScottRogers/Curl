using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser reads <c>--etag-save</c> and <c>--etag-compare</c>, and the refusal of a second URL
/// beside either, measured against the local curl 8.21.0 on 2026-09-29 (BL-619 Notes).
/// </summary>
[TestClass]
public sealed class CommandLineEtagOptionTests
{
    private const string Url = "http://127.0.0.1:18619/x";

    private const string OtherUrl = "http://127.0.0.1:18619/y";

    private const string SingleUrlLine = "curl: The etag options only work on a single URL";

    private static readonly Func<string, bool> NoPathExists = _ => false;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_NoEtagOptions_LeavesBothNotGiven()
    {
        CommandLineParseResult result = Parse([Url]);

        AssertEtagFiles(result, null, null);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.EtagSaveFile);
        Assert.IsNull(result.Options.EtagCompareFile);
    }

    [TestMethod]
    public void Parse_EtagSave_RecordsTheFileVerbatim()
    {
        CommandLineParseResult result = Parse(["--etag-save", "e.txt", Url]);

        AssertEtagFiles(result, "e.txt", null);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("e.txt", result.Options.EtagSaveFile);
        Assert.IsNull(result.Options.EtagCompareFile);
    }

    [TestMethod]
    public void Parse_EtagCompare_RecordsTheFileVerbatim()
    {
        CommandLineParseResult result = Parse(["--etag-compare=c.txt", Url]);

        AssertEtagFiles(result, null, "c.txt");
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("c.txt", result.Options.EtagCompareFile);
        Assert.IsNull(result.Options.EtagSaveFile);
    }

    [TestMethod]
    public void Parse_BothEtagOptionsNamingOneFile_RecordsItForBoth()
    {
        CommandLineParseResult result = Parse(["--etag-compare", "e.txt", "--etag-save", "e.txt", Url]);

        AssertEtagFiles(result, "e.txt", "e.txt");
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("e.txt", result.Options.EtagSaveFile);
        Assert.AreEqual("e.txt", result.Options.EtagCompareFile);
    }

    [TestMethod]
    public void Parse_EtagSaveGivenTwice_KeepsTheLast()
    {
        CommandLineParseResult result = Parse(["--etag-save", "a.txt", "--etag-save", "b.txt", Url]);

        Diagnostics.Assert("etag save file", "b.txt", result.Options?.EtagSaveFile);
        Assert.AreEqual("b.txt", result.Options!.EtagSaveFile);
    }

    [TestMethod]
    public void Parse_EtagSaveToStandardOutput_RecordsTheDashWithoutAWarning()
    {
        CommandLineParseResult result = Parse(["--etag-save", "-", Url]);

        Diagnostics.Assert("etag save file", "-", result.Options?.EtagSaveFile);
        Diagnostics.Assert("warning line count", 0, result.WarningLines.Count);
        Assert.AreEqual("-", result.Options!.EtagSaveFile);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow("--etag-save")]
    [DataRow("--etag-compare")]
    public void Parse_EtagFileLooksLikeAFlag_WarnsAndTakesIt(string option)
    {
        CommandLineParseResult result = Parse([option, "-x", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert(
            "warning lines",
            CommandLineParseDiagnostics.QuoteEach([CommandLineWarning.FileNameLooksLikeFlag("-x")]),
            CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { CommandLineWarning.FileNameLooksLikeFlag("-x") }, result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow("--etag-save")]
    [DataRow("--etag-compare")]
    public void Parse_BlankEtagFile_RefusesItAsBlank(string option)
    {
        CommandLineParseResult result = Parse([option, string.Empty, Url]);

        AssertRefused(result, $"curl: option {option}: blank argument where content is expected");
    }

    [TestMethod]
    public void Parse_BlankUrlOption_RefusesItAsBlank()
    {
        CommandLineParseResult result = Parse(["--url", string.Empty]);

        AssertRefused(result, "curl: option --url: blank argument where content is expected");
    }

    [TestMethod]
    [DataRow("--etag-save")]
    [DataRow("--etag-compare")]
    public void Parse_SecondUrlAfterAnEtagOption_RefusesTheSecondUrl(string option)
    {
        CommandLineParseResult result = Parse([option, "e.txt", Url, OtherUrl]);

        AssertRefused(result, SingleUrlLine, $"curl: option {OtherUrl}: is badly used here");
    }

    [TestMethod]
    public void Parse_EtagOptionAfterTwoUrls_RefusesTheOption()
    {
        CommandLineParseResult result = Parse([Url, OtherUrl, "--etag-save", "e.txt"]);

        AssertRefused(result, SingleUrlLine, "curl: option --etag-save: is badly used here");
    }

    [TestMethod]
    public void Parse_SecondUrlOptionBesideAnEtagOption_RefusesTheUrlOption()
    {
        CommandLineParseResult result = Parse(["--etag-save", "e.txt", "--url", Url, "--url", OtherUrl]);

        AssertRefused(result, SingleUrlLine, "curl: option --url: is badly used here");
    }

    [TestMethod]
    [DataRow("-s", SingleUrlLine, false)]
    [DataRow("-sS", SingleUrlLine, true)]
    public void Parse_SecondUrlBesideAnEtagOptionWhileSilent_HidesTheFirstLineUnlessShowError(string silent, string firstLine, bool firstLineShown)
    {
        CommandLineParseResult result = Parse([silent, "--etag-save", "e.txt", Url, OtherUrl]);

        string[] expected = firstLineShown
            ? [firstLine, $"curl: option {OtherUrl}: is badly used here"]
            : [$"curl: option {OtherUrl}: is badly used here"];
        AssertRefused(result, expected);
    }

    [TestMethod]
    public void Parse_TwoUrlsWithoutAnEtagOption_AcceptsBoth()
    {
        CommandLineParseResult result = Parse([Url, "--url", OtherUrl]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("urls", CommandLineParseDiagnostics.QuoteEach([Url, OtherUrl]), CommandLineParseDiagnostics.QuoteEach(result.Options?.Urls ?? []));
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { Url, OtherUrl }, result.Options.Urls.ToArray());
    }

    [TestMethod]
    public void Parse_EtagOptionWithOneGlobUrl_AcceptsIt()
    {
        CommandLineParseResult result = Parse(["--etag-save", "e.txt", "http://127.0.0.1:18619/{a,b}"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
    }

    [TestMethod]
    public void Parse_UrlInALaterGroupThanTheEtagOption_AcceptsIt()
    {
        CommandLineParseResult result = Parse(["--etag-save", "e.txt", Url, "--next", OtherUrl]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("group count", 2, result.Groups.Count);
        Diagnostics.Assert("group 0 etag save file", "e.txt", result.Groups.ElementAtOrDefault(0)?.EtagSaveFile);
        Diagnostics.Assert("group 1 etag save file", null, result.Groups.ElementAtOrDefault(1)?.EtagSaveFile);
        Assert.IsTrue(result.IsAccepted);
        Assert.HasCount(2, result.Groups);
        Assert.AreEqual("e.txt", result.Groups[0].EtagSaveFile);
        Assert.IsNull(result.Groups[1].EtagSaveFile);
    }

    private CommandLineParseResult Parse(string[] arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments, NoPathExists);
        Diagnostics.ActParse(result);
        return result;
    }

    private void AssertEtagFiles(CommandLineParseResult result, string? expectedSaveFile, string? expectedCompareFile)
    {
        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("etag save file", expectedSaveFile, result.Options?.EtagSaveFile);
        Diagnostics.Assert("etag compare file", expectedCompareFile, result.Options?.EtagCompareFile);
    }

    private void AssertRefused(CommandLineParseResult result, params string[] expectedLinesBeforeTryHelp)
    {
        string[] expectedLines = expectedLinesBeforeTryHelp.Append(CommandLineRefusal.TryHelpLine).ToArray();
        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Diagnostics.Assert("exit code", CurlExitCode.FailedInit, result.Refusal?.ExitCode);
        Diagnostics.Assert(
            "stderr lines",
            CommandLineParseDiagnostics.QuoteEach(expectedLines),
            CommandLineParseDiagnostics.QuoteEach(result.Refusal?.StandardErrorLines ?? []));
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            expectedLinesBeforeTryHelp.Append(CommandLineRefusal.TryHelpLine).ToArray(),
            result.Refusal.StandardErrorLines.ToArray());
    }
}
