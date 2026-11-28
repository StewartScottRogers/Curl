using Curl.Protocol.Abstractions;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Core;

/// <summary>
/// Pins where <see cref="RedirectFollower" /> counts a chain's times from when <c>-m</c> ends a
/// followed hop: the <c>-m</c> start (<see cref="ITransferContext.OperationStarted" />), a few
/// milliseconds before the first handler's, so <c>%{time_total}</c> is not under <c>-m</c>'s value,
/// as curl 8.22.0's is not under <c>curl -L -m 2</c> against a 302 to a server that never answers
/// (measured 2026-11-28, BL-6092 Notes).
/// </summary>
[TestClass]
public sealed class RedirectFollowerMaxTimeTotalTests
{
    private const long MaxTimeStarted = 1000;
    private const long FirstHandlerStarted = 1030;
    private const long LastHandlerStarted = 2300;
    private const long Completed = 3005;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task FollowAsync_MaxTimeEndsAFollowedHop_CountsTheTimesFromTheMaxTimeStart()
    {
        TransferTimings timings = await FollowedTimingsAsync(MaxTimeStarted, TimedOut());

        Assert.AreEqual(MaxTimeStarted, timings.Started);
        Assert.AreEqual(Completed, timings.Completed);
        Assert.AreEqual(TimeSpan.FromMilliseconds(LastHandlerStarted - MaxTimeStarted), timings.RedirectDuration);
    }

    [TestMethod]
    public async Task FollowAsync_FollowedHopSucceedsUnderMaxTime_CountsTheTimesFromTheFirstHandlerStart()
    {
        TransferTimings timings = await FollowedTimingsAsync(MaxTimeStarted, TransferResult.Success(0));

        Assert.AreEqual(FirstHandlerStarted, timings.Started);
        Assert.AreEqual(TimeSpan.FromMilliseconds(LastHandlerStarted - FirstHandlerStarted), timings.RedirectDuration);
    }

    [TestMethod]
    public async Task FollowAsync_FollowedHopTimesOutWithoutMaxTime_CountsTheTimesFromTheFirstHandlerStart()
    {
        TransferTimings timings = await FollowedTimingsAsync(null, TimedOut());

        Assert.AreEqual(FirstHandlerStarted, timings.Started);
        Assert.AreEqual(TimeSpan.FromMilliseconds(LastHandlerStarted - FirstHandlerStarted), timings.RedirectDuration);
    }

    [TestMethod]
    public async Task FollowAsync_MaxTimeEndsTheFirstHop_KeepsTheTimingsItsHandlerReported()
    {
        TransferTimings timings = await TimingsAsync(MaxTimeStarted, Timed(TimedOut(), FirstHandlerStarted));

        Assert.AreEqual(FirstHandlerStarted, timings.Started);
        Assert.AreEqual(TimeSpan.Zero, timings.RedirectDuration);
    }

    [TestMethod]
    public async Task FollowAsync_MaxTimeEndsAFollowedHopThatReportedNoTimings_ReportsNone()
    {
        TransferResult result = await FollowAsync(
            MaxTimeStarted,
            Timed(Redirect(), FirstHandlerStarted),
            TimedOut() with { Report = new TransferReport() });

        Assert.IsNull(result.Report!.Timings);
    }

    private static TransferResult TimedOut() =>
        TransferResult.Failure(CurlExitCode.OperationTimedOut, "Operation timed out after 2000 milliseconds with 0 bytes received", 0);

    private static TransferResult Redirect() =>
        TransferResult.Success(0) with { Report = new TransferReport { ResponseCode = 302, RedirectUrl = "http://127.0.0.1:38193/" } };

    private static TransferResult Timed(TransferResult result, long started) =>
        result with
        {
            Report = (result.Report ?? new TransferReport()) with { Timings = new TransferTimings(started, null, null, null, null, Completed) },
        };

    private Task<TransferTimings> FollowedTimingsAsync(long? operationStarted, TransferResult last) =>
        TimingsAsync(operationStarted, Timed(Redirect(), FirstHandlerStarted), Timed(last, LastHandlerStarted));

    private async Task<TransferTimings> TimingsAsync(long? operationStarted, params TransferResult[] script)
    {
        TransferResult result = await FollowAsync(operationStarted, script);
        return result.Report!.Timings!;
    }

    private async Task<TransferResult> FollowAsync(long? operationStarted, params TransferResult[] script)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://127.0.0.1:38192/"),
            Output = Stream.Null,
            Http = new HttpRequestOptions { FollowRedirects = true },
            MaxTime = operationStarted is null ? null : TimeSpan.FromSeconds(2),
            OperationStarted = operationStarted,
            TimeProvider = new MillisecondTimeProvider(),
        };
        diagnostics.Arrange("operation started", operationStarted);
        diagnostics.Arrange("hops", script.Length);

        TransferResult result = await new RedirectFollower(new ProtocolDispatcher([new ScriptedHandler(script)])).FollowAsync(context, new RedirectPolicy());

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("timings", result.Report?.Timings);
        return result;
    }

    private sealed class ScriptedHandler(TransferResult[] script) : IProtocolHandler
    {
        private int calls;

        public IReadOnlyCollection<string> SupportedSchemes => ["http"];

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context) => ValueTask.FromResult(script[calls++]);
    }

    private sealed class MillisecondTimeProvider : TimeProvider
    {
        public override long TimestampFrequency => 1000;
    }
}
