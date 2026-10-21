using System.Text;
using Curl.Conformance.UnitProgram.Asn1.Der;

namespace Curl.Conformance.UnitProgram.UnitTests.Asn1.Der;

[TestClass]
public sealed class Unit1656GeneralizedTimeTests
{
    [TestMethod]
    public async Task RunAsync_CurlPrinter_EveryRowPassesAndNothingIsWritten()
    {
        using StringWriter standardError = new();
        using MemoryStream standardOutput = new();

        UnitProgramResult result = await RunAsync(new Unit1656GeneralizedTime(true), standardError, standardOutput);

        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0L, standardOutput.Length);
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("unit1656", new Unit1656GeneralizedTime().ProgramName);
    }

    [TestMethod]
    public async Task RunAsync_OpenSslBuild_PrintsNotTestedAndPasses()
    {
        using StringWriter standardError = new();
        using MemoryStream standardOutput = new();

        UnitProgramResult result = await RunAsync(new Unit1656GeneralizedTime(false), standardError, standardOutput);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("not tested since Curl_x509_GTime2str() is not built in\n", Encoding.ASCII.GetString(standardOutput.ToArray()));
        Assert.AreEqual(string.Empty, standardError.ToString());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task RunAsync_RegisteredPortOnWindows_RunsTheChecks()
    {
        using MemoryStream standardOutput = new();

        UnitProgramResult result = await RunAsync(new Unit1656GeneralizedTime(), new StringWriter(), standardOutput);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(0L, standardOutput.Length);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task RunAsync_RegisteredPortOffWindows_PrintsNotTested()
    {
        using MemoryStream standardOutput = new();

        UnitProgramResult result = await RunAsync(new Unit1656GeneralizedTime(), new StringWriter(), standardOutput);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(Unit1656GeneralizedTime.NotTestedLine, Encoding.ASCII.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_PrinterThatAlwaysPrints_WritesEachDifferingRowAndCountsOneFailure()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit1656GeneralizedTime(_ => "x", true), standardError, Stream.Null);

        string[] lines = standardError.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual(1, result.ExitCode);
        Assert.HasCount(27, lines);
        Assert.AreEqual("test 0: input '190321134340', expected output '1903-21-13 43:40:00', got 'x'", lines[0]);
        Assert.AreEqual("test 1: expect result 43, got 0", lines[1]);
        Assert.AreEqual("unit1656: some tests of Curl_x509_GTime2str() fails", lines[^1]);
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit1656GeneralizedTime().RunAsync(null!, CancellationToken.None));
    }

    private static Task<UnitProgramResult> RunAsync(Unit1656GeneralizedTime program, StringWriter standardError, Stream standardOutput) =>
        program.RunAsync(new UnitProgramContext(1656, []) { StandardError = standardError, StandardOutput = standardOutput }, CancellationToken.None);
}
