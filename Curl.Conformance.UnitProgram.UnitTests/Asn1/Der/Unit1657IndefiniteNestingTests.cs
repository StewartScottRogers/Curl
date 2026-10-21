using System.Text;
using Curl.Conformance.UnitProgram.Asn1.Der;

namespace Curl.Conformance.UnitProgram.UnitTests.Asn1.Der;

[TestClass]
public sealed class Unit1657IndefiniteNestingTests
{
    [TestMethod]
    public async Task RunAsync_CurlPrinter_EveryRowPassesAndNothingIsWritten()
    {
        using StringWriter standardError = new();
        using MemoryStream standardOutput = new();

        UnitProgramResult result = await RunAsync(new Unit1657IndefiniteNesting(true), standardError, standardOutput);

        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0L, standardOutput.Length);
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("unit1657", new Unit1657IndefiniteNesting().ProgramName);
    }

    [TestMethod]
    public async Task RunAsync_OpenSslBuild_PrintsNotTestedAndPasses()
    {
        using StringWriter standardError = new();
        using MemoryStream standardOutput = new();

        UnitProgramResult result = await RunAsync(new Unit1657IndefiniteNesting(false), standardError, standardOutput);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("not tested since getASN1Element() is not built in\n", Encoding.ASCII.GetString(standardOutput.ToArray()));
        Assert.AreEqual(string.Empty, standardError.ToString());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task RunAsync_RegisteredPortOnWindows_RunsTheChecks()
    {
        using MemoryStream standardOutput = new();

        UnitProgramResult result = await RunAsync(new Unit1657IndefiniteNesting(), new StringWriter(), standardOutput);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(0L, standardOutput.Length);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task RunAsync_RegisteredPortOffWindows_PrintsNotTested()
    {
        using MemoryStream standardOutput = new();

        UnitProgramResult result = await RunAsync(new Unit1657IndefiniteNesting(), new StringWriter(), standardOutput);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(Unit1657IndefiniteNesting.NotTestedLine, Encoding.ASCII.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_ReaderThatAcceptsAnyDepth_WritesTheTwoDeepRowsAndCountsOneFailure()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit1657IndefiniteNesting(_ => true, true), standardError, Stream.Null);

        string newLine = Environment.NewLine;
        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual(
            "test 2: expect result 43, got 0" + newLine
            + "test 3: expect result 43, got 0" + newLine
            + "unit1657: some tests of getASN1Element() fails" + newLine,
            standardError.ToString());
    }

    [TestMethod]
    public async Task RunAsync_ReaderThatRefusesEverything_WritesTheTwoShallowRows()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit1657IndefiniteNesting(_ => false, true), standardError, Stream.Null);

        Assert.AreEqual(1, result.ExitCode);
        Assert.StartsWith("test 0: expect result 0, got 43" + Environment.NewLine + "test 1: expect result 0, got 43", standardError.ToString());
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit1657IndefiniteNesting().RunAsync(null!, CancellationToken.None));
    }

    private static Task<UnitProgramResult> RunAsync(Unit1657IndefiniteNesting program, StringWriter standardError, Stream standardOutput) =>
        program.RunAsync(new UnitProgramContext(1657, []) { StandardError = standardError, StandardOutput = standardOutput }, CancellationToken.None);
}
