using System.Net;
using System.Text;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that the dialing runner, reached through the public <see cref="InProcessCurl" /> overload that takes a
/// <see cref="TimeProvider" />, runs every timer of the run on that clock (BL-4500): <c>-m 1</c> (and <c>-m 100</c>) against an
/// in-memory server that accepts the connection and never answers ends with exit 28 once the test advances its
/// fake clock by that time, with no real socket and no real wait: under <c>-m 100</c> the run must end well inside the 30 seconds the test waits, which the real clock never could.
/// </summary>
[TestClass]
public sealed class CurlCompositionInjectedTimeProviderTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(1, DisplayName = "-m 1")]
    [DataRow(100, DisplayName = "-m 100, longer than the test waits")]
    public async Task RunAsync_MaxTimeAgainstASilentServer_EndsWithExit28WhenTheInjectedClockAdvances(int maxTimeSeconds)
    {
        TimerAdvancingTimeProvider clock = new();
        SilentServerDialer dialer = new();
        MemoryStream standardError = new();
        Diagnostics.Arrange("command line", $"-sS -m {maxTimeSeconds} http://127.0.0.1/");
        Diagnostics.Arrange("server", "accepts the connection and never answers");

        Task<int> run = InProcessCurl.RunAsync(
            ["-sS", "-m", maxTimeSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture), "http://127.0.0.1/"],
            new MemoryStream(),
            standardError,
            new MemoryStream(),
            dialer,
            new LoopbackDnsResolver(),
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"),
            _ => null,
            clock);
        Task firstRead = await Task.WhenAny(dialer.ReadStarted, run);
        Diagnostics.Assert("the transfer waits on the server before the clock moves", true, firstRead == dialer.ReadStarted);
        Assert.AreSame(dialer.ReadStarted, firstRead);

        clock.Advance(TimeSpan.FromSeconds(maxTimeSeconds));
        int exitCode = await run.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        string standardErrorText = Encoding.ASCII.GetString(standardError.ToArray());
        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stderr", standardErrorText);
        Diagnostics.Assert("exit code", (int)CurlExitCode.OperationTimedOut, exitCode);
        Assert.AreEqual((int)CurlExitCode.OperationTimedOut, exitCode);
        StringAssert.StartsWith(standardErrorText, "curl: (28) ");
    }

    /// <summary>A dialer whose every connection reads nothing until its token is cancelled.</summary>
    private sealed class SilentServerDialer : ITcpDialer
    {
        private readonly TaskCompletionSource readStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets a task that completes when the transfer first reads from the server.</summary>
        public Task ReadStarted => readStarted.Task;

        public ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new DialedTcpConnection(new SilentConnection(endPoint, readStarted), new IPEndPoint(IPAddress.Loopback, 50000)));

        public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
            DialAsync(endPoint, cancellationToken);

        public ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The test dials no Unix socket.");
    }

    /// <summary>A connection that takes every write and never answers.</summary>
    private sealed class SilentConnection(EndPoint remote, TaskCompletionSource readStarted) : IConnection
    {
        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => remote;

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            readStarted.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>A clock that moves only when the test advances it, firing every timer that falls due.</summary>
    private sealed class TimerAdvancingTimeProvider : TimeProvider
    {
        private readonly object gate = new();
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
