using Curl.Conformance.UnitProgram.Dns.Shuffle;

namespace Curl.Conformance.UnitProgram.UnitTests.Dns.Shuffle;

[TestClass]
public sealed class Unit1608DnsShuffleTests
{
    [TestMethod]
    public async Task RunAsync_OverTheShuffle_TheHeadChangesAndNothingIsWritten()
    {
        IUnitProgram program = UnitPrograms.Ported.Find("unit1608")!;
        using StringWriter standardError = new();

        UnitProgramResult result = await program.RunAsync(new UnitProgramContext(1608, []) { StandardError = standardError }, CancellationToken.None);

        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0, result.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_ShuffleThatNeverMovesTheHead_ShufflesTenTimesAndFailsOnce()
    {
        int shuffles = 0;
        Unit1608DnsShuffle program = new(addresses =>
        {
            shuffles++;
            return addresses;
        });
        using StringWriter standardError = new();

        UnitProgramResult result = await program.RunAsync(new UnitProgramContext(1608, []) { StandardError = standardError }, CancellationToken.None);

        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual(10, shuffles);
        Assert.AreEqual("unit1608.c Assertion FAILED: addresses are not being reordered\n", standardError.ToString());
    }

    [TestMethod]
    public async Task RunAsync_ShuffleThatMovesTheHeadOnTheThirdTry_StopsThere()
    {
        int shuffles = 0;
        Unit1608DnsShuffle program = new(addresses => ++shuffles == 3 ? [.. addresses.Reverse()] : addresses);
        using StringWriter standardError = new();

        UnitProgramResult result = await program.RunAsync(new UnitProgramContext(1608, []) { StandardError = standardError }, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(3, shuffles);
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit1608DnsShuffle().RunAsync(null!, CancellationToken.None));
    }
}
