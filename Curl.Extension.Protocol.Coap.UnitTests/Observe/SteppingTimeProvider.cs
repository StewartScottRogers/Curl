namespace Curl.Extension.Protocol.Coap.Observe;

/// <summary>
/// A fake clock whose time moves on by a fixed step each time it is read, and whose timers never fire, so a test
/// decides how far apart notifications arrive and never sees a retransmission.
/// </summary>
/// <param name="step">How far the time moves on at each read.</param>
internal sealed class SteppingTimeProvider(TimeSpan step) : TimeProvider
{
    private DateTimeOffset now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow()
    {
        now += step;
        return now;
    }

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => new NoTimer();

    private sealed class NoTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
