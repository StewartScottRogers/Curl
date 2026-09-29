namespace Curl.Quic;

/// <summary>
/// The round-trip time estimate of RFC 9002 section 5 and Appendix A.7: the latest sample,
/// the minimum, the exponentially weighted smoothed RTT and its variation. Before the first
/// sample the smoothed RTT is <see cref="InitialRtt" /> and the variation half of it.
/// </summary>
public sealed class QuicRttEstimator
{
    /// <summary>The RTT assumed before the first sample (RFC 9002 section 6.2.2, <c>kInitialRtt</c>).</summary>
    public static readonly TimeSpan InitialRtt = TimeSpan.FromMilliseconds(333);

    /// <summary>The timer granularity (RFC 9002 section 6.1.2, <c>kGranularity</c>).</summary>
    public static readonly TimeSpan Granularity = TimeSpan.FromMilliseconds(1);

    /// <summary>Gets the most recent RTT sample, or zero before any.</summary>
    public TimeSpan LatestRtt { get; private set; }

    /// <summary>Gets the smallest RTT sample seen, or zero before any.</summary>
    public TimeSpan MinRtt { get; private set; }

    /// <summary>Gets the smoothed RTT.</summary>
    public TimeSpan SmoothedRtt { get; private set; } = InitialRtt;

    /// <summary>Gets the RTT variation.</summary>
    public TimeSpan RttVariation { get; private set; } = InitialRtt / 2;

    /// <summary>Gets a value indicating whether a sample has been taken.</summary>
    public bool HasSample { get; private set; }

    /// <summary>Gets the probe timeout before backoff and before the peer's <c>max_ack_delay</c>: smoothed RTT plus the larger of four variations and the granularity (RFC 9002 section 6.2.1).</summary>
    public TimeSpan ProbeTimeout => SmoothedRtt + Max(4 * RttVariation, Granularity);

    /// <summary>Gets how long after a later packet is acknowledged an earlier one is declared lost: 9/8 of the larger of the latest and smoothed RTT, at least the granularity (RFC 9002 section 6.1.2).</summary>
    public TimeSpan LossDelay => Max(Max(LatestRtt, SmoothedRtt) * 9 / 8, Granularity);

    /// <summary>Takes one RTT sample (RFC 9002 sections 5.2 and 5.3).</summary>
    /// <param name="latestRtt">The time from sending the largest newly acknowledged packet to its acknowledgement.</param>
    /// <param name="ackDelay">The delay the peer reported, already decoded; zero to ignore it.</param>
    /// <param name="maxAckDelay">The peer's <c>max_ack_delay</c>, which caps <paramref name="ackDelay" /> once the handshake is confirmed.</param>
    /// <param name="handshakeConfirmed">Whether the handshake is confirmed.</param>
    public void Update(TimeSpan latestRtt, TimeSpan ackDelay, TimeSpan maxAckDelay, bool handshakeConfirmed)
    {
        LatestRtt = latestRtt;
        if (!HasSample)
        {
            HasSample = true;
            MinRtt = latestRtt;
            SmoothedRtt = latestRtt;
            RttVariation = latestRtt / 2;
            return;
        }

        MinRtt = Min(MinRtt, latestRtt);
        if (handshakeConfirmed)
        {
            ackDelay = Min(ackDelay, maxAckDelay);
        }

        // The acknowledgement delay is taken off only when that leaves the sample at least min_rtt.
        var adjustedRtt = latestRtt >= MinRtt + ackDelay ? latestRtt - ackDelay : latestRtt;
        RttVariation = (3 * RttVariation / 4) + ((SmoothedRtt - adjustedRtt).Duration() / 4);
        SmoothedRtt = (7 * SmoothedRtt / 8) + (adjustedRtt / 8);
    }

    private static TimeSpan Max(TimeSpan left, TimeSpan right) => left > right ? left : right;

    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left < right ? left : right;
}
