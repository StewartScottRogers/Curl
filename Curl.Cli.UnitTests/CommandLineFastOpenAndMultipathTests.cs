namespace Curl.Cli;

/// <summary>
/// Pins how the parser records <c>--tcp-fastopen</c> and <c>--mptcp</c>: plain flags, off by default, each
/// turned off again by its <c>--no-</c> form, as curl 8.21.0 accepts them (measured 2026-10-01, BL-647 Notes).
/// </summary>
[TestClass]
public sealed class CommandLineFastOpenAndMultipathTests
{
    private const string Url = "http://127.0.0.1:1/";

    [TestMethod]
    public void Parse_NeitherOption_LeavesBothOff()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.TcpFastOpen);
        Assert.IsFalse(result.Options.MultipathTcp);
    }

    [TestMethod]
    public void Parse_TcpFastOpen_TurnsFastOpenOn()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--tcp-fastopen", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.TcpFastOpen);
        Assert.IsFalse(result.Options.MultipathTcp);
    }

    [TestMethod]
    public void Parse_Mptcp_TurnsMultipathTcpOn()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--mptcp", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.MultipathTcp);
        Assert.IsFalse(result.Options.TcpFastOpen);
    }

    [TestMethod]
    public void Parse_NoFormsAfterTheOptions_TurnBothOffAgain()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--tcp-fastopen", "--mptcp", "--no-tcp-fastopen", "--no-mptcp", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.TcpFastOpen);
        Assert.IsFalse(result.Options.MultipathTcp);
    }
}
