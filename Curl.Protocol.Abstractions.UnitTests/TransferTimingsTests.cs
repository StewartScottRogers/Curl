namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins that <see cref="TransferTimings" /> carries the timestamps it is
/// given, and that a reader turns them into exact durations on the
/// provider that produced them, with no real clock.
/// </summary>
[TestClass]
public sealed class TransferTimingsTests
{
    [TestMethod]
    public void New_WithEveryTimestamp_CarriesEachOneAndNoRedirectDuration()
    {
        var connect = new ConnectTimings(10, 20, 30, 40);

        var timings = new TransferTimings(5, connect, 50, 60, 70, 80);

        Assert.AreEqual(5L, timings.Started);
        Assert.AreSame(connect, timings.Connect);
        Assert.AreEqual(50L, timings.RequestReady);
        Assert.AreEqual(60L, timings.RequestSent);
        Assert.AreEqual(70L, timings.FirstByteReceived);
        Assert.AreEqual(80L, timings.Completed);
        Assert.AreEqual(TimeSpan.Zero, timings.RedirectDuration);
    }

    [TestMethod]
    public void New_WithEventsThatDidNotHappen_LeavesThemNull()
    {
        var timings = new TransferTimings(5, null, null, null, null, 80);

        Assert.IsNull(timings.Connect);
        Assert.IsNull(timings.RequestReady);
        Assert.IsNull(timings.RequestSent);
        Assert.IsNull(timings.FirstByteReceived);
    }

    [TestMethod]
    public void With_SettingRedirectDuration_CarriesIt()
    {
        var timings = new TransferTimings(0, null, null, null, null, 1) with
        {
            RedirectDuration = TimeSpan.FromMilliseconds(250),
        };

        Assert.AreEqual(TimeSpan.FromMilliseconds(250), timings.RedirectDuration);
    }

    [TestMethod]
    public void With_ReplacingEveryTimestamp_CarriesTheNewValues()
    {
        var connect = new ConnectTimings(1, 2, 3, 4);

        var timings = new TransferTimings(0, null, null, null, null, 0) with
        {
            Started = 5,
            Connect = connect,
            RequestReady = 50,
            RequestSent = 60,
            FirstByteReceived = 70,
            Completed = 80,
        };

        Assert.AreEqual(new TransferTimings(5, connect, 50, 60, 70, 80), timings);
    }

    [TestMethod]
    public void GetElapsedTime_BetweenTwoTimestampsOfOneProvider_IsTheExactDuration()
    {
        var clock = new SteppedTimeProvider();
        var started = clock.GetTimestamp();
        clock.Advance(TimeSpan.FromMilliseconds(125));
        var completed = clock.GetTimestamp();

        var timings = new TransferTimings(started, null, null, null, null, completed);

        Assert.AreEqual(
            TimeSpan.FromMilliseconds(125),
            clock.GetElapsedTime(timings.Started, timings.Completed));
    }

    private sealed class SteppedTimeProvider : TimeProvider
    {
        private long timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => timestamp;

        public void Advance(TimeSpan by) => timestamp += by.Ticks;
    }
}
