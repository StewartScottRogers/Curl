using System.Text;
using Curl.Conformance.UnitProgram.Asn1.Der;

namespace Curl.Conformance.UnitProgram.UnitTests.Asn1.Der;

[TestClass]
public sealed class Unit1666ObjectIdentifierEncodingTests
{
    [TestMethod]
    public async Task RunAsync_CurlPrinter_EveryRowPassesAndNothingIsWritten()
    {
        using StringWriter standardError = new();
        using MemoryStream standardOutput = new();

        UnitProgramResult result = await RunAsync(new Unit1666ObjectIdentifierEncoding(true), standardError, standardOutput);

        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0L, standardOutput.Length);
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("unit1666", new Unit1666ObjectIdentifierEncoding().ProgramName);
    }

    [TestMethod]
    public async Task RunAsync_OpenSslBuild_PrintsNotTestedAndPasses()
    {
        using StringWriter standardError = new();
        using MemoryStream standardOutput = new();

        UnitProgramResult result = await RunAsync(new Unit1666ObjectIdentifierEncoding(false), standardError, standardOutput);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("not tested since encodeOID() is not built in\n", Encoding.ASCII.GetString(standardOutput.ToArray()));
        Assert.AreEqual(string.Empty, standardError.ToString());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task RunAsync_RegisteredPortOnWindows_RunsTheChecks()
    {
        using MemoryStream standardOutput = new();

        UnitProgramResult result = await RunAsync(new Unit1666ObjectIdentifierEncoding(), new StringWriter(), standardOutput);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(0L, standardOutput.Length);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task RunAsync_RegisteredPortOffWindows_PrintsNotTested()
    {
        using MemoryStream standardOutput = new();

        UnitProgramResult result = await RunAsync(new Unit1666ObjectIdentifierEncoding(), new StringWriter(), standardOutput);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(Unit1666ObjectIdentifierEncoding.NotTestedLine, Encoding.ASCII.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_PrinterThatAlwaysPrints_WritesEachDifferingRowAndCountsOneFailure()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit1666ObjectIdentifierEncoding(oid => oid is [0x2B] ? "9.9" : new string('x', 120), true), standardError, Stream.Null);

        string[] lines = standardError.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual("test 0: expect result 43, got 100", lines[0]);
        Assert.AreEqual("test 1: expect result 43, got 100", lines[1]);
        Assert.AreEqual("test 2: expect result 0, got 100", lines[2]);
        Assert.AreEqual("test 2: expected output '2.4294967215'", lines[3]);
        Assert.Contains("test 21: expected output '1.3', got '9.9'", lines);
        Assert.DoesNotContain("test 54: expect result 100, got 100", lines);
        Assert.AreEqual("unit1666: some tests of encodeOID() failed", lines[^1]);
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit1666ObjectIdentifierEncoding().RunAsync(null!, CancellationToken.None));
    }

    private static Task<UnitProgramResult> RunAsync(Unit1666ObjectIdentifierEncoding program, StringWriter standardError, Stream standardOutput) =>
        program.RunAsync(new UnitProgramContext(1666, []) { StandardError = standardError, StandardOutput = standardOutput }, CancellationToken.None);
}
