using Curl.Conformance.UnitProgram.Limits;
using Curl.Core;

namespace Curl.Conformance.UnitProgram.UnitTests.Limits;

[TestClass]
public sealed class Unit1606SpeedCheckTests
{
    [TestMethod]
    public async Task RunAsync_LowSpeedWatchdog_EveryRowPassesAndNothingIsWritten()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit1606SpeedCheck(), standardError);

        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("unit1606", new Unit1606SpeedCheck().ProgramName);
    }

    [TestMethod]
    public async Task RunAsync_WatchdogTwiceAsPatient_EveryRowThatTimesOutFails()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(
            new Unit1606SpeedCheck((limit, time, clock) => new LowSpeedWatchdog(limit, time * 2, clock)),
            standardError);

        string written = standardError.ToString();
        Assert.AreEqual(5, result.ExitCode);
        StringAssert.Contains(written, "unit1606: wrong low speed timeout");
        StringAssert.Contains(written, "unit1606: wrong log speed timeout");
        StringAssert.Contains(written, "unit1606: bad timeout");
        Assert.DoesNotContain("should not time out", written);
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit1606SpeedCheck().RunAsync(null!, CancellationToken.None));
    }

    private static Task<UnitProgramResult> RunAsync(Unit1606SpeedCheck program, StringWriter standardError) =>
        program.RunAsync(new UnitProgramContext(1606, []) { StandardError = standardError }, CancellationToken.None);
}
