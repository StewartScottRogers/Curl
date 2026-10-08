using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser records <c>-R</c>/<c>--remote-time</c> and its <c>--no-remote-time</c>
/// negation: off when absent, and the last spelling wins, as for every negatable flag.
/// </summary>
[TestClass]
public sealed class CommandLineRemoteTimeOptionTests
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
            Diagnostics.Act("remote time", result.Options.RemoteTime);
        }

        return result;
    }

    [TestMethod]
    public void Parse_NoRemoteTimeOption_DoesNotAskForRemoteTime()
    {
        CommandLineParseResult result = Parse([Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.RemoteTime);
    }

    [TestMethod]
    [DataRow("-R")]
    [DataRow("--remote-time")]
    public void Parse_RemoteTime_AsksForRemoteTime(string spelledOption)
    {
        CommandLineParseResult result = Parse([spelledOption, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.RemoteTime);
    }

    [TestMethod]
    public void Parse_RemoteTimeThenNoRemoteTime_DoesNotAskForRemoteTime()
    {
        CommandLineParseResult result = Parse(["-R", "--no-remote-time", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.RemoteTime);
    }

    [TestMethod]
    public void Parse_NoRemoteTimeThenRemoteTime_AsksForRemoteTime()
    {
        CommandLineParseResult result = Parse(["--no-remote-time", "-R", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.RemoteTime);
    }
}
