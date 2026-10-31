namespace Curl.Extended.Protocol.Coap.Dtls.Handshake;

/// <summary>A clock whose timers never fire, so a handshake test sees no retransmission it did not cause.</summary>
internal sealed class SilentTimeProvider : TimeProvider
{
    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => new SilentTimer();

    private sealed class SilentTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
