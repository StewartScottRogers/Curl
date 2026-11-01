using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Imap.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Imap;

/// <summary>
/// CGR-003 (BL-3532): <c>--connect-timeout</c> bounds the IMAP connect phase - the greeting,
/// <c>CAPABILITY</c> and the login - counted from the start of the request. Measured on curl 8.22.0
/// Schannel against a listener that accepts and never sends a byte:
/// <c>curl -sS -w '%{exitcode}|%{errormsg}\n' imap://127.0.0.1:20700/ --connect-timeout 2</c>
/// is exit 28 after about 2 s with <c>Operation timed out after 2007 milliseconds with 0 bytes
/// received</c>, and nothing is sent. Each test runs on a manual clock so no second is real.
/// </summary>
[TestClass]
public sealed class Cgr003Tests
{
    public TestContext TestContext { get; set; } = null!;

    private const string Url = "imap://127.0.0.1:20700/";

    private const string Greeting = "* OK ready\r\n";

    private const string Opened = Greeting + "* CAPABILITY IMAP4rev1\r\nA001 OK done\r\n";

    private static readonly TimeSpan TwoSeconds = TimeSpan.FromSeconds(2);

    private readonly ManualClock clock = new();

    [TestMethod]
    public async Task ExecuteAsync_NoGreetingBeforeTheConnectTimeout_EndsWithExit28AndCurlsOperationMessage()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_NoGreetingBeforeTheConnectTimeout_EndsWithExit28AndCurlsOperationMessage");
        var connection = new StallingConnection(string.Empty);
        Task<TransferResult> run = StartAsync(Url, connection, TwoSeconds);
        await connection.Stalled;

        clock.Advance(TwoSeconds);

        diagnostics.Act("steps before the checks", "completed");
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.OperationTimedOut, "Operation timed out after 2000 milliseconds with 0 bytes received"),
            (await run) with { Report = null });
        Assert.AreEqual(string.Empty, connection.SentText);
    }

    [TestMethod]
    public async Task ExecuteAsync_CapabilityUnansweredBeforeTheConnectTimeout_EndsWithExit28AndNoQuit()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_CapabilityUnansweredBeforeTheConnectTimeout_EndsWithExit28AndNoQuit");
        var connection = new StallingConnection(Greeting);
        Task<TransferResult> run = StartAsync(Url, connection, TwoSeconds);
        await connection.Stalled;

        clock.Advance(TwoSeconds);

        diagnostics.Act("steps before the checks", "completed");
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        Assert.AreEqual(CurlExitCode.OperationTimedOut, (await run).ExitCode);
        Assert.AreEqual("A001 CAPABILITY\r\n", connection.SentText);
    }

    [TestMethod]
    public async Task ExecuteAsync_StalledAfterTheConnectPhase_IsNotHeldToTheConnectTimeout()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_StalledAfterTheConnectPhase_IsNotHeldToTheConnectTimeout");
        var connection = new StallingConnection(Opened);
        using var cancellation = new CancellationTokenSource();
        Task<TransferResult> run = StartAsync("imap://127.0.0.1:20700/INBOX", connection, TwoSeconds, cancellation.Token);
        await connection.Stalled;

        clock.Advance(TimeSpan.FromSeconds(10));
        bool runningAfterTheLimit = !run.IsCompleted;
        await cancellation.CancelAsync();

        diagnostics.Act("steps before the checks", "completed");
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        Assert.IsTrue(runningAfterTheLimit);
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => run);
        Assert.AreEqual("A001 CAPABILITY\r\nA002 LIST \"INBOX\" *\r\n", connection.SentText);
    }

    [TestMethod]
    public async Task ExecuteAsync_TransferCancelledDuringTheGreeting_ThrowsTheCancellation()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_TransferCancelledDuringTheGreeting_ThrowsTheCancellation");
        var connection = new StallingConnection(string.Empty);
        using var cancellation = new CancellationTokenSource();
        Task<TransferResult> run = StartAsync(Url, connection, TwoSeconds, cancellation.Token);
        await connection.Stalled;

        await cancellation.CancelAsync();

        diagnostics.Act("steps before the checks", "completed");
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => run);
    }

    [TestMethod]
    [DataRow(-1, DisplayName = "no --connect-timeout")]
    [DataRow(0, DisplayName = "--connect-timeout 0")]
    public async Task ExecuteAsync_NoConnectTimeout_HoldsTheGreetingToCurls300Seconds(int connectTimeoutSeconds)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("connect timeout seconds", connectTimeoutSeconds);
        var connection = new StallingConnection(string.Empty);
        Task<TransferResult> run = StartAsync(Url, connection, connectTimeoutSeconds < 0 ? null : TimeSpan.FromSeconds(connectTimeoutSeconds));
        await connection.Stalled;

        clock.Advance(TimeSpan.FromSeconds(299));
        bool runningBeforeTheDefault = !run.IsCompleted;
        clock.Advance(TimeSpan.FromSeconds(1));

        diagnostics.Act("steps before the checks", "completed");
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        Assert.IsTrue(runningBeforeTheDefault);
        Assert.AreEqual("Operation timed out after 300000 milliseconds with 0 bytes received", (await run).ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_TimerFiresEarly_WaitsOutTheRestOfTheConnectTimeout()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_TimerFiresEarly_WaitsOutTheRestOfTheConnectTimeout");
        var connection = new StallingConnection(string.Empty);
        Task<TransferResult> run = StartAsync(Url, connection, TwoSeconds);
        await connection.Stalled;

        clock.Advance(TimeSpan.FromSeconds(1));
        clock.FireEveryTimer();
        bool runningAfterTheEarlyFire = !run.IsCompleted;
        clock.Advance(TimeSpan.FromSeconds(1));

        diagnostics.Act("steps before the checks", "completed");
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        Assert.IsTrue(runningAfterTheEarlyFire);
        Assert.AreEqual("Operation timed out after 2000 milliseconds with 0 bytes received", (await run).ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectOutlastsTheConnectTimeout_EndsAtTheGreeting()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_ConnectOutlastsTheConnectTimeout_EndsAtTheGreeting");
        var connection = new StallingConnection(string.Empty);
        var connector = new SlowConnector(clock, TimeSpan.FromMilliseconds(2500), connection);

        TransferResult result = await StartAsync(Url, connector, TwoSeconds, CancellationToken.None);

        diagnostics.Act("steps before the checks", "completed");
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        Assert.AreEqual("Operation timed out after 2500 milliseconds with 0 bytes received", result.ErrorMessage);
        Assert.AreEqual(string.Empty, connection.SentText);
    }

    [TestMethod]
    public async Task ExecuteAsync_TimerFiresAsItIsCreated_LeavesTheGreetingToTheTransfersToken()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_TimerFiresAsItIsCreated_LeavesTheGreetingToTheTransfersToken");
        clock.FiresTimersAsCreated = true;
        var connection = new StallingConnection(string.Empty);
        using var cancellation = new CancellationTokenSource();
        Task<TransferResult> run = StartAsync(Url, connection, TwoSeconds, cancellation.Token);
        await connection.Stalled;

        bool runningAfterTheFire = !run.IsCompleted;
        await cancellation.CancelAsync();

        diagnostics.Act("steps before the checks", "completed");
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        Assert.IsTrue(runningAfterTheFire);
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => run);
    }

    private Task<TransferResult> StartAsync(string url, StallingConnection connection, TimeSpan? connectTimeout, CancellationToken cancellationToken = default) =>
        StartAsync(url, new QueuedConnector(ConnectResult.Connected(connection)), connectTimeout, cancellationToken);

    private Task<TransferResult> StartAsync(string url, IConnector connector, TimeSpan? connectTimeout, CancellationToken cancellationToken)
    {
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
            ConnectTimeout = connectTimeout,
            TimeProvider = clock,
            CancellationToken = cancellationToken,
        };
        return new ImapProtocolHandler(connector, new QueuedTlsProvider()).ExecuteAsync(context).AsTask();
    }

    /// <summary>A clock that moves only when told, firing each timer once it falls due.</summary>
    private sealed class ManualClock : TimeProvider
    {
        private readonly Lock gate = new();

        private readonly List<ManualTimer> timers = [];

        private TimeSpan elapsed;

        public bool FiresTimersAsCreated { get; set; }

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public TimeSpan Elapsed
        {
            get
            {
                lock (gate)
                {
                    return elapsed;
                }
            }
        }

        public override long GetTimestamp() => Elapsed.Ticks;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            lock (gate)
            {
                timers.Add(timer);
            }

            timer.Change(dueTime, period);
            return timer;
        }

        public void Advance(TimeSpan duration)
        {
            lock (gate)
            {
                elapsed += duration;
            }

            foreach (ManualTimer timer in Snapshot().Where(timer => timer.DueAt <= Elapsed))
            {
                timer.Fire();
            }
        }

        public void FireEveryTimer()
        {
            foreach (ManualTimer timer in Snapshot().Where(timer => timer.DueAt is not null))
            {
                timer.Fire();
            }
        }

        public void Remove(ManualTimer timer)
        {
            lock (gate)
            {
                timers.Remove(timer);
            }
        }

        private List<ManualTimer> Snapshot()
        {
            lock (gate)
            {
                return [.. timers];
            }
        }
    }

    /// <summary>A one-shot timer on a <see cref="ManualClock" />; due now fires at once.</summary>
    private sealed class ManualTimer(ManualClock clock, TimerCallback callback, object? state) : ITimer
    {
        public TimeSpan? DueAt { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : clock.Elapsed + dueTime;
            if (dueTime == TimeSpan.Zero || clock.FiresTimersAsCreated)
            {
                Fire();
            }

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

    /// <summary>A connector whose connect takes <paramref name="duration" /> on the clock.</summary>
    private sealed class SlowConnector(ManualClock clock, TimeSpan duration, IConnection connection) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            clock.Advance(duration);
            return ValueTask.FromResult(ConnectResult.Connected(connection));
        }
    }

    /// <summary>
    /// Serves <paramref name="script" />, then holds every later read open until its token is
    /// cancelled, as a server that stops answering does.
    /// </summary>
    private sealed class StallingConnection(string script) : IConnection
    {
        private readonly TaskCompletionSource stalled = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly MemoryStream sent = new();

        private byte[] unread = Encoding.Latin1.GetBytes(script);

        public Task Stalled => stalled.Task;

        public string SentText => Encoding.Latin1.GetString(sent.ToArray());

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (unread.Length > 0)
            {
                int count = Math.Min(buffer.Length, unread.Length);
                unread.AsSpan(0, count).CopyTo(buffer.Span);
                unread = unread[count..];
                return count;
            }

            stalled.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            sent.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
