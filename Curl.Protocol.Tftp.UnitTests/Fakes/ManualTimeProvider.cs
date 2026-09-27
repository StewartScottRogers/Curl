namespace Curl.Protocol.Tftp.Fakes;

/// <summary>
/// A <see cref="TimeProvider" /> whose clock moves only when a test, or a fake acting for
/// the network, jumps it to the next timer due, so no test reads or waits on the real
/// clock.
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> timers = [];

    /// <summary>
    /// Gets how far the clock has moved since it was created.
    /// </summary>
    public TimeSpan Now { get; private set; }

    /// <inheritdoc />
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc />
    public override long GetTimestamp() => Now.Ticks;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch + Now;

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>
    /// Moves the clock to the earliest timer due and fires it.
    /// </summary>
    /// <returns><see langword="false" /> when no timer is due, so the clock did not move.</returns>
    public bool AdvanceToNextTimer()
    {
        var next = timers.Where(timer => timer.DueAt is not null).MinBy(timer => timer.DueAt);
        if (next is null)
        {
            return false;
        }

        Now = next.DueAt!.Value;
        next.Fire();
        return true;
    }

    /// <summary>A timer that fires only when the clock is moved to it.</summary>
    private sealed class ManualTimer(ManualTimeProvider clock, TimerCallback callback, object? state) : ITimer
    {
        private TimeSpan period = Timeout.InfiniteTimeSpan;

        /// <summary>Gets when the timer fires next, or <see langword="null" /> when it will not.</summary>
        internal TimeSpan? DueAt { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            this.period = period;
            DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : clock.Now + dueTime;
            if (!clock.timers.Contains(this))
            {
                clock.timers.Add(this);
            }

            return true;
        }

        public void Dispose() => clock.timers.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        internal void Fire()
        {
            DueAt = period == Timeout.InfiniteTimeSpan ? null : clock.Now + period;
            callback(state);
        }
    }
}
