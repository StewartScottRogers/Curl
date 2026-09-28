namespace Curl.Core.Fakes;

/// <summary>
/// A <see cref="TimeProvider" /> whose clock moves only by <see cref="Advance" /> and whose one
/// timer fires only when a test calls <see cref="Fire" />, whatever its due time and whether or
/// not it was disposed, as a real timer may fire early or run an already-queued callback after
/// disposal. Every due time the timer is created or changed with is recorded in
/// <see cref="DueTimes" />. Hand-written because the solution takes no package beyond MSTest.
/// </summary>
internal sealed class HandFiredTimeProvider : TimeProvider
{
    private readonly List<TimeSpan> dueTimes = [];

    private TimerCallback? callback;

    private object? state;

    private TimeSpan elapsed;

    /// <summary>Gets every due time the timer was created or changed with, in order.</summary>
    public IReadOnlyList<TimeSpan> DueTimes => dueTimes;

    /// <inheritdoc />
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc />
    public override long GetTimestamp() => elapsed.Ticks;

    /// <summary>Moves the clock forward without firing anything.</summary>
    /// <param name="duration">How far to move it.</param>
    public void Advance(TimeSpan duration) => elapsed += duration;

    /// <summary>Runs the timer's callback on the calling thread.</summary>
    public void Fire() => callback!(state);

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        this.callback = callback;
        this.state = state;
        dueTimes.Add(dueTime);
        return new RecordingTimer(dueTimes);
    }

    /// <summary>A timer that only records the due times it is changed to.</summary>
    private sealed class RecordingTimer(List<TimeSpan> dueTimes) : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            dueTimes.Add(dueTime);
            return true;
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
