using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <c>--disallow-username-in-url</c> / <c>--no-disallow-username-in-url</c> as curl 8.21.0 reads
/// them, measured with the local curl 8.21.0 on 2026-09-29 through <c>Record-CurlExchange.ps1</c>: both
/// spellings are accepted, the last one wins, and a <c>--next</c> group starts without it (BL-626 Notes).
/// </summary>
[TestClass]
public sealed class CommandLineDisallowUsernameInUrlTests
{
    private const string Url = "http://u@127.0.0.1:1/";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Parse_NoSpelling_AllowsAUserName()
    {
        CommandLineParseResult result = Parse([Url]);

        TestDiagnostics.For(TestContext).Assert("disallow username in url", false, result.Options?.DisallowUsernameInUrl);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.DisallowUsernameInUrl);
    }

    [TestMethod]
    [DataRow("--disallow-username-in-url", true)]
    [DataRow("--no-disallow-username-in-url", false)]
    public void Parse_OneSpelling_SetsDisallowUsernameInUrl(string spelling, bool disallow)
    {
        CommandLineParseResult result = Parse([spelling, Url]);

        TestDiagnostics.For(TestContext).Assert("disallow username in url", disallow, result.Options?.DisallowUsernameInUrl);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(disallow, result.Options.DisallowUsernameInUrl);
    }

    [TestMethod]
    [DataRow("--no-disallow-username-in-url", "--disallow-username-in-url", true)]
    [DataRow("--disallow-username-in-url", "--no-disallow-username-in-url", false)]
    public void Parse_TwoSpellings_LastOneWins(string first, string second, bool disallow)
    {
        CommandLineParseResult result = Parse([first, second, Url]);

        TestDiagnostics.For(TestContext).Assert("disallow username in url", disallow, result.Options?.DisallowUsernameInUrl);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(disallow, result.Options.DisallowUsernameInUrl);
    }

    [TestMethod]
    public void Parse_DisallowBeforeNext_DoesNotCarryIntoTheNextGroup()
    {
        CommandLineParseResult result = Parse(["--disallow-username-in-url", Url, "--next", Url]);

        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        diagnostics.Assert("group 0 disallow username in url", true, result.Groups[0].DisallowUsernameInUrl);
        diagnostics.Assert("group 1 disallow username in url", false, result.Groups[1].DisallowUsernameInUrl);
        Assert.IsTrue(result.Groups[0].DisallowUsernameInUrl);
        Assert.IsFalse(result.Groups[1].DisallowUsernameInUrl);
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
