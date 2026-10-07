using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <c>--clobber</c> / <c>--no-clobber</c> as curl 8.21.0 reads them, measured with the local
/// curl 8.21.0 on 2026-09-28: both spellings are accepted and the last one wins (BL-492).
/// </summary>
[TestClass]
public sealed class CommandLineClobberTests
{
    private const string Url = "http://127.0.0.1:1/";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Parse_NoSpelling_LeavesClobberUnset()
    {
        CommandLineParseResult result = Parse([Url]);

        TestDiagnostics.For(TestContext).Assert("clobber", null, result.Options?.Clobber);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.Clobber);
    }

    [TestMethod]
    [DataRow("--clobber", true)]
    [DataRow("--no-clobber", false)]
    public void Parse_OneSpelling_SetsClobber(string spelling, bool clobber)
    {
        CommandLineParseResult result = Parse([spelling, Url]);

        TestDiagnostics.For(TestContext).Assert("clobber", clobber, result.Options?.Clobber);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(clobber, result.Options.Clobber);
    }

    [TestMethod]
    [DataRow("--no-clobber", "--clobber", true)]
    [DataRow("--clobber", "--no-clobber", false)]
    public void Parse_TwoSpellings_LastOneWins(string first, string second, bool clobber)
    {
        CommandLineParseResult result = Parse([first, second, Url]);

        TestDiagnostics.For(TestContext).Assert("clobber", clobber, result.Options?.Clobber);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(clobber, result.Options.Clobber);
    }

    private CommandLineParseResult Parse(string[] arguments)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        diagnostics.ActParse(result);
        return result;
    }
}
