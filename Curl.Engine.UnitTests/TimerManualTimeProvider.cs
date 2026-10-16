namespace Curl.Engine;

/// <summary>
/// A <see cref="TimeProvider" /> whose clock and timers move only when a test advances it, so
/// <see cref="Task.Delay(TimeSpan, TimeProvider)" /> completes exactly when the test says.
/// </summary>
internal sealed class TimerManualTimeProvider : TimeProvider
{
    private readonly Lock gate = new();
    private readonly List<ManualTimer> timers = [];
    private long elapsedTicks;

    /// <inheritdoc />
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc />
    public override long GetTimestamp()
    {
        lock (gate)
        {
            return elapsedTicks;
        }
    }

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ManualTimer timer = new(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>Moves the clock forward and fires every timer that falls due, in time order.</summary>
    /// <param name="milliseconds">How far to move it.</param>
    public void Advance(long milliseconds)
    {
        long target;
        lock (gate)
        {
            target = elapsedTicks + TimeSpan.FromMilliseconds(milliseconds).Ticks;
        }

        while (TryTakeDueTimer(target, out ManualTimer? due))
        {
            due.Fire();
        }

        lock (gate)
        {
            elapsedTicks = target;
        }
    }

    private bool TryTakeDueTimer(long target, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ManualTimer? due)
    {
        lock (gate)
        {
            due = timers.Where(timer => timer.DueTicks <= target).OrderBy(timer => timer.DueTicks).FirstOrDefault();
            if (due is null)
            {
                return false;
            }

            elapsedTicks = Math.Max(elapsedTicks, due.DueTicks);
            due.Reschedule();
            return true;
        }
    }

    private sealed class ManualTimer(TimerManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        private long periodTicks;

        public long DueTicks { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner.gate)
            {
                owner.timers.Remove(this);
                periodTicks = period == Timeout.InfiniteTimeSpan ? 0 : period.Ticks;
                if (dueTime != Timeout.InfiniteTimeSpan)
                {
                    DueTicks = owner.elapsedTicks + dueTime.Ticks;
                    owner.timers.Add(this);
                }
            }

            return true;
        }

        public void Reschedule()
        {
            owner.timers.Remove(this);
            if (periodTicks > 0)
            {
                DueTicks += periodTicks;
                owner.timers.Add(this);
            }
        }

        public void Fire() => callback(state);

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
