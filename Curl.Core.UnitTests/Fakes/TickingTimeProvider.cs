namespace Curl.Core.Fakes;

/// <summary>
/// A <see cref="TimeProvider" /> whose clock moves only when a test calls <see cref="Tick" />,
/// which moves it by one timer period and runs every timer's callback on the calling thread,
/// disposed or not, as a real timer's already-queued callback still runs after disposal.
/// Hand-written because the solution takes no package beyond MSTest.
/// </summary>
internal sealed class TickingTimeProvider : TimeProvider
{
    private readonly List<(TimerCallback Callback, object? State, TimeSpan Period)> timers = [];

    private TimeSpan elapsed;

    /// <inheritdoc />
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc />
    public override long GetTimestamp() => elapsed.Ticks;

    /// <inheritdoc />
    /// <remarks>Records the timer; <see cref="Tick" /> fires it.</remarks>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        timers.Add((callback, state, period));
        return new InertTimer();
    }

    /// <summary>Moves the clock by the first timer's period and fires every timer.</summary>
    /// <param name="count">How many ticks to take.</param>
    public void Tick(int count = 1)
    {
        for (int tick = 0; tick < count; tick++)
        {
            elapsed += timers[0].Period;
            foreach ((TimerCallback callback, object? state, TimeSpan _) in timers.ToArray())
            {
                callback(state);
            }
        }
    }

    /// <summary>A timer whose changes and disposal do nothing; <see cref="Tick" /> drives it.</summary>
    private sealed class InertTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => false;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
