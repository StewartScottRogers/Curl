using Curl.Conformance.UnitProgram.Limits.Unit1399;

namespace Curl.Conformance.UnitProgram.UnitTests.Limits.Unit1399;

[TestClass]
public sealed class Unit1399RedirectAdditiveTimersTests
{
    [TestMethod]
    public async Task RunAsync_RedirectHopTimings_EveryCheckPassesAndWritesYes()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit1399RedirectAdditiveTimers(), standardError);

        string[] lines = standardError.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual(0, result.ExitCode);
        Assert.HasCount(10, lines);
        Assert.AreEqual("is 2000000 us same as 2 seconds? Yes", lines[0]);
        Assert.AreEqual("is 3000000 us same as 3 seconds? Yes", lines[9]);
        Assert.AreEqual("unit1399", new Unit1399RedirectAdditiveTimers().ProgramName);
    }

    [TestMethod]
    public async Task RunAsync_LastHopMeasuredFromFirstStart_TheFiveChecksAfterTheRedirectFail()
    {
        using StringWriter standardError = new();
        Unit1399RedirectAdditiveTimers program = new((hops, clock) => hops[^1] with { Started = hops[0].Started });

        UnitProgramResult result = await RunAsync(program, standardError);

        string written = standardError.ToString();
        Assert.AreEqual(5, result.ExitCode);
        StringAssert.Contains(written, "is 4000000 us same as 3 seconds? No");
        StringAssert.Contains(written, "unit1399: about 3 seconds should have passed");
        Assert.DoesNotContain("unit1399: about 2 seconds should have passed", written);
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit1399RedirectAdditiveTimers().RunAsync(null!, CancellationToken.None));
    }

    private static Task<UnitProgramResult> RunAsync(Unit1399RedirectAdditiveTimers program, StringWriter standardError) =>
        program.RunAsync(new UnitProgramContext(1399, []) { StandardError = standardError }, CancellationToken.None);
}
