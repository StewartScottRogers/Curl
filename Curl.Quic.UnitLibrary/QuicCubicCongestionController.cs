namespace Curl.Quic;

/// <summary>
/// CUBIC (RFC 9438), the congestion controller curl's ngtcp2 build runs (ADR-0144 section 5),
/// over RFC 9002's recovery. The first slow start runs HyStart++ (RFC 9406): once a round's
/// minimum RTT has risen past the last round's by a threshold, Conservative Slow Start grows
/// the window by a quarter of each acknowledged byte, returns to slow start if the RTT falls
/// back, and after <see cref="ConservativeSlowStartRounds" /> rounds hands over to congestion
/// avoidance. In congestion avoidance the window follows <c>W_cubic(t) = C (t - K)^3 + W_max</c>
/// towards the target one RTT ahead, never below the Reno-friendly estimate <c>W_est</c>. A
/// congestion event multiplies it by 0.7, with fast convergence lowering <c>W_max</c> when the
/// window had not regained the last one. Packets sent while application-limited do not grow
/// the window, and time spent so does not advance the cubic (RFC 9002 section 7.8), as in
/// ngtcp2. Windows in the formulas are in datagrams, time in seconds.
/// </summary>
/// <param name="maxDatagramSize">The largest datagram the path carries, at least 1200 bytes.</param>
public sealed class QuicCubicCongestionController(int maxDatagramSize) : QuicCongestionController(maxDatagramSize)
{
    /// <summary>The cubic's scaling constant, in datagrams per second cubed (RFC 9438 section 5.1, <c>C</c>).</summary>
    public const double Scale = 0.4;

    /// <summary>What a congestion event multiplies the window by (RFC 9438 section 4.6, <c>beta_cubic</c>).</summary>
    public const double Beta = 0.7;

    /// <summary>What Conservative Slow Start divides each acknowledged byte by before adding it to the window (RFC 9406 section 4.3, <c>CSS_GROWTH_DIVISOR</c>).</summary>
    public const int ConservativeSlowStartGrowthDivisor = 4;

    /// <summary>How many rounds Conservative Slow Start lasts, counting the one it starts in, before congestion avoidance takes over (RFC 9406 section 4.3, <c>CSS_ROUNDS</c>).</summary>
    public const int ConservativeSlowStartRounds = 5;

    /// <summary>How many RTT samples a round takes before HyStart++ compares its minimum with the last round's (RFC 9406 section 4.3, <c>N_RTT_SAMPLE</c>).</summary>
    public const int RttSamplesPerRound = 8;

    /// <summary>The smallest RTT rise that ends slow start (RFC 9406 section 4.3, <c>MIN_RTT_THRESH</c>).</summary>
    public static readonly TimeSpan MinimumRttThreshold = TimeSpan.FromMilliseconds(4);

    /// <summary>The largest RTT rise needed to end slow start (RFC 9406 section 4.3, <c>MAX_RTT_THRESH</c>).</summary>
    public static readonly TimeSpan MaximumRttThreshold = TimeSpan.FromMilliseconds(16);

    /// <summary>The Reno-friendly estimate's growth per window acknowledged until it reaches the window before the last loss (RFC 9438 section 4.3, <c>alpha_cubic</c>).</summary>
    public static readonly double Alpha = 3 * (1 - Beta) / (1 + Beta);

    // RFC 9406 section 4.3, MIN_RTT_DIVISOR: the RTT threshold is the last round's minimum over this.
    private const int MinimumRttDivisor = 8;

    private TimeSpan? epochStart;

    private double windowBeforeLoss;

    private double renoEstimate;

    private TimeSpan? applicationLimitedSince;

    private TimeSpan? roundStart;

    private TimeSpan? lastRoundMinimumRtt;

    private TimeSpan conservativeSlowStartBaselineRtt;

    /// <summary>Gets <c>W_max</c>: the window, in datagrams, just before the last reduction, less fast convergence; zero before any.</summary>
    public double WindowMaximum { get; private set; }

    /// <summary>Gets <c>K</c>: how long, in seconds, the cubic takes from the start of the congestion avoidance epoch to reach <see cref="WindowMaximum" />.</summary>
    public double TimeToWindowMaximum { get; private set; }

    /// <summary>Gets how many HyStart++ rounds have started, the first acknowledgement starting the first; they are counted only in the first slow start.</summary>
    public int HyStartRound { get; private set; }

    /// <summary>Gets the lowest RTT sampled in the current HyStart++ round, or <see langword="null" /> before its first sample.</summary>
    public TimeSpan? CurrentRoundMinimumRtt { get; private set; }

    /// <summary>Gets how many RTT samples the current HyStart++ round has taken: one per acknowledgement.</summary>
    public int CurrentRoundRttSamples { get; private set; }

    /// <summary>Gets which round of Conservative Slow Start is running, from 1, or zero outside it (RFC 9406 section 4.2).</summary>
    public int ConservativeSlowStartRound { get; private set; }

    /// <summary>Returns <c>W_cubic(t)</c> in datagrams (RFC 9438 section 4.2).</summary>
    /// <param name="secondsSinceEpoch">The time since the congestion avoidance epoch started.</param>
    /// <returns>The window the cubic gives.</returns>
    public double CubicWindow(double secondsSinceEpoch) => (Scale * Math.Pow(secondsSinceEpoch - TimeToWindowMaximum, 3)) + WindowMaximum;

    /// <summary>Returns the RTT rise over <paramref name="lastRoundMinimumRtt" /> that ends slow start: an eighth of it, kept between 4 and 16 ms (RFC 9406 section 4.2).</summary>
    /// <param name="lastRoundMinimumRtt">The last round's minimum RTT.</param>
    /// <returns>The threshold.</returns>
    public static TimeSpan RttThreshold(TimeSpan lastRoundMinimumRtt) =>
        TimeSpan.FromTicks(Math.Clamp(lastRoundMinimumRtt.Ticks / MinimumRttDivisor, MinimumRttThreshold.Ticks, MaximumRttThreshold.Ticks));

    /// <inheritdoc />
    protected override void IncreaseWindow(IReadOnlyList<QuicSentPacket> acknowledged, TimeSpan now, QuicRttEstimator rtt)
    {
        if (CongestionWindow < SlowStartThreshold)
        {
            SlowStart(acknowledged, now, rtt.LatestRtt);
            return;
        }

        foreach (var packet in acknowledged)
        {
            AvoidCongestion(packet, now, rtt);
        }
    }

    /// <inheritdoc />
    protected override void ReduceWindow(TimeSpan now)
    {
        var window = (double)CongestionWindow / MaxDatagramSize;

        // Fast convergence: a window that has not regained the last W_max releases bandwidth sooner (section 4.7).
        WindowMaximum = window < WindowMaximum ? window * (1 + Beta) / 2 : window;
        windowBeforeLoss = window;
        epochStart = null;
        applicationLimitedSince = null;
        SlowStartThreshold = Math.Max((long)(CongestionWindow * Beta), MinimumWindow);
        CongestionWindow = SlowStartThreshold;
    }

    /// <inheritdoc />
    protected override void OnPersistentCongestion()
    {
        epochStart = null;
        applicationLimitedSince = null;
    }

    // Slow start grows the window by the acknowledged bytes not sent application-limited, a
    // quarter of them in Conservative Slow Start. HyStart++ runs in the first slow start only,
    // the one that starts with no threshold (RFC 9406 section 4.2), and samples the RTT whether
    // or not the window grew, as ngtcp2 does.
    private void SlowStart(IReadOnlyList<QuicSentPacket> acknowledged, TimeSpan now, TimeSpan latestRtt)
    {
        var growth = acknowledged.Where(packet => !packet.IsApplicationLimited).Sum(packet => (long)packet.SentBytes);
        CongestionWindow += ConservativeSlowStartRound > 0 ? growth / ConservativeSlowStartGrowthDivisor : growth;
        if (SlowStartThreshold == long.MaxValue)
        {
            SampleHyStartRound(acknowledged, now, latestRtt);
        }
    }

    // A round ends when a packet sent after it started is acknowledged; the acknowledgement
    // that ends one starts the next (RFC 9406 section 4.2).
    private void SampleHyStartRound(IReadOnlyList<QuicSentPacket> acknowledged, TimeSpan now, TimeSpan latestRtt)
    {
        if (EndsRound(acknowledged))
        {
            StartRound(now);
        }

        var minimum = LowerRtt(CurrentRoundMinimumRtt, latestRtt);
        CurrentRoundMinimumRtt = minimum;
        CurrentRoundRttSamples++;
        if (ConservativeSlowStartRound > 0)
        {
            ContinueConservativeSlowStart(minimum, now);
        }
        else if (RoseByTheThreshold(minimum))
        {
            conservativeSlowStartBaselineRtt = minimum;
            ConservativeSlowStartRound = 1;
        }
    }

    private bool EndsRound(IReadOnlyList<QuicSentPacket> acknowledged) =>
        roundStart is not { } start || acknowledged.Max(packet => packet.TimeSent) >= start;

    private static TimeSpan LowerRtt(TimeSpan? current, TimeSpan latest) => current is { } rtt && rtt < latest ? rtt : latest;

    // Once the round has N_RTT_SAMPLE samples, its minimum is compared with the last round's (RFC 9406 section 4.2).
    private bool RoseByTheThreshold(TimeSpan minimum) =>
        CurrentRoundRttSamples >= RttSamplesPerRound && lastRoundMinimumRtt is { } last && minimum >= last + RttThreshold(last);

    private void StartRound(TimeSpan now)
    {
        roundStart = now;
        HyStartRound++;
        lastRoundMinimumRtt = CurrentRoundMinimumRtt;
        CurrentRoundMinimumRtt = null;
        CurrentRoundRttSamples = 0;
        ConservativeSlowStartRound += Math.Sign(ConservativeSlowStartRound);
    }

    // An RTT back below the one that started Conservative Slow Start means the rise was
    // spurious, so slow start resumes; its last round hands over to congestion avoidance, with
    // the epoch starting at the window reached (RFC 9406 section 4.2).
    private void ContinueConservativeSlowStart(TimeSpan minimum, TimeSpan now)
    {
        if (minimum < conservativeSlowStartBaselineRtt)
        {
            ConservativeSlowStartRound = 0;
        }
        else if (ConservativeSlowStartRound >= ConservativeSlowStartRounds)
        {
            SlowStartThreshold = CongestionWindow;
            StartEpoch(now, (double)CongestionWindow / MaxDatagramSize);
        }
    }

    // While application-limited the window holds and the cubic's clock stops: the first
    // acknowledgement after moves the epoch on by the time spent limited, as ngtcp2 does.
    private void AvoidCongestion(QuicSentPacket packet, TimeSpan now, QuicRttEstimator rtt)
    {
        if (packet.IsApplicationLimited)
        {
            applicationLimitedSince ??= now;
            return;
        }

        ResumeAfterApplicationLimited(now);
        GrowTowardsTheCubic(packet.SentBytes, now, rtt);
    }

    private void ResumeAfterApplicationLimited(TimeSpan now)
    {
        if (applicationLimitedSince is { } since)
        {
            epochStart += now - since;
            applicationLimitedSince = null;
        }
    }

    private void GrowTowardsTheCubic(int acknowledgedBytes, TimeSpan now, QuicRttEstimator rtt)
    {
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
