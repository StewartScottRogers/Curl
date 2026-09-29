namespace Curl.Console;

/// <summary>
/// A clock that moves only when a test advances it, in whole milliseconds from zero, and whose timers
/// fire only as it moves past their due time, on the test's own thread.
/// </summary>
public sealed class ManualTimerTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> timers = [];

    private long elapsedMilliseconds;

    /// <inheritdoc />
    public override long TimestampFrequency => 1000;

    /// <inheritdoc />
    public override long GetTimestamp() => elapsedMilliseconds;

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ManualTimer timer = new(this, callback, state);
        timer.Change(dueTime, period);
        timers.Add(timer);

        return timer;
    }

    /// <summary>Moves the clock on, firing each timer whose due time it passes, once.</summary>
    /// <param name="milliseconds">How far.</param>
    public void Advance(long milliseconds)
    {
        elapsedMilliseconds += milliseconds;
        foreach (ManualTimer timer in timers.ToArray())
        {
            timer.FireIfDue(elapsedMilliseconds);
        }
    }

    /// <summary>A timer of the manual clock; its period is ignored, as a one-shot timer's.</summary>
    private sealed class ManualTimer(ManualTimerTimeProvider clock, TimerCallback callback, object? state) : ITimer
    {
        private long? dueAt;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            dueAt = dueTime == Timeout.InfiniteTimeSpan ? null : clock.elapsedMilliseconds + (long)dueTime.TotalMilliseconds;

            return true;
        }

        public void FireIfDue(long now)
        {
            if (dueAt is { } due && due <= now)
            {
                dueAt = null;
                callback(state);
            }
        }

        public void Dispose() => dueAt = null;

        public ValueTask DisposeAsync()
        {
            Dispose();

            return ValueTask.CompletedTask;
        }
    }
}
