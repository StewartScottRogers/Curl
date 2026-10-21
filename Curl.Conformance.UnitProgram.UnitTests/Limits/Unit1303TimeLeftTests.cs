using Curl.Conformance.UnitProgram.Limits;

namespace Curl.Conformance.UnitProgram.UnitTests.Limits;

[TestClass]
public sealed class Unit1303TimeLeftTests
{
    [TestMethod]
    public async Task RunAsync_TransferTimeLeft_EveryRowPassesAndNothingIsWritten()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit1303TimeLeft(), standardError);

        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("unit1303", new Unit1303TimeLeft().ProgramName);
        Assert.AreEqual(33, Unit1303TimeLeft.RowCount);
    }

    [TestMethod]
    public async Task RunAsync_TimeLeftAlwaysZero_EveryRowWithTimeLeftFailsWithItsComment()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit1303TimeLeft((_, _, _, _) => 0), standardError);

        string[] lines = standardError.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual(25, result.ExitCode);
        Assert.HasCount(25, lines);
        Assert.AreEqual("unit1303: 6 seconds should be left", lines[0]);
        Assert.AreEqual("unit1303: no timeout active", lines[^2]);
        Assert.AreEqual("unit1303: 6 seconds should be left", lines[^1]);
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit1303TimeLeft().RunAsync(null!, CancellationToken.None));
    }

    private static Task<UnitProgramResult> RunAsync(Unit1303TimeLeft program, StringWriter standardError) =>
        program.RunAsync(new UnitProgramContext(1303, []) { StandardError = standardError }, CancellationToken.None);
}
