namespace Curl.Cli;

/// <summary>
/// Pins how the parser records <c>-g</c>/<c>--globoff</c> and <c>--no-globoff</c>, measured against
/// the local curl 8.21.0 on 2026-09-26: <c>curl -g --no-globoff "http://127.0.0.1:1/{a,b}"</c> makes
/// two transfers, so the last spelling wins as for every other boolean option.
/// </summary>
[TestClass]
public sealed class CommandLineGlobOffOptionTests
{
    private const string Url = "http://127.0.0.1:1/{a,b}";

    [TestMethod]
    public void Parse_NoGlobOffOption_GlobsUrls()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.GlobOff);
    }

    [TestMethod]
    [DataRow("-g")]
    [DataRow("--globoff")]
    public void Parse_GlobOff_SetsGlobOff(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.GlobOff);
        Assert.AreEqual(Url, result.Options.Urls.Single());
    }

    [TestMethod]
    public void Parse_GlobOffThenNoGlobOff_GlobsUrls()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-g", "--no-globoff", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.GlobOff);
    }

    [TestMethod]
    public void Parse_NoGlobOffThenGlobOff_SetsGlobOff()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--no-globoff", "--globoff", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.GlobOff);
    }

    [TestMethod]
    public void Parse_GlobOffBundledWithSilent_SetsBoth()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-gs", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.GlobOff);
        Assert.IsTrue(result.Options.Silent);
    }
}
