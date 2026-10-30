namespace Curl.Extension.WireGuard.UnitTests.IpStack;

/// <summary>A <see cref="TimeProvider" /> whose clock moves only when a test advances it, firing every timer that falls due on the way.</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly Lock timersLock = new();
    private readonly List<ManualTimer> timers = [];
    private TimeSpan elapsed;

    /// <inheritdoc />
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc />
    public override long GetTimestamp()
    {
        lock (timersLock)
        {
            return elapsed.Ticks;
        }
    }

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ManualTimer timer = new(this, callback, state);
        lock (timersLock)
        {
            timers.Add(timer);
        }

        timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>Moves the clock on by <paramref name="duration" />, firing each timer due by then.</summary>
    /// <param name="duration">How far to move the clock.</param>
    public void Advance(TimeSpan duration)
    {
        List<ManualTimer> due;
        lock (timersLock)
        {
            elapsed += duration;
            due = timers.Where(timer => timer.DueAt <= elapsed).ToList();
            due.ForEach(timer => timer.DueAt = null);
        }

        due.ForEach(timer => timer.Fire());
    }

    /// <summary>Fires every timer now, armed or not, as a callback already queued when its timer was stopped would.</summary>
    public void FireEveryTimer()
    {
        List<ManualTimer> all;
        lock (timersLock)
        {
            all = [.. timers];
        }

        all.ForEach(timer => timer.Fire());
    }

    /// <summary>A one-shot timer that fires only when the clock is advanced past it.</summary>
    private sealed class ManualTimer(ManualTimeProvider clock, TimerCallback callback, object? state) : ITimer
    {
        public TimeSpan? DueAt { get; set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (clock.timersLock)
            {
                DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : clock.elapsed + dueTime;
            }

            return true;
        }

        public void Dispose()
        {
            lock (clock.timersLock)
            {
                clock.timers.Remove(this);
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        public void Fire() => callback(state);
    }
}
