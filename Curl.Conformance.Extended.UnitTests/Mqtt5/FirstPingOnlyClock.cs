namespace Curl.Conformance.Extended.UnitTests.Mqtt5;

/// <summary>
/// A clock that reads the system time but fires only the first timer due after <paramref name="pingInterval" />, at once,
/// and never fires any other timer. A subscriber's first PINGREQ then goes out without a real wait and no second one
/// falls due however slowly the broker's answer arrives, so a case pinning one ping cannot race the Keep Alive
/// timeout on a loaded runner (BL-4847).
/// </summary>
/// <param name="pingInterval">The delay the client waits before each PINGREQ: half the Server Keep Alive.</param>
internal sealed class FirstPingOnlyClock(TimeSpan pingInterval) : TimeProvider
{
    private int pingTimersCreated;

    public override long TimestampFrequency => System.TimestampFrequency;

    public override long GetTimestamp() => System.GetTimestamp();

    public override DateTimeOffset GetUtcNow() => System.GetUtcNow();

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        bool isFirstPing = dueTime == pingInterval && Interlocked.Increment(ref pingTimersCreated) == 1;
        return System.CreateTimer(callback, state, isFirstPing ? TimeSpan.Zero : Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }
}
