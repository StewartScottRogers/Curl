using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Network;
using Curl.Console;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins <see cref="ManagedEasyTransfer.ToolProgress" /> (BL-2398, ADR-0567): a perform from the options with the curl
/// tool's progress set reports the same progress sequence as the same attempt the runner sets up and runs through
/// <see cref="ManagedEasyTransfer.ToolAttempt" />, calls no progress callback, starts fresh <c>-Y</c>/<c>-y</c> and
/// <c>-m</c> watchdogs on every perform, so a stalled attempt ends with the runner's exit 28 text, and stops on the
/// runner's abort token.
/// </summary>
public sealed partial class ManagedEasyTransferTests
{
    private const string ProgressResponse = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello";

    [TestMethod]
    public async Task PerformAsync_ToolProgressSet_ReportsTheProgressSequenceTheToolAttemptReports()
    {
        int callbackCalls = 0;
        EasyOptions options = new()
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Behaviour = new EasyBehaviourOptions { NoProgress = false },
            Callbacks = new() { TransferInfo = (_, _, _, _) => { ++callbackCalls; return 0; } },
        };
        RecordingToolProgress fromOptions = new();
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(new ScriptedConnector([Encoding.ASCII.GetBytes(ProgressResponse)])).CreateEasy();
        easy.Options = options;
        easy.ToolProgress = new EngineToolTransferProgress(fromOptions, CancellationToken.None);
        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        RecordingToolProgress fromAttempt = new();
        await using ManagedEasyTransfer runner = (ManagedEasyTransfer)CreateEngine(new ScriptedConnector([Encoding.ASCII.GetBytes(ProgressResponse)])).CreateEasy();
        runner.Options = options;
        runner.ToolAttempt = new ToolAttempt(
            new EngineTransferState(),
            new RedirectFollower(CreateDispatcher(new ScriptedConnector([Encoding.ASCII.GetBytes(ProgressResponse)]), null)),
            RunnersContext(options, TimeProvider.System, progress: fromAttempt),
            new RedirectPolicy());
        await runner.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.Contains("downloaded:5/5", fromOptions.Log);
        CollectionAssert.AreEqual(fromAttempt.Log.ToArray(), fromOptions.Log.ToArray());
        Assert.AreEqual(0, callbackCalls);
    }

    [TestMethod]
    public async Task PerformAsync_ToolProgressSetAndStalledPastMaxTime_EndsWithTheToolAttemptsExit28Text()
    {
        EasyOptions options = new() { Network = new EasyNetworkOptions { Url = "http://h/x" }, Transfer = new EasyTransferOptions { Timeout = TimeSpan.FromSeconds(1) } };
        SteppingTimeProvider clock = new();
        StallingProtocolHandler handler = new();
        await using ManagedEasyTransfer easy = EasyOn(clock, handler);
        easy.Options = options;
        easy.ToolProgress = new EngineToolTransferProgress(new RecordingToolProgress(), CancellationToken.None);
        Task<CurlCode> perform = easy.PerformAsync(CancellationToken.None).AsTask();
        await handler.Stalled;
        clock.Advance(TimeSpan.FromSeconds(1));
        CurlCode code = await perform;

        SteppingTimeProvider runnerClock = new();
        StallingProtocolHandler runnerHandler = new();
        TransferPerformer performer = new(runnerClock, _ => string.Empty);
        EngineTransferState state = new();
        await using ManagedEasyTransfer runner = EasyOn(runnerClock, runnerHandler);
        runner.Options = options;
        runner.ToolAttempt = new ToolAttempt(
            state,
            new RedirectFollower(new ProtocolDispatcher([runnerHandler])),
            RunnersContext(
                options,
                runnerClock,
                lowSpeedWatchdog: performer.StartLowSpeedWatchdog(state, options.Transfer, NoDiagnosticLog.Instance),
                maxTimeWatchdog: performer.StartMaxTimeWatchdog(state, options.Transfer, NoDiagnosticLog.Instance)),
            new RedirectPolicy());
        Task<CurlCode> attempt = runner.PerformAsync(CancellationToken.None).AsTask();
        await runnerHandler.Stalled;
        runnerClock.Advance(TimeSpan.FromSeconds(1));
        CurlCode attemptCode = await attempt;

        Assert.AreEqual(CurlCode.OperationTimedOut, code);
        Assert.AreEqual("Connection timed out after 1000 milliseconds", easy.ErrorText);
        Assert.AreEqual(attemptCode, code);
        Assert.AreEqual(runner.ErrorText, easy.ErrorText);
    }

    [TestMethod]
    public async Task PerformAsync_ToolProgressSetAndStalledBelowSpeedLimit_EndsTooSlow()
    {
        SteppingTimeProvider clock = new();
        StallingProtocolHandler handler = new();
        await using ManagedEasyTransfer easy = EasyOn(clock, handler);
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Transfer = new EasyTransferOptions { LowSpeedLimit = 100, LowSpeedTime = TimeSpan.FromSeconds(2) },
        };
        easy.ToolProgress = new EngineToolTransferProgress(new RecordingToolProgress(), CancellationToken.None);

        Task<CurlCode> perform = easy.PerformAsync(CancellationToken.None).AsTask();
        await handler.Stalled;
        clock.Advance(TimeSpan.FromSeconds(3));
        CurlCode code = await perform;

        Assert.AreEqual(CurlCode.OperationTimedOut, code);
        Assert.AreEqual("Operation too slow. Less than 100 bytes/sec transferred the last 2 seconds", easy.ErrorText);
    }

    [TestMethod]
    public async Task PerformAsync_ToolProgressSetTwice_StartsAFreshMaxTimeWatchdogForEachPerform()
    {
        SteppingTimeProvider clock = new();
        List<long?> operationStarts = [];
        RecordingProtocolHandler handler = new("http", context =>
        {
            operationStarts.Add(context.OperationStarted);
            clock.Advance(TimeSpan.FromSeconds(2));
            return ValueTask.FromResult(TransferResult.Success(0));
        });
        await using ManagedEasyTransfer easy = EasyOn(clock, handler);
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/x" }, Transfer = new EasyTransferOptions { Timeout = TimeSpan.FromSeconds(3) } };
        easy.ToolProgress = new EngineToolTransferProgress(new RecordingToolProgress(), CancellationToken.None);

        CurlCode first = await easy.PerformAsync(CancellationToken.None);
        CurlCode second = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, first);
        Assert.AreEqual(CurlCode.Ok, second);
        Assert.HasCount(2, operationStarts);
        Assert.AreEqual(TimeSpan.FromSeconds(2).Ticks, operationStarts[1] - operationStarts[0]);
    }

    [TestMethod]
    public async Task PerformAsync_NoToolProgress_StartsNoWatchdog()
    {
        RecordingProtocolHandler handler = RecordingProtocolHandler.WritingPath("http");
        await using ManagedEasyTransfer easy = EasyOn(new SteppingTimeProvider(), handler);
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/x" }, Transfer = new EasyTransferOptions { Timeout = TimeSpan.FromSeconds(1) } };

        await easy.PerformAsync(CancellationToken.None);

        Assert.IsNull(handler.Contexts.Single().OperationStarted);
    }

    [TestMethod]
    public async Task PerformAsync_ToolProgressAbortTokenCancelled_StopsTheTransfer()
    {
        using CancellationTokenSource abort = new();
        await abort.CancelAsync();
        RecordingProtocolHandler handler = new("http", context =>
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(TransferResult.Success(0));
        });
        await using ManagedEasyTransfer easy = EasyOn(TimeProvider.System, handler);
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/x" } };
        easy.ToolProgress = new EngineToolTransferProgress(new RecordingToolProgress(), abort.Token);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await easy.PerformAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task Duplicate_HandleWithToolProgress_DoesNotCopyItAndResetClearsIt()
    {
        await using ManagedEasyTransfer easy = EasyOver(RecordingProtocolHandler.WritingPath("http"));
        easy.ToolProgress = new EngineToolTransferProgress(new RecordingToolProgress(), CancellationToken.None);

        await using IEasyTransfer copy = easy.Duplicate();
        easy.Reset();

        Assert.IsNull(((ManagedEasyTransfer)copy).ToolProgress);
        Assert.IsNull(easy.ToolProgress);
    }

    private static ManagedEasyTransfer EasyOn(TimeProvider clock, IProtocolHandler handler) =>
        (ManagedEasyTransfer)new ManagedCurlEngine(clock, (_, _, _) => new ProtocolDispatcher([handler])).CreateEasy();

    // The context the runner's transferContextFactory.Create call builds for the attempt, with its progress and watchdogs.
    private static TransferContext RunnersContext(
        EasyOptions options,
        TimeProvider clock,
        ITransferProgress? progress = null,
        LowSpeedWatchdog? lowSpeedWatchdog = null,
        MaxTimeWatchdog? maxTimeWatchdog = null) =>
        new TransferContextFactory(Stream.Null, clock).Create(
            ManagedEasyTransfer.TransferSettingsOf(options),
            options.Http,
            options.Behaviour,
            options.Transfer,
            options.Protocols,
            options.Proxy,
            options.Authentication,
            options.Network,
            CurlUrl.Parse(options.Network.Url!),
            Stream.Null,
            range: null,
            resumeFrom: null,
            headerOutput: null,
            progress: progress,
            lowSpeedWatchdog: lowSpeedWatchdog,
            maxTimeWatchdog: maxTimeWatchdog);

    // Logs every report the runner's TransferProgressRecorder would take.
    private sealed class RecordingToolProgress : ITransferProgress
    {
        public List<string> Log { get; } = [];

        public void ReportTransferStarted() => Log.Add("started");

        public void ReportRequestRetried() => Log.Add("retried");

        public void ReportRequestResentOnFreshConnection() => Log.Add("resent");

        public void ReportDownloaded(long bytesSoFar, long? expectedTotal) => Log.Add($"downloaded:{bytesSoFar}/{expectedTotal}");

        public void ReportUploaded(long bytesSoFar, long? expectedTotal) => Log.Add($"uploaded:{bytesSoFar}/{expectedTotal}");

        public void ReportTransferDone() => Log.Add("done");
    }

    // Waits until the transfer is cancelled, as a server that never answers.
    private sealed class StallingProtocolHandler : IProtocolHandler
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
