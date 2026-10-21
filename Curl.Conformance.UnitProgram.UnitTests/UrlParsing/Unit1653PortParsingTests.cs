using Curl.Conformance.UnitProgram.UrlParsing;

namespace Curl.Conformance.UnitProgram.UnitTests.UrlParsing;

[TestClass]
public sealed class Unit1653PortParsingTests
{
    [TestMethod]
    public async Task RunAsync_CurlPortSplitter_EveryCheckPassesAndNothingIsWritten()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit1653PortParsing(), standardError);

        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("unit1653", new Unit1653PortParsing().ProgramName);
    }

    [TestMethod]
    public async Task RunAsync_EverythingAcceptedWithPort1_FailsEveryVerdictAndPortCheck()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit1653PortParsing((_, _) => (true, 1)), standardError);

        string written = standardError.ToString();
        string newLine = Environment.NewLine;
        StringAssert.StartsWith(written, "unit1653: curl_url_get portnum returned something" + newLine + "unit1653: parse_port true on error" + newLine + "unit1653: Check portnumber" + newLine);
        StringAssert.Contains(written, "unit1653: parse_port did wrong");
        Assert.AreEqual(9, result.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_NothingAcceptedAndNoPort_FailsAcceptedRowsAndPortLookups()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit1653PortParsing((_, _) => (false, null)), standardError);

        StringAssert.Contains(standardError.ToString(), "unit1653: curl_url_get portnum returned error");
        Assert.AreEqual(16, result.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit1653PortParsing().RunAsync(null!, CancellationToken.None));
    }

    private static Task<UnitProgramResult> RunAsync(Unit1653PortParsing program, StringWriter standardError) =>
        program.RunAsync(new UnitProgramContext(1653, []) { StandardError = standardError }, CancellationToken.None);
}
