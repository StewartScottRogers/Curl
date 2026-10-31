using System.Collections.Concurrent;

namespace Curl.Extension.Protocol.DnsQuery.UnitTests.Transport;

/// <summary>
/// A fake clock whose every timer fires at once on the thread pool, recording the time it was set for,
/// so a test sees each timeout without waiting for it.
/// </summary>
internal sealed class ImmediateTimeProvider : TimeProvider
{
    private readonly ConcurrentQueue<TimeSpan> dueTimes = new();

    /// <summary>Gets each timer's due time, in the order the timers were created.</summary>
    public IReadOnlyList<TimeSpan> DueTimes => [.. dueTimes];

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        dueTimes.Enqueue(dueTime);
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
