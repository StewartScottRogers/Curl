using static Curl.Quic.QuicLossRecoveryTests;

namespace Curl.Quic;

[TestClass]
public sealed class QuicCubicCongestionControllerTests
{
    [TestMethod]
    public void ReduceWindow_FirstLossThenOneBeforeRegainingWMax_MultipliesBy07WithFastConvergence()
    {
        QuicCubicCongestionController controller = new(1200);
        controller.OnPacketsAcknowledged([Packet(0, 0)], Ms(10), new QuicRttEstimator());
        Assert.AreEqual(13200, controller.CongestionWindow);

        // RFC 9438 section 4.6: W_max = 11 datagrams, ssthresh = cwnd = 13200 x 0.7.
        controller.OnPacketsLost([Packet(1, 5)], persistentCongestion: false, Ms(20));
        Assert.AreEqual(11, controller.WindowMaximum, 1e-9);
        Assert.AreEqual((9240L, 9240L), (controller.CongestionWindow, controller.SlowStartThreshold));

        // Section 4.7: 7.7 datagrams is below W_max, so W_max = 7.7 x (1 + 0.7) / 2.
        controller.OnPacketsLost([Packet(2, 25)], persistentCongestion: false, Ms(30));
        Assert.AreEqual(6.545, controller.WindowMaximum, 1e-9);
        Assert.AreEqual(6468, controller.CongestionWindow);

        for (ulong packetNumber = 3; packetNumber < 10; packetNumber++)
        {
            controller.OnPacketsLost([Packet(packetNumber, 40 + (double)packetNumber)], persistentCongestion: false, Ms(40.5 + packetNumber));
        }

        Assert.AreEqual(controller.MinimumWindow, controller.CongestionWindow);
    }

    [TestMethod]
    public void IncreaseWindow_FirstCongestionAvoidanceAcknowledgement_StartsTheEpochInTheRenoFriendlyRegion()
    {
        QuicCubicCongestionController controller = AfterFirstLoss();

        controller.OnPacketsAcknowledged([Packet(9, 25)], Ms(100), new QuicRttEstimator());

        // K = cbrt((11 - 7.7) / 0.4); W_cubic(0) = 7.7 is below W_est = 7.7 + alpha_cubic / 7.7, so the window is W_est (section 4.3).
        Assert.AreEqual(Math.Cbrt(3.3 / 0.4), controller.TimeToWindowMaximum, 1e-9);
        Assert.AreEqual(11, controller.CubicWindow(controller.TimeToWindowMaximum), 1e-9);
        Assert.AreEqual(7.7, controller.CubicWindow(0), 1e-9);
        Assert.AreEqual((long)((7.7 + (QuicCubicCongestionController.Alpha / 7.7)) * 1200), controller.CongestionWindow);
    }

    [TestMethod]
    public void IncreaseWindow_LongRoundTrips_FollowsTheCubicConcaveThenConvexPastWMax()
    {
        QuicCubicCongestionController controller = AfterFirstLoss();
        List<long> windows = RunRoundTrips(controller, roundTrip: Ms(1000), rounds: 6);

        // K is about 2 s: the window climbs, flattens as it nears W_max = 13200 bytes around then, and accelerates past it.
        long[] growth = [.. windows.Zip(windows.Skip(1), (before, after) => after - before)];
        Assert.IsTrue(growth.All(step => step >= 0), string.Join(" ", windows));
        Assert.AreEqual(13200, windows[3], 13200 * 0.05, string.Join(" ", windows));
        Assert.IsGreaterThan(13200, windows[^1], string.Join(" ", windows));
        Assert.IsLessThan(growth[1], growth[2], string.Join(" ", windows));
        Assert.IsGreaterThan(growth[2], growth[3], string.Join(" ", windows));
    }

    [TestMethod]
    public void IncreaseWindow_ShortRoundTrips_GrowsAsRenoOnceTheEstimatePassesTheWindowBeforeLoss()
    {
        QuicCubicCongestionController controller = AfterFirstLoss();
        List<long> windows = RunRoundTrips(controller, roundTrip: Ms(50), rounds: 12);

        // W_est grows by alpha_cubic per round trip until it reaches the 11 datagrams before the loss, then by 1.
        Assert.IsGreaterThan(13200, windows[^1], string.Join(" ", windows));
        Assert.AreEqual(1200, windows[^1] - windows[^2], 60, string.Join(" ", windows));
    }

    [TestMethod]
    public void OnPacketsLost_PersistentCongestion_RestartsTheEpochFromTheMinimumWindow()
    {
        QuicCubicCongestionController controller = AfterFirstLoss();
        controller.OnPacketsAcknowledged([Packet(9, 25)], Ms(100), new QuicRttEstimator());

        controller.OnPacketsLost([Packet(10, 110)], persistentCongestion: true, Ms(120));
        Assert.AreEqual(2400, controller.CongestionWindow);
        RunRoundTrips(controller, Ms(100), rounds: 3);

        // Slow start back to ssthresh, then a new epoch whose K is measured from the window it starts at.
        Assert.IsGreaterThanOrEqualTo(controller.SlowStartThreshold, controller.CongestionWindow);
        Assert.AreNotEqual(Math.Cbrt(3.3 / 0.4), controller.TimeToWindowMaximum);
    }

    [TestMethod]
    public void OnPacketsAcknowledged_ApplicationLimitedPacketInSlowStart_LeavesTheWindowButSamplesTheRtt()
    {
        QuicCubicCongestionController controller = new(1200);

        controller.OnPacketsAcknowledged([Packet(0, 0) with { IsApplicationLimited = true }], Ms(10), new QuicRttEstimator());

        // RFC 9002 section 7.8; HyStart++ still counts the acknowledgement's RTT sample, as ngtcp2 does.
        Assert.AreEqual(12000, controller.CongestionWindow);
        Assert.AreEqual(1, controller.CurrentRoundRttSamples);
    }

    [TestMethod]
    public void OnPacketsAcknowledged_ApplicationLimitedBeforeTheEpochStarts_LeavesTheWindowThenStartsTheEpochAsUsual()
    {
        QuicCubicCongestionController controller = AfterFirstLoss();

        controller.OnPacketsAcknowledged([Packet(9, 25) with { IsApplicationLimited = true }], Ms(90), new QuicRttEstimator());
        Assert.AreEqual(9240, controller.CongestionWindow);

        controller.OnPacketsAcknowledged([Packet(10, 25)], Ms(100), new QuicRttEstimator());
        Assert.AreEqual((long)((7.7 + (QuicCubicCongestionController.Alpha / 7.7)) * 1200), controller.CongestionWindow);
    }

    [TestMethod]
    public void OnPacketsAcknowledged_ApplicationLimitedInCongestionAvoidance_HoldsTheWindowAndStopsTheCubicsClock()
    {
        QuicCubicCongestionController limited = InEpochFrom100Ms();
        QuicCubicCongestionController unlimitedSoon = InEpochFrom100Ms();
        QuicCubicCongestionController unlimitedLate = InEpochFrom100Ms();
        long window = limited.CongestionWindow;

        // A second in which only application-limited packets are acknowledged.
        limited.OnPacketsAcknowledged([Packet(20, 150) with { IsApplicationLimited = true }], Ms(200), Rtt());
        limited.OnPacketsAcknowledged([Packet(21, 150) with { IsApplicationLimited = true }], Ms(900), Rtt());
        Assert.AreEqual(window, limited.CongestionWindow);
        limited.OnPacketsAcknowledged([Packet(22, 1150)], Ms(1200), Rtt());

        // The epoch moved on by that second, so the window is where it would be 100 ms into the epoch, not 1100 ms.
        unlimitedSoon.OnPacketsAcknowledged([Packet(22, 150)], Ms(200), Rtt());
        unlimitedLate.OnPacketsAcknowledged([Packet(22, 1150)], Ms(1200), Rtt());
        Assert.AreEqual(unlimitedSoon.CongestionWindow, limited.CongestionWindow);
        Assert.AreNotEqual(unlimitedLate.CongestionWindow, limited.CongestionWindow);
    }

    [TestMethod]
    [DataRow(16, 4)]
    [DataRow(100, 12.5)]
    [DataRow(200, 16)]
    public void RttThreshold_LastRoundMinimum_IsAnEighthKeptBetween4And16Ms(double lastRoundMilliseconds, double thresholdMilliseconds) =>
        Assert.AreEqual(Ms(thresholdMilliseconds), QuicCubicCongestionController.RttThreshold(Ms(lastRoundMilliseconds)));

    [TestMethod]
    public void OnPacketsAcknowledged_SamplesWithinARound_KeepTheLowestAndCountOnePerAcknowledgement()
    {
        SlowStart run = new();

        run.Round(120, 100, 110);

        Assert.AreEqual(1, run.Controller.HyStartRound);
        Assert.AreEqual(Ms(100), run.Controller.CurrentRoundMinimumRtt);
        Assert.AreEqual(3, run.Controller.CurrentRoundRttSamples);
    }

    [TestMethod]
    public void OnPacketsAcknowledged_RoundMinimumRisesByTheThreshold_EntersConservativeSlowStart()
    {
        SlowStart run = new();
        run.Rounds(2, 100);

        // 112 ms is below 100 + 12.5 ms: slow start goes on, and each round of 8 datagrams adds 9600 bytes.
        run.Round(Eight(112));
        Assert.AreEqual((3, 0, 40800L), (run.Controller.HyStartRound, run.Controller.ConservativeSlowStartRound, run.Controller.CongestionWindow));

        // 126 ms reaches 112 + 14 ms: the round's eighth sample enters Conservative Slow Start, after its full growth.
        run.Round(Eight(126));
        Assert.AreEqual((4, 1, 50400L), (run.Controller.HyStartRound, run.Controller.ConservativeSlowStartRound, run.Controller.CongestionWindow));
    }

    [TestMethod]
    public void OnPacketsAcknowledged_SevenSamplesPastTheThreshold_StaysInSlowStart()
    {
        SlowStart run = new();
        run.Rounds(2, 100);

        // N_RTT_SAMPLE is 8: a round of seven acknowledgements never compares, however high its RTT.
        run.Round(200, 200, 200, 200, 200, 200, 200);

        Assert.AreEqual((3, 7, 0), (run.Controller.HyStartRound, run.Controller.CurrentRoundRttSamples, run.Controller.ConservativeSlowStartRound));
    }

    [TestMethod]
    public void OnPacketsAcknowledged_ConservativeSlowStart_GrowsByAQuarterAndExitsAfterItsFifthRoundStarts()
    {
        SlowStart run = EnteredConservativeSlowStart();
        Assert.AreEqual((1, 50400L), (run.Controller.ConservativeSlowStartRound, run.Controller.CongestionWindow));

        // CSS_GROWTH_DIVISOR 4: 8 datagrams add 2400 bytes a round.
        run.Rounds(3, 126);
        Assert.AreEqual((4, 57600L), (run.Controller.ConservativeSlowStartRound, run.Controller.CongestionWindow));
        Assert.AreEqual(long.MaxValue, run.Controller.SlowStartThreshold);

        // CSS_ROUNDS 5: the first acknowledgement of the fifth round grows by 300 bytes, then slow start ends at the window reached.
        run.Round(126);
        Assert.AreEqual((5, 57900L, 57900L), (run.Controller.ConservativeSlowStartRound, run.Controller.CongestionWindow, run.Controller.SlowStartThreshold));
        Assert.AreEqual((57900 / 1200.0, 0.0), (run.Controller.WindowMaximum, run.Controller.TimeToWindowMaximum));
    }

    [TestMethod]
    public void OnPacketsAcknowledged_RttFallsBelowTheConservativeSlowStartBaseline_ReturnsToSlowStart()
    {
        SlowStart run = EnteredConservativeSlowStart();

        // The first acknowledgement of the round still grows by a quarter, then its 120 ms sample is below the 126 ms baseline.
        run.Round(Eight(120));

        Assert.AreEqual(0, run.Controller.ConservativeSlowStartRound);
        Assert.AreEqual(50400L + 300 + (7 * 1200), run.Controller.CongestionWindow);
        Assert.AreEqual(long.MaxValue, run.Controller.SlowStartThreshold);
    }

    [TestMethod]
    public void OnPacketsAcknowledged_SlowStartAfterPersistentCongestion_RunsWithoutHyStart()
    {
        SlowStart run = new();
        run.Rounds(4, 100);
        QuicSentPacket lost = Packet(1000, run.Now.TotalMilliseconds);
        run.Controller.OnPacketSent(lost);
        run.Controller.OnPacketsLost([lost], persistentCongestion: true, run.Now + Ms(1));
        run.Now += Ms(2);
        Assert.AreEqual((2400L, 35280L), (run.Controller.CongestionWindow, run.Controller.SlowStartThreshold));

        // A rise from 100 to 200 ms would enter Conservative Slow Start in the first slow start (RFC 9406 section 4.2: HyStart++ is for that one only).
        run.Round(Eight(100));
        run.Round(Eight(200));

        Assert.AreEqual((4, 0), (run.Controller.HyStartRound, run.Controller.ConservativeSlowStartRound));
        Assert.AreEqual(2400 + (16 * 1200), run.Controller.CongestionWindow);
    }

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
