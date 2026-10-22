namespace Curl.Conformance.UnitProgram.Limits;

/// <summary>
/// A clock a unit program moves by hand, as the C programs set <c>struct curltime now</c> themselves: its
/// timestamps are ticks from 0, and <see cref="Step"/> moves it on and fires every timer still alive once,
/// each step standing for one period of every timer it made.
/// </summary>
public sealed class SteppedTimeProvider : TimeProvider
{
    private readonly List<SteppedTimer> timers = [];

    private long ticks;

    /// <inheritdoc />
    /// <remarks><see cref="TimeSpan.TicksPerSecond"/>: a timestamp is a count of ticks.</remarks>
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc />
    public override long GetTimestamp() => ticks;

    /// <inheritdoc />
    /// <remarks>The timer fires on every <see cref="Step"/> until it is disposed; its due time and period are not read.</remarks>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);
        SteppedTimer timer = new(this, callback, state);
        timers.Add(timer);
        return timer;
    }

    /// <summary>Moves the clock on by <paramref name="elapsed"/>, then fires every timer still alive once, oldest first.</summary>
    /// <param name="elapsed">How far the clock moves.</param>
    public void Step(TimeSpan elapsed)
    {
        ticks += elapsed.Ticks;
        foreach (SteppedTimer timer in timers.ToArray())
        {
            timer.Fire();
        }
    }

    /// <summary>One timer of the clock: fires on each step until disposed.</summary>
    private sealed class SteppedTimer(SteppedTimeProvider clock, TimerCallback callback, object? state) : ITimer
    {
        public void Fire() => callback(state);

        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose() => clock.timers.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
