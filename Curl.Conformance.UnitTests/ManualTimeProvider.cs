namespace Curl.Conformance;

/// <summary>
/// A <see cref="TimeProvider"/> whose clock moves only when a test advances it, firing every
/// timer that falls due on the way, so no test reads or waits on the real clock.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> timers = [];

    /// <summary>How far the clock has moved since it was created.</summary>
    public TimeSpan Now { get; private set; }

    /// <inheritdoc />
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc />
    public override long GetTimestamp() => Now.Ticks;

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ManualTimer timer = new(this, callback, state, Now + dueTime);
        timers.Add(timer);
        return timer;
    }

    /// <summary>Moves the clock on by <paramref name="duration"/>, firing each timer due by then at its due time.</summary>
    /// <param name="duration">How far to move the clock.</param>
    public void Advance(TimeSpan duration)
    {
        TimeSpan end = Now + duration;
        while (timers.Where(timer => timer.DueAt <= end).MinBy(timer => timer.DueAt) is { } next)
        {
            Now = next.DueAt;
            timers.Remove(next);
            next.Fire();
        }

        Now = end;
    }

    /// <summary>A one-shot timer that fires only when the clock is advanced past it.</summary>
    private sealed class ManualTimer(ManualTimeProvider clock, TimerCallback callback, object? state, TimeSpan dueAt) : ITimer
    {
        public TimeSpan DueAt { get; } = dueAt;

        public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();

        public void Dispose() => clock.timers.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        public void Fire() => callback(state);
    }
}
