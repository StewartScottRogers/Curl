namespace Curl.Protocol.Ftp.Fakes;

/// <summary>
/// A clock that stands still until <see cref="Advance(TimeSpan)" /> moves it, firing each timer
/// that falls due on the way at its due time, so a test decides to the tick when a limit
/// passes. <see cref="FireEveryTimerNow" /> fires every armed timer early, as a real timer
/// can, without moving the clock.
/// </summary>
public sealed class SteppingTimeProvider : TimeProvider
{
    private readonly Lock gate = new();

    private readonly List<SteppingTimer> timers = [];

    private TimeSpan elapsed;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp()
    {
        lock (gate)
        {
            return elapsed.Ticks;
        }
    }

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

    /// <summary>Moves the clock on by <paramref name="duration" />, firing each timer due on the way.</summary>
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

    /// <summary>Fires every armed timer once, whatever its due time, leaving the clock where it is.</summary>
    public void FireEveryTimerNow()
    {
        SteppingTimer[] armed;
        lock (gate)
        {
            armed = [.. timers.Where(timer => timer.DueAt != TimeSpan.MaxValue)];
        }

        foreach (SteppingTimer timer in armed)
        {
            timer.Fire();
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
        internal TimeSpan DueAt { get; private set; } = TimeSpan.MaxValue;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
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
            DueAt = TimeSpan.MaxValue;
            callback(state);
        }
    }
}
