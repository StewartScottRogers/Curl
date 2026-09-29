using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking.Fakes;

/// <summary>
/// A <see cref="TimeProvider" /> whose timestamp moves only when a test advances it, in
/// milliseconds, so no test reads the real clock. A timer it creates fires, on the advancing
/// thread, when an advance reaches its due time. Timers may be created and stopped from any thread.
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];
    private readonly Lock _timersLock = new();

    private long _elapsedMilliseconds;

    /// <inheritdoc />
    public override long TimestampFrequency => 1000;

    /// <inheritdoc />
    public override long GetTimestamp() => _elapsedMilliseconds;

    /// <summary>Gets how many timers are waiting to fire.</summary>
    public int PendingTimerCount
    {
        get
        {
            lock (_timersLock)
            {
                return _timers.Count;
            }
        }
    }

    /// <summary>Gets the timestamp the next timer fires at, or <see langword="null" /> when none is waiting.</summary>
    public long? NextDueAt
    {
        get
        {
            lock (_timersLock)
            {
                return _timers.Count == 0 ? null : _timers.Min(timer => timer.DueAt);
            }
        }
    }

    /// <summary>Moves the timestamp forward and fires every timer it reaches.</summary>
    /// <param name="milliseconds">How far to move it.</param>
    public void Advance(long milliseconds)
    {
        _elapsedMilliseconds += milliseconds;
        ManualTimer[] reached;
        lock (_timersLock)
        {
            reached = _timers.Where(timer => timer.DueAt <= _elapsedMilliseconds).ToArray();
            _timers.RemoveAll(reached.Contains);
        }

        foreach (var due in reached)
        {
            due.Fire();
        }
    }

    /// <inheritdoc />
    /// <remarks>The timer fires once; a period is not supported.</remarks>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, () => callback(state), _elapsedMilliseconds + (long)dueTime.TotalMilliseconds);
        lock (_timersLock)
        {
            _timers.Add(timer);
        }

        return timer;
    }

    /// <summary>One pending timer; disposing it, or changing it, stops it firing.</summary>
    private sealed class ManualTimer(ManualTimeProvider owner, Action fire, long dueAt) : ITimer
    {
        public long DueAt { get; } = dueAt;

        public void Fire() => fire();

        public bool Change(TimeSpan dueTime, TimeSpan period) => Remove();

        public void Dispose() => Remove();

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        private bool Remove()
        {
            lock (owner._timersLock)
            {
                return owner._timers.Remove(this);
            }
        }
    }
}
