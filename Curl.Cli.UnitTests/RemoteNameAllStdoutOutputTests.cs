using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins that a <c>-o</c> paired with a URL replaces the remote name <c>--remote-name-all</c> would give it,
/// so <c>-o -</c> writes that URL's body to standard output. curl 8.22.0 (Schannel, Windows 11 x64), measured
/// in the outside fitness review of 2026-11-11 (STD-004, BL-4908): <c>--remote-name-all -o - u/a</c> prints
/// the body and saves no file; <c>--remote-name-all -o - u/a u/b</c> prints <c>a</c>'s body and saves <c>b</c>.
/// </summary>
[TestClass]
public sealed class RemoteNameAllStdoutOutputTests
{
    private const string Url = "http://127.0.0.1:1/a";

    private const string OtherUrl = "http://127.0.0.1:1/b";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_RemoteNameAllThenOutputDashBeforeUrl_WritesToStandardOutput()
    {
        CommandLineParseResult result = Parse(["--remote-name-all", "-o", "-", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        UrlOutput output = result.Options.UrlOutputs.Single();
        Assert.AreEqual("-", output.FileName);
        Assert.IsFalse(output.UsesRemoteName);
    }

    [TestMethod]
    public void Parse_RemoteNameAllThenOutputDashAfterUrl_WritesToStandardOutput()
    {
        CommandLineParseResult result = Parse(["--remote-name-all", Url, "-o", "-"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        UrlOutput output = result.Options.UrlOutputs.Single();
        Assert.AreEqual("-", output.FileName);
        Assert.IsFalse(output.UsesRemoteName);
    }

    [TestMethod]
    public void Parse_RemoteNameAllOutputDashTwoUrls_SavesOnlyTheSecondUnderItsRemoteName()
    {
        CommandLineParseResult result = Parse(["--remote-name-all", "-o", "-", Url, OtherUrl]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Assert.HasCount(2, result.Options.UrlOutputs);
        Assert.IsFalse(result.Options.UrlOutputs[0].UsesRemoteName);
        Assert.AreEqual("-", result.Options.UrlOutputs[0].FileName);
        Assert.IsTrue(result.Options.UrlOutputs[1].UsesRemoteName);
        Assert.IsNull(result.Options.UrlOutputs[1].FileName);
    }

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments)
    {
        Diagnostics.ArrangeArguments(arguments);

        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        return result;
    }
}
