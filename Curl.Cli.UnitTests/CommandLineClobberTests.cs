namespace Curl.Cli;

/// <summary>
/// Pins <c>--clobber</c> / <c>--no-clobber</c> as curl 8.21.0 reads them, measured with the local
/// curl 8.21.0 on 2026-09-28: both spellings are accepted and the last one wins (BL-492).
/// </summary>
[TestClass]
public sealed class CommandLineClobberTests
{
    private const string Url = "http://127.0.0.1:1/";

    [TestMethod]
    public void Parse_NoSpelling_LeavesClobberUnset()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.Clobber);
    }

    [TestMethod]
    [DataRow("--clobber", true)]
    [DataRow("--no-clobber", false)]
    public void Parse_OneSpelling_SetsClobber(string spelling, bool clobber)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelling, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(clobber, result.Options.Clobber);
    }

    [TestMethod]
    [DataRow("--no-clobber", "--clobber", true)]
    [DataRow("--clobber", "--no-clobber", false)]
    public void Parse_TwoSpellings_LastOneWins(string first, string second, bool clobber)
    {
        CommandLineParseResult result = CommandLineParser.Parse([first, second, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(clobber, result.Options.Clobber);
    }
}
