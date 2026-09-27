namespace Curl.Cli;

/// <summary>
/// Pins how the parser records <c>-R</c>/<c>--remote-time</c> and its <c>--no-remote-time</c>
/// negation: off when absent, and the last spelling wins, as for every negatable flag.
/// </summary>
[TestClass]
public sealed class CommandLineRemoteTimeOptionTests
{
    private const string Url = "http://127.0.0.1:1/";

    [TestMethod]
    public void Parse_NoRemoteTimeOption_DoesNotAskForRemoteTime()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.RemoteTime);
    }

    [TestMethod]
    [DataRow("-R")]
    [DataRow("--remote-time")]
    public void Parse_RemoteTime_AsksForRemoteTime(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.RemoteTime);
    }

    [TestMethod]
    public void Parse_RemoteTimeThenNoRemoteTime_DoesNotAskForRemoteTime()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-R", "--no-remote-time", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.RemoteTime);
    }

    [TestMethod]
    public void Parse_NoRemoteTimeThenRemoteTime_AsksForRemoteTime()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--no-remote-time", "-R", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.RemoteTime);
    }
}
