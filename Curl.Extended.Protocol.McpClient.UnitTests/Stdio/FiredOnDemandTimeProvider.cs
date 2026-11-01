namespace Curl.Extended.Protocol.McpClient.Stdio;

/// <summary>A time provider whose timers fire only when the test calls <see cref="FireTimers"/>, recording each timer's due time.</summary>
internal sealed class FiredOnDemandTimeProvider : TimeProvider
{
    private readonly List<(TimerCallback Callback, object? State)> timers = [];

    public List<TimeSpan> DueTimes { get; } = [];

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        timers.Add((callback, state));
        DueTimes.Add(dueTime);
        return new InertTimer();
    }

    public void FireTimers()
    {
        foreach (var (callback, state) in timers)
        {
            callback(state);
        }
    }

    private sealed class InertTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
