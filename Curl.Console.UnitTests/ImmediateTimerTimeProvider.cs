namespace Curl.Console;

/// <summary>
/// A <see cref="TimeProvider" /> whose short timers fire as soon as they are created, moving
/// its timestamp forward by their due time, so a <c>--retry</c> or <c>--limit-rate</c> wait
/// takes no real time; it records every wait it fired. A timer due in
/// <see cref="NeverFiresFrom" /> or later, such as curl's 300-second connect timeout, never
/// fires, so it cannot end a transfer the test lets finish.
/// </summary>
public sealed class ImmediateTimerTimeProvider : TimeProvider
{
    /// <summary>The due time from which a timer never fires: one minute.</summary>
    public static readonly TimeSpan NeverFiresFrom = TimeSpan.FromMinutes(1);

    private readonly List<TimeSpan> waits = [];

    private long elapsedMilliseconds;

    /// <summary>Gets the due time of every timer fired, in order.</summary>
    public IReadOnlyList<TimeSpan> Waits => waits;

    /// <inheritdoc />
    public override long TimestampFrequency => 1000;

    /// <inheritdoc />
    public override long GetTimestamp() => elapsedMilliseconds;

    /// <inheritdoc />
    /// <remarks>
    /// A timer due before <see cref="NeverFiresFrom" /> moves the timestamp forward by
    /// <paramref name="dueTime" />, then calls <paramref name="callback" /> once; a later one
    /// does nothing.
    /// </remarks>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        if (dueTime < NeverFiresFrom)
        {
            waits.Add(dueTime);
            elapsedMilliseconds += (long)dueTime.TotalMilliseconds;
            callback(state);
        }

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
