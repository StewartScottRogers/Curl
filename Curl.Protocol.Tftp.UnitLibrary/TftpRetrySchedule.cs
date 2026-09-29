namespace Curl.Protocol.Tftp;

/// <summary>
/// How often a TFTP transfer re-sends its last packet to a silent server, and how many
/// times, derived from the time the transfer has left the way curl 8.21.0's
/// <c>tftp_set_timeouts</c> derives them.
/// </summary>
/// <param name="retryLimit">The value of <see cref="RetryLimit" />.</param>
/// <param name="retrySeconds">The value of <see cref="RetrySeconds" />.</param>
/// <param name="timeLeftMilliseconds">The value of <see cref="TimeLeftMilliseconds" />.</param>
internal readonly struct TftpRetrySchedule(int retryLimit, int retrySeconds, long timeLeftMilliseconds = 0)
{
    /// <summary>The seconds the schedule is derived from when no time limit applies.</summary>
    private const long UnlimitedTimeoutSeconds = 15;

    /// <summary>The longest time left that is taken as a limit rather than as no limit.</summary>
    private static readonly TimeSpan LongestTimeLeft = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets how many times a request is sent, or an ACK or DATA block re-sent, before
    /// the transfer gives up: the time left in seconds divided by 5, clamped to 3-50.
    /// </summary>
    internal int RetryLimit { get; } = retryLimit;

    /// <summary>
    /// Gets the time left in seconds divided by <see cref="RetryLimit" />, at least 1. It
    /// is the <c>timeout</c> option the read or write request carries.
    /// </summary>
    internal int RetrySeconds { get; } = retrySeconds;

    /// <summary>
    /// Gets how long the transfer waits after a send before re-sending it. curl re-sends
    /// once the whole-second clock is past the last send plus <see cref="RetrySeconds" />,
    /// so the observed interval is one second longer.
    /// </summary>
    internal TimeSpan ResendInterval => TimeSpan.FromSeconds(RetrySeconds + 1);

    /// <summary>
    /// Gets the whole milliseconds left when the schedule was derived, 0 when no limit
    /// applies: the <c>Total</c> curl's <c>set timeouts</c> line reports.
    /// </summary>
    internal long TimeLeftMilliseconds { get; } = timeLeftMilliseconds;

    /// <summary>
    /// Derives the schedule from the time the transfer has left.
    /// </summary>
    /// <param name="timeLeft">
    /// The time left before a limit ends the transfer, or <see langword="null" /> when
    /// none applies.
    /// </param>
    /// <returns>
    /// The schedule for the time left rounded to whole seconds when it is under 1 hour,
    /// otherwise for 15 seconds. Time already run out gives the shortest schedule, 3
    /// sends 2 seconds apart.
    /// </returns>
    internal static TftpRetrySchedule ForTimeLeft(TimeSpan? timeLeft)
    {
        var timeoutSeconds = timeLeft is { } left && left < LongestTimeLeft
            ? ((long)left.TotalMilliseconds + 500) / 1000
            : UnlimitedTimeoutSeconds;
        var retryLimit = (int)Math.Clamp(timeoutSeconds / 5, 3, 50);
        return new TftpRetrySchedule(
            retryLimit,
            (int)Math.Max(1, timeoutSeconds / retryLimit),
            (long)(timeLeft?.TotalMilliseconds ?? 0));
    }
}
