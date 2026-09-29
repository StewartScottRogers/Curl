namespace Curl.Quic;

/// <summary>
/// The congestion controller's shared part, RFC 9002 section 7 and Appendix B: bytes in
/// flight, the congestion window and slow start threshold, the recovery period that starts
/// at the first loss and ends when a packet sent after it is acknowledged, and persistent
/// congestion, which collapses the window to its minimum. How the window grows and how far
/// it falls on a congestion event is the derived controller's.
/// </summary>
public abstract class QuicCongestionController
{
    /// <summary>Initializes a new instance of the <see cref="QuicCongestionController" /> class with the initial window.</summary>
    /// <param name="maxDatagramSize">The largest datagram the path carries, at least 1200 bytes; it sets the initial and minimum windows.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxDatagramSize" /> is below 1200.</exception>
    protected QuicCongestionController(int maxDatagramSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDatagramSize, QuicDatagramAssembler.DatagramSize);
        MaxDatagramSize = maxDatagramSize;
        CongestionWindow = InitialWindow;
    }

    /// <summary>Gets the largest datagram the path carries.</summary>
    public int MaxDatagramSize { get; }

    /// <summary>Gets the window a connection starts with: ten datagrams, capped at the larger of 14720 bytes and two datagrams (RFC 9002 section 7.2).</summary>
    public long InitialWindow => Math.Min(10L * MaxDatagramSize, Math.Max(14720L, 2L * MaxDatagramSize));

    /// <summary>Gets the smallest the window becomes: two datagrams (RFC 9002 section 7.2).</summary>
    public long MinimumWindow => 2L * MaxDatagramSize;

    /// <summary>Gets the congestion window: the most bytes that may be in flight.</summary>
    public long CongestionWindow { get; protected set; }

    /// <summary>Gets the slow start threshold; slow start runs while the window is below it.</summary>
    public long SlowStartThreshold { get; protected set; } = long.MaxValue;

    /// <summary>Gets the bytes of the packets in flight: sent, ack-eliciting or padded, and neither acknowledged, lost nor discarded.</summary>
    public long BytesInFlight { get; private set; }

    /// <summary>Gets how many more bytes the window lets go now.</summary>
    public long AvailableWindow => Math.Max(0, CongestionWindow - BytesInFlight);

    /// <summary>Gets when the current recovery period started, or <see langword="null" /> when none has.</summary>
    public TimeSpan? RecoveryStartTime { get; private set; }

    /// <summary>Creates the controller for <paramref name="algorithm" />.</summary>
    /// <param name="algorithm">The algorithm.</param>
    /// <param name="maxDatagramSize">The largest datagram the path carries.</param>
    /// <returns>The controller.</returns>
    public static QuicCongestionController Create(QuicCongestionControlAlgorithm algorithm, int maxDatagramSize) => algorithm == QuicCongestionControlAlgorithm.NewReno
        ? new QuicNewRenoCongestionController(maxDatagramSize)
        : new QuicCubicCongestionController(maxDatagramSize);

    /// <summary>Gets whether a packet sent at <paramref name="timeSent" /> was sent during the current recovery period, so its acknowledgement does not grow the window and its loss starts no new period.</summary>
    /// <param name="timeSent">When the packet was sent.</param>
    /// <returns><see langword="true" /> when it was sent at or before the period started.</returns>
    public bool IsInRecovery(TimeSpan timeSent) => RecoveryStartTime is { } start && timeSent <= start;

    /// <summary>Counts a sent packet in flight (RFC 9002 section B.4).</summary>
    /// <param name="packet">The packet.</param>
    public void OnPacketSent(QuicSentPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (packet.IsInFlight)
        {
            BytesInFlight += packet.SentBytes;
        }
    }

    /// <summary>
    /// Takes acknowledged packets out of flight and hands those sent outside recovery to the
    /// derived controller to grow the window, once per acknowledgement (RFC 9002 section B.5).
    /// The derived controller does not grow it for packets sent while application-limited
    /// (section 7.8).
    /// </summary>
    /// <param name="packets">The newly acknowledged packets.</param>
    /// <param name="now">The time now.</param>
    /// <param name="rtt">The RTT estimate, after this acknowledgement's sample.</param>
    public void OnPacketsAcknowledged(IEnumerable<QuicSentPacket> packets, TimeSpan now, QuicRttEstimator rtt)
    {
        ArgumentNullException.ThrowIfNull(packets);
        ArgumentNullException.ThrowIfNull(rtt);
        List<QuicSentPacket> outsideRecovery = [];
        foreach (var packet in packets.Where(packet => packet.IsInFlight))
        {
            BytesInFlight -= packet.SentBytes;
            if (!IsInRecovery(packet.TimeSent))
            {
                outsideRecovery.Add(packet);
            }
        }

        if (outsideRecovery.Count > 0)
        {
            IncreaseWindow(outsideRecovery, now, rtt);
        }
    }

    /// <summary>
    /// Takes lost packets out of flight and, when any was in flight, enters recovery unless
    /// the last of them was sent during the current period (RFC 9002 sections B.6 and B.8).
    /// Persistent congestion then collapses the window to <see cref="MinimumWindow" />.
    /// </summary>
    /// <param name="packets">The packets declared lost.</param>
    /// <param name="persistentCongestion">Whether they establish persistent congestion (RFC 9002 section 7.6).</param>
    /// <param name="now">The time now.</param>
    public void OnPacketsLost(IEnumerable<QuicSentPacket> packets, bool persistentCongestion, TimeSpan now)
    {
        ArgumentNullException.ThrowIfNull(packets);
        List<QuicSentPacket> inFlight = [.. packets.Where(packet => packet.IsInFlight)];
        BytesInFlight -= inFlight.Sum(packet => (long)packet.SentBytes);
        if (inFlight.Count > 0)
        {
            OnCongestionEvent(inFlight.Max(packet => packet.TimeSent), now);
        }

        if (persistentCongestion)
        {
            CongestionWindow = MinimumWindow;
            RecoveryStartTime = null;
            OnPersistentCongestion();
        }
    }

    /// <summary>Takes the packets of a discarded packet number space out of flight without counting them lost (RFC 9002 section 6.4).</summary>
    /// <param name="packets">The space's packets.</param>
    public void RemoveFromBytesInFlight(IEnumerable<QuicSentPacket> packets)
    {
        ArgumentNullException.ThrowIfNull(packets);
        BytesInFlight -= packets.Where(packet => packet.IsInFlight).Sum(packet => (long)packet.SentBytes);
    }

    /// <summary>Starts a recovery period and lowers the window, unless the lost packet was sent within the current period (RFC 9002 section B.6).</summary>
    /// <param name="sentTime">When the last lost packet was sent.</param>
    /// <param name="now">The time now.</param>
    private void OnCongestionEvent(TimeSpan sentTime, TimeSpan now)
    {
        if (!IsInRecovery(sentTime))
        {
            RecoveryStartTime = now;
            ReduceWindow(now);
        }
    }

    /// <summary>Grows the window for the packets one acknowledgement newly acknowledged that were sent outside recovery, leaving it as it is for those sent while application-limited.</summary>
    /// <param name="acknowledged">The packets, at least one.</param>
    /// <param name="now">The time now.</param>
    /// <param name="rtt">The RTT estimate, after this acknowledgement's sample.</param>
    protected abstract void IncreaseWindow(IReadOnlyList<QuicSentPacket> acknowledged, TimeSpan now, QuicRttEstimator rtt);

    /// <summary>Lowers the window and slow start threshold at the start of a recovery period.</summary>
    /// <param name="now">The time now.</param>
    protected abstract void ReduceWindow(TimeSpan now);

    /// <summary>Resets what the controller keeps once the window has collapsed to its minimum.</summary>
    protected abstract void OnPersistentCongestion();
}
