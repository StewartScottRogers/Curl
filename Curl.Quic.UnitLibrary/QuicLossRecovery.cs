namespace Curl.Quic;

/// <summary>
/// A QUIC client's loss detection, RFC 9002 section 6 and Appendix A, with no clock of its
/// own: every call is told the time. It remembers each sent packet per packet number space,
/// takes RTT samples from ACK frames, declares a packet lost once a packet sent at least
/// <see cref="PacketThreshold" /> later is acknowledged or once 9/8 of an RTT has passed
/// since it went while a later one was acknowledged, and runs the probe timeout, doubling it
/// each time it fires without an acknowledgement. The <see cref="QuicCongestionController" />
/// hears of every packet sent, acknowledged and lost. Resending what lost packets carried is
/// the caller's: loss detection hands the packets back and never resends one.
/// </summary>
public sealed class QuicLossRecovery
{
    /// <summary>How many later packets must be acknowledged before an earlier one is lost (RFC 9002 section 6.1.1, <c>kPacketThreshold</c>).</summary>
    public const int PacketThreshold = 3;

    /// <summary>How many probe timeouts, plus the peer's <c>max_ack_delay</c>, a run of losses must span to be persistent congestion (RFC 9002 section 7.6.1, <c>kPersistentCongestionThreshold</c>).</summary>
    public const int PersistentCongestionThreshold = 3;

    private static readonly QuicPacketNumberSpaceId[] Spaces = [QuicPacketNumberSpaceId.Initial, QuicPacketNumberSpaceId.Handshake, QuicPacketNumberSpaceId.ApplicationData];

    private readonly SortedDictionary<ulong, QuicSentPacket>[] sentPackets = [new(), new(), new()];

    private readonly ulong?[] largestAcknowledged = new ulong?[3];

    private readonly TimeSpan?[] lossTime = new TimeSpan?[3];

    private readonly TimeSpan?[] timeOfLastAckElicitingPacket = new TimeSpan?[3];

    private TimeSpan? firstRttSampleTime;

    private bool handshakeAcknowledged;

    /// <summary>Initializes a new instance of the <see cref="QuicLossRecovery" /> class.</summary>
    /// <param name="congestion">The congestion controller that hears of every packet sent, acknowledged and lost.</param>
    public QuicLossRecovery(QuicCongestionController congestion)
    {
        ArgumentNullException.ThrowIfNull(congestion);
        Congestion = congestion;
        Pacer = new QuicPacer(congestion.MaxDatagramSize);
    }

    /// <summary>Gets the RTT estimate.</summary>
    public QuicRttEstimator Rtt { get; } = new();

    /// <summary>Gets the congestion controller.</summary>
    public QuicCongestionController Congestion { get; }

    /// <summary>Gets the pacer that spreads sending over the RTT (RFC 9002 section 7.7).</summary>
    public QuicPacer Pacer { get; }

    /// <summary>Gets how many times the probe timeout has fired since the last acknowledgement that reset it (<c>pto_count</c>).</summary>
    public int ProbeTimeoutCount { get; private set; }

    /// <summary>Gets or sets the peer's <c>max_ack_delay</c>, 25 ms until its transport parameters say otherwise.</summary>
    public TimeSpan MaxAckDelay { get; set; } = TimeSpan.FromMilliseconds(QuicTransportParameters.DefaultMaxAckDelay);

    /// <summary>Gets or sets a value indicating whether the client has Handshake keys, which makes the anti-deadlock probe a Handshake packet (RFC 9002 section 6.2.2.1).</summary>
    public bool HasHandshakeKeys { get; set; }

    /// <summary>Gets a value indicating whether the handshake is confirmed.</summary>
    public bool IsHandshakeConfirmed { get; private set; }

    /// <summary>Gets when the loss detection timer fires, or <see langword="null" /> while it is not armed.</summary>
    public TimeSpan? LossDetectionTimer { get; private set; }

    // The client assumes it validated the server's address; the server has validated the
    // client's once a Handshake packet is acknowledged or the handshake is confirmed.
    private bool PeerCompletedAddressValidation => handshakeAcknowledged || IsHandshakeConfirmed;

    private bool AckElicitingInFlight => Spaces.Any(AckElicitingInFlightIn);

    /// <summary>Gets the packets of a space sent and neither acknowledged nor lost, oldest first.</summary>
    /// <param name="space">The space.</param>
    /// <returns>The packets.</returns>
    public IReadOnlyCollection<QuicSentPacket> GetUnacknowledgedPackets(QuicPacketNumberSpaceId space) => sentPackets[(int)space].Values;

    /// <summary>Remembers a sent packet and arms the timer (RFC 9002 section A.5).</summary>
    /// <param name="space">Its packet number space.</param>
    /// <param name="packet">The packet.</param>
    public void OnPacketSent(QuicPacketNumberSpaceId space, QuicSentPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        sentPackets[(int)space][packet.PacketNumber] = packet;
        if (packet.IsInFlight)
        {
            if (packet.IsAckEliciting)
            {
                timeOfLastAckElicitingPacket[(int)space] = packet.TimeSent;
            }

            Congestion.OnPacketSent(packet);
            SetLossDetectionTimer(packet.TimeSent);
        }
    }

    /// <summary>
    /// Takes an ACK frame (RFC 9002 section A.7): removes the packets it acknowledges, takes
    /// an RTT sample when its largest is newly acknowledged and an ack-eliciting packet is,
    /// declares packets lost, tells the congestion controller, and rearms the timer.
    /// </summary>
    /// <param name="space">The space the frame arrived in.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="ackDelay">The frame's ACK Delay, decoded with the peer's <c>ack_delay_exponent</c>; it counts only in the application data space.</param>
    /// <param name="now">The time now.</param>
    /// <returns>The packets newly acknowledged and those declared lost.</returns>
    public QuicAcknowledgementOutcome OnAckReceived(QuicPacketNumberSpaceId space, QuicAckFrame frame, TimeSpan ackDelay, TimeSpan now)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var index = (int)space;
        largestAcknowledged[index] = Math.Max(largestAcknowledged[index] ?? 0, frame.LargestAcknowledged);
        var acknowledged = RemoveAcknowledgedPackets(space, frame);
        if (acknowledged.Count == 0)
        {
            return new QuicAcknowledgementOutcome([], []);
        }

        SampleRtt(space, frame, acknowledged, ackDelay, now);
        handshakeAcknowledged |= space == QuicPacketNumberSpaceId.Handshake;
        var lost = RemoveLostPackets(space, now);
        Congestion.OnPacketsLost(lost, IsPersistentCongestion(lost), now);
        Congestion.OnPacketsAcknowledged(acknowledged, now, Rtt);
        if (PeerCompletedAddressValidation)
        {
            ProbeTimeoutCount = 0;
        }

        SetLossDetectionTimer(now);
        return new QuicAcknowledgementOutcome(acknowledged, lost);
    }

    /// <summary>
    /// Runs the loss detection timer's expiry (RFC 9002 section A.9): declares packets lost by
    /// the time threshold when a loss time is due, otherwise counts a probe timeout and names
    /// the space to probe.
    /// </summary>
    /// <param name="now">The time now.</param>
    /// <returns>What the timer did.</returns>
    public QuicLossDetectionTimeoutOutcome OnLossDetectionTimeout(TimeSpan now)
    {
        if (EarliestLossTime() is { Space: var lossSpace })
        {
            var lost = RemoveLostPackets(lossSpace, now);
            Congestion.OnPacketsLost(lost, IsPersistentCongestion(lost), now);
            SetLossDetectionTimer(now);
            return new QuicLossDetectionTimeoutOutcome(lossSpace, lost, false);
        }

        var probeSpace = ProbeTimeout(now).Space;
        ProbeTimeoutCount++;
        SetLossDetectionTimer(now);
        return new QuicLossDetectionTimeoutOutcome(probeSpace, [], true);
    }

    /// <summary>Forgets a space whose keys are discarded (RFC 9002 section A.11): its packets leave flight without being lost, and the probe count resets.</summary>
    /// <param name="space">The space.</param>
    /// <param name="now">The time now.</param>
    public void DiscardSpace(QuicPacketNumberSpaceId space, TimeSpan now)
    {
        var index = (int)space;
        Congestion.RemoveFromBytesInFlight(sentPackets[index].Values);
        sentPackets[index].Clear();
        timeOfLastAckElicitingPacket[index] = null;
        lossTime[index] = null;
        ProbeTimeoutCount = 0;
        SetLossDetectionTimer(now);
    }

    /// <summary>Records that the handshake is confirmed, which lets the application data space arm the probe timeout and caps the ACK Delay at <see cref="MaxAckDelay" />.</summary>
    /// <param name="now">The time now.</param>
    public void ConfirmHandshake(TimeSpan now)
    {
        IsHandshakeConfirmed = true;
        SetLossDetectionTimer(now);
    }


    // Section 7.6.2 counts only packets sent after the first RTT sample; numbers rise with send
    // time, so those of a run are its tail.
    private static bool SpansMoreThan(List<QuicSentPacket> run, TimeSpan sampleTime, TimeSpan duration)
    {
        List<TimeSpan> sent = [.. run.Where(packet => packet.IsAckEliciting && packet.TimeSent >= sampleTime).Select(packet => packet.TimeSent)];
        return sent.Count > 1 && sent[^1] - sent[0] > duration;
    }

    // Splits packets into runs of consecutive numbers: nothing between two packets of a run was acknowledged.
    private static List<List<QuicSentPacket>> ConsecutiveRuns(List<QuicSentPacket> packets)
    {
        List<List<QuicSentPacket>> runs = [];
        foreach (var packet in packets)
        {
            if (runs.Count == 0 || runs[^1][^1].PacketNumber + 1 != packet.PacketNumber)
            {
                runs.Add([]);
            }

            runs[^1].Add(packet);
        }

        return runs;
    }

    // Section A.7: a sample when the largest acknowledged is newly acknowledged and something
    // newly acknowledged asked for it. Initial and Handshake acknowledgements are not delayed
    // on purpose, so their ACK Delay is ignored (section 5.3).
    private void SampleRtt(QuicPacketNumberSpaceId space, QuicAckFrame frame, List<QuicSentPacket> acknowledged, TimeSpan ackDelay, TimeSpan now)
    {
        var largest = acknowledged[^1];
        if (largest.PacketNumber == frame.LargestAcknowledged && acknowledged.Any(packet => packet.IsAckEliciting))
        {
            firstRttSampleTime ??= now;
            Rtt.Update(now - largest.TimeSent, space == QuicPacketNumberSpaceId.ApplicationData ? ackDelay : TimeSpan.Zero, MaxAckDelay, IsHandshakeConfirmed);
        }
    }

    private bool AckElicitingInFlightIn(QuicPacketNumberSpaceId space) => sentPackets[(int)space].Values.Any(packet => packet.IsAckEliciting);

    private List<QuicSentPacket> RemoveAcknowledgedPackets(QuicPacketNumberSpaceId space, QuicAckFrame frame)
    {
        var ranges = frame.GetAcknowledgedRanges();
        var sent = sentPackets[(int)space];
        List<QuicSentPacket> acknowledged = [.. sent.Values.Where(packet => ranges.Any(range => packet.PacketNumber >= range.Smallest && packet.PacketNumber <= range.Largest))];
        foreach (var packet in acknowledged)
        {
            sent.Remove(packet.PacketNumber);
        }

        return acknowledged;
    }

    // Section A.10: a packet at or below the largest acknowledged is lost by either threshold;
    // one that is not yet sets when it will be, by the time threshold.
    private List<QuicSentPacket> RemoveLostPackets(QuicPacketNumberSpaceId space, TimeSpan now)
    {
        var index = (int)space;
        var lossDelay = Rtt.LossDelay;
        var lostSendTime = now - lossDelay;
        var largest = largestAcknowledged[index].GetValueOrDefault();
        var sent = sentPackets[index];
        List<QuicSentPacket> candidates = [.. sent.Values.Where(packet => packet.PacketNumber <= largest)];
        List<QuicSentPacket> lost = [.. candidates.Where(packet => packet.TimeSent <= lostSendTime || largest >= packet.PacketNumber + PacketThreshold)];
        foreach (var packet in lost)
        {
            sent.Remove(packet.PacketNumber);
        }

        var earliestLossTime = candidates.Except(lost).Select(packet => packet.TimeSent + lossDelay).DefaultIfEmpty(TimeSpan.MaxValue).Min();
        lossTime[index] = earliestLossTime == TimeSpan.MaxValue ? null : earliestLossTime;
        return lost;
    }

    // Section 7.6.2: ack-eliciting losses sent after the first RTT sample, with no packet
    // between them acknowledged, spanning more than the persistent congestion duration.
    // Only the packets one call declares lost are weighed, so a run is consecutive numbers.
    private bool IsPersistentCongestion(List<QuicSentPacket> lost)
    {
        if (firstRttSampleTime is not { } sampleTime)
        {
            return false;
        }

        var duration = (Rtt.ProbeTimeout + MaxAckDelay) * PersistentCongestionThreshold;
        return ConsecutiveRuns(lost).Any(run => SpansMoreThan(run, sampleTime, duration));
    }

    private (QuicPacketNumberSpaceId Space, TimeSpan Time)? EarliestLossTime()
    {
        (QuicPacketNumberSpaceId Space, TimeSpan Time)? earliest = null;
        foreach (var space in Spaces)
        {
            if (lossTime[(int)space] is { } time && time < (earliest?.Time ?? TimeSpan.MaxValue))
            {
                earliest = (space, time);
            }
        }

        return earliest;
    }

    // Section A.8.
    private void SetLossDetectionTimer(TimeSpan now) =>
        LossDetectionTimer = EarliestLossTime() is { } loss
            ? loss.Time
            : !AckElicitingInFlight && PeerCompletedAddressValidation ? null : ProbeTimeout(now).Time;

    // Section A.8, GetPtoTimeAndSpace: the earliest space's last ack-eliciting packet plus the
    // backed-off probe timeout, with the peer's max_ack_delay for application data; with
    // nothing in flight, from now, in the space that has keys. The application data space
    // arms only once the handshake is confirmed, so with only it in flight the timer waits.
    private (TimeSpan? Time, QuicPacketNumberSpaceId Space) ProbeTimeout(TimeSpan now)
    {
        var backoff = 1L << Math.Min(ProbeTimeoutCount, 30);
        var duration = Rtt.ProbeTimeout * backoff;
        return AckElicitingInFlight
            ? EarliestProbeTimeout(duration, MaxAckDelay * backoff)
            : (now + duration, HasHandshakeKeys ? QuicPacketNumberSpaceId.Handshake : QuicPacketNumberSpaceId.Initial);
    }

    private (TimeSpan? Time, QuicPacketNumberSpaceId Space) EarliestProbeTimeout(TimeSpan duration, TimeSpan backedOffMaxAckDelay)
    {
        (TimeSpan? Time, QuicPacketNumberSpaceId Space) earliest = (null, QuicPacketNumberSpaceId.Initial);
        foreach (var space in Spaces.Where(IsProbeTimeoutArmedIn))
        {
            var spaceDuration = space == QuicPacketNumberSpaceId.ApplicationData ? duration + backedOffMaxAckDelay : duration;
            var time = timeOfLastAckElicitingPacket[(int)space].GetValueOrDefault() + spaceDuration;
            if (time < earliest.Time.GetValueOrDefault(TimeSpan.MaxValue))
            {
                earliest = (time, space);
            }
        }

        return earliest;
    }

    private bool IsProbeTimeoutArmedIn(QuicPacketNumberSpaceId space) =>
        AckElicitingInFlightIn(space) && (space != QuicPacketNumberSpaceId.ApplicationData || IsHandshakeConfirmed);
}
