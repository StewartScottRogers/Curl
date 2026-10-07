using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_NoGlobOffOption_GlobsUrls()
    {
        CommandLineParseResult result = Parse([Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("glob off", false, result.Options?.GlobOff);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.GlobOff);
    }

    [TestMethod]
    [DataRow("-g")]
    [DataRow("--globoff")]
    public void Parse_GlobOff_SetsGlobOff(string spelledOption)
    {
        CommandLineParseResult result = Parse([spelledOption, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("glob off", true, result.Options?.GlobOff);
        Diagnostics.Assert("urls", CommandLineParseDiagnostics.QuoteEach([Url]), CommandLineParseDiagnostics.QuoteEach(result.Options?.Urls ?? []));
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.GlobOff);
        Assert.AreEqual(Url, result.Options.Urls.Single());
    }

    [TestMethod]
    public void Parse_GlobOffThenNoGlobOff_GlobsUrls()
    {
        CommandLineParseResult result = Parse(["-g", "--no-globoff", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("glob off", false, result.Options?.GlobOff);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.GlobOff);
    }

    [TestMethod]
    public void Parse_NoGlobOffThenGlobOff_SetsGlobOff()
    {
        CommandLineParseResult result = Parse(["--no-globoff", "--globoff", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("glob off", true, result.Options?.GlobOff);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.GlobOff);
    }

    [TestMethod]
    public void Parse_GlobOffBundledWithSilent_SetsBoth()
    {
        CommandLineParseResult result = Parse(["-gs", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("glob off", true, result.Options?.GlobOff);
        Diagnostics.Assert("silent", true, result.Options?.Silent);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.GlobOff);
        Assert.IsTrue(result.Options.Silent);
    }

    private CommandLineParseResult Parse(string[] arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        return result;
    }
}
