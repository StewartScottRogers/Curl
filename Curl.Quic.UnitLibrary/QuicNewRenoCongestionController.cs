namespace Curl.Quic;

/// <summary>
/// NewReno, the congestion controller of RFC 9002 section 7 and Appendix B: slow start
/// grows the window by every acknowledged byte, congestion avoidance by one datagram per
/// window acknowledged, and a congestion event halves it. Packets sent while
/// application-limited do not grow it (section 7.8).
/// </summary>
/// <param name="maxDatagramSize">The largest datagram the path carries, at least 1200 bytes.</param>
public sealed class QuicNewRenoCongestionController(int maxDatagramSize) : QuicCongestionController(maxDatagramSize)
{
    /// <summary>What a congestion event multiplies the window by (RFC 9002 section 7.3.2, <c>kLossReductionFactor</c>).</summary>
    public const double LossReductionFactor = 0.5;

    /// <inheritdoc />
    protected override void IncreaseWindow(IReadOnlyList<QuicSentPacket> acknowledged, TimeSpan now, QuicRttEstimator rtt)
    {
        foreach (var packet in acknowledged.Where(packet => !packet.IsApplicationLimited))
        {
            CongestionWindow += CongestionWindow < SlowStartThreshold
                ? packet.SentBytes
                : (long)MaxDatagramSize * packet.SentBytes / CongestionWindow;
        }
    }

    /// <inheritdoc />
    protected override void ReduceWindow(TimeSpan now)
    {
        SlowStartThreshold = (long)(CongestionWindow * LossReductionFactor);
        CongestionWindow = Math.Max(SlowStartThreshold, MinimumWindow);
    }

    /// <inheritdoc />
    protected override void OnPersistentCongestion()
    {
        // NewReno keeps nothing beyond the window and threshold the base resets.
    }
}
