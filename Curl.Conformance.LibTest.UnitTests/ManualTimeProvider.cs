namespace Curl.Conformance.LibTest.UnitTests;

/// <summary>
/// A <see cref="TimeProvider"/> whose clock moves only when a test advances it, firing every timer
/// that falls due on the way, and whose local time zone is UTC, so no test reads the real clock.
/// </summary>
internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private readonly List<ManualTimer> timers = [];

    /// <summary>How far the clock has moved since it was created.</summary>
    public TimeSpan Elapsed { get; private set; }

    /// <inheritdoc />
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    /// <inheritdoc />
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc />
    public override long GetTimestamp() => Elapsed.Ticks;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => start + Elapsed;

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ManualTimer timer = new(this, callback, state, Elapsed + dueTime);
        timers.Add(timer);
        return timer;
    }

    /// <summary>Moves the clock on by <paramref name="duration"/>, firing each timer due by then.</summary>
    /// <param name="duration">How far to move the clock.</param>
    public void Advance(TimeSpan duration)
    {
        Elapsed += duration;
        foreach (ManualTimer due in timers.Where(timer => timer.DueAt <= Elapsed).ToList())
        {
            timers.Remove(due);
            due.Fire();
        }
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
