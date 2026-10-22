using Curl.Core.Fakes;

namespace Curl.Core;

[TestClass]
public sealed class TransferRateLimitTests
{
    [TestMethod]
    public void Constructor_NegativeRateBurstOrNullClock_Throws()
    {
        FakeTimeProvider clock = NewClock();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new TransferRateLimit(-1, 0, clock));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new TransferRateLimit(10, -1, clock));
        Assert.ThrowsExactly<ArgumentNullException>(() => new TransferRateLimit(10, 0, null!));
    }

    [TestMethod]
    public void Drain_NegativeCount_Throws()
    {
        TransferRateLimit limit = new(10, 0, NewClock());

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => limit.Drain(-1));
    }

    [TestMethod]
    public void Available_RateZero_IsUnlimitedAndNeverWaits()
    {
        TransferRateLimit limit = new(0, 0, NewClock());

        limit.Drain(1_000_000);

        Assert.AreEqual(long.MaxValue, limit.Available());
        Assert.AreEqual(0, limit.WaitMilliseconds());
    }

    [TestMethod]
    public void Block_Unlimited_HasNoTokensUntilUnblocked()
    {
        TransferRateLimit limit = new(0, 0, NewClock());

        limit.Block(true);
        limit.Drain(1_000_000);
        long blocked = limit.Available();
        limit.Block(false);

        Assert.AreEqual(0, blocked);
        Assert.AreEqual(long.MaxValue, limit.Available());
        Assert.IsFalse(limit.IsBlocked);
    }

    [TestMethod]
    public void Block_Limited_EmptiesAndNeverWaits_UnblockStartsAfreshWithOneSecondOfTokens()
    {
        FakeTimeProvider clock = NewClock();
        TransferRateLimit limit = new(10, 0, clock);
        limit.Drain(25);

        limit.Block(true);
        limit.Block(true);
        clock.Advance(TimeSpan.FromSeconds(5));
        limit.Drain(5);
        bool isBlocked = limit.IsBlocked;
        long blockedTokens = limit.Available();
        long blockedWait = limit.WaitMilliseconds();
        limit.Block(false);

        Assert.IsTrue(isBlocked);
        Assert.AreEqual(0, blockedTokens);
        Assert.AreEqual(0, blockedWait);
        Assert.AreEqual(10, limit.Available());
    }

    [TestMethod]
    public void Drain_PastZero_TokensGoNegativeAndTheWaitGrowsByTheDebtsShareOfTheRate()
    {
        FakeTimeProvider clock = NewClock();
        TransferRateLimit limit = new(10, 0, clock);
        limit.Drain(8);
        clock.Advance(TimeSpan.FromMilliseconds(1));

        limit.Drain(3);

        Assert.AreEqual(-1, limit.Available());
        Assert.AreEqual(1099, limit.WaitMilliseconds());
    }

    [TestMethod]
    public void Available_EachWholeSecond_AddsTheRateAndCarriesThePartLeftOver()
    {
        FakeTimeProvider clock = NewClock();
        TransferRateLimit limit = new(10, 0, clock);
        limit.Drain(10);
        clock.Advance(TimeSpan.FromMilliseconds(1500));
        long afterOneAndAHalf = limit.Available();
        long waitAfterOneAndAHalf = limit.WaitMilliseconds();

        clock.Advance(TimeSpan.FromMilliseconds(500));

        Assert.AreEqual(10, afterOneAndAHalf);
        Assert.AreEqual(0, waitAfterOneAndAHalf);
        Assert.AreEqual(20, limit.Available());
    }

    [TestMethod]
    public void WaitMilliseconds_Empty_WaitsForTheRestOfTheSecond()
    {
        FakeTimeProvider clock = NewClock();
        TransferRateLimit limit = new(10, 15, clock);
        limit.Drain(10);
        long atOnce = limit.WaitMilliseconds();

        clock.Advance(TimeSpan.FromMilliseconds(500));

        Assert.AreEqual(1000, atOnce);
        Assert.AreEqual(0, limit.Available());
        Assert.AreEqual(500, limit.WaitMilliseconds());
    }

    [TestMethod]
    public void Available_WithBurst_IsCappedAtTheBurst()
    {
        FakeTimeProvider clock = NewClock();
        TransferRateLimit limit = new(10, 15, clock);
        limit.Drain(11);
        clock.Advance(TimeSpan.FromSeconds(1));
        long belowBurst = limit.Available();

        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.AreEqual(9, belowBurst);
        Assert.AreEqual(15, limit.Available());
    }

    [TestMethod]
    public void Drain_PastTheSmallestCount_StaysAtTheSmallestCount()
    {
        TransferRateLimit limit = new(1, 0, NewClock());

        limit.Drain(long.MaxValue);
        limit.Drain(long.MaxValue);

        Assert.AreEqual(long.MinValue, limit.Available());
        Assert.AreEqual(long.MaxValue, limit.WaitMilliseconds());
    }

    [TestMethod]
    public void Available_GainPastTheLargestCount_StaysAtTheLargestCount()
    {
        FakeTimeProvider clock = NewClock();
        TransferRateLimit limit = new(long.MaxValue, 0, clock);

        clock.Advance(TimeSpan.FromSeconds(2));

        Assert.AreEqual(long.MaxValue, limit.Available());
    }

    private static FakeTimeProvider NewClock() => new(DateTimeOffset.UnixEpoch);
}
