namespace Curl.Console;

/// <summary>
/// A <see cref="TimeProvider" /> whose clock moves only when a test calls <see cref="Advance" />,
/// which fires, on the calling thread and in due order, every timer that falls due, running a
/// periodic one once for each period passed. No test reads the real clock or sleeps.
/// </summary>
public sealed class SteppingTimeProvider : TimeProvider
{
    private readonly Lock gate = new();

    private readonly List<SteppingTimer> timers = [];

    private TimeSpan elapsed;

    /// <inheritdoc />
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc />
    public override long GetTimestamp()
    {
        lock (gate)
        {
            return elapsed.Ticks;
        }
    }

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        SteppingTimer timer = new(this, callback, state);
        timer.Change(dueTime, period);
        lock (gate)
        {
            timers.Add(timer);
        }

        return timer;
    }

    /// <summary>Moves the clock forward, firing every timer that falls due on the way.</summary>
    /// <param name="duration">How far to move the clock.</param>
    public void Advance(TimeSpan duration)
    {
        TimeSpan until;
        lock (gate)
        {
            until = elapsed + duration;
        }

        while (NextDue(until) is { } timer)
        {
            timer.Fire();
        }

        lock (gate)
        {
            elapsed = until;
        }
    }

    private SteppingTimer? NextDue(TimeSpan until)
    {
        lock (gate)
        {
            SteppingTimer? next = timers.Where(timer => timer.DueAt <= until).MinBy(timer => timer.DueAt);
            if (next is not null)
            {
                elapsed = next.DueAt;
            }

            return next;
        }
    }

    private sealed class SteppingTimer(SteppingTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        private TimeSpan period = Timeout.InfiniteTimeSpan;

        internal TimeSpan DueAt { get; private set; } = TimeSpan.MaxValue;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            this.period = period;
            DueAt = dueTime == Timeout.InfiniteTimeSpan ? TimeSpan.MaxValue : owner.elapsed + dueTime;
            return true;
        }

        public void Dispose()
        {
            lock (owner.gate)
            {
                owner.timers.Remove(this);
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        internal void Fire()
        {
            DueAt = period == Timeout.InfiniteTimeSpan || period == TimeSpan.Zero ? TimeSpan.MaxValue : DueAt + period;
            callback(state);
        }
    }
}
