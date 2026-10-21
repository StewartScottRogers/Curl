using System.Text;
using Curl.Conformance.UnitProgram.Asn1.Der;

namespace Curl.Conformance.UnitProgram.UnitTests.Asn1.Der;

[TestClass]
public sealed class Unit1667ElementTextTests
{
    [TestMethod]
    public async Task RunAsync_CurlPrinter_EveryRowPassesAndNothingIsWritten()
    {
        using StringWriter standardError = new();
        using MemoryStream standardOutput = new();

        UnitProgramResult result = await RunAsync(new Unit1667ElementText(true), standardError, standardOutput);

        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0L, standardOutput.Length);
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("unit1667", new Unit1667ElementText().ProgramName);
    }

    [TestMethod]
    public async Task RunAsync_OpenSslBuild_PrintsNotTestedAndPasses()
    {
        using StringWriter standardError = new();
        using MemoryStream standardOutput = new();

        UnitProgramResult result = await RunAsync(new Unit1667ElementText(false), standardError, standardOutput);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("not tested since ASN1tostr() is not built in\n", Encoding.ASCII.GetString(standardOutput.ToArray()));
        Assert.AreEqual(string.Empty, standardError.ToString());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task RunAsync_RegisteredPortOnWindows_RunsTheChecks()
    {
        using MemoryStream standardOutput = new();

        UnitProgramResult result = await RunAsync(new Unit1667ElementText(), new StringWriter(), standardOutput);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(0L, standardOutput.Length);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task RunAsync_RegisteredPortOffWindows_PrintsNotTested()
    {
        using MemoryStream standardOutput = new();

        UnitProgramResult result = await RunAsync(new Unit1667ElementText(), new StringWriter(), standardOutput);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(Unit1667ElementText.NotTestedLine, Encoding.ASCII.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_PrinterThatAlwaysPrints_WritesEachDifferingRowAndCountsOneFailure()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit1667ElementText((tag, _) => tag == 12 ? "ABCDE" : new string('x', 50), true), standardError, Stream.Null);

        string[] lines = standardError.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual(1, result.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "test 0 (type 31): expect result 43, got 100",
                "test 1 (type 99): expect result 43, got 100",
                "test 2 (type 12): expected output 'abcde', got 'ABCDE' (length 5)",
                "test 2: index 0 byte 41",
                "test 2: index 1 byte 42",
                "test 2: index 2 byte 43",
                "test 2: index 3 byte 44",
                "test 2: index 4 byte 45",
                "test 3 (type 12): unexpected length. Got 5, expected 6)",
                "test 3 (type 12): expected output 'åäö', got 'ABCDE' (length 5)",
            },
            lines[..10]);
        Assert.AreEqual("test 4 (type 18): expect result 0, got 100", lines[15]);
        Assert.AreEqual("test 4: expected output 'abcde'", lines[16]);
        Assert.AreEqual("unit1667: some tests of ASN1tostr() failed", lines[^1]);
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit1667ElementText().RunAsync(null!, CancellationToken.None));
    }

    private static Task<UnitProgramResult> RunAsync(Unit1667ElementText program, StringWriter standardError, Stream standardOutput) =>
        program.RunAsync(new UnitProgramContext(1667, []) { StandardError = standardError, StandardOutput = standardOutput }, CancellationToken.None);
}
