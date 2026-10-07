using System.Globalization;
using Curl.Testing;

namespace Curl.Quic;

[TestClass]
public sealed class QuicLossRecoveryTests
{
    private const QuicPacketNumberSpaceId Initial = QuicPacketNumberSpaceId.Initial;

    private const QuicPacketNumberSpaceId Handshake = QuicPacketNumberSpaceId.Handshake;

    private const QuicPacketNumberSpaceId Application = QuicPacketNumberSpaceId.ApplicationData;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void OnAckReceived_ScriptedAcknowledgements_SamplesRttAsAppendixA7()
    {
        Diagnostics.Arrange("sent", "application 0 at 0 ms, application 1 at 10 ms, initial 0 at 200 ms");
        Diagnostics.Arrange("acks", "app 0 delay 5 ms at 5 ms... app 0 at 100 ms; app 1 delay 5 ms at 130 ms; initial 0 delay 50 ms at 300 ms");
        QuicLossRecovery recovery = Recovery();
        Send(recovery, Application, 0, 0);
        Send(recovery, Application, 1, 10);

        recovery.OnAckReceived(Application, Ack(0), Ms(5), Ms(100));
        Diagnostics.Act("smoothed rtt ms after first ack", Text(recovery.Rtt.SmoothedRtt));
        Diagnostics.Assert("smoothed rtt ms after first ack", 100, Text(recovery.Rtt.SmoothedRtt));
        Assert.AreEqual(100, recovery.Rtt.SmoothedRtt.TotalMilliseconds, 0.001);

        // Application data: the 5 ms ACK Delay comes off the 120 ms sample.
        QuicAcknowledgementOutcome outcome = recovery.OnAckReceived(Application, Ack(1), Ms(5), Ms(130));
        Diagnostics.Act("acknowledged packet", outcome.Acknowledged.Single().PacketNumber);
        Diagnostics.Act("smoothed and variation ms after second ack", $"{Text(recovery.Rtt.SmoothedRtt)}, {Text(recovery.Rtt.RttVariation)}");
        Diagnostics.Assert("acknowledged packet", 1UL, outcome.Acknowledged.Single().PacketNumber);
        Assert.AreEqual(1UL, outcome.Acknowledged.Single().PacketNumber);
        Diagnostics.Assert("smoothed rtt ms", 101.875, Text(recovery.Rtt.SmoothedRtt));
        Assert.AreEqual(101.875, recovery.Rtt.SmoothedRtt.TotalMilliseconds, 0.001);
        Diagnostics.Assert("rtt variation ms", 41.25, Text(recovery.Rtt.RttVariation));
        Assert.AreEqual(41.25, recovery.Rtt.RttVariation.TotalMilliseconds, 0.001);

        // Initial: the ACK Delay is ignored, so the 100 ms sample is taken whole.
        Send(recovery, Initial, 0, 200);
        recovery.OnAckReceived(Initial, Ack(0), Ms(50), Ms(300));
        Diagnostics.Act("smoothed and variation ms after initial ack", $"{Text(recovery.Rtt.SmoothedRtt)}, {Text(recovery.Rtt.RttVariation)}");
        Diagnostics.Assert("smoothed rtt ms after initial ack", 101.640625, Text(recovery.Rtt.SmoothedRtt));
        Assert.AreEqual(101.640625, recovery.Rtt.SmoothedRtt.TotalMilliseconds, 0.001);
        Diagnostics.Assert("rtt variation ms after initial ack", 31.40625, Text(recovery.Rtt.RttVariation));
        Assert.AreEqual(31.40625, recovery.Rtt.RttVariation.TotalMilliseconds, 0.001);
    }

    [TestMethod]
    public void OnAckReceived_LargestNotNewlyAcknowledged_TakesNoSample()
    {
        Diagnostics.Arrange("sent", "application 0 and 1 at 0 ms");
        Diagnostics.Arrange("acks", "largest 1 at 50 ms, then largest 1 with first range 1 at 500 ms");
        QuicLossRecovery recovery = Recovery();
        Send(recovery, Application, 0, 0);
        Send(recovery, Application, 1, 0);
        recovery.OnAckReceived(Application, Ack(1), TimeSpan.Zero, Ms(50));

        QuicAcknowledgementOutcome outcome = recovery.OnAckReceived(Application, new QuicAckFrame(1, 0, 1, [], null), TimeSpan.Zero, Ms(500));

        Diagnostics.Act("acknowledged packet", outcome.Acknowledged.Single().PacketNumber);
        Diagnostics.Act("latest rtt ms", Text(recovery.Rtt.LatestRtt));
        Diagnostics.Assert("acknowledged packet", 0UL, outcome.Acknowledged.Single().PacketNumber);
        Assert.AreEqual(0UL, outcome.Acknowledged.Single().PacketNumber);
        Diagnostics.Assert("latest rtt ms", 50, Text(recovery.Rtt.LatestRtt));
        Assert.AreEqual(Ms(50), recovery.Rtt.LatestRtt);
    }

    [TestMethod]
    public void OnAckReceived_OnlyAnAckOnlyPacketAcknowledged_TakesNoSample()
    {
        Diagnostics.Arrange("sent", "application 0 at 0 ms, not ack-eliciting");
        QuicLossRecovery recovery = Recovery();
        Send(recovery, Application, 0, 0, ackEliciting: false);

        QuicAcknowledgementOutcome outcome = recovery.OnAckReceived(Application, Ack(0), TimeSpan.Zero, Ms(50));

        Diagnostics.Act("acknowledged count", outcome.Acknowledged.Count);
        Diagnostics.Act("rtt has sample", recovery.Rtt.HasSample);
        Diagnostics.Assert("acknowledged count", 1, outcome.Acknowledged.Count);
        Assert.HasCount(1, outcome.Acknowledged);
        Diagnostics.Assert("rtt has sample", false, recovery.Rtt.HasSample);
        Assert.IsFalse(recovery.Rtt.HasSample);
    }

    [TestMethod]
    public void OnAckReceived_NothingNew_ReturnsNothing()
    {
        Diagnostics.Arrange("sent", "application 0 at 0 ms");
        Diagnostics.Arrange("acks", "largest 0 at 10 ms, again at 20 ms");
        QuicLossRecovery recovery = Recovery();
        Send(recovery, Application, 0, 0);
        recovery.OnAckReceived(Application, Ack(0), TimeSpan.Zero, Ms(10));

        QuicAcknowledgementOutcome outcome = recovery.OnAckReceived(Application, Ack(0), TimeSpan.Zero, Ms(20));

        Diagnostics.Act("acknowledged count", outcome.Acknowledged.Count);
        Diagnostics.Act("lost count", outcome.Lost.Count);
        Diagnostics.Assert("acknowledged count", 0, outcome.Acknowledged.Count);
        Assert.IsEmpty(outcome.Acknowledged);
        Diagnostics.Assert("lost count", 0, outcome.Lost.Count);
        Assert.IsEmpty(outcome.Lost);
    }

    [TestMethod]
    public void OnAckReceived_ThreeLaterPacketsAcknowledged_DeclaresLossByPacketThresholdAndArmsTheTimeThreshold()
    {
        Diagnostics.Arrange("sent", "application 0 to 3 at 0 ms");
        Diagnostics.Arrange("ack", "largest 3 at 100 ms");
        QuicLossRecovery recovery = Recovery();
        for (ulong packetNumber = 0; packetNumber < 4; packetNumber++)
        {
            Send(recovery, Application, packetNumber, 0);
        }

        // Packet 3 acknowledged at 100 ms: 3 >= 0 + 3 loses packet 0; packets 1 and 2 wait for
        // 9/8 x 100 = 112.5 ms after they were sent (Appendix A.10).
        QuicAcknowledgementOutcome outcome = recovery.OnAckReceived(Application, Ack(3), TimeSpan.Zero, Ms(100));

        Diagnostics.Act("lost packet", outcome.Lost.Single().PacketNumber);
        Diagnostics.Act("loss detection timer ms", Text(recovery.LossDetectionTimer));
        Diagnostics.Act("unacknowledged packets", Numbers(recovery.GetUnacknowledgedPackets(Application).Select(packet => packet.PacketNumber)));
        Diagnostics.Assert("lost packet", 0UL, outcome.Lost.Single().PacketNumber);
        Assert.AreEqual(0UL, outcome.Lost.Single().PacketNumber);
        Diagnostics.Assert("loss detection timer ms", 112.5, Text(recovery.LossDetectionTimer));
        Assert.AreEqual(Ms(112.5), recovery.LossDetectionTimer);
        Diagnostics.Assert("unacknowledged packets", "1 2", Numbers(recovery.GetUnacknowledgedPackets(Application).Select(packet => packet.PacketNumber)));
        CollectionAssert.AreEqual(new ulong[] { 1, 2 }, recovery.GetUnacknowledgedPackets(Application).Select(packet => packet.PacketNumber).ToArray());

        QuicLossDetectionTimeoutOutcome timeout = recovery.OnLossDetectionTimeout(Ms(112.5));

        Diagnostics.Act("timeout space", timeout.Space);
        Diagnostics.Act("probe required", timeout.ProbeRequired);
        Diagnostics.Act("timeout lost packets", Numbers(timeout.Lost.Select(packet => packet.PacketNumber)));
        Diagnostics.Assert("timeout space", Application, timeout.Space);
        Assert.AreEqual(Application, timeout.Space);
        Diagnostics.Assert("probe required", false, timeout.ProbeRequired);
        Assert.IsFalse(timeout.ProbeRequired);
        Diagnostics.Assert("timeout lost packets", "1 2", Numbers(timeout.Lost.Select(packet => packet.PacketNumber)));
        CollectionAssert.AreEqual(new ulong[] { 1, 2 }, timeout.Lost.Select(packet => packet.PacketNumber).ToArray());
        Diagnostics.Assert("unacknowledged count after timeout", 0, recovery.GetUnacknowledgedPackets(Application).Count);
        Assert.IsEmpty(recovery.GetUnacknowledgedPackets(Application));
        Diagnostics.Assert("probe timeout count", 0, recovery.ProbeTimeoutCount);
        Assert.AreEqual(0, recovery.ProbeTimeoutCount);
    }

    [TestMethod]
    public void OnAckReceived_PacketSentLongerAgoThanTheLossDelay_DeclaresLossByTimeThreshold()
    {
        Diagnostics.Arrange("sent", "application 0 at 0 ms, application 1 at 200 ms");
        Diagnostics.Arrange("ack", "largest 1 at 210 ms");
        QuicLossRecovery recovery = Recovery();
        Send(recovery, Application, 0, 0);
        Send(recovery, Application, 1, 200);

        // Sample 10 ms: loss delay 11.25 ms, so packet 0, sent 210 ms ago, is lost with only one packet after it.
        QuicAcknowledgementOutcome outcome = recovery.OnAckReceived(Application, Ack(1), TimeSpan.Zero, Ms(210));

        Diagnostics.Act("lost packet", outcome.Lost.Single().PacketNumber);
        Diagnostics.Assert("lost packet", 0UL, outcome.Lost.Single().PacketNumber);
        Assert.AreEqual(0UL, outcome.Lost.Single().PacketNumber);
    }

    [TestMethod]
    public void OnLossDetectionTimeout_TwoSpacesWithLossTimes_TakesTheEarlier()
    {
        Diagnostics.Arrange("sent", "initial 0 and 1 at 0 ms, handshake 0 at 50 ms, handshake 1 at 60 ms");
        Diagnostics.Arrange("acks", "largest 1 in both spaces at 100 ms");
        QuicLossRecovery recovery = Recovery();
        Send(recovery, Initial, 0, 0);
        Send(recovery, Initial, 1, 0);
        Send(recovery, Handshake, 0, 50);
        Send(recovery, Handshake, 1, 60);

        // Initial: sample 100 ms, packet 0 lost at 0 + 112.5. Handshake: smoothed 92.5 ms, loss delay 104.0625 ms, packet 0 lost at 154.0625.
        recovery.OnAckReceived(Initial, Ack(1), TimeSpan.Zero, Ms(100));
        recovery.OnAckReceived(Handshake, Ack(1), TimeSpan.Zero, Ms(100));

        Diagnostics.Act("loss detection timer ms", Text(recovery.LossDetectionTimer));
        Diagnostics.Assert("loss detection timer ms", 112.5, Text(recovery.LossDetectionTimer));
        Assert.AreEqual(Ms(112.5), recovery.LossDetectionTimer);
        var first = recovery.OnLossDetectionTimeout(Ms(112.5)).Space;
        Diagnostics.Act("first timeout space", first);
        Diagnostics.Assert("first timeout space", Initial, first);
        Assert.AreEqual(Initial, first);
        Diagnostics.Act("next loss detection timer ms", Text(recovery.LossDetectionTimer));
        Diagnostics.Assert("next loss detection timer ms (within 0.001)", 154.0625, Text(recovery.LossDetectionTimer));
        Assert.AreEqual(154.0625, recovery.LossDetectionTimer!.Value.TotalMilliseconds, 0.001);
        var second = recovery.OnLossDetectionTimeout(recovery.LossDetectionTimer.Value).Space;
        Diagnostics.Act("second timeout space", second);
        Diagnostics.Assert("second timeout space", Handshake, second);
        Assert.AreEqual(Handshake, second);
    }

    [TestMethod]
    public void OnLossDetectionTimeout_NoAcknowledgement_ProbesWithExponentialBackoff()
    {
        Diagnostics.Arrange("sent", "initial 0 at 0 ms, then a probe each time the timer fires (3 probes)");
        QuicLossRecovery recovery = Recovery();
        Send(recovery, Initial, 0, 0);
        List<TimeSpan?> timers = [recovery.LossDetectionTimer];

        // Each probe goes when the timer fires; the next timer is the probe's send time plus twice the last probe timeout.
        for (ulong probe = 1; probe <= 3; probe++)
        {
            TimeSpan firesAt = recovery.LossDetectionTimer!.Value;
            QuicLossDetectionTimeoutOutcome outcome = recovery.OnLossDetectionTimeout(firesAt);
            Diagnostics.Act($"probe {probe.ToString(CultureInfo.InvariantCulture)} fired at ms, probe required, space", $"{Text(firesAt)}, {outcome.ProbeRequired}, {outcome.Space}");
            Diagnostics.Assert($"probe {probe.ToString(CultureInfo.InvariantCulture)} probe required", true, outcome.ProbeRequired);
            Assert.IsTrue(outcome.ProbeRequired);
            Diagnostics.Assert($"probe {probe.ToString(CultureInfo.InvariantCulture)} space", Initial, outcome.Space);
            Assert.AreEqual(Initial, outcome.Space);
            Diagnostics.Assert($"probe {probe.ToString(CultureInfo.InvariantCulture)} lost count", 0, outcome.Lost.Count);
            Assert.IsEmpty(outcome.Lost);
            Send(recovery, Initial, probe, firesAt.TotalMilliseconds);
            timers.Add(recovery.LossDetectionTimer);
        }

        var actual = string.Join(", ", timers.Select(timer => Text(timer)));
        Diagnostics.Act("timers ms", actual);
        Diagnostics.Assert("timers ms", "999, 2997, 6993, 14985", actual);
        CollectionAssert.AreEqual(new TimeSpan?[] { Ms(999), Ms(2997), Ms(6993), Ms(14985) }, timers);
        Diagnostics.Assert("probe timeout count", 3, recovery.ProbeTimeoutCount);
        Assert.AreEqual(3, recovery.ProbeTimeoutCount);
    }

    [TestMethod]
    public void LossDetectionTimer_InitialAndHandshakeInFlight_ArmsForTheEarlierLastPacket()
    {
        Diagnostics.Arrange("sent", "initial 0 at 0 ms, handshake 0 at 10 ms");
        QuicLossRecovery recovery = Recovery();
        Send(recovery, Initial, 0, 0);
        Send(recovery, Handshake, 0, 10);

        Diagnostics.Act("loss detection timer ms", Text(recovery.LossDetectionTimer));
        Diagnostics.Assert("loss detection timer ms", 999, Text(recovery.LossDetectionTimer));
        Assert.AreEqual(Ms(999), recovery.LossDetectionTimer);
        var space = recovery.OnLossDetectionTimeout(Ms(999)).Space;
        Diagnostics.Act("timeout space", space);
        Diagnostics.Assert("timeout space", Initial, space);
        Assert.AreEqual(Initial, space);
    }

    [TestMethod]
    public void LossDetectionTimer_OnlyApplicationDataInFlight_WaitsForConfirmationThenAddsMaxAckDelay()
    {
        Diagnostics.Arrange("max ack delay ms", 20);
        Diagnostics.Arrange("sent", "application 0 at 0 ms; handshake confirmed at 5 ms");
        QuicLossRecovery recovery = Recovery();
        recovery.MaxAckDelay = Ms(20);
        Send(recovery, Application, 0, 0);

        Diagnostics.Assert("loss detection timer before confirmation is null", true, recovery.LossDetectionTimer is null);
        Assert.IsNull(recovery.LossDetectionTimer);

        recovery.ConfirmHandshake(Ms(5));

        Diagnostics.Act("handshake confirmed", recovery.IsHandshakeConfirmed);
        Diagnostics.Act("loss detection timer ms", Text(recovery.LossDetectionTimer));
        Diagnostics.Assert("handshake confirmed", true, recovery.IsHandshakeConfirmed);
        Assert.IsTrue(recovery.IsHandshakeConfirmed);
        Diagnostics.Assert("loss detection timer ms", 1019, Text(recovery.LossDetectionTimer));
        Assert.AreEqual(Ms(1019), recovery.LossDetectionTimer);
        var space = recovery.OnLossDetectionTimeout(Ms(1019)).Space;
        Diagnostics.Act("timeout space", space);
        Diagnostics.Assert("timeout space", Application, space);
        Assert.AreEqual(Application, space);
    }

    [TestMethod]
    public void LossDetectionTimer_NothingInFlightBeforeTheServerValidatedTheClient_ArmsTheAntiDeadlockProbe()
    {
        Diagnostics.Arrange("sent", "initial 0 at 0 ms (probe at 999 ms), initial 1 at 999 ms");
        Diagnostics.Arrange("ack", "initial largest 1 first range 1 at 1099 ms");
        QuicLossRecovery recovery = Recovery();
        Send(recovery, Initial, 0, 0);
        recovery.OnLossDetectionTimeout(Ms(999));
        Send(recovery, Initial, 1, 999);

        // An Initial acknowledgement does not validate the client, so the probe count stays and the timer runs from now (section 6.2.2.1).
        recovery.OnAckReceived(Initial, new QuicAckFrame(1, 0, 1, [], null), TimeSpan.Zero, Ms(1099));
        Diagnostics.Assert("probe timeout count", 1, recovery.ProbeTimeoutCount);
        Assert.AreEqual(1, recovery.ProbeTimeoutCount);
        TimeSpan expected = Ms(1099) + (recovery.Rtt.ProbeTimeout * 2);
        Diagnostics.Act("loss detection timer ms", Text(recovery.LossDetectionTimer));
        Diagnostics.Assert("loss detection timer ms", Text(expected), Text(recovery.LossDetectionTimer));
        Assert.AreEqual(expected, recovery.LossDetectionTimer);

        var initialSpace = recovery.OnLossDetectionTimeout(expected).Space;
        Diagnostics.Act("timeout space without handshake keys", initialSpace);
        Diagnostics.Assert("timeout space without handshake keys", Initial, initialSpace);
        Assert.AreEqual(Initial, initialSpace);
        recovery.HasHandshakeKeys = true;
        var handshakeSpace = recovery.OnLossDetectionTimeout(expected).Space;
        Diagnostics.Act("timeout space with handshake keys", handshakeSpace);
        Diagnostics.Assert("timeout space with handshake keys", Handshake, handshakeSpace);
        Assert.AreEqual(Handshake, handshakeSpace);
    }

    [TestMethod]
    public void OnAckReceived_HandshakeAcknowledged_ValidatesTheClientResetsTheProbeCountAndDisarms()
    {
        Diagnostics.Arrange("sent", "handshake 0 at 0 ms, probe at 999 ms");
        Diagnostics.Arrange("ack", "handshake largest 0 at 1000 ms");
        QuicLossRecovery recovery = Recovery();
        Send(recovery, Handshake, 0, 0);
        recovery.OnLossDetectionTimeout(Ms(999));

        recovery.OnAckReceived(Handshake, Ack(0), TimeSpan.Zero, Ms(1000));

        Diagnostics.Act("probe timeout count", recovery.ProbeTimeoutCount);
        Diagnostics.Act("loss detection timer is null", recovery.LossDetectionTimer is null);
        Diagnostics.Assert("probe timeout count", 0, recovery.ProbeTimeoutCount);
        Assert.AreEqual(0, recovery.ProbeTimeoutCount);
        Diagnostics.Assert("loss detection timer is null", true, recovery.LossDetectionTimer is null);
        Assert.IsNull(recovery.LossDetectionTimer);
    }

    [TestMethod]
    public void DiscardSpace_PacketsInFlight_TakesThemOutWithoutLossAndResetsTheProbeCount()
    {
        Diagnostics.Arrange("sent", "initial 0 and handshake 0 at 0 ms, probe at 999 ms");
        Diagnostics.Arrange("discarded space", "Initial at 999 ms");
        QuicLossRecovery recovery = Recovery();
        Send(recovery, Initial, 0, 0);
        Send(recovery, Handshake, 0, 0);
        recovery.OnLossDetectionTimeout(Ms(999));

        recovery.DiscardSpace(Initial, Ms(999));

        Diagnostics.Act("bytes in flight", recovery.Congestion.BytesInFlight);
        Diagnostics.Act("probe timeout count", recovery.ProbeTimeoutCount);
        Diagnostics.Act("initial unacknowledged count", recovery.GetUnacknowledgedPackets(Initial).Count);
        Diagnostics.Act("congestion window", recovery.Congestion.CongestionWindow);
        Diagnostics.Act("loss detection timer ms", Text(recovery.LossDetectionTimer));
        Diagnostics.Assert("bytes in flight", 1200, recovery.Congestion.BytesInFlight);
        Assert.AreEqual(1200, recovery.Congestion.BytesInFlight);
        Diagnostics.Assert("probe timeout count", 0, recovery.ProbeTimeoutCount);
        Assert.AreEqual(0, recovery.ProbeTimeoutCount);
        Diagnostics.Assert("initial unacknowledged count", 0, recovery.GetUnacknowledgedPackets(Initial).Count);
        Assert.IsEmpty(recovery.GetUnacknowledgedPackets(Initial));
        Diagnostics.Assert("congestion window", 12000, recovery.Congestion.CongestionWindow);
        Assert.AreEqual(12000, recovery.Congestion.CongestionWindow);
        Diagnostics.Assert("loss detection timer ms", 999, Text(recovery.LossDetectionTimer));
        Assert.AreEqual(Ms(999), recovery.LossDetectionTimer);
    }

    [TestMethod]
    public void OnAckReceived_LostRunSpanningThePersistentCongestionDuration_CollapsesTheWindow()
    {
        Diagnostics.Arrange("scenario", "persistent congestion, middle packet not acknowledged");
        QuicLossRecovery recovery = PersistentCongestionScenario(acknowledgeMiddle: false, out QuicAcknowledgementOutcome outcome);

        Diagnostics.Act("lost packets", Numbers(outcome.Lost.Select(packet => packet.PacketNumber)));
        Diagnostics.Act("congestion window", recovery.Congestion.CongestionWindow);
        Diagnostics.Act("recovery start time is null", recovery.Congestion.RecoveryStartTime is null);
        Diagnostics.Assert("lost packets", "1 2 3 4 5", Numbers(outcome.Lost.Select(packet => packet.PacketNumber)));
        CollectionAssert.AreEqual(new ulong[] { 1, 2, 3, 4, 5 }, outcome.Lost.Select(packet => packet.PacketNumber).ToArray());
        // The window collapses to two datagrams, then packet 6, sent after recovery ended, grows it in slow start.
        Diagnostics.Assert("congestion window", recovery.Congestion.MinimumWindow + 1200, recovery.Congestion.CongestionWindow);
        Assert.AreEqual(recovery.Congestion.MinimumWindow + 1200, recovery.Congestion.CongestionWindow);
        Diagnostics.Assert("recovery start time is null", true, recovery.Congestion.RecoveryStartTime is null);
        Assert.IsNull(recovery.Congestion.RecoveryStartTime);
    }

    [TestMethod]
    public void OnAckReceived_LostRunBrokenByAnAcknowledgement_IsNotPersistentCongestion()
    {
        Diagnostics.Arrange("scenario", "persistent congestion, middle packet 3 acknowledged");
        QuicLossRecovery recovery = PersistentCongestionScenario(acknowledgeMiddle: true, out QuicAcknowledgementOutcome outcome);

        Diagnostics.Act("lost packets", Numbers(outcome.Lost.Select(packet => packet.PacketNumber)));
        Diagnostics.Act("congestion window", recovery.Congestion.CongestionWindow);
        Diagnostics.Act("recovery start time ms", Text(recovery.Congestion.RecoveryStartTime));
        Diagnostics.Assert("lost packets", "1 2 4 5", Numbers(outcome.Lost.Select(packet => packet.PacketNumber)));
        CollectionAssert.AreEqual(new ulong[] { 1, 2, 4, 5 }, outcome.Lost.Select(packet => packet.PacketNumber).ToArray());
        Diagnostics.Assert("congestion window", 6600, recovery.Congestion.CongestionWindow);
        Assert.AreEqual(6600, recovery.Congestion.CongestionWindow);
        Diagnostics.Assert("recovery start time ms", 320, Text(recovery.Congestion.RecoveryStartTime));
        Assert.AreEqual(Ms(320), recovery.Congestion.RecoveryStartTime);
    }

    [TestMethod]
    public void OnAckReceived_LossBeforeAnyRttSample_IsNotPersistentCongestion()
    {
        Diagnostics.Arrange("sent", "application 0 at 0 ms, 1 at 10000 ms, 2 and 3 at 10000 ms not ack-eliciting");
        Diagnostics.Arrange("ack", "largest 3 at 10010 ms");
        QuicLossRecovery recovery = Recovery();
        Send(recovery, Application, 0, 0);
        Send(recovery, Application, 1, 10000);
        Send(recovery, Application, 2, 10000, ackEliciting: false);
        Send(recovery, Application, 3, 10000, ackEliciting: false);

        QuicAcknowledgementOutcome outcome = recovery.OnAckReceived(Application, Ack(3), TimeSpan.Zero, Ms(10010));

        Diagnostics.Act("lost packet", outcome.Lost.Single().PacketNumber);
        Diagnostics.Act("rtt has sample", recovery.Rtt.HasSample);
        Diagnostics.Act("congestion window", recovery.Congestion.CongestionWindow);
        Diagnostics.Assert("lost packet", 0UL, outcome.Lost.Single().PacketNumber);
        Assert.AreEqual(0UL, outcome.Lost.Single().PacketNumber);
        Diagnostics.Assert("rtt has sample", false, recovery.Rtt.HasSample);
        Assert.IsFalse(recovery.Rtt.HasSample);
        Diagnostics.Assert("congestion window", 6000, recovery.Congestion.CongestionWindow);
        Assert.AreEqual(6000, recovery.Congestion.CongestionWindow);
    }

    [TestMethod]
    public void Constructor_And_Calls_RejectNull()
    {
        Diagnostics.Arrange("null arguments", "constructor controller, OnPacketSent packet, OnAckReceived frame");
        var constructor = Assert.ThrowsExactly<ArgumentNullException>(() => new QuicLossRecovery(null!));
        QuicLossRecovery recovery = Recovery();
        var sent = Assert.ThrowsExactly<ArgumentNullException>(() => recovery.OnPacketSent(Initial, null!));
        var ack = Assert.ThrowsExactly<ArgumentNullException>(() => recovery.OnAckReceived(Initial, null!, TimeSpan.Zero, TimeSpan.Zero));

        var actual = $"{constructor.ParamName}, {sent.ParamName}, {ack.ParamName}";
        Diagnostics.Act("exception parameter names", actual);
        Diagnostics.Assert("exception types", "ArgumentNullException x3", $"{constructor.GetType().Name}, {sent.GetType().Name}, {ack.GetType().Name}");
    }

    internal static TimeSpan Ms(double milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    internal static QuicAckFrame Ack(ulong packetNumber) => new(packetNumber, 0, 0, [], null);

    internal static QuicSentPacket Packet(ulong packetNumber, double sentAtMilliseconds, bool ackEliciting = true, int bytes = 1200) =>
        new(packetNumber, Ms(sentAtMilliseconds), ackEliciting, ackEliciting, bytes, ackEliciting ? [new QuicPingFrame()] : [new QuicAckFrame(0, 0, 0, [], null)]);

    private static string Text(TimeSpan? value) => value is { } time ? time.TotalMilliseconds.ToString(CultureInfo.InvariantCulture) : "none";

    private static string Numbers(IEnumerable<ulong> values) => string.Join(" ", values.Select(value => value.ToString(CultureInfo.InvariantCulture)));

    private static QuicLossRecovery Recovery() => new(new QuicNewRenoCongestionController(1200));

    private static void Send(QuicLossRecovery recovery, QuicPacketNumberSpaceId space, ulong packetNumber, double sentAtMilliseconds, bool ackEliciting = true) =>
        recovery.OnPacketSent(space, Packet(packetNumber, sentAtMilliseconds, ackEliciting));

    // Packet 0 at 0 ms, acknowledged at 10 ms, is the first RTT sample (smoothed 10 ms, persistent
    // congestion duration (10 + 20 + 25) x 3 = 165 ms, 150 ms after the second sample). Packet 1
    // went before that sample; packets 2 to 5 span 20 to 300 ms, packet 3 carrying only an ACK;
    // packet 6 is acknowledged at 320 ms, with packet 3 too when acknowledgeMiddle is set.
    private static QuicLossRecovery PersistentCongestionScenario(bool acknowledgeMiddle, out QuicAcknowledgementOutcome outcome)
    {
        QuicLossRecovery recovery = Recovery();
        Send(recovery, Application, 0, 0);
        Send(recovery, Application, 1, 5);
        recovery.OnAckReceived(Application, Ack(0), TimeSpan.Zero, Ms(10));
        Send(recovery, Application, 2, 20);
        Send(recovery, Application, 3, 100, ackEliciting: false);
        Send(recovery, Application, 4, 200);
        Send(recovery, Application, 5, 300);
        Send(recovery, Application, 6, 310);
        QuicAckFrame ack = acknowledgeMiddle ? new QuicAckFrame(6, 0, 0, [new QuicAckRange(1, 0)], null) : Ack(6);
        outcome = recovery.OnAckReceived(Application, ack, TimeSpan.Zero, Ms(320));
        return recovery;
    }
}
