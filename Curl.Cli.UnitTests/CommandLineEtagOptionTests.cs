using Curl.Protocol.Abstractions;

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

    [TestMethod]
    public void Parse_NoEtagOptions_LeavesBothNotGiven()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.EtagSaveFile);
        Assert.IsNull(result.Options.EtagCompareFile);
    }

    [TestMethod]
    public void Parse_EtagSave_RecordsTheFileVerbatim()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--etag-save", "e.txt", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("e.txt", result.Options.EtagSaveFile);
        Assert.IsNull(result.Options.EtagCompareFile);
    }

    [TestMethod]
    public void Parse_EtagCompare_RecordsTheFileVerbatim()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--etag-compare=c.txt", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("c.txt", result.Options.EtagCompareFile);
        Assert.IsNull(result.Options.EtagSaveFile);
    }

    [TestMethod]
    public void Parse_BothEtagOptionsNamingOneFile_RecordsItForBoth()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--etag-compare", "e.txt", "--etag-save", "e.txt", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("e.txt", result.Options.EtagSaveFile);
        Assert.AreEqual("e.txt", result.Options.EtagCompareFile);
    }

    [TestMethod]
    public void Parse_EtagSaveGivenTwice_KeepsTheLast()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--etag-save", "a.txt", "--etag-save", "b.txt", Url], NoPathExists);

        Assert.AreEqual("b.txt", result.Options!.EtagSaveFile);
    }

    [TestMethod]
    public void Parse_EtagSaveToStandardOutput_RecordsTheDashWithoutAWarning()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--etag-save", "-", Url], NoPathExists);

        Assert.AreEqual("-", result.Options!.EtagSaveFile);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow("--etag-save")]
    [DataRow("--etag-compare")]
    public void Parse_EtagFileLooksLikeAFlag_WarnsAndTakesIt(string option)
    {
        CommandLineParseResult result = CommandLineParser.Parse([option, "-x", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { CommandLineWarning.FileNameLooksLikeFlag("-x") }, result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow("--etag-save")]
    [DataRow("--etag-compare")]
    public void Parse_BlankEtagFile_RefusesItAsBlank(string option)
    {
        CommandLineParseResult result = CommandLineParser.Parse([option, string.Empty, Url], NoPathExists);

        AssertRefused(result, $"curl: option {option}: blank argument where content is expected");
    }

    [TestMethod]
    public void Parse_BlankUrlOption_RefusesItAsBlank()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--url", string.Empty], NoPathExists);

        AssertRefused(result, "curl: option --url: blank argument where content is expected");
    }

    [TestMethod]
    [DataRow("--etag-save")]
    [DataRow("--etag-compare")]
    public void Parse_SecondUrlAfterAnEtagOption_RefusesTheSecondUrl(string option)
    {
        CommandLineParseResult result = CommandLineParser.Parse([option, "e.txt", Url, OtherUrl], NoPathExists);

        AssertRefused(result, SingleUrlLine, $"curl: option {OtherUrl}: is badly used here");
    }

    [TestMethod]
    public void Parse_EtagOptionAfterTwoUrls_RefusesTheOption()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url, OtherUrl, "--etag-save", "e.txt"], NoPathExists);

        AssertRefused(result, SingleUrlLine, "curl: option --etag-save: is badly used here");
    }

    [TestMethod]
    public void Parse_SecondUrlOptionBesideAnEtagOption_RefusesTheUrlOption()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--etag-save", "e.txt", "--url", Url, "--url", OtherUrl], NoPathExists);

        AssertRefused(result, SingleUrlLine, "curl: option --url: is badly used here");
    }

    [TestMethod]
    [DataRow("-s", SingleUrlLine, false)]
    [DataRow("-sS", SingleUrlLine, true)]
    public void Parse_SecondUrlBesideAnEtagOptionWhileSilent_HidesTheFirstLineUnlessShowError(string silent, string firstLine, bool firstLineShown)
    {
        CommandLineParseResult result = CommandLineParser.Parse([silent, "--etag-save", "e.txt", Url, OtherUrl], NoPathExists);

        string[] expected = firstLineShown
            ? [firstLine, $"curl: option {OtherUrl}: is badly used here"]
            : [$"curl: option {OtherUrl}: is badly used here"];
        AssertRefused(result, expected);
    }

    [TestMethod]
    public void Parse_TwoUrlsWithoutAnEtagOption_AcceptsBoth()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url, "--url", OtherUrl], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { Url, OtherUrl }, result.Options.Urls.ToArray());
    }

    [TestMethod]
    public void Parse_EtagOptionWithOneGlobUrl_AcceptsIt()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--etag-save", "e.txt", "http://127.0.0.1:18619/{a,b}"], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
    }

    [TestMethod]
    public void Parse_UrlInALaterGroupThanTheEtagOption_AcceptsIt()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--etag-save", "e.txt", Url, "--next", OtherUrl], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.HasCount(2, result.Groups);
        Assert.AreEqual("e.txt", result.Groups[0].EtagSaveFile);
        Assert.IsNull(result.Groups[1].EtagSaveFile);
    }

    private static void AssertRefused(CommandLineParseResult result, params string[] expectedLinesBeforeTryHelp)
    {
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            expectedLinesBeforeTryHelp.Append(CommandLineRefusal.TryHelpLine).ToArray(),
            result.Refusal.StandardErrorLines.ToArray());
    }
}
