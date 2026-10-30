using System.Collections.Concurrent;

namespace Curl.Extension.RateLimit.UnitTests;

/// <summary>
/// A fake clock that starts at <paramref name="start" />, moves forward by each timer's due time as the timer is
/// created and fires the timer at once on the thread pool, recording the due time, so a test sees each wait without
/// waiting for it.
/// </summary>
internal sealed class SteppingTimeProvider(DateTimeOffset start) : TimeProvider
{
    private readonly ConcurrentQueue<TimeSpan> dueTimes = new();

    private DateTimeOffset now = start;

    /// <summary>Gets each timer's due time, in the order the timers were created.</summary>
    public IReadOnlyList<TimeSpan> DueTimes => [.. dueTimes];

    /// <summary>Moves the clock forward.</summary>
    /// <param name="by">How far.</param>
    public void Advance(TimeSpan by) => now += by;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => now;

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        dueTimes.Enqueue(dueTime);
        Advance(dueTime);
        ThreadPool.QueueUserWorkItem(_ => callback(state));
        return new NoTimer();
    }

    private sealed class NoTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
