namespace Curl.Extension.Dane.Lookup;

/// <summary>
/// A clock that stands still at <see cref="Dnssec.DnssecTestRecords.GeneratedValidAt" /> until a test advances it,
/// firing the timers that fall due.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> timers = [];

    private DateTimeOffset now = Dnssec.DnssecTestRecords.GeneratedValidAt;

    public override DateTimeOffset GetUtcNow() => now;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ManualTimer timer = new(callback, state, now + dueTime);
        lock (timers)
        {
            timers.Add(timer);
        }

        return timer;
    }

    public void Advance(TimeSpan by)
    {
        now += by;
        ManualTimer[] due;
        lock (timers)
        {
            due = [.. timers.Where(timer => !timer.IsStopped && timer.DueAt <= now)];
        }

        foreach (ManualTimer timer in due)
        {
            timer.Fire();
        }
    }

    private sealed class ManualTimer(TimerCallback callback, object? state, DateTimeOffset dueAt) : ITimer
    {
        public DateTimeOffset DueAt => dueAt;

        public bool IsStopped { get; private set; }

        public void Fire()
        {
            IsStopped = true;
            callback(state);
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            IsStopped = true;
            return true;
        }

        public void Dispose() => IsStopped = true;

        public ValueTask DisposeAsync()
        {
            IsStopped = true;
            return ValueTask.CompletedTask;
        }
    }
}
