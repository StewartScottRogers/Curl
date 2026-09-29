namespace Curl.Cli;

/// <summary>
/// Pins how the parser records <c>-B</c>/<c>--use-ascii</c>, <c>--crlf</c> and <c>-a</c>/<c>--append</c>
/// and their <c>--no-</c> negations: off when absent, the last spelling wins, and the short
/// letters bundle. Curl 8.21.0 accepts every spelling here (measured 2026-09-29 with
/// <c>curl &lt;option&gt; file:///nonexist/zz</c>: exit 37, never 2).
/// </summary>
[TestClass]
public sealed class CommandLineAsciiCrlfAndAppendOptionTests
{
    private const string Url = "http://127.0.0.1:1/";

    [TestMethod]
    public void Parse_NoneOfTheOptions_LeavesAllOff()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.UseAscii);
        Assert.IsFalse(result.Options.ConvertLineEndings);
        Assert.IsFalse(result.Options.Append);
    }

    [TestMethod]
    [DataRow("-B")]
    [DataRow("--use-ascii")]
    public void Parse_UseAscii_AsksForAscii(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.UseAscii);
    }

    [TestMethod]
    public void Parse_Crlf_AsksForLineEndingConversion()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--crlf", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.ConvertLineEndings);
    }

    [TestMethod]
    [DataRow("-a")]
    [DataRow("--append")]
    public void Parse_Append_AsksToAppend(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.Append);
    }

    [TestMethod]
    public void Parse_BAndAInABundle_AsksForAsciiAndAppend()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-sBa", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.Silent);
        Assert.IsTrue(result.Options.UseAscii);
        Assert.IsTrue(result.Options.Append);
    }

    [TestMethod]
    public void Parse_EachOptionThenItsNoSpelling_LeavesAllOff()
    {
        CommandLineParseResult result = CommandLineParser.Parse(
            ["-B", "--crlf", "-a", "--no-use-ascii", "--no-crlf", "--no-append", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.UseAscii);
        Assert.IsFalse(result.Options.ConvertLineEndings);
        Assert.IsFalse(result.Options.Append);
    }

    [TestMethod]
    public void Parse_EachNoSpellingThenItsOption_TurnsAllOn()
    {
        CommandLineParseResult result = CommandLineParser.Parse(
            ["--no-use-ascii", "--no-crlf", "--no-append", "--use-ascii", "--crlf", "--append", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.UseAscii);
        Assert.IsTrue(result.Options.ConvertLineEndings);
        Assert.IsTrue(result.Options.Append);
    }
}
