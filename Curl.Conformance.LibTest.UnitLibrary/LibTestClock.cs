namespace Curl.Conformance.LibTest;

/// <summary>
/// libtest's monotonic time over an injected <see cref="TimeProvider"/>: <c>curlx_now</c> (before
/// 8.15, <c>tutil_tvnow</c>) as <see cref="Now"/>, <c>curlx_timediff_ms</c> (<c>tutil_tvdiff</c>) as
/// <see cref="DifferenceMilliseconds"/>, <c>start_test_timing</c>'s <c>tv_test_start</c> as
/// <see cref="StartTestTiming"/> and <see cref="TestStart"/>, and <c>curlx_wait_ms</c> as
/// <see cref="WaitAsync"/>. Nothing sleeps: waits go through the provider's timers.
/// </summary>
/// <param name="timeProvider">Where the time comes from.</param>
public sealed class LibTestClock(TimeProvider timeProvider)
{
    /// <summary><c>TEST_HANG_TIMEOUT</c>: 60 000 ms, after which a program is taken to run forever.</summary>
    public const long TestHangTimeoutMilliseconds = 60 * 1000;

    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary><c>tv_test_start</c>: the timestamp <see cref="StartTestTiming"/> took, 0 before it ran.</summary>
    public long TestStart { get; private set; }

    /// <summary><c>curlx_now()</c>: the provider's current monotonic timestamp.</summary>
    /// <returns>The timestamp, in the provider's <see cref="TimeProvider.TimestampFrequency"/>.</returns>
    public long Now() => timeProvider.GetTimestamp();

    /// <summary><c>curlx_timediff_ms(newer, older)</c>: the whole milliseconds from <paramref name="older"/> to <paramref name="newer"/>, truncated.</summary>
    /// <param name="newer">The later timestamp.</param>
    /// <param name="older">The earlier timestamp.</param>
    /// <returns>The milliseconds between them; negative when <paramref name="newer"/> is earlier.</returns>
    public long DifferenceMilliseconds(long newer, long older) =>
        timeProvider.GetElapsedTime(older, newer).Ticks / TimeSpan.TicksPerMillisecond;

    /// <summary><c>start_test_timing()</c>: <see cref="TestStart"/> becomes <see cref="Now"/>.</summary>
    public void StartTestTiming() => TestStart = Now();

    /// <summary>The whole milliseconds since <see cref="StartTestTiming"/>, as <c>exe_test_timedout</c> measures them.</summary>
    /// <returns>The milliseconds since the test started.</returns>
    public long MillisecondsSinceTestStart() => DifferenceMilliseconds(Now(), TestStart);

    /// <summary><c>curlx_wait_ms(ms)</c>: waits <paramref name="milliseconds"/> on the provider's timer; 0 or less returns at once.</summary>
    /// <param name="milliseconds">How long to wait.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>A task that completes when the time has passed.</returns>
    public Task WaitAsync(int milliseconds, CancellationToken cancellationToken) =>
        milliseconds <= 0
            ? Task.CompletedTask
            : Task.Delay(TimeSpan.FromMilliseconds(milliseconds), timeProvider, cancellationToken);
}
