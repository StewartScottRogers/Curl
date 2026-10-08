using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins that <see cref="TransferTimings" /> carries the timestamps it is
/// given, and that a reader turns them into exact durations on the
/// provider that produced them, with no real clock.
/// </summary>
[TestClass]
public sealed class TransferTimingsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void New_WithEveryTimestamp_CarriesEachOneAndNoRedirectDuration()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var connect = new ConnectTimings(10, 20, 30, 40);
        diagnostics.Arrange("timestamps", "started 5, ready 50, sent 60, first byte 70, completed 80");

        var timings = new TransferTimings(5, connect, 50, 60, 70, 80);

        diagnostics.Act("Started", timings.Started);
        diagnostics.Act("Completed", timings.Completed);
        diagnostics.Act("RedirectDuration", timings.RedirectDuration);
        diagnostics.Assert("RedirectDuration", TimeSpan.Zero, timings.RedirectDuration);
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
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("timestamps", "started 5, completed 80, all others null");

        var timings = new TransferTimings(5, null, null, null, null, 80);

        diagnostics.Act("Connect", timings.Connect);
        diagnostics.Act("RequestReady", timings.RequestReady);
        diagnostics.Assert("Connect", null, timings.Connect);
        Assert.IsNull(timings.Connect);
        Assert.IsNull(timings.RequestReady);
        Assert.IsNull(timings.RequestSent);
        Assert.IsNull(timings.FirstByteReceived);
    }

    [TestMethod]
    public void With_SettingRedirectDuration_CarriesIt()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("RedirectDuration", TimeSpan.FromMilliseconds(250));

        var timings = new TransferTimings(0, null, null, null, null, 1) with
        {
            RedirectDuration = TimeSpan.FromMilliseconds(250),
        };

        diagnostics.Act("RedirectDuration", timings.RedirectDuration);
        diagnostics.Assert("RedirectDuration", TimeSpan.FromMilliseconds(250), timings.RedirectDuration);
        Assert.AreEqual(TimeSpan.FromMilliseconds(250), timings.RedirectDuration);
    }

    [TestMethod]
    public void With_ReplacingEveryTimestamp_CarriesTheNewValues()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var connect = new ConnectTimings(1, 2, 3, 4);
        diagnostics.Arrange("replacement timestamps", "5, 50, 60, 70, 80");

        var timings = new TransferTimings(0, null, null, null, null, 0) with
        {
            Started = 5,
            Connect = connect,
            RequestReady = 50,
            RequestSent = 60,
            FirstByteReceived = 70,
            Completed = 80,
        };

        diagnostics.Act("Started", timings.Started);
        diagnostics.Act("Completed", timings.Completed);
        diagnostics.Assert("Completed", 80L, timings.Completed);
        Assert.AreEqual(new TransferTimings(5, connect, 50, 60, 70, 80), timings);
    }

    [TestMethod]
    public void GetElapsedTime_BetweenTwoTimestampsOfOneProvider_IsTheExactDuration()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var clock = new SteppedTimeProvider();
        var started = clock.GetTimestamp();
        clock.Advance(TimeSpan.FromMilliseconds(125));
        var completed = clock.GetTimestamp();
        diagnostics.Arrange("clock advance", TimeSpan.FromMilliseconds(125));

        var timings = new TransferTimings(started, null, null, null, null, completed);

        TimeSpan elapsed = clock.GetElapsedTime(timings.Started, timings.Completed);
        diagnostics.Act("elapsed", elapsed);
        diagnostics.Assert("elapsed", TimeSpan.FromMilliseconds(125), elapsed);
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
