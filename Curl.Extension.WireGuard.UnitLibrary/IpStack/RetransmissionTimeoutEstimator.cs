namespace Curl.Extension.WireGuard.IpStack;

/// <summary>
/// The retransmission timeout of RFC 6298: one second until the first round-trip sample, then the smoothed round-trip
/// time plus four times its variation, never under one second nor over sixty, doubled on each timeout.
/// </summary>
public sealed class RetransmissionTimeoutEstimator
{
    /// <summary>The timeout before any round trip is measured (RFC 6298 section 2.1).</summary>
    public static readonly TimeSpan InitialTimeout = TimeSpan.FromSeconds(1);

    /// <summary>The least timeout (RFC 6298 section 2.4).</summary>
    public static readonly TimeSpan MinimumTimeout = TimeSpan.FromSeconds(1);

    /// <summary>The greatest timeout (RFC 6298 section 2.5).</summary>
    public static readonly TimeSpan MaximumTimeout = TimeSpan.FromSeconds(60);

    /// <summary>The clock granularity G.</summary>
    public static readonly TimeSpan ClockGranularity = TimeSpan.FromMilliseconds(1);

    private TimeSpan roundTripTimeVariation;

    /// <summary>How long to wait for an acknowledgement before retransmitting.</summary>
    public TimeSpan Timeout { get; private set; } = InitialTimeout;

    /// <summary>The smoothed round-trip time, or <see langword="null" /> before the first sample.</summary>
    public TimeSpan? SmoothedRoundTripTime { get; private set; }

    /// <summary>Takes one round-trip measurement of a segment that was not retransmitted (Karn's algorithm).</summary>
    /// <param name="roundTripTime">The time from sending the segment to its acknowledgement.</param>
    public void AddSample(TimeSpan roundTripTime)
    {
        if (SmoothedRoundTripTime is not { } smoothed)
        {
            smoothed = roundTripTime;
            roundTripTimeVariation = roundTripTime / 2;
        }
        else
        {
            roundTripTimeVariation = (roundTripTimeVariation * 3 / 4) + ((smoothed - roundTripTime).Duration() / 4);
            smoothed = (smoothed * 7 / 8) + (roundTripTime / 8);
        }

        SmoothedRoundTripTime = smoothed;
        var variation = roundTripTimeVariation * 4;
        Timeout = Clamp(smoothed + (variation > ClockGranularity ? variation : ClockGranularity));
    }

    /// <summary>Doubles the timeout after it expired (RFC 6298 section 5.5).</summary>
    public void BackOff() => Timeout = Clamp(Timeout * 2);

    private static TimeSpan Clamp(TimeSpan timeout) =>
        timeout < MinimumTimeout ? MinimumTimeout : timeout > MaximumTimeout ? MaximumTimeout : timeout;
}
