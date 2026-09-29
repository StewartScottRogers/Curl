namespace Curl.Quic;

/// <summary>
/// CUBIC (RFC 9438), the congestion controller curl's ngtcp2 build runs (ADR-0144 section 5),
/// over RFC 9002's recovery. Slow start grows the window by every acknowledged byte. In
/// congestion avoidance the window follows <c>W_cubic(t) = C (t - K)^3 + W_max</c> towards the
/// target one RTT ahead, never below the Reno-friendly estimate <c>W_est</c>. A congestion
/// event multiplies it by 0.7, with fast convergence lowering <c>W_max</c> when the window
/// had not regained the last one. Windows in the formulas are in datagrams, time in seconds.
/// </summary>
/// <param name="maxDatagramSize">The largest datagram the path carries, at least 1200 bytes.</param>
public sealed class QuicCubicCongestionController(int maxDatagramSize) : QuicCongestionController(maxDatagramSize)
{
    /// <summary>The cubic's scaling constant, in datagrams per second cubed (RFC 9438 section 5.1, <c>C</c>).</summary>
    public const double Scale = 0.4;

    /// <summary>What a congestion event multiplies the window by (RFC 9438 section 4.6, <c>beta_cubic</c>).</summary>
    public const double Beta = 0.7;

    /// <summary>The Reno-friendly estimate's growth per window acknowledged until it reaches the window before the last loss (RFC 9438 section 4.3, <c>alpha_cubic</c>).</summary>
    public static readonly double Alpha = 3 * (1 - Beta) / (1 + Beta);

    private TimeSpan? epochStart;

    private double windowBeforeLoss;

    private double renoEstimate;

    /// <summary>Gets <c>W_max</c>: the window, in datagrams, just before the last reduction, less fast convergence; zero before any.</summary>
    public double WindowMaximum { get; private set; }

    /// <summary>Gets <c>K</c>: how long, in seconds, the cubic takes from the start of the congestion avoidance epoch to reach <see cref="WindowMaximum" />.</summary>
    public double TimeToWindowMaximum { get; private set; }

    /// <summary>Returns <c>W_cubic(t)</c> in datagrams (RFC 9438 section 4.2).</summary>
    /// <param name="secondsSinceEpoch">The time since the congestion avoidance epoch started.</param>
    /// <returns>The window the cubic gives.</returns>
    public double CubicWindow(double secondsSinceEpoch) => (Scale * Math.Pow(secondsSinceEpoch - TimeToWindowMaximum, 3)) + WindowMaximum;

    /// <inheritdoc />
    protected override void IncreaseWindow(int acknowledgedBytes, TimeSpan now, QuicRttEstimator rtt)
    {
        if (CongestionWindow < SlowStartThreshold)
        {
            CongestionWindow += acknowledgedBytes;
            return;
        }

        var window = (double)CongestionWindow / MaxDatagramSize;
        var acknowledged = (double)acknowledgedBytes / MaxDatagramSize;
        var elapsed = (now - StartEpoch(now, window)).TotalSeconds;

        // The target is where the cubic will be one RTT from now, kept between the window and 1.5 times it (section 4.2).
        var target = Math.Clamp(CubicWindow(elapsed + rtt.SmoothedRtt.TotalSeconds), window, 1.5 * window);
        renoEstimate += (renoEstimate >= windowBeforeLoss ? 1 : Alpha) * acknowledged / window;
        var next = CubicWindow(elapsed) < renoEstimate
            ? renoEstimate
            : window + ((target - window) / window * acknowledged);
        CongestionWindow = Math.Max(CongestionWindow, (long)(next * MaxDatagramSize));
    }

    /// <inheritdoc />
    protected override void ReduceWindow(TimeSpan now)
    {
        var window = (double)CongestionWindow / MaxDatagramSize;

        // Fast convergence: a window that has not regained the last W_max releases bandwidth sooner (section 4.7).
        WindowMaximum = window < WindowMaximum ? window * (1 + Beta) / 2 : window;
        windowBeforeLoss = window;
        epochStart = null;
        SlowStartThreshold = Math.Max((long)(CongestionWindow * Beta), MinimumWindow);
        CongestionWindow = SlowStartThreshold;
    }

    /// <inheritdoc />
    protected override void OnPersistentCongestion() => epochStart = null;

    // A congestion avoidance epoch starts at its first acknowledgement (section 4.2). With no
    // loss yet, or a window already past W_max, the cubic starts at the window with K zero.
    private TimeSpan StartEpoch(TimeSpan now, double window)
    {
        if (epochStart is { } start)
        {
            return start;
        }

        epochStart = now;
        renoEstimate = window;
        WindowMaximum = Math.Max(WindowMaximum, window);
        TimeToWindowMaximum = Math.Cbrt((WindowMaximum - window) / Scale);
        return now;
    }
}
