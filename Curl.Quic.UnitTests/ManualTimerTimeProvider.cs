namespace Curl.Quic;

/// <summary>
/// A clock that moves only when a test advances it, in whole milliseconds from zero, and whose timers
/// fire only as it reaches their due time, rounded up to a whole millisecond as a real timer never
/// fires early, on the thread that moves it. A connection's loop may create and change timers on its
/// own thread while a test moves the clock on another.
/// </summary>
public sealed class ManualTimerTimeProvider : TimeProvider
{
    private readonly Lock gate = new();

    private readonly List<ManualTimer> timers = [];

    private TaskCompletionSource timerArmed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private long elapsedMilliseconds;

    /// <inheritdoc />
    public override long TimestampFrequency => 1000;

    /// <inheritdoc />
    public override long GetTimestamp()
    {
        lock (gate)
        {
            return elapsedMilliseconds;
        }
    }

    /// <inheritdoc />
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

    /// <summary>Moves the clock on, firing each timer whose due time it passes, once.</summary>
    /// <param name="milliseconds">How far.</param>
    public void Advance(long milliseconds)
    {
        long now;
        ManualTimer[] snapshot;
        lock (gate)
        {
            elapsedMilliseconds += milliseconds;
            now = elapsedMilliseconds;
            snapshot = [.. timers];
        }

        foreach (ManualTimer timer in snapshot)
        {
            timer.FireIfDue(now);
        }
    }

    /// <summary>
    /// Moves the clock on to the earliest due time of the timers waiting to fire and fires them.
    /// Does nothing when no timer waits, so the clock never moves past a due time.
    /// </summary>
    public void AdvanceToNextTimer()
    {
        long? earliest;
        long now;
        lock (gate)
        {
            earliest = timers.Min(timer => timer.DueAt);
            now = elapsedMilliseconds;
        }

        if (earliest is { } due)
        {
            Advance(due - now);
        }
    }

    /// <summary>Returns a task that completes once at least one timer waits to fire; at once when one already does.</summary>
    /// <returns>The task.</returns>
    public Task WaitForPendingTimerAsync()
    {
        lock (gate)
        {
            return timers.Exists(timer => timer.DueAt is not null) ? Task.CompletedTask : timerArmed.Task;
        }
    }

    // Tells whoever waits for a pending timer that one now waits to fire.
    private void OnTimerArmed()
    {
        TaskCompletionSource armed;
        lock (gate)
        {
            armed = timerArmed;
            timerArmed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        armed.SetResult();
    }

    /// <summary>A timer of the manual clock; its period is ignored, as a one-shot timer's.</summary>
    private sealed class ManualTimer(ManualTimerTimeProvider clock, TimerCallback callback, object? state) : ITimer
    {
        private long? dueAt;

        /// <summary>Gets when the timer fires, or <see langword="null" /> while it is not waiting to.</summary>
        public long? DueAt
        {
            get
            {
                lock (clock.gate)
                {
                    return dueAt;
                }
            }
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            bool armed = dueTime != Timeout.InfiniteTimeSpan;
            lock (clock.gate)
            {
                dueAt = armed ? clock.elapsedMilliseconds + (long)Math.Ceiling(dueTime.TotalMilliseconds) : null;
            }

            if (armed)
            {
                clock.OnTimerArmed();
            }

            return true;
        }

        public void FireIfDue(long now)
        {
            lock (clock.gate)
            {
                if (dueAt is not { } due || due > now)
                {
                    return;
                }

                dueAt = null;
            }

            callback(state);
        }

        public void Dispose()
        {
            lock (clock.gate)
            {
                dueAt = null;
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();

            return ValueTask.CompletedTask;
        }
    }
}
