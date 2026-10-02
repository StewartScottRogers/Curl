namespace Curl.Protocol.Mqtt.Fakes;

/// <summary>
/// A <see cref="TimeProvider" /> whose clock moves only when a test advances it, firing every
/// timer that falls due on the way, so no test reads or waits on the real clock. Copied from
/// <c>Curl.Conformance.UnitTests</c>, with a lock because the session creates and cancels its
/// timers on whichever thread its awaits resume on.
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> timers = [];

    private readonly Lock gate = new();

    private TimeSpan now;

    /// <summary>Gets how far the clock has moved since it was created.</summary>
    public TimeSpan Now
    {
        get
        {
            lock (gate)
            {
                return now;
            }
        }
    }

    /// <summary>
    /// Gets when the earliest pending timer falls due, or <see langword="null" /> when none is pending.
    /// </summary>
    public TimeSpan? NextTimerDueAt
    {
        get
        {
            lock (gate)
            {
                return timers.Count == 0 ? null : timers.Min(timer => timer.DueAt);
            }
        }
    }

    /// <inheritdoc />
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc />
    public override long GetTimestamp() => Now.Ticks;

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (gate)
        {
            ManualTimer timer = new(this, callback, state, now + dueTime);
            timers.Add(timer);
            return timer;
        }
    }

    /// <summary>Moves the clock on by <paramref name="duration" />, firing each timer due by then at its due time.</summary>
    /// <param name="duration">How far to move the clock.</param>
    public void Advance(TimeSpan duration)
    {
        TimeSpan end = Now + duration;
        while (TakeNextDue(end) is { } next)
        {
            next.Fire();
        }

        lock (gate)
        {
            now = end;
        }
    }

    private ManualTimer? TakeNextDue(TimeSpan end)
    {
        lock (gate)
        {
            if (timers.Where(timer => timer.DueAt <= end).MinBy(timer => timer.DueAt) is not { } next)
            {
                return null;
            }

            now = next.DueAt;
            timers.Remove(next);
            return next;
        }
    }

    private void Remove(ManualTimer timer)
    {
        lock (gate)
        {
            timers.Remove(timer);
        }
    }

    /// <summary>A one-shot timer that fires only when the clock is advanced past it.</summary>
    private sealed class ManualTimer(ManualTimeProvider clock, TimerCallback callback, object? state, TimeSpan dueAt) : ITimer
    {
        public TimeSpan DueAt { get; } = dueAt;

        public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();

        public void Dispose() => clock.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        public void Fire() => callback(state);
    }
}
