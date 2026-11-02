namespace Curl.Conformance.Extended.UnitTests.Coap.Messaging;

/// <summary>A clock that moves only when <see cref="JumpToNextTimer" /> moves it to the earliest pending timer and fires that timer.</summary>
internal sealed class TimerJumpingClock : TimeProvider
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);
    private readonly Lock gate = new();
    private readonly List<JumpTimer> timers = [];
    private long elapsedTicks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp()
    {
        lock (gate)
        {
            return elapsedTicks;
        }
    }

    public override DateTimeOffset GetUtcNow() => Start.AddTicks(GetTimestamp());

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        JumpTimer timer = new(this, callback, state);
        lock (gate)
        {
            timers.Add(timer);
        }

        timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>Moves the clock to the earliest pending timer and fires it.</summary>
    /// <returns><see langword="false" /> when no timer is pending.</returns>
    public bool JumpToNextTimer()
    {
        JumpTimer? next;
        lock (gate)
        {
            next = timers.Where(timer => timer.DueAt != long.MaxValue).OrderBy(timer => timer.DueAt).FirstOrDefault();
            if (next is null)
            {
                return false;
            }

            elapsedTicks = Math.Max(elapsedTicks, next.DueAt);
        }

        next.Fire();
        return true;
    }

    private sealed class JumpTimer(TimerJumpingClock owner, TimerCallback callback, object? state) : ITimer
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
