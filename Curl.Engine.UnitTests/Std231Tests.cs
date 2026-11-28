using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins STD-231: a connect that failed, or that <c>-m</c> cancelled, during its TLS handshake had its TCP connection
/// open, which curl 8.22.0 counts in the failed transfer's <c>%{num_connects}</c>, as 1 for both <c>https</c> and
/// <c>wss</c> (measured, BL-6028 Notes); <see cref="EndPointRecordingConnector" /> counts it, and
/// <see cref="EndPointReportingProtocolHandler" /> times a timed-out transfer from <c>-m</c>'s start (ADR-1467).
/// </summary>
[TestClass]
public sealed class Std231Tests
{
    private static readonly ConnectTarget Target = new("127.0.0.1", 29990, true);

    private static readonly ConnectResult HandshakeTimedOut =
        ConnectResult.Failed(CurlExitCode.OperationTimedOut, "Connection timed out after 2000 milliseconds").WithTcpConnectedBeforeFailure(true);

    [TestMethod]
    public async Task ConnectAsync_FailedAfterTheTcpConnectionOpened_CountsOneConnectionOnAReportItGivesTheFailure()
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingConnector connector = new(new AnsweringConnector(() => HandshakeTimedOut), recorder);

        await connector.ConnectAsync(Target, CancellationToken.None);
        TransferResult reported = recorder.ReportOn(TransferResult.Failure(CurlExitCode.OperationTimedOut, "timed out"));

        Assert.IsNotNull(reported.Report);
        Assert.AreEqual(1, reported.Report.ConnectionCount);
    }

    [TestMethod]
    public async Task ConnectAsync_CancelledAfterTheTcpConnectionOpened_CountsOneConnectionEvenWhenTheHandlerCounts()
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingConnector connector = new(new AnsweringConnector(() => throw MarkedCancellation()), recorder);

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await connector.ConnectAsync(Target, CancellationToken.None));
        TransferResult reported = recorder.ReportOn(
            TransferResult.Failure(CurlExitCode.OperationTimedOut, "timed out") with { Report = new TransferReport() },
            handlerCountsConnections: true);

        Assert.AreEqual(1, reported.Report!.ConnectionCount);
    }

    [TestMethod]
    public async Task ConnectAsync_CancelledBeforeAnyTcpConnection_CountsNothing()
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingConnector connector = new(new AnsweringConnector(() => throw new OperationCanceledException()), recorder);
        TransferResult failure = TransferResult.Failure(CurlExitCode.OperationTimedOut, "timed out");

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await connector.ConnectAsync(Target, CancellationToken.None));

        Assert.AreSame(failure, recorder.ReportOn(failure));
    }

    [TestMethod]
    public async Task ReportOn_SuccessAfterAFailedConnect_DoesNotCountTheFailedOne()
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingConnector connector = new(new AnsweringConnector(() => HandshakeTimedOut), recorder);
        TransferResult success = TransferResult.Success(0);

        await connector.ConnectAsync(Target, CancellationToken.None);

        Assert.AreSame(success, recorder.ReportOn(success));
    }

    [TestMethod]
    public async Task ReportOn_HandlerReportAlreadyCounted_KeepsTheHandlerCount()
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingConnector connector = new(new AnsweringConnector(() => HandshakeTimedOut), recorder);
        TransferResult failure = TransferResult.Failure(CurlExitCode.OperationTimedOut, "timed out") with { Report = new TransferReport { ConnectionCount = 2 } };

        await connector.ConnectAsync(Target, CancellationToken.None);

        Assert.AreSame(failure, recorder.ReportOn(failure, handlerCountsConnections: true));
    }

    [TestMethod]
    public async Task Clear_AfterAFailedConnect_ForgetsIt()
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingConnector connector = new(new AnsweringConnector(() => HandshakeTimedOut), recorder);
        TransferResult failure = TransferResult.Failure(CurlExitCode.OperationTimedOut, "timed out");

        await connector.ConnectAsync(Target, CancellationToken.None);
        recorder.Clear();

        Assert.AreSame(failure, recorder.ReportOn(failure));
    }

    [TestMethod]
    [DataRow(0, true, true, DisplayName = "The first hop counts from the -m watchdog's start")]
    [DataRow(1, true, false, DisplayName = "A redirect hop counts from its own start")]
    [DataRow(0, false, false, DisplayName = "With no operation start the handler's start counts")]
    public async Task ExecuteAsync_Cancelled_TimesTheTransferFromWhereMaxTimeCounts(int redirectsFollowed, bool hasOperationStart, bool countsFromIt)
    {
        long operationStarted = TimeProvider.System.GetTimestamp() - TimeProvider.System.TimestampFrequency;
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("wss://127.0.0.1:29990/"),
            Output = new MemoryStream(),
            Http = new Curl.Protocol.Abstractions.HttpRequestOptions { RedirectsFollowed = redirectsFollowed },
            OperationStarted = hasOperationStart ? operationStarted : null,
        };
        EndPointReportingProtocolHandler handler = new(
            new RecordingProtocolHandler("wss", _ => throw new OperationCanceledException()),
            new ConnectionEndPointRecorder());

        OperationCanceledException canceled = await Assert.ThrowsAsync<OperationCanceledException>(async () => await handler.ExecuteAsync(context));

        TransferTimings timings = EndPointReportingProtocolHandler.ReportCarriedBy(canceled)!.Timings!;
        Assert.AreEqual(countsFromIt, timings.Started == operationStarted);
    }

    [TestMethod]
    [DataRow(CurlExitCode.OperationTimedOut, true, DisplayName = "A connect limit's failure counts from the -m watchdog's start")]
    [DataRow(CurlExitCode.CouldntConnect, false, DisplayName = "Another failure counts from the handler's start")]
    public async Task ExecuteAsync_FailedAfterTheTcpConnectionOpened_TimesATimeoutFromWhereMaxTimeCounts(CurlExitCode exitCode, bool countsFromIt)
    {
        long operationStarted = TimeProvider.System.GetTimestamp() - TimeProvider.System.TimestampFrequency;
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingConnector connector = new(new AnsweringConnector(() => HandshakeTimedOut), recorder);
        TransferContext context = new() { Url = CurlUrl.Parse("wss://127.0.0.1:29990/"), Output = new MemoryStream(), OperationStarted = operationStarted };
        EndPointReportingProtocolHandler handler = new(
            new RecordingProtocolHandler("wss", async _ =>
            {
                await connector.ConnectAsync(Target, CancellationToken.None);
                return TransferResult.Failure(exitCode, "failed");
            }),
            recorder);

        TransferResult result = await handler.ExecuteAsync(context);

        Assert.AreEqual(1, result.Report!.ConnectionCount);
        Assert.AreEqual(countsFromIt, result.Report.Timings!.Started == operationStarted);
    }

    private static OperationCanceledException MarkedCancellation()
    {
        OperationCanceledException canceled = new();
        canceled.Data[ConnectResult.TcpConnectedOnCancellationKey] = true;
        return canceled;
    }

    /// <summary>A connector whose every connect runs the given answer; QUIC keeps the interface's default refusal.</summary>
    private sealed class AnsweringConnector(Func<ConnectResult> answer) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) => ValueTask.FromResult(answer());

        public ValueTask<ConnectResult> ConnectMultiplexedSessionAsync(ConnectTarget target, Func<IMultiplexedConnection, IConnection> openSession, CancellationToken cancellationToken) =>
            ValueTask.FromResult(answer());
    }
}
