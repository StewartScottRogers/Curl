using Curl.Conformance.UnitProgram.Limits;
using Curl.Core;

namespace Curl.Conformance.UnitProgram.UnitTests.Limits;

[TestClass]
public sealed class Unit3216RateLimitTests
{
    [TestMethod]
    public async Task RunAsync_TransferRateLimit_EveryCheckPassesAndNothingIsWritten()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit3216RateLimit(), standardError);

        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("unit3216", new Unit3216RateLimit().ProgramName);
    }

    [TestMethod]
    public async Task RunAsync_LimitTwiceAsFast_TheLimitedChecksFailAndTheUnlimitedOnesPass()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(
            new Unit3216RateLimit((rate, burst, clock) => new TransferRateLimit(rate * 2, burst * 2, clock)),
            standardError);

        string written = standardError.ToString();
        Assert.AreNotEqual(0, result.ExitCode);
        StringAssert.Contains(written, "unit3216: initial 10");
        StringAssert.Contains(written, "unit3216: 10/15 burst limit");
        Assert.DoesNotContain("unit3216: inf", written);
        Assert.DoesNotContain("blocked to 0", written);
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit3216RateLimit().RunAsync(null!, CancellationToken.None));
    }

    private static Task<UnitProgramResult> RunAsync(Unit3216RateLimit program, StringWriter standardError) =>
        program.RunAsync(new UnitProgramContext(3216, []) { StandardError = standardError }, CancellationToken.None);
}
