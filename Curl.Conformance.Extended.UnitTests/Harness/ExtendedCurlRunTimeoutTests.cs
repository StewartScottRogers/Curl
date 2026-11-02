using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.Harness;

/// <summary>
/// Pins that a harness run's timers run on the <see cref="TimeProvider" /> the test hands
/// <see cref="ExtendedConformanceHarness.RunAsync" /> (BL-4503): <c>-m 100</c> against a reference server that
/// takes curl's request and never answers ends with exit 28 once the test advances its fake clock by 100
/// seconds, well inside the 30 seconds the test waits, which the real clock never could.
/// </summary>
[TestClass]
public sealed class ExtendedCurlRunTimeoutTests
{
    private const int MaxTimeSeconds = 100;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_MaxTimeAgainstAServerThatNeverAnswers_EndsWithExit28WhenTheInjectedClockAdvances()
    {
        TimerAdvancingTimeProvider clock = new();
        NeverAnsweringServer server = new("HELLO\r\n$1\r\n3\r\n");

        Task<CurlRunResult> run = ExtendedConformanceHarness.RunAsync(
            InProcessCurl.RunAsync,
            [ReferenceRoute.Tcp(6379, server)],
            ["-sS", "-m", MaxTimeSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture), "redis://localhost/PING"],
            timeProvider: clock);
        await server.RequestReceived.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        for (int second = 0; second <= MaxTimeSeconds && !run.IsCompleted; second++)
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        CurlRunResult result = await run.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        string standardError = Encoding.UTF8.GetString(result.StandardError);
        Assert.AreEqual(28, result.ExitCode, standardError);
        StringAssert.StartsWith(standardError, "curl: (28) ");
        Assert.IsEmpty(result.StandardOutput);
        Assert.IsEmpty(result.Exchanges.Single().ServerBytes);
    }

    /// <summary>A server that reads curl's request up to <paramref name="requestEnd" />, then reads on and never sends a byte.</summary>
    private sealed class NeverAnsweringServer(string requestEnd) : IReferenceServer
    {
        private readonly TaskCompletionSource requestReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task RequestReceived => requestReceived.Task;

        public async Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken)
        {
            StringBuilder received = new();
            byte[] buffer = new byte[1];
            while (await connection.ReadAsync(buffer, cancellationToken) == 1)
            {
                received.Append((char)buffer[0]);
                if (received.ToString().EndsWith(requestEnd, StringComparison.Ordinal))
                {
                    requestReceived.TrySetResult();
                }
            }
        }
    }

    /// <summary>A clock that moves only when the test advances it, firing every timer that falls due.</summary>
    private sealed class TimerAdvancingTimeProvider : TimeProvider
    {
        private readonly Lock gate = new();
        private readonly List<FakeTimer> timers = [];
        private long elapsedTicks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
        {
            lock (gate)
            {
                return elapsedTicks;
            }
        }

        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero).AddTicks(GetTimestamp());

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            FakeTimer timer = new(this, callback, state);
            lock (gate)
            {
                timers.Add(timer);
            }

            timer.Change(dueTime, period);
            return timer;
        }

        /// <summary>Moves the clock forward and fires each timer due by then, in order.</summary>
        public void Advance(TimeSpan by)
        {
            long target;
            lock (gate)
            {
                target = elapsedTicks + by.Ticks;
            }

            while (NextDue(target) is { } due)
            {
                lock (gate)
                {
                    elapsedTicks = Math.Max(elapsedTicks, due.DueAt);
                }

                due.Fire();
            }

            lock (gate)
            {
                elapsedTicks = target;
            }
        }

        private FakeTimer? NextDue(long target)
        {
            lock (gate)
            {
                return timers.Where(t => t.DueAt <= target).OrderBy(t => t.DueAt).FirstOrDefault();
            }
        }

        private void Remove(FakeTimer timer)
        {
            lock (gate)
            {
                timers.Remove(timer);
            }
        }

        private sealed class FakeTimer(TimerAdvancingTimeProvider owner, TimerCallback callback, object? state) : ITimer
        {
            private long period = -1;

            public long DueAt { get; private set; } = long.MaxValue;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                lock (owner.gate)
                {
                    DueAt = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : owner.elapsedTicks + dueTime.Ticks;
                    this.period = period == Timeout.InfiniteTimeSpan || period == TimeSpan.Zero ? -1 : period.Ticks;
                }

                return true;
            }

            public void Fire()
            {
                lock (owner.gate)
                {
                    DueAt = period < 0 ? long.MaxValue : DueAt + period;
                }

                callback(state);
            }

            public void Dispose() => owner.Remove(this);

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
