using Curl.Conformance.UnitProgram.Hashes;
using Curl.Conformance.UnitProgram.UrlParsing;

namespace Curl.Conformance.UnitProgram.UnitTests.UrlParsing;

[TestClass]
public sealed class Unit1620UrlLoginDetailsTests
{
    [TestMethod]
    public async Task RunAsync_CurlLoginParserAndLibcurlHandle_EveryCheckPassesAndNothingIsWritten()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit1620UrlLoginDetails(), standardError);

        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("unit1620", new Unit1620UrlLoginDetails().ProgramName);
    }

    [TestMethod]
    public async Task RunAsync_PresenceIsWrong_FailsPresenceChecksAndSkipsEqualityChecks()
    {
        using StringWriter standardError = new();
        Unit1620UrlLoginDetails program = new(_ => (null, "x", "x"), (_, _) => Task.CompletedTask);

        UnitProgramResult result = await RunAsync(program, standardError);

        string written = standardError.ToString();
        StringAssert.StartsWith(written, "unit1620: username expectation failed" + Environment.NewLine + "unit1620: password expectation failed");
        Assert.DoesNotContain("should be equal", written);
        Assert.IsGreaterThan(0, result.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_ValuesDifferButArePresent_FailsEqualityChecks()
    {
        using StringWriter standardError = new();
        Unit1620UrlLoginDetails program = new(_ => ("wrong", null, null), (_, _) => Task.CompletedTask);

        UnitProgramResult result = await RunAsync(program, standardError);

        StringAssert.StartsWith(
            standardError.ToString(),
            "unit1620: userstr should be equal to exp_username" + Environment.NewLine + "unit1620: password expectation failed");
        Assert.IsGreaterThan(0, result.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_HandleCheckFails_CountsIt()
    {
        using StringWriter standardError = new();
        Unit1620UrlLoginDetails program = new(Unit1620UrlLoginDetails.ParseLoginDetailsWithCurl, FailOneHandleCheckAsync);

        UnitProgramResult result = await RunAsync(program, standardError);

        Assert.AreEqual(1, result.ExitCode);
        StringAssert.StartsWith(standardError.ToString(), "unit1620: Curl_connect() failed to return CURLE_URL_MALFORMAT");
    }

    [TestMethod]
    public async Task CheckEasyHandleWithLibcurlAsync_NullChecks_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Unit1620UrlLoginDetails.CheckEasyHandleWithLibcurlAsync(null!, CancellationToken.None));
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit1620UrlLoginDetails().RunAsync(null!, CancellationToken.None));
    }

    private static Task FailOneHandleCheckAsync(UnitChecks checks, CancellationToken cancellationToken)
    {
        checks.FailUnless(false, "Curl_connect() failed to return CURLE_URL_MALFORMAT");
        return Task.CompletedTask;
    }

    private static Task<UnitProgramResult> RunAsync(Unit1620UrlLoginDetails program, StringWriter standardError) =>
        program.RunAsync(new UnitProgramContext(1620, []) { StandardError = standardError }, CancellationToken.None);
}
