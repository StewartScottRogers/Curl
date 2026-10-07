using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser records <c>--tcp-fastopen</c> and <c>--mptcp</c>: plain flags, off by default, each
/// turned off again by its <c>--no-</c> form, as curl 8.21.0 accepts them (measured 2026-10-01, BL-647 Notes).
/// </summary>
[TestClass]
public sealed class CommandLineFastOpenAndMultipathTests
{
    private const string Url = "http://127.0.0.1:1/";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_NeitherOption_LeavesBothOff()
    {
        CommandLineParseResult result = Parse([Url]);

        AssertFlags(result, tcpFastOpen: false, multipathTcp: false);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.TcpFastOpen);
        Assert.IsFalse(result.Options.MultipathTcp);
    }

    [TestMethod]
    public void Parse_TcpFastOpen_TurnsFastOpenOn()
    {
        CommandLineParseResult result = Parse(["--tcp-fastopen", Url]);

        AssertFlags(result, tcpFastOpen: true, multipathTcp: false);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.TcpFastOpen);
        Assert.IsFalse(result.Options.MultipathTcp);
    }

    [TestMethod]
    public void Parse_Mptcp_TurnsMultipathTcpOn()
    {
        CommandLineParseResult result = Parse(["--mptcp", Url]);

        AssertFlags(result, tcpFastOpen: false, multipathTcp: true);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.MultipathTcp);
        Assert.IsFalse(result.Options.TcpFastOpen);
    }

    [TestMethod]
    public void Parse_NoFormsAfterTheOptions_TurnBothOffAgain()
    {
        CommandLineParseResult result = Parse(["--tcp-fastopen", "--mptcp", "--no-tcp-fastopen", "--no-mptcp", Url]);

        AssertFlags(result, tcpFastOpen: false, multipathTcp: false);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.TcpFastOpen);
        Assert.IsFalse(result.Options.MultipathTcp);
    }

    private CommandLineParseResult Parse(string[] arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        return result;
    }

    private void AssertFlags(CommandLineParseResult result, bool tcpFastOpen, bool multipathTcp)
    {
        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("tcp fast open", tcpFastOpen, result.Options?.TcpFastOpen);
        Diagnostics.Assert("multipath tcp", multipathTcp, result.Options?.MultipathTcp);
    }
}
