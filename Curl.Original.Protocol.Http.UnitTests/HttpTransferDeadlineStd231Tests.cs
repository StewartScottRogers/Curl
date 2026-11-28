using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins STD-231: <see cref="HttpTransferDeadline" /> ends a transfer for <c>-m</c> only once the whole limit has passed
/// on the transfer's clock, even when its timer fires early, so <c>%{time_total}</c> is not under the <c>-m</c> value
/// (ADR-1470).
/// </summary>
[TestClass]
public sealed class HttpTransferDeadlineStd231Tests
{
    private static readonly TimeSpan MaxTime = TimeSpan.FromSeconds(2);

    [TestMethod]
    public void Token_TimerFiresBeforeMaxTimeHasPassed_IsCancelledOnlyOnceItHas()
    {
        HandFiredTimeProvider time = new();
        using HttpTransferDeadline deadline = new(ContextOf(time, MaxTime));

        time.Elapsed = TimeSpan.FromMilliseconds(1990.4);
        time.Timer!.Fire();

        Assert.IsFalse(deadline.Token.IsCancellationRequested);
        Assert.AreEqual(TimeSpan.FromMilliseconds(10), time.Timer.DueTime);

        time.Elapsed = MaxTime;
        time.Timer.Fire();

        Assert.IsTrue(deadline.Token.IsCancellationRequested);
        Assert.IsTrue(deadline.EndedByLimit);
        Assert.AreEqual(2000, deadline.OperationElapsedMilliseconds);
    }

    [TestMethod]
    public void Constructor_MaxTimeAlreadyPassed_CancelsAtOnceWithNoTimer()
    {
        HandFiredTimeProvider time = new() { Elapsed = TimeSpan.FromSeconds(3) };

        using HttpTransferDeadline deadline = new(ContextOf(time, MaxTime, operationStarted: 0));

        Assert.IsTrue(deadline.Token.IsCancellationRequested);
        Assert.IsNull(time.Timer);
    }

    [TestMethod]
    public void Constructor_MaxTimeLongerThanATimerTakes_WaitsAsLongAsOneTakes()
    {
        HandFiredTimeProvider time = new();

        using HttpTransferDeadline deadline = new(ContextOf(time, TimeSpan.FromDays(100)));

        Assert.AreEqual(TimeSpan.FromMilliseconds(uint.MaxValue - 1), time.Timer!.DueTime);
        Assert.IsFalse(deadline.Token.IsCancellationRequested);
    }

    [TestMethod]
    public void Dispose_UnderMaxTime_DisposesTheTimer()
    {
        HandFiredTimeProvider time = new();
        HttpTransferDeadline deadline = new(ContextOf(time, MaxTime));

        deadline.Dispose();

        Assert.IsTrue(time.Timer!.IsDisposed);
    }

    private static TransferContext ContextOf(TimeProvider time, TimeSpan maxTime, long? operationStarted = null) => new()
    {
        Url = CurlUrl.Parse("https://127.0.0.1:29990/"),
        Output = new MemoryStream(),
        TimeProvider = time,
        MaxTime = maxTime,
        OperationStarted = operationStarted,
    };

    // A clock the test sets, whose one timer fires only when the test says, early or not.
    private sealed class HandFiredTimeProvider : TimeProvider
    {
        public TimeSpan Elapsed { get; set; }

        public HandFiredTimer? Timer { get; private set; }

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Elapsed.Ticks;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            Timer = new HandFiredTimer(callback, state, dueTime);
    }

    private sealed class HandFiredTimer(TimerCallback callback, object? state, TimeSpan dueTime) : ITimer
    {
        public TimeSpan DueTime { get; private set; } = dueTime;

        public bool IsDisposed { get; private set; }

        public void Fire() => callback(state);

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            DueTime = dueTime;
            return true;
        }

        public void Dispose() => IsDisposed = true;

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
