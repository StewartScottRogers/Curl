namespace Curl.Extended.Protocol.SocketIo;

/// <summary>A clock that stands still until a test advances it, firing each timer whose time has come.</summary>
internal sealed class ManualClock : TimeProvider
{
    private readonly List<ManualTimer> timers = [];

    private TimeSpan now;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        lock (timers)
        {
            timers.Add(timer);
        }

        return timer;
    }

    /// <summary>Moves the clock on and fires every timer now due.</summary>
    public void Advance(TimeSpan by)
    {
        ManualTimer[] due;
        lock (timers)
        {
            now += by;
            due = [.. timers.Where(timer => timer.DueAt is { } at && at <= now)];
            foreach (ManualTimer timer in due)
            {
                timer.DueAt = null;
            }
        }

        foreach (ManualTimer timer in due)
        {
            timer.Fire();
        }
    }

    private sealed class ManualTimer(ManualClock clock, TimerCallback callback, object? state) : ITimer
    {
        public TimeSpan? DueAt { get; set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (clock.timers)
            {
                DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : clock.now + dueTime;
            }

            return true;
        }

        public void Fire() => callback(state);

        public void Dispose() => Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
