using Curl.Console;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins STD-231 for a handler that times itself, as HTTP does: a first hop that ends with exit 28 counts its
/// <c>%{time_total}</c> from <c>-m</c>'s start (<see cref="ITransferContext.OperationStarted" />), which is before the
/// handler's own, so the total is not under the <c>-m</c> value, as curl's is not (ADR-1470).
/// </summary>
[TestClass]
public sealed class Std231TimeTotalTests
{
    private static readonly TransferTimings HandlersOwn = new(30, null, 2000, 2000, 2000, 2000);

    [TestMethod]
    public async Task ExecuteAsync_HandlerTimedItsOwnTimedOutTransfer_CountsItFromMaxTimesStart()
    {
        TransferResult result = await HandlerEndingWith(TimedOut()).ExecuteAsync(ContextOf(operationStarted: 0));

        Assert.AreEqual(HandlersOwn with { Started = 0 }, result.Report!.Timings);
        Assert.AreEqual(3, result.Report.ConnectionCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_CancellationCarriesTheHandlersTimings_CountsThemFromMaxTimesStart()
    {
        OperationCanceledException cancellation = new();
        cancellation.Data[EndPointReportingProtocolHandler.ReportDataKey] = TimedOut().Report;
        EndPointReportingProtocolHandler handler = new(
            new RecordingProtocolHandler("https", _ => throw cancellation),
            new ConnectionEndPointRecorder());

        OperationCanceledException thrown = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await handler.ExecuteAsync(ContextOf(operationStarted: 0)));

        Assert.AreEqual(HandlersOwn with { Started = 0 }, EndPointReportingProtocolHandler.ReportCarriedBy(thrown)!.Timings);
    }

    [TestMethod]
    public async Task ExecuteAsync_TimedOutWithoutMaxTime_KeepsTheHandlersStart()
    {
        TransferResult result = await HandlerEndingWith(TimedOut()).ExecuteAsync(ContextOf(operationStarted: null));

        Assert.AreSame(HandlersOwn, result.Report!.Timings);
    }

    [TestMethod]
    public async Task ExecuteAsync_RedirectHopTimedOut_KeepsTheHandlersStart()
    {
        TransferResult result = await HandlerEndingWith(TimedOut()).ExecuteAsync(ContextOf(operationStarted: 0, redirectsFollowed: 1));

        Assert.AreSame(HandlersOwn, result.Report!.Timings);
    }

    [TestMethod]
    public async Task ExecuteAsync_TransferSucceededUnderMaxTime_KeepsTheHandlersStart()
    {
        TransferResult success = TransferResult.Success(0) with { Report = new TransferReport { Timings = HandlersOwn } };

        TransferResult result = await HandlerEndingWith(success).ExecuteAsync(ContextOf(operationStarted: 0));

        Assert.AreSame(HandlersOwn, result.Report!.Timings);
    }

    private static TransferResult TimedOut() =>
        TransferResult.Failure(CurlExitCode.OperationTimedOut, "Connection timed out after 2000 milliseconds") with
        {
            Report = new TransferReport { Timings = HandlersOwn, ConnectionCount = 3 },
        };

    private static EndPointReportingProtocolHandler HandlerEndingWith(TransferResult result) => new(
        new RecordingProtocolHandler("https", _ => ValueTask.FromResult(result)),
        new ConnectionEndPointRecorder());

    private static TransferContext ContextOf(long? operationStarted, int redirectsFollowed = 0) => new()
    {
        Url = CurlUrl.Parse("https://127.0.0.1:29990/"),
        Output = new MemoryStream(),
        TimeProvider = new ManualTimeProvider(),
        OperationStarted = operationStarted,
        Http = new Curl.Protocol.Abstractions.HttpRequestOptions { RedirectsFollowed = redirectsFollowed },
    };
}
