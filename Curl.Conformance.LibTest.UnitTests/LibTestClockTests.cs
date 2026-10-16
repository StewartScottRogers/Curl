namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class LibTestClockTests
{
    [TestMethod]
    public void Constructor_RefusesANullTimeProvider()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new LibTestClock(null!));
    }

    [TestMethod]
    public void Now_AndDifferenceMilliseconds_MeasureTheProvidersTimeTruncated()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var clock = new LibTestClock(time);
        long older = clock.Now();

        time.Advance(TimeSpan.FromMicroseconds(1999));
        long newer = clock.Now();

        Assert.AreEqual(1L, clock.DifferenceMilliseconds(newer, older));
        Assert.AreEqual(-1L, clock.DifferenceMilliseconds(older, newer));
    }

    [TestMethod]
    public void StartTestTiming_TakesNowAsTheTestStart()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var clock = new LibTestClock(time);
        time.Advance(TimeSpan.FromSeconds(3));

        clock.StartTestTiming();
        time.Advance(TimeSpan.FromMilliseconds(250));

        Assert.AreEqual(TimeSpan.FromSeconds(3).Ticks, clock.TestStart);
        Assert.AreEqual(250L, clock.MillisecondsSinceTestStart());
    }

    [TestMethod]
    public async Task WaitAsync_CompletesOnlyWhenTheProvidersClockReachesIt()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var clock = new LibTestClock(time);

        Task wait = clock.WaitAsync(100, CancellationToken.None);
        time.Advance(TimeSpan.FromMilliseconds(99));
        bool doneEarly = wait.IsCompleted;
        time.Advance(TimeSpan.FromMilliseconds(1));
        await wait;

        Assert.IsFalse(doneEarly);
        Assert.IsTrue(wait.IsCompletedSuccessfully);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-5)]
    public void WaitAsync_OfNoTime_ReturnsAtOnce(int milliseconds)
    {
        var clock = new LibTestClock(new ManualTimeProvider(DateTimeOffset.UnixEpoch));

        Task wait = clock.WaitAsync(milliseconds, CancellationToken.None);

        Assert.IsTrue(wait.IsCompletedSuccessfully);
    }
}
