using Curl.Api.Codes;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Libcurl.Global;
using Libcurl.Multi;
using Libcurl.Options;

namespace Libcurl.UnitTests.Options;

/// <summary>
/// Pins <c>CURLOPT_LOW_SPEED_LIMIT</c> with <c>CURLOPT_LOW_SPEED_TIME</c> through the public options (BL-3196): a
/// transfer that writes 10 bytes and stalls under a 100000 bytes/sec limit for 2 seconds ends with
/// <see cref="CurlCode.OperationTimedOut" /> and libcurl 8.22.0's <c>Operation too slow</c> text, on its own and in a
/// multi handle.
/// </summary>
[TestClass]
public sealed class LowSpeedOptionTests
{
    [TestMethod]
    public async Task PerformAsync_BelowTheLowSpeedLimitForTheLowSpeedTime_AnswersOperationTimedOut()
    {
        StepClock clock = new();
        TenBytesThenStallingHandler handler = new();
        await using var library = CurlGlobal.Init(EngineOver(clock, handler), CurlGlobalFlags.Nothing);
        using var easy = library.EasyInit();
        SetSlowOptions(easy);

        Task<CurlCode> perform = easy.PerformAsync().AsTask();
        await handler.Stalled;
        clock.Advance(TimeSpan.FromSeconds(1));
        clock.Advance(TimeSpan.FromSeconds(1));
        CurlCode code = await perform;

        Assert.AreEqual(CurlCode.OperationTimedOut, code);
        Assert.AreEqual("Operation too slow. Less than 100000 bytes/sec transferred the last 2 seconds", easy.ErrorText);
    }

    [TestMethod]
    public async Task MultiPerformAsync_BelowTheLowSpeedLimitForTheLowSpeedTime_FinishesWithOperationTimedOut()
    {
        StepClock clock = new();
        TenBytesThenStallingHandler handler = new();
        await using var library = CurlGlobal.Init(EngineOver(clock, handler), CurlGlobalFlags.Nothing);
        using var easy = library.EasyInit();
        SetSlowOptions(easy);
        await using CurlMulti multi = library.MultiInit();
        multi.AddHandle(easy);

        (_, int running) = await multi.PerformAsync();
        await handler.Stalled;
        clock.Advance(TimeSpan.FromSeconds(1));
        clock.Advance(TimeSpan.FromSeconds(1));
        await handler.Ended;
        for (int turn = 0; running > 0 && turn < 1000; turn++)
        {
            await Task.Delay(1);
            (_, running) = await multi.PerformAsync();
        }

        Assert.AreEqual(CurlCode.OperationTimedOut, multi.InfoRead(out _)?.Result);
    }

    private static void SetSlowOptions(Libcurl.Easy.CurlEasy easy)
    {
        easy.SetOption(CurlOption.Url, "http://h/x");
        easy.SetOption(CurlOption.LowSpeedLimit, 100000L);
        easy.SetOption(CurlOption.LowSpeedTime, 2L);
    }

    private static ManagedCurlEngine EngineOver(StepClock clock, TenBytesThenStallingHandler handler) =>
        new(clock, (_, _, _) => new ProtocolDispatcher([handler]), readEnvironmentVariable: _ => null);

    // Writes 10 bytes, then waits until the transfer is cancelled, as a server that pauses.
    private sealed class TenBytesThenStallingHandler : IProtocolHandler
    {
        private readonly TaskCompletionSource stalled = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource ended = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Stalled => stalled.Task;

        public Task Ended => ended.Task;

        public IReadOnlyCollection<string> SupportedSchemes { get; } = ["http"];

        public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            try
            {
                await context.Output.WriteAsync(new byte[10], context.CancellationToken);
                stalled.TrySetResult();
                await Task.Delay(Timeout.Infinite, context.CancellationToken);
                return TransferResult.Success(10);
            }
            finally
            {
                ended.TrySetResult();
            }
        }
    }

    // A clock whose time and timers move only when the test advances it.
    private sealed class StepClock : TimeProvider
    {
        private readonly Lock gate = new();

        private readonly List<StepTimer> timers = [];

        private long now;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
        {
            lock (gate)
            {
                return now;
            }
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            StepTimer timer = new(this, callback, state);
            timer.Change(dueTime, period);
            lock (gate)
            {
                timers.Add(timer);
            }

            return timer;
        }

        public void Advance(TimeSpan duration)
        {
            long until;
            lock (gate)
            {
                until = now + duration.Ticks;
            }

            while (NextDue(until) is { } timer)
            {
                timer.Fire();
            }

            lock (gate)
            {
                now = until;
            }
        }

        private StepTimer? NextDue(long until)
        {
            lock (gate)
            {
                StepTimer? next = timers.Where(timer => timer.DueAt <= until).MinBy(timer => timer.DueAt);
                if (next is not null)
                {
                    now = next.DueAt;
                }

                return next;
            }
        }

        private sealed class StepTimer(StepClock owner, TimerCallback callback, object? state) : ITimer
        {
            private long period = -1;

            internal long DueAt { get; private set; } = long.MaxValue;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                this.period = period <= TimeSpan.Zero ? -1 : period.Ticks;
                DueAt = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : owner.now + dueTime.Ticks;
                return true;
            }

            public void Dispose()
            {
                lock (owner.gate)
                {
                    owner.timers.Remove(this);
                }
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }

            internal void Fire()
            {
                DueAt = period < 0 ? long.MaxValue : DueAt + period;
                callback(state);
            }
        }
    }
}
