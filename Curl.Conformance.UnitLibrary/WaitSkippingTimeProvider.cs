namespace Curl.Conformance;

/// <summary>
/// A <see cref="TimeProvider"/> whose clock stands still until something waits on it: each timer
/// made from it moves the clock on by its due time and fires at once, on the thread pool. The
/// emulated server's waits (<c>writedelay</c>, the <c>&lt;postcmd&gt;</c> <c>wait</c>) keep their
/// order and their effect on what it sends - each delayed write still reaches curl in a read of
/// its own - and take no real time, so no case's run grows with the machine's load (BL-1355).
/// </summary>
/// <remarks>
/// Only for a case in which nothing on curl's side is timed (<see cref="CurlTimerOptions"/>): a
/// skipped wait would otherwise lose a race against curl's real clock that upstream's server wins.
/// </remarks>
public sealed class WaitSkippingTimeProvider : TimeProvider
{
    private readonly Lock clockLock = new();

    private TimeSpan now;

    /// <inheritdoc />
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc />
    public override long GetTimestamp()
    {
        lock (clockLock)
        {
            return now.Ticks;
        }
    }

    /// <summary>
    /// Moves the clock on by <paramref name="dueTime"/> and fires <paramref name="callback"/> at
    /// once, on the thread pool; a timer due at <see cref="Timeout.InfiniteTimeSpan"/> never fires.
    /// </summary>
    /// <param name="callback">What the timer calls when it fires.</param>
    /// <param name="state">What the timer passes to <paramref name="callback"/>.</param>
    /// <param name="dueTime">How far the clock moves before the timer fires.</param>
    /// <param name="period">Must be <see cref="Timeout.InfiniteTimeSpan"/>: a timer fires once.</param>
    /// <returns>A timer that has already fired, or never will, and cannot be changed.</returns>
    /// <exception cref="NotSupportedException"><paramref name="period"/> asks for a repeating timer.</exception>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (period != Timeout.InfiniteTimeSpan)
        {
            throw new NotSupportedException("A timer on a wait-skipping clock fires once; it cannot repeat.");
        }

        if (dueTime != Timeout.InfiniteTimeSpan)
        {
            lock (clockLock)
            {
                now += dueTime;
            }

            ThreadPool.UnsafeQueueUserWorkItem(_ => callback(state), null);
        }

        return new SkippedTimer();
    }

    private sealed class SkippedTimer : ITimer
    {
        // The timer fired, or never will, as it was made; it does not move.
        public bool Change(TimeSpan dueTime, TimeSpan period) => false;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
