namespace Curl.Quic;

/// <summary>
/// Paces sending (RFC 9002 section 7.7) as a token bucket: it fills at
/// <c>N * congestion_window / smoothed_rtt</c> with <c>N</c> = 1.25, holds at most a burst
/// of <see cref="BurstDatagrams" /> datagrams, starts full, and each packet sent takes its
/// size out of it. A packet may go once the bucket holds its size.
/// </summary>
/// <param name="maxDatagramSize">The largest datagram the path carries.</param>
public sealed class QuicPacer(int maxDatagramSize)
{
    /// <summary>How much faster than one window per RTT the bucket fills, so that pacing never holds the window back (RFC 9002 section 7.7, <c>N</c>).</summary>
    public const double Gain = 1.25;

    /// <summary>How many datagrams may go back to back, as RFC 9002 section 7.7 allows for the initial window.</summary>
    public const int BurstDatagrams = 10;

    private readonly double capacity = (double)BurstDatagrams * maxDatagramSize;

    private double tokens = (double)BurstDatagrams * maxDatagramSize;

    private TimeSpan lastRefill;

    /// <summary>Returns how long to wait before a packet of <paramref name="bytes" /> may go; zero when it may go now.</summary>
    /// <param name="now">The time now.</param>
    /// <param name="bytes">The packet's size.</param>
    /// <param name="congestionWindow">The congestion window.</param>
    /// <param name="smoothedRtt">The smoothed RTT.</param>
    /// <returns>The wait.</returns>
    public TimeSpan TimeUntilSend(TimeSpan now, int bytes, long congestionWindow, TimeSpan smoothedRtt)
    {
        var rate = Refill(now, congestionWindow, smoothedRtt);
        return tokens >= bytes ? TimeSpan.Zero : TimeSpan.FromSeconds((bytes - tokens) / rate);
    }

    /// <summary>Takes a sent packet's size out of the bucket.</summary>
    /// <param name="now">The time now.</param>
    /// <param name="bytes">The packet's size.</param>
    /// <param name="congestionWindow">The congestion window.</param>
    /// <param name="smoothedRtt">The smoothed RTT.</param>
    public void OnPacketSent(TimeSpan now, int bytes, long congestionWindow, TimeSpan smoothedRtt)
    {
        Refill(now, congestionWindow, smoothedRtt);
        tokens -= bytes;
    }

    // Fills the bucket for the time since the last fill and returns the rate, in bytes per
    // second; an RTT below the timer granularity counts as the granularity.
    private double Refill(TimeSpan now, long congestionWindow, TimeSpan smoothedRtt)
    {
        var rate = Gain * congestionWindow / Math.Max(smoothedRtt.TotalSeconds, QuicRttEstimator.Granularity.TotalSeconds);
        tokens = Math.Min(capacity, tokens + (rate * (now - lastRefill).TotalSeconds));
        lastRefill = now;
        return rate;
    }
}
