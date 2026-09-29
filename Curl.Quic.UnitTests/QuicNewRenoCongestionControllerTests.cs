using static Curl.Quic.QuicLossRecoveryTests;

namespace Curl.Quic;

[TestClass]
public sealed class QuicNewRenoCongestionControllerTests
{
    [TestMethod]
    public void Constructor_1200ByteDatagrams_StartsWithTenDatagramsAndFloorsAtTwo()
    {
        QuicNewRenoCongestionController controller = new(1200);

        // RFC 9002 section 7.2: min(10 x 1200, max(14720, 2 x 1200)) and 2 x 1200.
        Assert.AreEqual(12000, controller.InitialWindow);
        Assert.AreEqual(12000, controller.CongestionWindow);
        Assert.AreEqual(2400, controller.MinimumWindow);
        Assert.AreEqual(long.MaxValue, controller.SlowStartThreshold);
        Assert.AreEqual(12000, controller.AvailableWindow);
        Assert.IsNull(controller.RecoveryStartTime);
        Assert.AreEqual(14720, new QuicNewRenoCongestionController(1472).InitialWindow);
    }

    [TestMethod]
    public void OnPacketSent_TenDatagrams_FillsTheWindow()
    {
        QuicNewRenoCongestionController controller = new(1200);

        for (ulong packetNumber = 0; packetNumber < 10; packetNumber++)
        {
            controller.OnPacketSent(Packet(packetNumber, 0));
        }

        controller.OnPacketSent(Packet(10, 0, ackEliciting: false));

        Assert.AreEqual(12000, controller.BytesInFlight);
        Assert.AreEqual(0, controller.AvailableWindow);
    }

    [TestMethod]
    public void Window_GrowthLossAndRecovery_FollowsAppendixB()
    {
        QuicNewRenoCongestionController controller = new(1200);
        QuicRttEstimator rtt = new();
        for (ulong packetNumber = 0; packetNumber < 4; packetNumber++)
        {
            controller.OnPacketSent(Packet(packetNumber, 0));
        }

        // Slow start: every acknowledged byte grows the window.
        controller.OnPacketsAcknowledged([Packet(0, 0), Packet(9, 0, ackEliciting: false)], Ms(10), rtt);
        Assert.AreEqual(13200, controller.CongestionWindow);

        // A loss halves it and starts recovery.
        controller.OnPacketsLost([Packet(1, 0)], persistentCongestion: false, Ms(20));
        Assert.AreEqual((6600L, 6600L, Ms(20)), (controller.CongestionWindow, controller.SlowStartThreshold, controller.RecoveryStartTime!.Value));

        // Within recovery a further loss or an acknowledgement of a packet sent before it changes nothing.
        controller.OnPacketsLost([Packet(2, 0)], persistentCongestion: false, Ms(30));
        controller.OnPacketsAcknowledged([Packet(3, 0)], Ms(30), rtt);
        Assert.AreEqual(6600, controller.CongestionWindow);
        Assert.IsTrue(controller.IsInRecovery(Ms(20)));
        Assert.IsFalse(controller.IsInRecovery(Ms(21)));

        // A packet sent after recovery started ends it; congestion avoidance adds 1200 x 1200 / 6600 bytes.
        controller.OnPacketSent(Packet(4, 25));
        controller.OnPacketsAcknowledged([Packet(4, 25)], Ms(40), rtt);
        Assert.AreEqual(6818, controller.CongestionWindow);
        Assert.AreEqual(0, controller.BytesInFlight);

        // A loss of packets sent after the last recovery started opens a new one, whatever order they come in.
        controller.OnPacketsLost([Packet(6, 50), Packet(5, 45)], persistentCongestion: false, Ms(60));
        Assert.AreEqual((3409L, Ms(60)), (controller.CongestionWindow, controller.RecoveryStartTime!.Value));

        // The window never falls below two datagrams.
        controller.OnPacketsLost([Packet(7, 70), Packet(8, 80)], persistentCongestion: false, Ms(100));
        Assert.AreEqual((2400L, 1704L), (controller.CongestionWindow, controller.SlowStartThreshold));
    }

    [TestMethod]
    public void OnPacketsLost_PersistentCongestion_CollapsesTheWindowAndEndsRecovery()
    {
        QuicNewRenoCongestionController controller = new(1200);
        controller.OnPacketSent(Packet(0, 0));

        controller.OnPacketsLost([Packet(0, 0), Packet(1, 0, ackEliciting: false)], persistentCongestion: true, Ms(10));

        Assert.AreEqual(2400, controller.CongestionWindow);
        Assert.AreEqual(6000, controller.SlowStartThreshold);
        Assert.IsNull(controller.RecoveryStartTime);
        Assert.AreEqual(0, controller.BytesInFlight);
    }

    [TestMethod]
    public void OnPacketsLost_OnlyPacketsNotInFlight_IsNoCongestionEvent()
    {
        QuicNewRenoCongestionController controller = new(1200);

        controller.OnPacketsLost([Packet(0, 0, ackEliciting: false)], persistentCongestion: false, Ms(10));

        Assert.AreEqual(12000, controller.CongestionWindow);
        Assert.IsNull(controller.RecoveryStartTime);
    }

    [TestMethod]
    public void RemoveFromBytesInFlight_DiscardedPackets_LeaveTheWindowAlone()
    {
        QuicNewRenoCongestionController controller = new(1200);
        controller.OnPacketSent(Packet(0, 0));
        controller.OnPacketSent(Packet(1, 0));

        controller.RemoveFromBytesInFlight([Packet(0, 0), Packet(2, 0, ackEliciting: false)]);

        Assert.AreEqual(1200, controller.BytesInFlight);
        Assert.AreEqual(12000, controller.CongestionWindow);
    }

    [TestMethod]
    public void Create_EachAlgorithm_ReturnsItsController()
    {
        Assert.IsInstanceOfType<QuicNewRenoCongestionController>(QuicCongestionController.Create(QuicCongestionControlAlgorithm.NewReno, 1200));
        Assert.IsInstanceOfType<QuicCubicCongestionController>(QuicCongestionController.Create(QuicCongestionControlAlgorithm.Cubic, 1200));
    }

    [TestMethod]
    public void Calls_BadArguments_Throw()
    {
        QuicNewRenoCongestionController controller = new(1200);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QuicNewRenoCongestionController(1199));
        Assert.ThrowsExactly<ArgumentNullException>(() => controller.OnPacketSent(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => controller.OnPacketsAcknowledged(null!, TimeSpan.Zero, new QuicRttEstimator()));
        Assert.ThrowsExactly<ArgumentNullException>(() => controller.OnPacketsAcknowledged([], TimeSpan.Zero, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => controller.OnPacketsLost(null!, false, TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentNullException>(() => controller.RemoveFromBytesInFlight(null!));
    }

    [TestMethod]
    public void OnPacketsAcknowledged_ApplicationLimitedPacketsInSlowStart_GrowsTheWindowOnlyForTheOthers()
    {
        QuicNewRenoCongestionController controller = new(1200);

        // RFC 9002 section 7.8: a packet sent while the application limited sending says nothing about the path's capacity.
        controller.OnPacketsAcknowledged([Packet(0, 0) with { IsApplicationLimited = true }], Ms(10), new QuicRttEstimator());
        Assert.AreEqual(12000, controller.CongestionWindow);

        controller.OnPacketsAcknowledged([Packet(1, 0) with { IsApplicationLimited = true }, Packet(2, 0)], Ms(20), new QuicRttEstimator());
        Assert.AreEqual(13200, controller.CongestionWindow);
    }

    [TestMethod]
    public void OnPacketsAcknowledged_ApplicationLimitedPacketInCongestionAvoidance_LeavesTheWindow()
    {
        QuicNewRenoCongestionController controller = new(1200);
        controller.OnPacketsLost([Packet(0, 0)], persistentCongestion: false, Ms(10));
        Assert.AreEqual(6000, controller.CongestionWindow);

        controller.OnPacketsAcknowledged([Packet(1, 20) with { IsApplicationLimited = true }], Ms(30), new QuicRttEstimator());
        Assert.AreEqual(6000, controller.CongestionWindow);

        controller.OnPacketsAcknowledged([Packet(2, 20)], Ms(30), new QuicRttEstimator());
        Assert.AreEqual(6240, controller.CongestionWindow);
    }
}
