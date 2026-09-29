namespace Curl.Quic;

[TestClass]
public sealed class QuicLossRecoveryTests
{
    private const QuicPacketNumberSpaceId Initial = QuicPacketNumberSpaceId.Initial;

    private const QuicPacketNumberSpaceId Handshake = QuicPacketNumberSpaceId.Handshake;

    private const QuicPacketNumberSpaceId Application = QuicPacketNumberSpaceId.ApplicationData;

    [TestMethod]
    public void OnAckReceived_ScriptedAcknowledgements_SamplesRttAsAppendixA7()
    {
        QuicLossRecovery recovery = Recovery();
        Send(recovery, Application, 0, 0);
        Send(recovery, Application, 1, 10);

        recovery.OnAckReceived(Application, Ack(0), Ms(5), Ms(100));
        Assert.AreEqual(100, recovery.Rtt.SmoothedRtt.TotalMilliseconds, 0.001);

        // Application data: the 5 ms ACK Delay comes off the 120 ms sample.
        QuicAcknowledgementOutcome outcome = recovery.OnAckReceived(Application, Ack(1), Ms(5), Ms(130));
        Assert.AreEqual(1UL, outcome.Acknowledged.Single().PacketNumber);
        Assert.AreEqual(101.875, recovery.Rtt.SmoothedRtt.TotalMilliseconds, 0.001);
        Assert.AreEqual(41.25, recovery.Rtt.RttVariation.TotalMilliseconds, 0.001);

        // Initial: the ACK Delay is ignored, so the 100 ms sample is taken whole.
        Send(recovery, Initial, 0, 200);
        recovery.OnAckReceived(Initial, Ack(0), Ms(50), Ms(300));
        Assert.AreEqual(101.640625, recovery.Rtt.SmoothedRtt.TotalMilliseconds, 0.001);
        Assert.AreEqual(31.40625, recovery.Rtt.RttVariation.TotalMilliseconds, 0.001);
    }

    [TestMethod]
    public void OnAckReceived_LargestNotNewlyAcknowledged_TakesNoSample()
    {
        QuicLossRecovery recovery = Recovery();
        Send(recovery, Application, 0, 0);
        Send(recovery, Application, 1, 0);
        recovery.OnAckReceived(Application, Ack(1), TimeSpan.Zero, Ms(50));

        QuicAcknowledgementOutcome outcome = recovery.OnAckReceived(Application, new QuicAckFrame(1, 0, 1, [], null), TimeSpan.Zero, Ms(500));

        Assert.AreEqual(0UL, outcome.Acknowledged.Single().PacketNumber);
        Assert.AreEqual(Ms(50), recovery.Rtt.LatestRtt);
    }

    [TestMethod]
    public void OnAckReceived_OnlyAnAckOnlyPacketAcknowledged_TakesNoSample()
    {
        QuicLossRecovery recovery = Recovery();
        Send(recovery, Application, 0, 0, ackEliciting: false);

        QuicAcknowledgementOutcome outcome = recovery.OnAckReceived(Application, Ack(0), TimeSpan.Zero, Ms(50));

        Assert.HasCount(1, outcome.Acknowledged);
        Assert.IsFalse(recovery.Rtt.HasSample);
    }

    [TestMethod]
    public void OnAckReceived_NothingNew_ReturnsNothing()
    {
        QuicLossRecovery recovery = Recovery();
        Send(recovery, Application, 0, 0);
        recovery.OnAckReceived(Application, Ack(0), TimeSpan.Zero, Ms(10));

        QuicAcknowledgementOutcome outcome = recovery.OnAckReceived(Application, Ack(0), TimeSpan.Zero, Ms(20));

        Assert.IsEmpty(outcome.Acknowledged);
        Assert.IsEmpty(outcome.Lost);
    }

    [TestMethod]
    public void OnAckReceived_ThreeLaterPacketsAcknowledged_DeclaresLossByPacketThresholdAndArmsTheTimeThreshold()
    {
        QuicLossRecovery recovery = Recovery();
        for (ulong packetNumber = 0; packetNumber < 4; packetNumber++)
        {
            Send(recovery, Application, packetNumber, 0);
        }

        // Packet 3 acknowledged at 100 ms: 3 >= 0 + 3 loses packet 0; packets 1 and 2 wait for
        // 9/8 x 100 = 112.5 ms after they were sent (Appendix A.10).
        QuicAcknowledgementOutcome outcome = recovery.OnAckReceived(Application, Ack(3), TimeSpan.Zero, Ms(100));

        Assert.AreEqual(0UL, outcome.Lost.Single().PacketNumber);
        Assert.AreEqual(Ms(112.5), recovery.LossDetectionTimer);
        CollectionAssert.AreEqual(new ulong[] { 1, 2 }, recovery.GetUnacknowledgedPackets(Application).Select(packet => packet.PacketNumber).ToArray());

        QuicLossDetectionTimeoutOutcome timeout = recovery.OnLossDetectionTimeout(Ms(112.5));

        Assert.AreEqual(Application, timeout.Space);
        Assert.IsFalse(timeout.ProbeRequired);
        CollectionAssert.AreEqual(new ulong[] { 1, 2 }, timeout.Lost.Select(packet => packet.PacketNumber).ToArray());
        Assert.IsEmpty(recovery.GetUnacknowledgedPackets(Application));
        Assert.AreEqual(0, recovery.ProbeTimeoutCount);
    }

    [TestMethod]
    public void OnAckReceived_PacketSentLongerAgoThanTheLossDelay_DeclaresLossByTimeThreshold()
    {
        QuicLossRecovery recovery = Recovery();
        Send(recovery, Application, 0, 0);
        Send(recovery, Application, 1, 200);

        // Sample 10 ms: loss delay 11.25 ms, so packet 0, sent 210 ms ago, is lost with only one packet after it.
        QuicAcknowledgementOutcome outcome = recovery.OnAckReceived(Application, Ack(1), TimeSpan.Zero, Ms(210));

        Assert.AreEqual(0UL, outcome.Lost.Single().PacketNumber);
    }

    [TestMethod]
    public void OnLossDetectionTimeout_TwoSpacesWithLossTimes_TakesTheEarlier()
    {
        QuicLossRecovery recovery = Recovery();
        Send(recovery, Initial, 0, 0);
        Send(recovery, Initial, 1, 0);
        Send(recovery, Handshake, 0, 50);
        Send(recovery, Handshake, 1, 60);

        // Initial: sample 100 ms, packet 0 lost at 0 + 112.5. Handshake: smoothed 92.5 ms, loss delay 104.0625 ms, packet 0 lost at 154.0625.
        recovery.OnAckReceived(Initial, Ack(1), TimeSpan.Zero, Ms(100));
        recovery.OnAckReceived(Handshake, Ack(1), TimeSpan.Zero, Ms(100));

        Assert.AreEqual(Ms(112.5), recovery.LossDetectionTimer);
        Assert.AreEqual(Initial, recovery.OnLossDetectionTimeout(Ms(112.5)).Space);
        Assert.AreEqual(154.0625, recovery.LossDetectionTimer!.Value.TotalMilliseconds, 0.001);
        Assert.AreEqual(Handshake, recovery.OnLossDetectionTimeout(recovery.LossDetectionTimer.Value).Space);
    }

    [TestMethod]
    public void OnLossDetectionTimeout_NoAcknowledgement_ProbesWithExponentialBackoff()
    {
        QuicLossRecovery recovery = Recovery();
        Send(recovery, Initial, 0, 0);
        List<TimeSpan?> timers = [recovery.LossDetectionTimer];

        // Each probe goes when the timer fires; the next timer is the probe's send time plus twice the last probe timeout.
        for (ulong probe = 1; probe <= 3; probe++)
        {
            TimeSpan firesAt = recovery.LossDetectionTimer!.Value;
            QuicLossDetectionTimeoutOutcome outcome = recovery.OnLossDetectionTimeout(firesAt);
            Assert.IsTrue(outcome.ProbeRequired);
            Assert.AreEqual(Initial, outcome.Space);
            Assert.IsEmpty(outcome.Lost);
            Send(recovery, Initial, probe, firesAt.TotalMilliseconds);
            timers.Add(recovery.LossDetectionTimer);
        }

        CollectionAssert.AreEqual(new TimeSpan?[] { Ms(999), Ms(2997), Ms(6993), Ms(14985) }, timers);
        Assert.AreEqual(3, recovery.ProbeTimeoutCount);
    }

    [TestMethod]
    public void LossDetectionTimer_InitialAndHandshakeInFlight_ArmsForTheEarlierLastPacket()
    {
        QuicLossRecovery recovery = Recovery();
        Send(recovery, Initial, 0, 0);
        Send(recovery, Handshake, 0, 10);

        Assert.AreEqual(Ms(999), recovery.LossDetectionTimer);
        Assert.AreEqual(Initial, recovery.OnLossDetectionTimeout(Ms(999)).Space);
    }

    [TestMethod]
    public void LossDetectionTimer_OnlyApplicationDataInFlight_WaitsForConfirmationThenAddsMaxAckDelay()
    {
        QuicLossRecovery recovery = Recovery();
        recovery.MaxAckDelay = Ms(20);
        Send(recovery, Application, 0, 0);

        Assert.IsNull(recovery.LossDetectionTimer);

        recovery.ConfirmHandshake(Ms(5));

        Assert.IsTrue(recovery.IsHandshakeConfirmed);
        Assert.AreEqual(Ms(1019), recovery.LossDetectionTimer);
        Assert.AreEqual(Application, recovery.OnLossDetectionTimeout(Ms(1019)).Space);
    }

    [TestMethod]
    public void LossDetectionTimer_NothingInFlightBeforeTheServerValidatedTheClient_ArmsTheAntiDeadlockProbe()
    {
        QuicLossRecovery recovery = Recovery();
        Send(recovery, Initial, 0, 0);
        recovery.OnLossDetectionTimeout(Ms(999));
        Send(recovery, Initial, 1, 999);

        // An Initial acknowledgement does not validate the client, so the probe count stays and the timer runs from now (section 6.2.2.1).
        recovery.OnAckReceived(Initial, new QuicAckFrame(1, 0, 1, [], null), TimeSpan.Zero, Ms(1099));
        Assert.AreEqual(1, recovery.ProbeTimeoutCount);
        TimeSpan expected = Ms(1099) + (recovery.Rtt.ProbeTimeout * 2);
        Assert.AreEqual(expected, recovery.LossDetectionTimer);

        Assert.AreEqual(Initial, recovery.OnLossDetectionTimeout(expected).Space);
        recovery.HasHandshakeKeys = true;
        Assert.AreEqual(Handshake, recovery.OnLossDetectionTimeout(expected).Space);
    }

    [TestMethod]
    public void OnAckReceived_HandshakeAcknowledged_ValidatesTheClientResetsTheProbeCountAndDisarms()
    {
        QuicLossRecovery recovery = Recovery();
        Send(recovery, Handshake, 0, 0);
        recovery.OnLossDetectionTimeout(Ms(999));

        recovery.OnAckReceived(Handshake, Ack(0), TimeSpan.Zero, Ms(1000));

        Assert.AreEqual(0, recovery.ProbeTimeoutCount);
        Assert.IsNull(recovery.LossDetectionTimer);
    }

    [TestMethod]
    public void DiscardSpace_PacketsInFlight_TakesThemOutWithoutLossAndResetsTheProbeCount()
    {
        QuicLossRecovery recovery = Recovery();
        Send(recovery, Initial, 0, 0);
        Send(recovery, Handshake, 0, 0);
        recovery.OnLossDetectionTimeout(Ms(999));

        recovery.DiscardSpace(Initial, Ms(999));

        Assert.AreEqual(1200, recovery.Congestion.BytesInFlight);
        Assert.AreEqual(0, recovery.ProbeTimeoutCount);
        Assert.IsEmpty(recovery.GetUnacknowledgedPackets(Initial));
        Assert.AreEqual(12000, recovery.Congestion.CongestionWindow);
        Assert.AreEqual(Ms(999), recovery.LossDetectionTimer);
    }

    [TestMethod]
    public void OnAckReceived_LostRunSpanningThePersistentCongestionDuration_CollapsesTheWindow()
    {
        QuicLossRecovery recovery = PersistentCongestionScenario(acknowledgeMiddle: false, out QuicAcknowledgementOutcome outcome);

        CollectionAssert.AreEqual(new ulong[] { 1, 2, 3, 4, 5 }, outcome.Lost.Select(packet => packet.PacketNumber).ToArray());
        // The window collapses to two datagrams, then packet 6, sent after recovery ended, grows it in slow start.
        Assert.AreEqual(recovery.Congestion.MinimumWindow + 1200, recovery.Congestion.CongestionWindow);
        Assert.IsNull(recovery.Congestion.RecoveryStartTime);
    }

    [TestMethod]
    public void OnAckReceived_LostRunBrokenByAnAcknowledgement_IsNotPersistentCongestion()
    {
        QuicLossRecovery recovery = PersistentCongestionScenario(acknowledgeMiddle: true, out QuicAcknowledgementOutcome outcome);

        CollectionAssert.AreEqual(new ulong[] { 1, 2, 4, 5 }, outcome.Lost.Select(packet => packet.PacketNumber).ToArray());
        Assert.AreEqual(6600, recovery.Congestion.CongestionWindow);
        Assert.AreEqual(Ms(320), recovery.Congestion.RecoveryStartTime);
    }

    [TestMethod]
    public void OnAckReceived_LossBeforeAnyRttSample_IsNotPersistentCongestion()
    {
        QuicLossRecovery recovery = Recovery();
        Send(recovery, Application, 0, 0);
        Send(recovery, Application, 1, 10000);
        Send(recovery, Application, 2, 10000, ackEliciting: false);
        Send(recovery, Application, 3, 10000, ackEliciting: false);

        QuicAcknowledgementOutcome outcome = recovery.OnAckReceived(Application, Ack(3), TimeSpan.Zero, Ms(10010));

        Assert.AreEqual(0UL, outcome.Lost.Single().PacketNumber);
        Assert.IsFalse(recovery.Rtt.HasSample);
        Assert.AreEqual(6000, recovery.Congestion.CongestionWindow);
    }

    [TestMethod]
    public void Constructor_And_Calls_RejectNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new QuicLossRecovery(null!));
        QuicLossRecovery recovery = Recovery();
        Assert.ThrowsExactly<ArgumentNullException>(() => recovery.OnPacketSent(Initial, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => recovery.OnAckReceived(Initial, null!, TimeSpan.Zero, TimeSpan.Zero));
    }

    internal static TimeSpan Ms(double milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    internal static QuicAckFrame Ack(ulong packetNumber) => new(packetNumber, 0, 0, [], null);

    internal static QuicSentPacket Packet(ulong packetNumber, double sentAtMilliseconds, bool ackEliciting = true, int bytes = 1200) =>
        new(packetNumber, Ms(sentAtMilliseconds), ackEliciting, ackEliciting, bytes, ackEliciting ? [new QuicPingFrame()] : [new QuicAckFrame(0, 0, 0, [], null)]);

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
