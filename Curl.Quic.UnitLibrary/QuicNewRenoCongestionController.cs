namespace Curl.Quic;

/// <summary>
/// NewReno, the congestion controller of RFC 9002 section 7 and Appendix B: slow start
/// grows the window by every acknowledged byte, congestion avoidance by one datagram per
/// window acknowledged, and a congestion event halves it.
/// </summary>
/// <param name="maxDatagramSize">The largest datagram the path carries, at least 1200 bytes.</param>
public sealed class QuicNewRenoCongestionController(int maxDatagramSize) : QuicCongestionController(maxDatagramSize)
{
    /// <summary>What a congestion event multiplies the window by (RFC 9002 section 7.3.2, <c>kLossReductionFactor</c>).</summary>
    public const double LossReductionFactor = 0.5;

    /// <inheritdoc />
    protected override void IncreaseWindow(int acknowledgedBytes, TimeSpan now, QuicRttEstimator rtt) =>
        CongestionWindow += CongestionWindow < SlowStartThreshold
            ? acknowledgedBytes
            : (long)MaxDatagramSize * acknowledgedBytes / CongestionWindow;

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
