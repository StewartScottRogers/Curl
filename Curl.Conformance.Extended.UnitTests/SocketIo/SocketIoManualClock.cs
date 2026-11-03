namespace Curl.Conformance.Extended.UnitTests.SocketIo;

/// <summary>A clock that moves only when a case advances it, firing every timer that falls due on the way.</summary>
internal sealed class SocketIoManualClock : TimeProvider
{
    private readonly Lock gate = new();

    private readonly List<ManualTimer> timers = [];

    private long elapsedTicks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp()
    {
        lock (gate)
        {
            return elapsedTicks;
        }
    }

    public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero).AddTicks(GetTimestamp());

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ManualTimer timer = new(this, callback, state);
        lock (gate)
        {
            timers.Add(timer);
        }

        timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>Moves the clock forward by <paramref name="by" />, firing each timer due by then in order.</summary>
    public void Advance(TimeSpan by)
    {
        long target = GetTimestamp() + by.Ticks;
        while (NextDue(target) is { } due)
        {
            lock (gate)
            {
                elapsedTicks = Math.Max(elapsedTicks, due.DueAt);
            }

            due.Fire();
        }

        lock (gate)
        {
            elapsedTicks = target;
        }
    }

    private ManualTimer? NextDue(long target)
    {
        lock (gate)
        {
            return timers.Where(timer => timer.DueAt <= target).OrderBy(timer => timer.DueAt).FirstOrDefault();
        }
    }

    private sealed class ManualTimer(SocketIoManualClock owner, TimerCallback callback, object? state) : ITimer
    {
        private long period = -1;

        public long DueAt { get; private set; } = long.MaxValue;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner.gate)
            {
                DueAt = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : owner.elapsedTicks + dueTime.Ticks;
                this.period = period == Timeout.InfiniteTimeSpan || period == TimeSpan.Zero ? -1 : period.Ticks;
            }

            return true;
        }

        public void Fire()
        {
            lock (owner.gate)
            {
                DueAt = period < 0 ? long.MaxValue : DueAt + period;
            }

            callback(state);
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
    }
}
