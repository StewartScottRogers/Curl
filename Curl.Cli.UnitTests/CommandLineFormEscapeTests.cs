namespace Curl.Cli;

/// <summary>
/// Pins <c>--form-escape</c> / <c>--no-form-escape</c> as curl 8.21.0 reads them, measured with the
/// local curl 8.21.0 on 2026-09-29 through <c>Record-CurlExchange.ps1</c>: both spellings are
/// accepted, the last one wins, and a <c>--next</c> group starts without it (BL-625 Notes).
/// </summary>
[TestClass]
public sealed class CommandLineFormEscapeTests
{
    private const string Url = "http://127.0.0.1:1/";

    [TestMethod]
    public void Parse_NoSpelling_DoesNotEscapeWithBackslashes()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-F", "a\"b=1", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.FormEscape);
    }

    [TestMethod]
    [DataRow("--form-escape", true)]
    [DataRow("--no-form-escape", false)]
    public void Parse_OneSpelling_SetsFormEscape(string spelling, bool formEscape)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-F", "a\"b=1", spelling, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(formEscape, result.Options.FormEscape);
    }

    [TestMethod]
    [DataRow("--no-form-escape", "--form-escape", true)]
    [DataRow("--form-escape", "--no-form-escape", false)]
    public void Parse_TwoSpellings_LastOneWins(string first, string second, bool formEscape)
    {
        CommandLineParseResult result = CommandLineParser.Parse([first, second, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(formEscape, result.Options.FormEscape);
    }

    [TestMethod]
    public void Parse_FormEscapeBeforeNext_DoesNotCarryIntoTheNextGroup()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--form-escape", Url, "--next", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Groups[0].FormEscape);
        Assert.IsFalse(result.Groups[1].FormEscape);
    }
}
