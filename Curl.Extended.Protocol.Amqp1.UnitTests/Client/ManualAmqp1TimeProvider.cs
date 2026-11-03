namespace Curl.Extended.Protocol.Amqp1.Client;

/// <summary>
/// A <see cref="TimeProvider"/> whose clock moves only when a test advances it, firing every timer due on the way,
/// and which signals each timer created so a test can wait for the code under test to start waiting.
/// </summary>
internal sealed class ManualAmqp1TimeProvider : TimeProvider
{
    private readonly Lock gate = new();

    private readonly List<ManualTimer> timers = [];

    private TimeSpan elapsed;

    /// <summary>Released once for every timer created.</summary>
    public SemaphoreSlim TimerCreated { get; } = new(0);

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp()
    {
        lock (gate)
        {
            return elapsed.Ticks;
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ManualTimer timer;
        lock (gate)
        {
            timer = new(this, callback, state, elapsed + dueTime);
            timers.Add(timer);
        }

        TimerCreated.Release();
        return timer;
    }

    /// <summary>Moves the clock on by <paramref name="duration"/>, firing each timer due by then.</summary>
    public void Advance(TimeSpan duration)
    {
        List<ManualTimer> due;
        lock (gate)
        {
            elapsed += duration;
            due = timers.Where(timer => timer.DueAt <= elapsed).ToList();
            timers.RemoveAll(due.Contains);
        }

        due.ForEach(timer => timer.Fire());
    }

    private void Remove(ManualTimer timer)
    {
        lock (gate)
        {
            timers.Remove(timer);
        }
    }

    private sealed class ManualTimer(ManualAmqp1TimeProvider clock, TimerCallback callback, object? state, TimeSpan dueAt) : ITimer
    {
        public TimeSpan DueAt { get; } = dueAt;

        public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();

        public void Dispose() => clock.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        public void Fire() => callback(state);
    }
}
