using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_NoSpelling_DoesNotEscapeWithBackslashes()
    {
        CommandLineParseResult result = Parse(["-F", "a\"b=1", Url]);

        AssertFormEscape(result, false);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.FormEscape);
    }

    [TestMethod]
    [DataRow("--form-escape", true)]
    [DataRow("--no-form-escape", false)]
    public void Parse_OneSpelling_SetsFormEscape(string spelling, bool formEscape)
    {
        CommandLineParseResult result = Parse(["-F", "a\"b=1", spelling, Url]);

        AssertFormEscape(result, formEscape);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(formEscape, result.Options.FormEscape);
    }

    [TestMethod]
    [DataRow("--no-form-escape", "--form-escape", true)]
    [DataRow("--form-escape", "--no-form-escape", false)]
    public void Parse_TwoSpellings_LastOneWins(string first, string second, bool formEscape)
    {
        CommandLineParseResult result = Parse([first, second, Url]);

        AssertFormEscape(result, formEscape);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(formEscape, result.Options.FormEscape);
    }

    [TestMethod]
    public void Parse_FormEscapeBeforeNext_DoesNotCarryIntoTheNextGroup()
    {
        CommandLineParseResult result = Parse(["--form-escape", Url, "--next", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("group 0 form escape", true, result.Groups.ElementAtOrDefault(0)?.FormEscape);
        Diagnostics.Assert("group 1 form escape", false, result.Groups.ElementAtOrDefault(1)?.FormEscape);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Groups[0].FormEscape);
        Assert.IsFalse(result.Groups[1].FormEscape);
    }

    private CommandLineParseResult Parse(string[] arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        return result;
    }

    private void AssertFormEscape(CommandLineParseResult result, bool formEscape)
    {
        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("form escape", formEscape, result.Options?.FormEscape);
    }
}
