using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins CGR-109 (BL-3638) in <see cref="CurlCommandRunner" />: under <c>-Z</c> without
/// <c>--parallel-immediate</c> a second transfer to a host still waiting for the host's first connection
/// runs its <c>-m</c> clock while it waits, as curl 8.22.0 does, so when the first never answers both
/// end with exit 28 and the second never connects. Measured on curl 8.18.0 (BL-3638 Notes):
/// <c>curl -sS -Z -m 2 -w '[%{num_connects}]' $H/stall $H/ok</c> opens one connection and prints two
/// <c>(28)</c> lines.
/// </summary>
[TestClass]
public sealed class Cgr109Tests
{
    private const string Stall = "http://h.h/stall";

    private const string Ok = "http://h.h/ok";

    private readonly InMemoryFileSystem fileSystem = new();

    private readonly TextWaitingStream standardOutput = new();

    private readonly TextWaitingStream standardError = new();

    private readonly TimerFiringClock clock = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task RunAsync_SecondSameHostTransferWaitsPastMaxTime_TimesOutWithoutConnecting()
    {
        HeldTransferHandler http = new();
        http.Finish("/ok", "aaaaa");

        Task<int> run = RunAsync(http, ["-sS", "-Z", "-m", "2", "-w", "[%{exitcode}]", Stall, Ok]);
        await http.WhenStartedAsync("/stall");
        await clock.WhenTimersDueAsync(2000, 2);
        clock.Advance(2000);
        await clock.FireLateTimersUntilAsync(run);

        int exitCode = await run;
        string[] timedOutLines = Lf(standardError.Text).Split('\n').Where(line => line.StartsWith("curl: (28) ", StringComparison.Ordinal)).ToArray();
        Diagnostics.Assert("exit code", 28, exitCode);
        Diagnostics.Assert("started", "/stall", string.Join(',', http.Started));
        Diagnostics.Diff("stdout", "[28][28]", standardOutput.Text);
        Assert.AreEqual(28, exitCode);
        CollectionAssert.AreEqual(new[] { "/stall" }, http.Started.ToArray());
        Assert.AreEqual("[28][28]", standardOutput.Text);
        Assert.HasCount(2, timedOutLines);
        Assert.Contains("curl: (28) Operation timed out after 2000 milliseconds with 0 bytes received", timedOutLines);
    }

    [TestMethod]
    public async Task RunAsync_ParallelImmediate_StartsTheSecondSameHostTransferAtOnce()
    {
        HeldTransferHandler http = new();
        http.Finish("/ok", "aaaaa");

        Task<int> run = RunAsync(http, ["-sS", "-Z", "--parallel-immediate", "-m", "2", "-w", "[%{exitcode}]", Stall, Ok]);
        await http.WhenStartedAsync("/stall");
        await standardOutput.WhenEndsWithAsync("aaaaa[0]");
        await clock.WhenTimersDueAsync(2000, 1);
        clock.Advance(2000);
        await clock.FireLateTimersUntilAsync(run);

        int exitCode = await run;
        Diagnostics.Assert("exit code", 28, exitCode);
        Diagnostics.Diff("stdout", "aaaaa[0][28]", standardOutput.Text);
        Assert.AreEqual(28, exitCode);
        CollectionAssert.AreEquivalent(new[] { "/stall", "/ok" }, http.Started.ToArray());
        Assert.AreEqual("aaaaa[0][28]", standardOutput.Text);
    }

    [TestMethod]
    public async Task RunAsync_FailEarlyAbortsATransferWaitingUnderMaxTime_EndsWithTheFirstFailure()
    {
        HeldTransferHandler http = new();

        Task<int> run = RunAsync(http, ["-s", "-Z", "--fail-early", "-m", "10", "http://a.h/a", "http://b.h/1", "http://b.h/2"]);
        await Task.WhenAll(http.WhenStartedAsync("/a"), http.WhenStartedAsync("/1"));
        await clock.WhenTimersDueAsync(10000, 3);
        http.Fail("/a", CurlExitCode.CouldntConnect, "Could not connect to server");
        await clock.WhenTimersDueAsync(1000, 1);
        for (int step = 0; step < 80 && !run.IsCompleted; step++)
        {
            clock.Advance(100);
            await Task.Delay(5);
        }

        int exitCode = await run;
        Diagnostics.Assert("exit code", 7, exitCode);
        Diagnostics.Assert("started", "/a,/1", string.Join(',', http.Started.Order(StringComparer.Ordinal)));
        Assert.AreEqual(7, exitCode);
        CollectionAssert.AreEquivalent(new[] { "/a", "/1" }, http.Started.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_HostFreedAfterMaxTimePassed_TimesOutTheWaiterWithoutConnecting()
    {
        HeldTransferHandler http = new();
        http.Finish("/ok", "aaaaa");

        Task<int> run = RunAsync(http, ["-sS", "-Z", "-m", "2", "-w", "[%{exitcode}]", Stall, Ok]);
        await http.WhenStartedAsync("/stall");
        await clock.WhenTimersDueAsync(2000, 2);
        clock.MoveWithoutFiring(2500);
        http.Fail("/stall", CurlExitCode.OperationTimedOut, "Operation timed out after 2500 milliseconds with 0 bytes received");

        // Wake only the -Z loop's polls (due within one poll of now), never the -m timers due at 2000 or a
        // later connect timeout, so the waiter still finds -m passed when the host frees it (BL-4444).
        await clock.FireLateTimersUntilAsync(
            run,
            due => due != 2000 && due <= clock.GetTimestamp() + ParallelTransferMulti.PollTimeoutMilliseconds);

        int exitCode = await run;
        string expectedStandardError =
            "curl: (28) Operation timed out after 2500 milliseconds with 0 bytes received\n"
            + "curl: (28) Operation timed out after 2500 milliseconds with 0 bytes received\n";
        Diagnostics.Assert("exit code", 28, exitCode);
        Diagnostics.Diff("stderr", expectedStandardError, Lf(standardError.Text));
        Assert.AreEqual(28, exitCode);
        CollectionAssert.AreEqual(new[] { "/stall" }, http.Started.ToArray());
        Assert.AreEqual("[28][28]", standardOutput.Text);
        Assert.AreEqual(expectedStandardError, Lf(standardError.Text));
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>Runs <paramref name="arguments" /> as on Windows through <paramref name="handler" /> alone, on <see cref="clock" />.</summary>
    private async Task<int> RunAsync(IProtocolHandler handler, string[] arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(new ProtocolDispatcher([handler])),
                    fileSystem,
                    fileSystem,
                    standardOutput,
                    standardError,
                    new MemoryStream(),
                    runsOnWindows: true,
                    timeProvider: clock)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stdout", Lf(standardOutput.Text));
        Diagnostics.Act("stderr", Lf(standardError.Text));
        return exitCode;
    }

    /// <summary>
    /// A clock that moves only when <see cref="Advance" /> is called and then fires, on the caller's thread,
    /// every timer whose due time has come, so a <c>-m</c> limit passes exactly when the test says.
    /// </summary>
    internal sealed class TimerFiringClock : TimeProvider
    {
        private readonly List<FiringTimer> timers = [];

        private long elapsedMilliseconds;

        public override long TimestampFrequency => 1000;

        public override long GetTimestamp() => Interlocked.Read(ref elapsedMilliseconds);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            FiringTimer timer = new(this, callback, state);
            timer.Change(dueTime, period);
            lock (timers)
            {
                timers.Add(timer);
            }

            return timer;
        }

        /// <summary>Waits until at least <paramref name="count" /> live timers are due at <paramref name="dueMilliseconds" />.</summary>
        public async Task WhenTimersDueAsync(long dueMilliseconds, int count)
        {
            using CancellationTokenSource giveUp = new(TimeSpan.FromSeconds(30));
            while (Due(dueMilliseconds) < count)
            {
                await Task.Delay(5, giveUp.Token);
            }
        }

        /// <summary>
        /// Fires every live timer, without moving the clock, until <paramref name="run" /> ends, so a timer the run
        /// creates or re-arms after <see cref="Advance" /> took its snapshot (a host waiter's <c>-m</c> timer, a poll
        /// timer) cannot leave it waiting on a clock nobody moves again (BL-4442).
        /// </summary>
        public Task FireLateTimersUntilAsync(Task run) => FireLateTimersUntilAsync(run, static _ => true);

        /// <summary>
        /// Fires, without moving the clock, every live timer whose due time <paramref name="fires" /> accepts, until
        /// <paramref name="run" /> ends (BL-4444): a test that must not fire a <c>-m</c> timer can still wake a
        /// <c>-Z</c> poll that missed its wakeup and would otherwise wait on a clock nobody moves.
        /// </summary>
        public async Task FireLateTimersUntilAsync(Task run, Func<long, bool> fires)
        {
            using CancellationTokenSource giveUp = new(TimeSpan.FromSeconds(30));
            while (!run.IsCompleted && !giveUp.IsCancellationRequested)
            {
                FiringTimer[] live;
                lock (timers)
                {
                    live = [.. timers.Where(timer => timer.DueAt is { } due && fires(due))];
                }

                foreach (FiringTimer timer in live)
                {
                    timer.Fire();
                }

                await Task.WhenAny(run, Task.Delay(5, giveUp.Token));
            }
        }

        public void MoveWithoutFiring(long milliseconds) => Interlocked.Add(ref elapsedMilliseconds, milliseconds);

        public void Advance(long milliseconds)
        {
            long now = Interlocked.Add(ref elapsedMilliseconds, milliseconds);
            FiringTimer[] firing;
            lock (timers)
            {
                firing = [.. timers.Where(timer => timer.DueAt is { } due && due <= now)];
            }

            foreach (FiringTimer timer in firing)
            {
                timer.Fire();
            }
        }

        private int Due(long dueMilliseconds)
        {
            lock (timers)
            {
                return timers.Count(timer => timer.DueAt == dueMilliseconds);
            }
        }

        private void Remove(FiringTimer timer)
        {
            lock (timers)
            {
                timers.Remove(timer);
            }
        }

        private sealed class FiringTimer(TimerFiringClock clock, TimerCallback callback, object? state) : ITimer
        {
            public long? DueAt { get; private set; }

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : clock.GetTimestamp() + (long)dueTime.TotalMilliseconds;
                return true;
            }

            public void Fire()
            {
                DueAt = null;
                callback(state);
            }

            public void Dispose() => clock.Remove(this);

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
