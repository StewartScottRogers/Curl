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
}
