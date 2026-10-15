using Curl.Cli;
using Curl.Core;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="TransferPerformer" />, the redirect-following attempt split out of
/// <see cref="CurlCommandRunner" /> (ADR-0506, BL-2282): one attempt follows a redirect, ends with
/// exit 28 when its <c>-Y</c>/<c>-y</c> or <c>-m</c> watchdog cancels it, and numbers the run's
/// transfers and connections, on fake handlers and a <see cref="SteppingTimeProvider" /> so no
/// second is real.
/// </summary>
[TestClass]
public sealed partial class TransferPerformerTests
{
    private const string First = "http://127.0.0.1:18282/a";
    private const string Next = "http://127.0.0.1:18282/next";

    private readonly SteppingTimeProvider clock = new();

    [TestMethod]
    public async Task FollowWatchingSpeedAsync_RedirectUnderFollow_FollowsToTheNextUrl()
    {
        ScriptedHandler handler = new(Redirect(302, Next), TransferResult.Success(6));
        RunningTransferState state = NewState();
        TransferContext context = new() { Url = CurlUrl.Parse(First), Output = Stream.Null, Http = new HttpRequestOptions { FollowRedirects = true } };

        TransferResult result = await TransferPerformer.FollowWatchingSpeedAsync(state, Follower(handler), context, new RedirectPolicy());

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.AreEqual(new[] { First, Next }, handler.Urls);
    }

    [TestMethod]
    public async Task FollowWatchingSpeedAsync_StalledBelowSpeedLimit_Exits28TooSlow()
    {
        TransferPerformer performer = new(clock);
        RunningTransferState state = NewState();
        LowSpeedWatchdog? watchdog = performer.StartLowSpeedWatchdog(state, 100, 2, NoDiagnosticLog.Instance);
        StallingHandler handler = new();
        TransferContext context = new() { Url = CurlUrl.Parse(First), Output = Stream.Null, CancellationToken = watchdog!.Token };

        Task<TransferResult> attempt = TransferPerformer.FollowWatchingSpeedAsync(state, Follower(handler), context, new RedirectPolicy());
        await handler.Stalled;
        clock.Advance(TimeSpan.FromSeconds(3));
        TransferResult result = await attempt;

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Operation too slow. Less than 100 bytes/sec transferred the last 2 seconds", result.ErrorMessage);
        Assert.IsNull(state.AttemptLowSpeedWatchdog);
    }

    [TestMethod]
    public async Task FollowWatchingSpeedAsync_PastMaxTime_Exits28TimedOut()
    {
        TransferPerformer performer = new(clock);
        RunningTransferState state = NewState();
        performer.StartLowSpeedWatchdog(state, 1, 100, NoDiagnosticLog.Instance);
        MaxTimeWatchdog? watchdog = performer.StartMaxTimeWatchdog(state, TimeSpan.FromSeconds(1), NoDiagnosticLog.Instance);
        StallingHandler handler = new();
        TransferContext context = new() { Url = CurlUrl.Parse(First), Output = Stream.Null, CancellationToken = watchdog!.Token };

        Task<TransferResult> attempt = TransferPerformer.FollowWatchingSpeedAsync(state, Follower(handler), context, new RedirectPolicy());
        await handler.Stalled;
        clock.Advance(TimeSpan.FromSeconds(1));
        TransferResult result = await attempt;

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Connection timed out after 1000 milliseconds", result.ErrorMessage);
        Assert.IsNull(state.AttemptMaxTimeWatchdog);
    }

    [TestMethod]
    public async Task FollowWatchingSpeedAsync_CancelledWithoutWatchdog_Throws()
    {
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();
        TransferContext context = new() { Url = CurlUrl.Parse(First), Output = Stream.Null, CancellationToken = cancelled.Token };

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await TransferPerformer.FollowWatchingSpeedAsync(NewState(), Follower(new StallingHandler()), context, new RedirectPolicy()));
    }

    [TestMethod]
    public void StartWatchdogs_NothingToWatch_KeepNone()
    {
        TransferPerformer performer = new(clock);
        RunningTransferState state = NewState();

        Assert.IsNull(performer.StartLowSpeedWatchdog(state, null, null, NoDiagnosticLog.Instance));
        Assert.IsNull(performer.StartMaxTimeWatchdog(state, null, NoDiagnosticLog.Instance));
        Assert.IsNull(state.AttemptLowSpeedWatchdog);
        Assert.IsNull(state.AttemptMaxTimeWatchdog);
    }

    [TestMethod]
    public void RecordSerialAttemptStart_SerialRun_RecordsNowAndParallelRunLeavesIt()
    {
        TransferPerformer performer = new(clock);
        Assert.IsNull(performer.SerialTransferStart);

        performer.RecordSerialAttemptStart(runsInParallel: true);
        Assert.IsNull(performer.SerialTransferStart);

        clock.Advance(TimeSpan.FromSeconds(2));
        performer.RecordSerialAttemptStart(runsInParallel: false);
        Assert.AreEqual(clock.GetTimestamp(), performer.SerialTransferStart);
    }

    [TestMethod]
    public void GivenOrOnClock_GivenOrNot_UsesTheGivenOneElseAClockOfItsOwn()
    {
        TransferPerformer given = new(clock);

        TransferPerformer onClock = TransferPerformer.GivenOrOnClock(null, clock);
        clock.Advance(TimeSpan.FromSeconds(4));
        onClock.RecordSerialAttemptStart(runsInParallel: false);

        Assert.AreSame(given, TransferPerformer.GivenOrOnClock(given, null));
        Assert.AreEqual(clock.GetTimestamp(), onClock.SerialTransferStart);
        Assert.IsNotNull(TransferPerformer.GivenOrOnClock(null, null));
    }

    [TestMethod]
    public void TakeIds_CountFromZero()
    {
        TransferPerformer performer = new(clock);

        Assert.AreEqual(0, performer.NextTransferId);
        Assert.AreEqual(0, performer.TakeTransferId());
        Assert.AreEqual(1, performer.NextTransferId);
        Assert.AreEqual(0, performer.TakeConnectionId());
        Assert.AreEqual(1, performer.TakeConnectionId());
    }

    [TestMethod]
    public void TakeNextAttemptIds_AttemptWithoutConnection_TakesNewIds()
    {
        TransferPerformer performer = new(clock);
        performer.TakeTransferId();
        RunningTransferState state = NewState();

        performer.TakeNextAttemptIds(state);

        Assert.AreEqual(0, state.RetriedConnectionId);
        Assert.IsNull(state.ConnectionId);
        Assert.AreEqual(1, state.RetryTransferId);
        Assert.AreEqual(1, performer.TakeConnectionId());
    }

    [TestMethod]
    public void TakeNextAttemptIds_AttemptWithConnection_KeepsItsConnectionId()
    {
        TransferPerformer performer = new(clock);
        RunningTransferState state = NewState();
        state.ConnectionId = 5;

        performer.TakeNextAttemptIds(state);

        Assert.AreEqual(5, state.RetriedConnectionId);
        Assert.IsNull(state.ConnectionId);
        Assert.AreEqual(0, state.RetryTransferId);
        Assert.AreEqual(0, performer.TakeConnectionId());
    }

    [TestMethod]
    [DataRow("http://h/", 0, true, DisplayName = "http reusing the connection keeps its id")]
    [DataRow("https://h/", 0, true, DisplayName = "https reusing the connection keeps its id")]
    [DataRow("http://h/", 1, false, DisplayName = "http opening a connection takes a new id")]
    [DataRow("ftp://h/", 0, false, DisplayName = "another scheme is taken to have opened one")]
    public void KeepRetriedConnectionIdWhenReused_Attempt_KeepsIdOnlyWhenReused(string url, int connections, bool keeps)
    {
        RunningTransferState state = NewState();
        state.RetriedConnectionId = 3;
        TransferResult attempt = TransferResult.Success(0) with { Report = new TransferReport { ConnectionCount = connections } };

        TransferPerformer.KeepRetriedConnectionIdWhenReused(state, new TransferContext { Url = CurlUrl.Parse(url), Output = Stream.Null }, attempt);

        Assert.AreEqual(keeps ? 3 : null, state.ConnectionId);
    }

    [TestMethod]
    public void KeepRetriedConnectionIdWhenReused_NoRetriedAttemptOrNoReport_LeavesConnectionId()
    {
        RunningTransferState state = NewState();
        TransferContext context = new() { Url = CurlUrl.Parse("http://h/"), Output = Stream.Null };

        TransferPerformer.KeepRetriedConnectionIdWhenReused(state, context, TransferResult.Success(0) with { Report = new TransferReport { ConnectionCount = 0 } });
        Assert.IsNull(state.ConnectionId);

        state.RetriedConnectionId = 3;
        TransferPerformer.KeepRetriedConnectionIdWhenReused(state, context, TransferResult.Success(0) with { Report = null });
        Assert.IsNull(state.ConnectionId);
    }

    [TestMethod]
    public void CreateRetryAttemptContext_StartsProgressThenCreatesTheContextAtTheOffset()
    {
        List<string> calls = [];
        TransferContext created = new() { Url = CurlUrl.Parse(First), Output = Stream.Null };

        TransferContext context = TransferPerformer.CreateRetryAttemptContext(
            () => calls.Add("progress"),
            resumeFrom =>
            {
                calls.Add($"context {resumeFrom}");
                return created;
            },
            7);

        Assert.AreSame(created, context);
        CollectionAssert.AreEqual(new[] { "progress", "context 7" }, calls);
    }

    private static RedirectFollower Follower(IProtocolHandler handler) => new(new ProtocolDispatcher([handler]));

    private static TransferResult Redirect(int status, string location) =>
        TransferResult.Success(0) with { Report = new TransferReport { ResponseCode = status, RedirectUrl = location } };

    private static RunningTransferState NewState()
    {
        StandardOutputFailureDeferringStream standardOutput = new(new MemoryStream());
        return new RunningTransferState(0, Array.Empty<CommandLineOptions>(), CancellationToken.None, standardOutput, standardOutput);
    }

    /// <summary>Answers each transfer with the next scripted result and records the URLs asked for.</summary>
    private sealed class ScriptedHandler(params TransferResult[] results) : IProtocolHandler
    {
        private int next;

        public List<string> Urls { get; } = [];

        public IReadOnlyCollection<string> SupportedSchemes { get; } = ["http"];

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            Urls.Add(context.Url.OriginalString);
            return ValueTask.FromResult(results[next++]);
        }
    }

    /// <summary>Stalls every transfer until its context's token is cancelled.</summary>
    private sealed class StallingHandler : IProtocolHandler
    {
        private readonly TaskCompletionSource stalled = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Stalled => stalled.Task;

        public IReadOnlyCollection<string> SupportedSchemes { get; } = ["http"];

        public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            stalled.TrySetResult();
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
            return TransferResult.Success(0);
        }
    }
}
