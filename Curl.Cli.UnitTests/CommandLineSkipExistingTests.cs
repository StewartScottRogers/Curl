using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <c>--skip-existing</c> / <c>--no-skip-existing</c> as curl 8.21.0 reads them, measured with
/// the local curl 8.21.0 on 2026-09-28: both spellings are accepted and the last one wins (BL-493).
/// </summary>
[TestClass]
public sealed class CommandLineSkipExistingTests
{
    private const string Url = "http://127.0.0.1:1/";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        if (result.IsAccepted)
        {
            Diagnostics.Act("skip existing", result.Options.SkipExisting);
        }

        return result;
    }

    [TestMethod]
    public void Parse_NoSpelling_DoesNotSkipExisting()
    {
        CommandLineParseResult result = Parse([Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.SkipExisting);
    }

    [TestMethod]
    [DataRow("--skip-existing", true)]
    [DataRow("--no-skip-existing", false)]
    public void Parse_OneSpelling_SetsSkipExisting(string spelling, bool skipExisting)
    {
        CommandLineParseResult result = Parse([spelling, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(skipExisting, result.Options.SkipExisting);
    }

    [TestMethod]
    [DataRow("--no-skip-existing", "--skip-existing", true)]
    [DataRow("--skip-existing", "--no-skip-existing", false)]
    public void Parse_TwoSpellings_LastOneWins(string first, string second, bool skipExisting)
    {
        CommandLineParseResult result = Parse([first, second, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(skipExisting, result.Options.SkipExisting);
    }
}
