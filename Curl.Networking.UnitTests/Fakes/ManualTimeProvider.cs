using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking.Fakes;

/// <summary>
/// A <see cref="TimeProvider" /> whose timestamp moves only when a test advances it, in
/// milliseconds, so no test reads the real clock. A timer it creates fires, on the advancing
/// thread, when an advance reaches its due time.
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];

    private long _elapsedMilliseconds;

    /// <inheritdoc />
    public override long TimestampFrequency => 1000;

    /// <inheritdoc />
    public override long GetTimestamp() => _elapsedMilliseconds;

    /// <summary>Moves the timestamp forward and fires every timer it reaches.</summary>
    /// <param name="milliseconds">How far to move it.</param>
    public void Advance(long milliseconds)
    {
        _elapsedMilliseconds += milliseconds;
        foreach (var due in _timers.Where(timer => timer.DueAt <= _elapsedMilliseconds).ToArray())
        {
            _timers.Remove(due);
            due.Fire();
        }
    }

    /// <inheritdoc />
    /// <remarks>The timer fires once; a period is not supported.</remarks>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, () => callback(state), _elapsedMilliseconds + (long)dueTime.TotalMilliseconds);
        _timers.Add(timer);
        return timer;
    }

    /// <summary>One pending timer; disposing it, or changing it, stops it firing.</summary>
    private sealed class ManualTimer(ManualTimeProvider owner, Action fire, long dueAt) : ITimer
    {
        public long DueAt { get; } = dueAt;

        public void Fire() => fire();

        public bool Change(TimeSpan dueTime, TimeSpan period) => owner._timers.Remove(this);

        public void Dispose() => owner._timers.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
