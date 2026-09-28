namespace Curl.Protocol.Ftp.Fakes;

/// <summary>
/// A <see cref="TimeProvider" /> whose timers fire as soon as they are created, so curl's
/// 60-second active-mode accept timeout runs out at once; it records the due time of every
/// timer it fired.
/// </summary>
public sealed class ImmediateTimerTimeProvider : TimeProvider
{
    /// <summary>Gets the due time of every timer fired, in order.</summary>
    public List<TimeSpan> Waits { get; } = [];

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        Waits.Add(dueTime);
        callback(state);
        return new InertTimer();
    }

    /// <summary>A timer that fires no more, whatever it is changed to.</summary>
    private sealed class InertTimer : ITimer
    {
        /// <inheritdoc />
        public bool Change(TimeSpan dueTime, TimeSpan period) => false;

        /// <inheritdoc />
        public void Dispose()
        {
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
