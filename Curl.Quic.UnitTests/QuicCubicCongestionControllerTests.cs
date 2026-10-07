using System.Globalization;
using Curl.Testing;
using static Curl.Quic.QuicLossRecoveryTests;

namespace Curl.Quic;

[TestClass]
public sealed class QuicCubicCongestionControllerTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void ReduceWindow_FirstLossThenOneBeforeRegainingWMax_MultipliesBy07WithFastConvergence()
    {
        Diagnostics.Arrange("max datagram size", 1200);
        Diagnostics.Arrange("events", "ack of packet 0 at 10 ms; losses at 20 ms, 30 ms, then 7 more");
        QuicCubicCongestionController controller = new(1200);
        controller.OnPacketsAcknowledged([Packet(0, 0)], Ms(10), new QuicRttEstimator());
        Diagnostics.Act("window after first acknowledgement", controller.CongestionWindow);
        Diagnostics.Assert("window after first acknowledgement", 13200, controller.CongestionWindow);
        Assert.AreEqual(13200, controller.CongestionWindow);

        // RFC 9438 section 4.6: W_max = 11 datagrams, ssthresh = cwnd = 13200 x 0.7.
        controller.OnPacketsLost([Packet(1, 5)], persistentCongestion: false, Ms(20));
        Diagnostics.Act("W_max after first loss", controller.WindowMaximum);
        Diagnostics.Act("window and threshold after first loss", $"{controller.CongestionWindow}, {controller.SlowStartThreshold}");
        Diagnostics.Assert("W_max after first loss", 11, controller.WindowMaximum);
        Assert.AreEqual(11, controller.WindowMaximum, 1e-9);
        Diagnostics.Assert("window and threshold after first loss", "9240, 9240", $"{controller.CongestionWindow}, {controller.SlowStartThreshold}");
        Assert.AreEqual((9240L, 9240L), (controller.CongestionWindow, controller.SlowStartThreshold));

        // Section 4.7: 7.7 datagrams is below W_max, so W_max = 7.7 x (1 + 0.7) / 2.
        controller.OnPacketsLost([Packet(2, 25)], persistentCongestion: false, Ms(30));
        Diagnostics.Act("W_max after second loss", controller.WindowMaximum);
        Diagnostics.Act("window after second loss", controller.CongestionWindow);
        Diagnostics.Assert("W_max after second loss", 6.545, controller.WindowMaximum);
        Assert.AreEqual(6.545, controller.WindowMaximum, 1e-9);
        Diagnostics.Assert("window after second loss", 6468, controller.CongestionWindow);
        Assert.AreEqual(6468, controller.CongestionWindow);

        for (ulong packetNumber = 3; packetNumber < 10; packetNumber++)
        {
            controller.OnPacketsLost([Packet(packetNumber, 40 + (double)packetNumber)], persistentCongestion: false, Ms(40.5 + packetNumber));
        }

        Diagnostics.Act("window after repeated losses", controller.CongestionWindow);
        Diagnostics.Assert("window after repeated losses", controller.MinimumWindow, controller.CongestionWindow);
        Assert.AreEqual(controller.MinimumWindow, controller.CongestionWindow);
    }

    [TestMethod]
    public void IncreaseWindow_FirstCongestionAvoidanceAcknowledgement_StartsTheEpochInTheRenoFriendlyRegion()
    {
        Diagnostics.Arrange("setup", "ack at 10 ms, loss at 20 ms (window 9240, W_max 11), ack of packet 9 sent at 25 ms received at 100 ms");
        QuicCubicCongestionController controller = AfterFirstLoss();

        controller.OnPacketsAcknowledged([Packet(9, 25)], Ms(100), new QuicRttEstimator());

        // K = cbrt((11 - 7.7) / 0.4); W_cubic(0) = 7.7 is below W_est = 7.7 + alpha_cubic / 7.7, so the window is W_est (section 4.3).
        var expectedWindow = (long)((7.7 + (QuicCubicCongestionController.Alpha / 7.7)) * 1200);
        Diagnostics.Act("K", controller.TimeToWindowMaximum);
        Diagnostics.Act("W_cubic(K)", controller.CubicWindow(controller.TimeToWindowMaximum));
        Diagnostics.Act("W_cubic(0)", controller.CubicWindow(0));
        Diagnostics.Act("window", controller.CongestionWindow);
        Diagnostics.Assert("K", Math.Cbrt(3.3 / 0.4), controller.TimeToWindowMaximum);
        Assert.AreEqual(Math.Cbrt(3.3 / 0.4), controller.TimeToWindowMaximum, 1e-9);
        Diagnostics.Assert("W_cubic(K)", 11, controller.CubicWindow(controller.TimeToWindowMaximum));
        Assert.AreEqual(11, controller.CubicWindow(controller.TimeToWindowMaximum), 1e-9);
        Diagnostics.Assert("W_cubic(0)", 7.7, controller.CubicWindow(0));
        Assert.AreEqual(7.7, controller.CubicWindow(0), 1e-9);
        Diagnostics.Assert("window", expectedWindow, controller.CongestionWindow);
        Assert.AreEqual((long)((7.7 + (QuicCubicCongestionController.Alpha / 7.7)) * 1200), controller.CongestionWindow);
    }

    [TestMethod]
    public void IncreaseWindow_LongRoundTrips_FollowsTheCubicConcaveThenConvexPastWMax()
    {
        Diagnostics.Arrange("round trip ms", 1000);
        Diagnostics.Arrange("rounds", 6);
        QuicCubicCongestionController controller = AfterFirstLoss();
        List<long> windows = RunRoundTrips(controller, roundTrip: Ms(1000), rounds: 6);

        // K is about 2 s: the window climbs, flattens as it nears W_max = 13200 bytes around then, and accelerates past it.
        long[] growth = [.. windows.Zip(windows.Skip(1), (before, after) => after - before)];
        Diagnostics.Act("windows", Join(windows));
        Diagnostics.Act("growth per round", Join(growth));
        Diagnostics.Assert("every step is non-negative", true, growth.All(step => step >= 0));
        Assert.IsTrue(growth.All(step => step >= 0), string.Join(" ", windows));
        Diagnostics.Assert("window in round 3 (within 5 percent)", 13200, windows[3]);
        Assert.AreEqual(13200, windows[3], 13200 * 0.05, string.Join(" ", windows));
        Diagnostics.Assert("last window is above W_max bytes", true, windows[^1] > 13200);
        Assert.IsGreaterThan(13200, windows[^1], string.Join(" ", windows));
        Diagnostics.Assert("growth flattens (growth[2] < growth[1])", true, growth[2] < growth[1]);
        Assert.IsLessThan(growth[1], growth[2], string.Join(" ", windows));
        Diagnostics.Assert("growth accelerates (growth[3] > growth[2])", true, growth[3] > growth[2]);
        Assert.IsGreaterThan(growth[2], growth[3], string.Join(" ", windows));
    }

    [TestMethod]
    public void IncreaseWindow_ShortRoundTrips_GrowsAsRenoOnceTheEstimatePassesTheWindowBeforeLoss()
    {
        Diagnostics.Arrange("round trip ms", 50);
        Diagnostics.Arrange("rounds", 12);
        QuicCubicCongestionController controller = AfterFirstLoss();
        List<long> windows = RunRoundTrips(controller, roundTrip: Ms(50), rounds: 12);

        // W_est grows by alpha_cubic per round trip until it reaches the 11 datagrams before the loss, then by 1.
        Diagnostics.Act("windows", Join(windows));
        Diagnostics.Assert("last window is above W_max bytes", true, windows[^1] > 13200);
        Assert.IsGreaterThan(13200, windows[^1], string.Join(" ", windows));
        Diagnostics.Assert("last round growth (within 60)", 1200, windows[^1] - windows[^2]);
        Assert.AreEqual(1200, windows[^1] - windows[^2], 60, string.Join(" ", windows));
    }

    [TestMethod]
    public void OnPacketsLost_PersistentCongestion_RestartsTheEpochFromTheMinimumWindow()
    {
        Diagnostics.Arrange("setup", "first loss, epoch started at 100 ms, then persistent congestion at 120 ms, then 3 rounds of 100 ms");
        QuicCubicCongestionController controller = AfterFirstLoss();
        controller.OnPacketsAcknowledged([Packet(9, 25)], Ms(100), new QuicRttEstimator());

        controller.OnPacketsLost([Packet(10, 110)], persistentCongestion: true, Ms(120));
        Diagnostics.Act("window after persistent congestion", controller.CongestionWindow);
        Diagnostics.Assert("window after persistent congestion", 2400, controller.CongestionWindow);
        Assert.AreEqual(2400, controller.CongestionWindow);
        RunRoundTrips(controller, Ms(100), rounds: 3);

        // Slow start back to ssthresh, then a new epoch whose K is measured from the window it starts at.
        Diagnostics.Act("window", controller.CongestionWindow);
        Diagnostics.Act("slow start threshold", controller.SlowStartThreshold);
        Diagnostics.Act("K", controller.TimeToWindowMaximum);
        Diagnostics.Assert("window is at least the threshold", true, controller.CongestionWindow >= controller.SlowStartThreshold);
        Assert.IsGreaterThanOrEqualTo(controller.SlowStartThreshold, controller.CongestionWindow);
        Diagnostics.Assert("K differs from the first epoch's", true, Math.Cbrt(3.3 / 0.4) != controller.TimeToWindowMaximum);
        Assert.AreNotEqual(Math.Cbrt(3.3 / 0.4), controller.TimeToWindowMaximum);
    }

    [TestMethod]
    public void OnPacketsAcknowledged_ApplicationLimitedPacketInSlowStart_LeavesTheWindowButSamplesTheRtt()
    {
        Diagnostics.Arrange("max datagram size", 1200);
        Diagnostics.Arrange("acknowledged packet", "0, application limited, at 10 ms");
        QuicCubicCongestionController controller = new(1200);

        controller.OnPacketsAcknowledged([Packet(0, 0) with { IsApplicationLimited = true }], Ms(10), new QuicRttEstimator());

        // RFC 9002 section 7.8; HyStart++ still counts the acknowledgement's RTT sample, as ngtcp2 does.
        Diagnostics.Act("window", controller.CongestionWindow);
        Diagnostics.Act("current round rtt samples", controller.CurrentRoundRttSamples);
        Diagnostics.Assert("window", 12000, controller.CongestionWindow);
        Assert.AreEqual(12000, controller.CongestionWindow);
        Diagnostics.Assert("current round rtt samples", 1, controller.CurrentRoundRttSamples);
        Assert.AreEqual(1, controller.CurrentRoundRttSamples);
    }

    [TestMethod]
    public void OnPacketsAcknowledged_ApplicationLimitedBeforeTheEpochStarts_LeavesTheWindowThenStartsTheEpochAsUsual()
    {
        Diagnostics.Arrange("setup", "after first loss; ack of limited packet 9 at 90 ms, then of packet 10 at 100 ms");
        QuicCubicCongestionController controller = AfterFirstLoss();

        controller.OnPacketsAcknowledged([Packet(9, 25) with { IsApplicationLimited = true }], Ms(90), new QuicRttEstimator());
        Diagnostics.Act("window after limited packet", controller.CongestionWindow);
        Diagnostics.Assert("window after limited packet", 9240, controller.CongestionWindow);
        Assert.AreEqual(9240, controller.CongestionWindow);

        controller.OnPacketsAcknowledged([Packet(10, 25)], Ms(100), new QuicRttEstimator());
        var expectedWindow = (long)((7.7 + (QuicCubicCongestionController.Alpha / 7.7)) * 1200);
        Diagnostics.Act("window after other packet", controller.CongestionWindow);
        Diagnostics.Assert("window after other packet", expectedWindow, controller.CongestionWindow);
        Assert.AreEqual((long)((7.7 + (QuicCubicCongestionController.Alpha / 7.7)) * 1200), controller.CongestionWindow);
    }

    [TestMethod]
    public void OnPacketsAcknowledged_ApplicationLimitedInCongestionAvoidance_HoldsTheWindowAndStopsTheCubicsClock()
    {
        Diagnostics.Arrange("setup", "three controllers in an epoch started at 100 ms");
        QuicCubicCongestionController limited = InEpochFrom100Ms();
        QuicCubicCongestionController unlimitedSoon = InEpochFrom100Ms();
        QuicCubicCongestionController unlimitedLate = InEpochFrom100Ms();
        long window = limited.CongestionWindow;
        Diagnostics.Arrange("window before", window);

        // A second in which only application-limited packets are acknowledged.
        limited.OnPacketsAcknowledged([Packet(20, 150) with { IsApplicationLimited = true }], Ms(200), Rtt());
        limited.OnPacketsAcknowledged([Packet(21, 150) with { IsApplicationLimited = true }], Ms(900), Rtt());
        Diagnostics.Act("limited window after limited acknowledgements", limited.CongestionWindow);
        Diagnostics.Assert("limited window after limited acknowledgements", window, limited.CongestionWindow);
        Assert.AreEqual(window, limited.CongestionWindow);
        limited.OnPacketsAcknowledged([Packet(22, 1150)], Ms(1200), Rtt());

        // The epoch moved on by that second, so the window is where it would be 100 ms into the epoch, not 1100 ms.
        unlimitedSoon.OnPacketsAcknowledged([Packet(22, 150)], Ms(200), Rtt());
        unlimitedLate.OnPacketsAcknowledged([Packet(22, 1150)], Ms(1200), Rtt());
        Diagnostics.Act("limited, soon, late windows", $"{limited.CongestionWindow}, {unlimitedSoon.CongestionWindow}, {unlimitedLate.CongestionWindow}");
        Diagnostics.Assert("limited window equals the soon window", unlimitedSoon.CongestionWindow, limited.CongestionWindow);
        Assert.AreEqual(unlimitedSoon.CongestionWindow, limited.CongestionWindow);
        Diagnostics.Assert("limited window differs from the late window", true, unlimitedLate.CongestionWindow != limited.CongestionWindow);
        Assert.AreNotEqual(unlimitedLate.CongestionWindow, limited.CongestionWindow);
    }

    [TestMethod]
    [DataRow(16, 4)]
    [DataRow(100, 12.5)]
    [DataRow(200, 16)]
    public void RttThreshold_LastRoundMinimum_IsAnEighthKeptBetween4And16Ms(double lastRoundMilliseconds, double thresholdMilliseconds)
    {
        Diagnostics.Arrange("last round minimum ms", lastRoundMilliseconds);

        var actual = QuicCubicCongestionController.RttThreshold(Ms(lastRoundMilliseconds));

        Diagnostics.Act("threshold ms", actual.TotalMilliseconds);
        Diagnostics.Assert("threshold ms", thresholdMilliseconds, actual.TotalMilliseconds);
        Assert.AreEqual(Ms(thresholdMilliseconds), QuicCubicCongestionController.RttThreshold(Ms(lastRoundMilliseconds)));
    }

    [TestMethod]
    public void OnPacketsAcknowledged_SamplesWithinARound_KeepTheLowestAndCountOnePerAcknowledgement()
    {
        Diagnostics.Arrange("round samples ms", "120, 100, 110");
        SlowStart run = new();

        run.Round(120, 100, 110);

        Diagnostics.Act("hystart round", run.Controller.HyStartRound);
        Diagnostics.Act("current round minimum rtt ms", run.Controller.CurrentRoundMinimumRtt?.TotalMilliseconds);
        Diagnostics.Act("current round rtt samples", run.Controller.CurrentRoundRttSamples);
        Diagnostics.Assert("hystart round", 1, run.Controller.HyStartRound);
        Assert.AreEqual(1, run.Controller.HyStartRound);
        Diagnostics.Assert("current round minimum rtt ms", 100, run.Controller.CurrentRoundMinimumRtt?.TotalMilliseconds);
        Assert.AreEqual(Ms(100), run.Controller.CurrentRoundMinimumRtt);
        Diagnostics.Assert("current round rtt samples", 3, run.Controller.CurrentRoundRttSamples);
        Assert.AreEqual(3, run.Controller.CurrentRoundRttSamples);
    }

    [TestMethod]
    public void OnPacketsAcknowledged_RoundMinimumRisesByTheThreshold_EntersConservativeSlowStart()
    {
        Diagnostics.Arrange("rounds", "2 of 8 samples at 100 ms, then 8 at 112 ms, then 8 at 126 ms");
        SlowStart run = new();
        run.Rounds(2, 100);

        // 112 ms is below 100 + 12.5 ms: slow start goes on, and each round of 8 datagrams adds 9600 bytes.
        run.Round(Eight(112));
        var afterFirst = $"{run.Controller.HyStartRound}, {run.Controller.ConservativeSlowStartRound}, {run.Controller.CongestionWindow}";
        Diagnostics.Act("hystart round, css round, window after 112 ms round", afterFirst);
        Diagnostics.Assert("hystart round, css round, window after 112 ms round", "3, 0, 40800", afterFirst);
        Assert.AreEqual((3, 0, 40800L), (run.Controller.HyStartRound, run.Controller.ConservativeSlowStartRound, run.Controller.CongestionWindow));

        // 126 ms reaches 112 + 14 ms: the round's eighth sample enters Conservative Slow Start, after its full growth.
        run.Round(Eight(126));
        var afterSecond = $"{run.Controller.HyStartRound}, {run.Controller.ConservativeSlowStartRound}, {run.Controller.CongestionWindow}";
        Diagnostics.Act("hystart round, css round, window after 126 ms round", afterSecond);
        Diagnostics.Assert("hystart round, css round, window after 126 ms round", "4, 1, 50400", afterSecond);
        Assert.AreEqual((4, 1, 50400L), (run.Controller.HyStartRound, run.Controller.ConservativeSlowStartRound, run.Controller.CongestionWindow));
    }

    [TestMethod]
    public void OnPacketsAcknowledged_SevenSamplesPastTheThreshold_StaysInSlowStart()
    {
        Diagnostics.Arrange("rounds", "2 of 8 samples at 100 ms, then 7 samples at 200 ms");
        SlowStart run = new();
        run.Rounds(2, 100);

        // N_RTT_SAMPLE is 8: a round of seven acknowledgements never compares, however high its RTT.
        run.Round(200, 200, 200, 200, 200, 200, 200);

        var actual = $"{run.Controller.HyStartRound}, {run.Controller.CurrentRoundRttSamples}, {run.Controller.ConservativeSlowStartRound}";
        Diagnostics.Act("hystart round, round samples, css round", actual);
        Diagnostics.Assert("hystart round, round samples, css round", "3, 7, 0", actual);
        Assert.AreEqual((3, 7, 0), (run.Controller.HyStartRound, run.Controller.CurrentRoundRttSamples, run.Controller.ConservativeSlowStartRound));
    }

    [TestMethod]
    public void OnPacketsAcknowledged_ConservativeSlowStart_GrowsByAQuarterAndExitsAfterItsFifthRoundStarts()
    {
        Diagnostics.Arrange("setup", "entered Conservative Slow Start, then 3 rounds at 126 ms, then 1 more");
        SlowStart run = EnteredConservativeSlowStart();
        var entered = $"{run.Controller.ConservativeSlowStartRound}, {run.Controller.CongestionWindow}";
        Diagnostics.Act("css round, window on entry", entered);
        Diagnostics.Assert("css round, window on entry", "1, 50400", entered);
        Assert.AreEqual((1, 50400L), (run.Controller.ConservativeSlowStartRound, run.Controller.CongestionWindow));

        // CSS_GROWTH_DIVISOR 4: 8 datagrams add 2400 bytes a round.
        run.Rounds(3, 126);
        var grown = $"{run.Controller.ConservativeSlowStartRound}, {run.Controller.CongestionWindow}";
        Diagnostics.Act("css round, window after 3 rounds", grown);
        Diagnostics.Assert("css round, window after 3 rounds", "4, 57600", grown);
        Assert.AreEqual((4, 57600L), (run.Controller.ConservativeSlowStartRound, run.Controller.CongestionWindow));
        Diagnostics.Assert("slow start threshold", long.MaxValue, run.Controller.SlowStartThreshold);
        Assert.AreEqual(long.MaxValue, run.Controller.SlowStartThreshold);

        // CSS_ROUNDS 5: the first acknowledgement of the fifth round grows by 300 bytes, then slow start ends at the window reached.
        run.Round(126);
        var exited = $"{run.Controller.ConservativeSlowStartRound}, {run.Controller.CongestionWindow}, {run.Controller.SlowStartThreshold}";
        Diagnostics.Act("css round, window, threshold after exit", exited);
        Diagnostics.Assert("css round, window, threshold after exit", "5, 57900, 57900", exited);
        Assert.AreEqual((5, 57900L, 57900L), (run.Controller.ConservativeSlowStartRound, run.Controller.CongestionWindow, run.Controller.SlowStartThreshold));
        Diagnostics.Act("W_max", run.Controller.WindowMaximum);
        Diagnostics.Act("K", run.Controller.TimeToWindowMaximum);
        Diagnostics.Assert("W_max", 57900 / 1200.0, run.Controller.WindowMaximum);
        Diagnostics.Assert("K", 0.0, run.Controller.TimeToWindowMaximum);
        Assert.AreEqual((57900 / 1200.0, 0.0), (run.Controller.WindowMaximum, run.Controller.TimeToWindowMaximum));
    }

    [TestMethod]
    public void OnPacketsAcknowledged_RttFallsBelowTheConservativeSlowStartBaseline_ReturnsToSlowStart()
    {
        Diagnostics.Arrange("setup", "entered Conservative Slow Start with a 126 ms baseline, then a round of 8 samples at 120 ms");
        SlowStart run = EnteredConservativeSlowStart();

        // The first acknowledgement of the round still grows by a quarter, then its 120 ms sample is below the 126 ms baseline.
        run.Round(Eight(120));

        Diagnostics.Act("css round", run.Controller.ConservativeSlowStartRound);
        Diagnostics.Act("window", run.Controller.CongestionWindow);
        Diagnostics.Act("slow start threshold", run.Controller.SlowStartThreshold);
        Diagnostics.Assert("css round", 0, run.Controller.ConservativeSlowStartRound);
        Assert.AreEqual(0, run.Controller.ConservativeSlowStartRound);
        Diagnostics.Assert("window", 50400L + 300 + (7 * 1200), run.Controller.CongestionWindow);
        Assert.AreEqual(50400L + 300 + (7 * 1200), run.Controller.CongestionWindow);
        Diagnostics.Assert("slow start threshold", long.MaxValue, run.Controller.SlowStartThreshold);
        Assert.AreEqual(long.MaxValue, run.Controller.SlowStartThreshold);
    }

    [TestMethod]
    public void OnPacketsAcknowledged_SlowStartAfterPersistentCongestion_RunsWithoutHyStart()
    {
        Diagnostics.Arrange("setup", "4 rounds of 8 samples at 100 ms, persistent congestion, then rounds at 100 ms and 200 ms");
        SlowStart run = new();
        run.Rounds(4, 100);
        QuicSentPacket lost = Packet(1000, run.Now.TotalMilliseconds);
        run.Controller.OnPacketSent(lost);
        run.Controller.OnPacketsLost([lost], persistentCongestion: true, run.Now + Ms(1));
        run.Now += Ms(2);
        var afterLoss = $"{run.Controller.CongestionWindow}, {run.Controller.SlowStartThreshold}";
        Diagnostics.Act("window, threshold after persistent congestion", afterLoss);
        Diagnostics.Assert("window, threshold after persistent congestion", "2400, 35280", afterLoss);
        Assert.AreEqual((2400L, 35280L), (run.Controller.CongestionWindow, run.Controller.SlowStartThreshold));

        // A rise from 100 to 200 ms would enter Conservative Slow Start in the first slow start (RFC 9406 section 4.2: HyStart++ is for that one only).
        run.Round(Eight(100));
        run.Round(Eight(200));

        var rounds = $"{run.Controller.HyStartRound}, {run.Controller.ConservativeSlowStartRound}";
        Diagnostics.Act("hystart round, css round", rounds);
        Diagnostics.Act("window", run.Controller.CongestionWindow);
        Diagnostics.Assert("hystart round, css round", "4, 0", rounds);
        Assert.AreEqual((4, 0), (run.Controller.HyStartRound, run.Controller.ConservativeSlowStartRound));
        Diagnostics.Assert("window", 2400 + (16 * 1200), run.Controller.CongestionWindow);
        Assert.AreEqual(2400 + (16 * 1200), run.Controller.CongestionWindow);
    }

    private static string Join(IEnumerable<long> values) => string.Join(" ", values.Select(value => value.ToString(CultureInfo.InvariantCulture)));

    private static QuicCubicCongestionController InEpochFrom100Ms()
    {
        QuicCubicCongestionController controller = AfterFirstLoss();
        controller.OnPacketsAcknowledged([Packet(9, 25)], Ms(100), Rtt());
        return controller;
    }

    private static QuicRttEstimator Rtt()
    {
        QuicRttEstimator rtt = new();
        rtt.Update(Ms(50), TimeSpan.Zero, Ms(25), handshakeConfirmed: true);
        return rtt;
    }

    private static QuicCubicCongestionController AfterFirstLoss()
    {
        QuicCubicCongestionController controller = new(1200);
        controller.OnPacketsAcknowledged([Packet(0, 0)], Ms(10), new QuicRttEstimator());
        controller.OnPacketsLost([Packet(1, 5)], persistentCongestion: false, Ms(20));
        return controller;
    }

    // Each round sends a window of packets and has them all acknowledged one round trip later, sampling that RTT.
    private static List<long> RunRoundTrips(QuicCubicCongestionController controller, TimeSpan roundTrip, int rounds)
    {
        QuicRttEstimator rtt = new();
        List<long> windows = [controller.CongestionWindow];
        TimeSpan now = Ms(100);
        ulong packetNumber = 100;
        for (var round = 0; round < rounds; round++)
        {
            List<QuicSentPacket> sent = [];
            for (var bytes = 0L; bytes < controller.CongestionWindow; bytes += 1200)
            {
                QuicSentPacket packet = Packet(packetNumber++, now.TotalMilliseconds);
                controller.OnPacketSent(packet);
                sent.Add(packet);
            }

            now += roundTrip;
            rtt.Update(roundTrip, TimeSpan.Zero, Ms(25), handshakeConfirmed: true);
            controller.OnPacketsAcknowledged(sent, now, rtt);
            windows.Add(controller.CongestionWindow);
        }

        return windows;
    }

    // Four rounds of 8 acknowledgements: 100, 100, 112 and 126 ms, the last entering Conservative Slow Start at its eighth.
    private static SlowStart EnteredConservativeSlowStart()
    {
        SlowStart run = new();
        run.Rounds(2, 100);
        run.Round(Eight(112));
        run.Round(Eight(126));
        Assert.AreEqual(4, run.Controller.HyStartRound);
        return run;
    }

    private static double[] Eight(double roundTripMilliseconds) => [.. Enumerable.Repeat(roundTripMilliseconds, 8)];

    // Each round sends one 1200-byte packet per sample at once, then acknowledges them one
    // acknowledgement each, in order, the i-th after the i-th sample. The next round sends when
    // the last acknowledgement arrives, so its first acknowledgement ends this round.
    private sealed class SlowStart
    {
        private readonly QuicRttEstimator rtt = new();

        private ulong packetNumber;

        public QuicCubicCongestionController Controller { get; } = new(1200);

        public TimeSpan Now { get; set; } = Ms(100);

        public void Rounds(int count, double roundTripMilliseconds)
        {
            for (var round = 0; round < count; round++)
            {
                Round(Eight(roundTripMilliseconds));
            }
        }

        public void Round(params double[] roundTripsMilliseconds)
        {
            TimeSpan sentAt = Now;
            List<QuicSentPacket> sent = [];
            foreach (var roundTrip in roundTripsMilliseconds)
            {
                QuicSentPacket packet = Packet(packetNumber++, sentAt.TotalMilliseconds);
                Controller.OnPacketSent(packet);
                sent.Add(packet);
            }

            for (var index = 0; index < sent.Count; index++)
            {
                rtt.Update(Ms(roundTripsMilliseconds[index]), TimeSpan.Zero, Ms(25), handshakeConfirmed: true);
                Controller.OnPacketsAcknowledged([sent[index]], sentAt + Ms(roundTripsMilliseconds[index]), rtt);
            }

            Now = sentAt + Ms(roundTripsMilliseconds.Max());
        }
    }
}
