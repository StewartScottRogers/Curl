namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// A <see cref="TimeProvider" /> whose clock moves only when a test calls
/// <see cref="Advance(TimeSpan)" />, so no test reads the real clock. Its timers, and so
/// <see cref="Task.Delay(TimeSpan, TimeProvider, CancellationToken)" />, fire only when
/// <see cref="Advance(TimeSpan)" /> moves the clock past them. Hand-written because the
/// solution takes no package beyond MSTest.
/// </summary>
/// <param name="start">The instant the clock starts at.</param>
public sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
{
    private readonly Lock gate = new();

    private readonly List<FakeTimer> timers = [];

    private readonly TaskCompletionSource timerCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private TimeSpan elapsed;

    /// <inheritdoc />
    /// <remarks>One timestamp tick is one <see cref="TimeSpan" /> tick.</remarks>
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <summary>
    /// Gets a task that completes when the first timer is created, so a test can wait until
    /// the code under test has started a wait before it moves the clock.
    /// </summary>
    public Task FirstTimerCreated => timerCreated.Task;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => start + elapsed;

    /// <inheritdoc />
    public override long GetTimestamp() => elapsed.Ticks;

    /// <inheritdoc />
    /// <remarks>The timer fires once per due time; a period is not supported.</remarks>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);

        FakeTimer timer = new(this, callback, state);
        timer.Change(dueTime, period);
        lock (gate)
        {
            timers.Add(timer);
        }

        timerCreated.TrySetResult();
        return timer;
    }

    /// <summary>
    /// Moves the clock forward and fires every timer that has come due, in the order they are
    /// due.
    /// </summary>
    /// <param name="duration">How far to move it; zero or more.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="duration" /> is negative.</exception>
    public void Advance(TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);
        FakeTimer[] due;
        lock (gate)
        {
            elapsed += duration;
            due = [.. timers.Where(timer => timer.DueAt <= elapsed).OrderBy(timer => timer.DueAt)];
            timers.RemoveAll(due.Contains);
        }

        foreach (FakeTimer timer in due)
        {
            timer.Fire();
        }
    }

    private void Remove(FakeTimer timer)
    {
        lock (gate)
        {
            timers.Remove(timer);
        }
    }

    /// <summary>
    /// One timer: due at an instant on the fake clock, or never.
    /// </summary>
    private sealed class FakeTimer(FakeTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        /// <summary>
        /// Gets the elapsed time the timer fires at, <see cref="TimeSpan.MaxValue" /> when never.
        /// </summary>
        internal TimeSpan DueAt { get; private set; } = TimeSpan.MaxValue;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            DueAt = dueTime == Timeout.InfiniteTimeSpan ? TimeSpan.MaxValue : owner.elapsed + dueTime;
            return true;
        }

        public void Dispose() => owner.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        internal void Fire() => callback(state);
    }
}
