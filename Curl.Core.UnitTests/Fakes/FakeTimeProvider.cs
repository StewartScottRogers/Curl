namespace Curl.Core.Fakes;

/// <summary>
/// A <see cref="TimeProvider" /> that never reads the real clock: each timer created moves
/// the clock straight to its due time and fires on the thread pool, and its due time is
/// recorded in <see cref="Waits" />, so a test pins every wait without sleeping.
/// Hand-written because the solution takes no package beyond MSTest.
/// </summary>
/// <param name="start">The instant the clock starts at.</param>
internal sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
{
    private readonly Lock gate = new();

    private readonly List<TimeSpan> waits = [];

    private TimeSpan elapsed;

    /// <summary>Gets the due time of every timer created, in order.</summary>
    public IReadOnlyList<TimeSpan> Waits
    {
        get
        {
            lock (gate)
            {
                return [.. waits];
            }
        }
    }

    /// <summary>
    /// Moves the clock forward without a timer, as time spent inside an operation does;
    /// <see cref="Waits" /> does not record it.
    /// </summary>
    /// <param name="duration">How far to move the clock.</param>
    public void Advance(TimeSpan duration)
    {
        lock (gate)
        {
            elapsed += duration;
        }
    }

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow()
    {
        lock (gate)
        {
            return start + elapsed;
        }
    }

    /// <inheritdoc />
    /// <remarks>One tick per <see cref="TimeSpan" /> tick.</remarks>
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc />
    /// <remarks>The ticks the clock has moved since <c>start</c>, so elapsed time follows the timers.</remarks>
    public override long GetTimestamp()
    {
        lock (gate)
        {
            return elapsed.Ticks;
        }
    }

    /// <inheritdoc />
    /// <remarks>The timer fires once, at once; a period is not supported.</remarks>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (gate)
        {
            waits.Add(dueTime);
            elapsed += dueTime;
        }

        ThreadPool.QueueUserWorkItem(_ => callback(state));
        return new FiredTimer();
    }

    /// <summary>A timer that has already fired; changing or disposing it does nothing.</summary>
    private sealed class FiredTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => false;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
