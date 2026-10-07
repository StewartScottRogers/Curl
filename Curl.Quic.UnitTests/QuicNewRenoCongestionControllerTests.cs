using System.Globalization;
using Curl.Testing;
using static Curl.Quic.QuicLossRecoveryTests;

namespace Curl.Quic;

[TestClass]
public sealed class QuicNewRenoCongestionControllerTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Constructor_1200ByteDatagrams_StartsWithTenDatagramsAndFloorsAtTwo()
    {
        Diagnostics.Arrange("max datagram size", 1200);

        QuicNewRenoCongestionController controller = new(1200);

        // RFC 9002 section 7.2: min(10 x 1200, max(14720, 2 x 1200)) and 2 x 1200.
        Diagnostics.Act("initial window", controller.InitialWindow);
        Diagnostics.Act("congestion window", controller.CongestionWindow);
        Diagnostics.Act("minimum window", controller.MinimumWindow);
        Diagnostics.Act("slow start threshold", controller.SlowStartThreshold);
        Diagnostics.Act("available window", controller.AvailableWindow);
        Diagnostics.Act("recovery start time is null", controller.RecoveryStartTime is null);
        Diagnostics.Assert("initial window", 12000, controller.InitialWindow);
        Assert.AreEqual(12000, controller.InitialWindow);
        Diagnostics.Assert("congestion window", 12000, controller.CongestionWindow);
        Assert.AreEqual(12000, controller.CongestionWindow);
        Diagnostics.Assert("minimum window", 2400, controller.MinimumWindow);
        Assert.AreEqual(2400, controller.MinimumWindow);
        Diagnostics.Assert("slow start threshold", long.MaxValue, controller.SlowStartThreshold);
        Assert.AreEqual(long.MaxValue, controller.SlowStartThreshold);
        Diagnostics.Assert("available window", 12000, controller.AvailableWindow);
        Assert.AreEqual(12000, controller.AvailableWindow);
        Diagnostics.Assert("recovery start time is null", true, controller.RecoveryStartTime is null);
        Assert.IsNull(controller.RecoveryStartTime);
        var larger = new QuicNewRenoCongestionController(1472).InitialWindow;
        Diagnostics.Assert("initial window for 1472-byte datagrams", 14720, larger);
        Assert.AreEqual(14720, larger);
    }

    [TestMethod]
    public void OnPacketSent_TenDatagrams_FillsTheWindow()
    {
        Diagnostics.Arrange("max datagram size", 1200);
        Diagnostics.Arrange("packets sent", "10 ack-eliciting of 1200 bytes, then 1 not ack-eliciting");
        QuicNewRenoCongestionController controller = new(1200);

        for (ulong packetNumber = 0; packetNumber < 10; packetNumber++)
        {
            controller.OnPacketSent(Packet(packetNumber, 0));
        }

        controller.OnPacketSent(Packet(10, 0, ackEliciting: false));

        Diagnostics.Act("bytes in flight", controller.BytesInFlight);
        Diagnostics.Act("available window", controller.AvailableWindow);
        Diagnostics.Assert("bytes in flight", 12000, controller.BytesInFlight);
        Assert.AreEqual(12000, controller.BytesInFlight);
        Diagnostics.Assert("available window", 0, controller.AvailableWindow);
        Assert.AreEqual(0, controller.AvailableWindow);
    }

    [TestMethod]
    public void Window_GrowthLossAndRecovery_FollowsAppendixB()
    {
        Diagnostics.Arrange("max datagram size", 1200);
        Diagnostics.Arrange("packets sent", "0 to 3 at 0 ms");
        QuicNewRenoCongestionController controller = new(1200);
        QuicRttEstimator rtt = new();
        for (ulong packetNumber = 0; packetNumber < 4; packetNumber++)
        {
            controller.OnPacketSent(Packet(packetNumber, 0));
        }

        // Slow start: every acknowledged byte grows the window.
        controller.OnPacketsAcknowledged([Packet(0, 0), Packet(9, 0, ackEliciting: false)], Ms(10), rtt);
        Diagnostics.Act("window after acknowledgement in slow start", controller.CongestionWindow);
        Diagnostics.Assert("window after acknowledgement in slow start", 13200, controller.CongestionWindow);
        Assert.AreEqual(13200, controller.CongestionWindow);

        // A loss halves it and starts recovery.
        controller.OnPacketsLost([Packet(1, 0)], persistentCongestion: false, Ms(20));
        Diagnostics.Act("window, threshold, recovery start ms after loss", $"{controller.CongestionWindow}, {controller.SlowStartThreshold}, {Text(controller.RecoveryStartTime!.Value)}");
        Diagnostics.Assert("window, threshold, recovery start ms after loss", "6600, 6600, 20", $"{controller.CongestionWindow}, {controller.SlowStartThreshold}, {Text(controller.RecoveryStartTime!.Value)}");
        Assert.AreEqual((6600L, 6600L, Ms(20)), (controller.CongestionWindow, controller.SlowStartThreshold, controller.RecoveryStartTime!.Value));

        // Within recovery a further loss or an acknowledgement of a packet sent before it changes nothing.
        controller.OnPacketsLost([Packet(2, 0)], persistentCongestion: false, Ms(30));
        controller.OnPacketsAcknowledged([Packet(3, 0)], Ms(30), rtt);
        Diagnostics.Act("window inside recovery", controller.CongestionWindow);
        Diagnostics.Assert("window inside recovery", 6600, controller.CongestionWindow);
        Assert.AreEqual(6600, controller.CongestionWindow);
        Diagnostics.Assert("in recovery for a packet sent at 20 ms", true, controller.IsInRecovery(Ms(20)));
        Assert.IsTrue(controller.IsInRecovery(Ms(20)));
        Diagnostics.Assert("in recovery for a packet sent at 21 ms", false, controller.IsInRecovery(Ms(21)));
        Assert.IsFalse(controller.IsInRecovery(Ms(21)));

        // A packet sent after recovery started ends it; congestion avoidance adds 1200 x 1200 / 6600 bytes.
        controller.OnPacketSent(Packet(4, 25));
        controller.OnPacketsAcknowledged([Packet(4, 25)], Ms(40), rtt);
        Diagnostics.Act("window and bytes in flight in congestion avoidance", $"{controller.CongestionWindow}, {controller.BytesInFlight}");
        Diagnostics.Assert("window in congestion avoidance", 6818, controller.CongestionWindow);
        Assert.AreEqual(6818, controller.CongestionWindow);
        Diagnostics.Assert("bytes in flight", 0, controller.BytesInFlight);
        Assert.AreEqual(0, controller.BytesInFlight);

        // A loss of packets sent after the last recovery started opens a new one, whatever order they come in.
        controller.OnPacketsLost([Packet(6, 50), Packet(5, 45)], persistentCongestion: false, Ms(60));
        Diagnostics.Act("window and recovery start ms after second loss", $"{controller.CongestionWindow}, {Text(controller.RecoveryStartTime!.Value)}");
        Diagnostics.Assert("window and recovery start ms after second loss", "3409, 60", $"{controller.CongestionWindow}, {Text(controller.RecoveryStartTime!.Value)}");
        Assert.AreEqual((3409L, Ms(60)), (controller.CongestionWindow, controller.RecoveryStartTime!.Value));

        // The window never falls below two datagrams.
        controller.OnPacketsLost([Packet(7, 70), Packet(8, 80)], persistentCongestion: false, Ms(100));
        Diagnostics.Act("window and threshold after third loss", $"{controller.CongestionWindow}, {controller.SlowStartThreshold}");
        Diagnostics.Assert("window and threshold after third loss", "2400, 1704", $"{controller.CongestionWindow}, {controller.SlowStartThreshold}");
        Assert.AreEqual((2400L, 1704L), (controller.CongestionWindow, controller.SlowStartThreshold));
    }

    [TestMethod]
    public void OnPacketsLost_PersistentCongestion_CollapsesTheWindowAndEndsRecovery()
    {
        Diagnostics.Arrange("max datagram size", 1200);
        Diagnostics.Arrange("lost packets", "0 (in flight) and 1 (not ack-eliciting), persistent congestion at 10 ms");
        QuicNewRenoCongestionController controller = new(1200);
        controller.OnPacketSent(Packet(0, 0));

        controller.OnPacketsLost([Packet(0, 0), Packet(1, 0, ackEliciting: false)], persistentCongestion: true, Ms(10));

        Diagnostics.Act("window", controller.CongestionWindow);
        Diagnostics.Act("slow start threshold", controller.SlowStartThreshold);
        Diagnostics.Act("recovery start time is null", controller.RecoveryStartTime is null);
        Diagnostics.Act("bytes in flight", controller.BytesInFlight);
        Diagnostics.Assert("window", 2400, controller.CongestionWindow);
        Assert.AreEqual(2400, controller.CongestionWindow);
        Diagnostics.Assert("slow start threshold", 6000, controller.SlowStartThreshold);
        Assert.AreEqual(6000, controller.SlowStartThreshold);
        Diagnostics.Assert("recovery start time is null", true, controller.RecoveryStartTime is null);
        Assert.IsNull(controller.RecoveryStartTime);
        Diagnostics.Assert("bytes in flight", 0, controller.BytesInFlight);
        Assert.AreEqual(0, controller.BytesInFlight);
    }

    [TestMethod]
    public void OnPacketsLost_OnlyPacketsNotInFlight_IsNoCongestionEvent()
    {
        Diagnostics.Arrange("max datagram size", 1200);
        Diagnostics.Arrange("lost packets", "0 (not ack-eliciting) at 10 ms");
        QuicNewRenoCongestionController controller = new(1200);

        controller.OnPacketsLost([Packet(0, 0, ackEliciting: false)], persistentCongestion: false, Ms(10));

        Diagnostics.Act("window", controller.CongestionWindow);
        Diagnostics.Act("recovery start time is null", controller.RecoveryStartTime is null);
        Diagnostics.Assert("window", 12000, controller.CongestionWindow);
        Assert.AreEqual(12000, controller.CongestionWindow);
        Diagnostics.Assert("recovery start time is null", true, controller.RecoveryStartTime is null);
        Assert.IsNull(controller.RecoveryStartTime);
    }

    [TestMethod]
    public void RemoveFromBytesInFlight_DiscardedPackets_LeaveTheWindowAlone()
    {
        Diagnostics.Arrange("max datagram size", 1200);
        Diagnostics.Arrange("packets sent", "0 and 1; discarded: 0 and 2 (not ack-eliciting)");
        QuicNewRenoCongestionController controller = new(1200);
        controller.OnPacketSent(Packet(0, 0));
        controller.OnPacketSent(Packet(1, 0));

        controller.RemoveFromBytesInFlight([Packet(0, 0), Packet(2, 0, ackEliciting: false)]);

        Diagnostics.Act("bytes in flight", controller.BytesInFlight);
        Diagnostics.Act("window", controller.CongestionWindow);
        Diagnostics.Assert("bytes in flight", 1200, controller.BytesInFlight);
        Assert.AreEqual(1200, controller.BytesInFlight);
        Diagnostics.Assert("window", 12000, controller.CongestionWindow);
        Assert.AreEqual(12000, controller.CongestionWindow);
    }

    [TestMethod]
    public void Create_EachAlgorithm_ReturnsItsController()
    {
        Diagnostics.Arrange("algorithms", "NewReno, Cubic");

        var newReno = QuicCongestionController.Create(QuicCongestionControlAlgorithm.NewReno, 1200);
        var cubic = QuicCongestionController.Create(QuicCongestionControlAlgorithm.Cubic, 1200);

        Diagnostics.Act("NewReno type", newReno.GetType().Name);
        Diagnostics.Act("Cubic type", cubic.GetType().Name);
        Diagnostics.Assert("NewReno type", nameof(QuicNewRenoCongestionController), newReno.GetType().Name);
        Assert.IsInstanceOfType<QuicNewRenoCongestionController>(QuicCongestionController.Create(QuicCongestionControlAlgorithm.NewReno, 1200));
        Diagnostics.Assert("Cubic type", nameof(QuicCubicCongestionController), cubic.GetType().Name);
        Assert.IsInstanceOfType<QuicCubicCongestionController>(QuicCongestionController.Create(QuicCongestionControlAlgorithm.Cubic, 1200));
    }

    [TestMethod]
    public void Calls_BadArguments_Throw()
    {
        Diagnostics.Arrange("max datagram size", 1200);
        Diagnostics.Arrange("bad calls", "datagram size 1199, and a null argument to each method");
        QuicNewRenoCongestionController controller = new(1200);

        var tooSmall = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QuicNewRenoCongestionController(1199));
        var nullSent = Assert.ThrowsExactly<ArgumentNullException>(() => controller.OnPacketSent(null!));
        var nullAcknowledged = Assert.ThrowsExactly<ArgumentNullException>(() => controller.OnPacketsAcknowledged(null!, TimeSpan.Zero, new QuicRttEstimator()));
        var nullRtt = Assert.ThrowsExactly<ArgumentNullException>(() => controller.OnPacketsAcknowledged([], TimeSpan.Zero, null!));
        var nullLost = Assert.ThrowsExactly<ArgumentNullException>(() => controller.OnPacketsLost(null!, false, TimeSpan.Zero));
        var nullRemoved = Assert.ThrowsExactly<ArgumentNullException>(() => controller.RemoveFromBytesInFlight(null!));

        var actual = string.Join(", ", new Exception[] { tooSmall, nullSent, nullAcknowledged, nullRtt, nullLost, nullRemoved }.Select(exception => exception.GetType().Name));
        Diagnostics.Act("exception types", actual);
        Diagnostics.Assert("exception types", "ArgumentOutOfRangeException, ArgumentNullException x5", actual);
    }

    [TestMethod]
    public void OnPacketsAcknowledged_ApplicationLimitedPacketsInSlowStart_GrowsTheWindowOnlyForTheOthers()
    {
        Diagnostics.Arrange("max datagram size", 1200);
        Diagnostics.Arrange("acknowledged packets", "0 application limited; then 1 application limited and 2");
        QuicNewRenoCongestionController controller = new(1200);

        // RFC 9002 section 7.8: a packet sent while the application limited sending says nothing about the path's capacity.
        controller.OnPacketsAcknowledged([Packet(0, 0) with { IsApplicationLimited = true }], Ms(10), new QuicRttEstimator());
        Diagnostics.Act("window after limited packet", controller.CongestionWindow);
        Diagnostics.Assert("window after limited packet", 12000, controller.CongestionWindow);
        Assert.AreEqual(12000, controller.CongestionWindow);

        controller.OnPacketsAcknowledged([Packet(1, 0) with { IsApplicationLimited = true }, Packet(2, 0)], Ms(20), new QuicRttEstimator());
        Diagnostics.Act("window after one limited and one other packet", controller.CongestionWindow);
        Diagnostics.Assert("window after one limited and one other packet", 13200, controller.CongestionWindow);
        Assert.AreEqual(13200, controller.CongestionWindow);
    }

    [TestMethod]
    public void OnPacketsAcknowledged_ApplicationLimitedPacketInCongestionAvoidance_LeavesTheWindow()
    {
        Diagnostics.Arrange("max datagram size", 1200);
        Diagnostics.Arrange("events", "loss of packet 0 at 10 ms, then acknowledgements of packets 1 (limited) and 2 sent at 20 ms");
        QuicNewRenoCongestionController controller = new(1200);
        controller.OnPacketsLost([Packet(0, 0)], persistentCongestion: false, Ms(10));
        Diagnostics.Assert("window after loss", 6000, controller.CongestionWindow);
        Assert.AreEqual(6000, controller.CongestionWindow);

        controller.OnPacketsAcknowledged([Packet(1, 20) with { IsApplicationLimited = true }], Ms(30), new QuicRttEstimator());
        Diagnostics.Act("window after limited packet", controller.CongestionWindow);
        Diagnostics.Assert("window after limited packet", 6000, controller.CongestionWindow);
        Assert.AreEqual(6000, controller.CongestionWindow);

        controller.OnPacketsAcknowledged([Packet(2, 20)], Ms(30), new QuicRttEstimator());
        Diagnostics.Act("window after other packet", controller.CongestionWindow);
        Diagnostics.Assert("window after other packet", 6240, controller.CongestionWindow);
        Assert.AreEqual(6240, controller.CongestionWindow);
    }

    private static string Text(TimeSpan value) => value.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
}
