using System.Net;
using Curl.Console;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Checks that the engine path gives a report without timings its connect time, total time and upload size (BL-5410).
/// </summary>
[TestClass]
public sealed class EndPointReportingProtocolHandlerTimingsTests
{
    private static readonly IPEndPoint Remote = new(IPAddress.Loopback, 6379);

    [TestMethod]
    public async Task ExecuteAsync_ReportHasNoTimings_StampsConnectTotalAndUploadSize()
    {
        ManualTimeProvider time = new();
        ConnectionEndPointRecorder recorder = new();
        EndPointReportingProtocolHandler handler = new(
            new RecordingProtocolHandler("redis", _ =>
            {
                time.Advance(5);
                recorder.Record(null, Remote, false, new ConnectTimings(0, 1, 5, null));
                time.Advance(20);
                return ValueTask.FromResult(TransferResult.Success(2));
            }),
            recorder);

        TransferResult result = await handler.ExecuteAsync(ContextOf(time, postData: [1, 2, 3]));

        TransferTimings timings = result.Report!.Timings!;
        Assert.AreEqual(25, timings.Completed - timings.Started);
        Assert.AreEqual(5, timings.Connect!.Connected);
        Assert.AreEqual(3, result.Report.UploadSize);
        Assert.AreEqual(2, result.Report.DownloadSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadStream_CountsTheBytesItGave()
    {
        ManualTimeProvider time = new();
        EndPointReportingProtocolHandler handler = new(
            new RecordingProtocolHandler("kafka", async context =>
            {
                byte[] buffer = new byte[10];
                _ = await context.Upload!.ReadAsync(buffer);
                return TransferResult.Success(0);
            }),
            new ConnectionEndPointRecorder());

        TransferResult result = await handler.ExecuteAsync(ContextOf(time, upload: new MemoryStream(new byte[4])));

        Assert.AreEqual(4, result.Report!.UploadSize);
        Assert.IsNull(result.Report.Timings!.Connect);
    }

    [TestMethod]
    public async Task ExecuteAsync_HandlerStampedItsOwnTimings_KeepsThem()
    {
        ManualTimeProvider time = new();
        TransferTimings own = new(1, null, null, null, null, 2);
        EndPointReportingProtocolHandler handler = new(
            new RecordingProtocolHandler("smb", _ => ValueTask.FromResult(
                TransferResult.Success(0) with { Report = new TransferReport { Timings = own, UploadSize = 9 } })),
            new ConnectionEndPointRecorder());

        TransferResult result = await handler.ExecuteAsync(ContextOf(time, postData: [1, 2, 3]));

        Assert.AreSame(own, result.Report!.Timings);
        Assert.AreEqual(9, result.Report.UploadSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_TransferFailsWithNoReport_LeavesNoReport()
    {
        ManualTimeProvider time = new();
        EndPointReportingProtocolHandler handler = new(
            new RecordingProtocolHandler("nats", _ =>
            {
                time.Advance(7);
                return ValueTask.FromResult(TransferResult.Failure(CurlExitCode.CouldntConnect, "x"));
            }),
            new ConnectionEndPointRecorder());

        TransferResult result = await handler.ExecuteAsync(ContextOf(time, postData: [1, 2]));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsNull(result.Report);
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelledAfterConnecting_CarriesTimingsAndEndPointsOnTheCancellation_BL5491()
    {
        ManualTimeProvider time = new();
        ConnectionEndPointRecorder recorder = new();
        IPEndPoint remote = new(IPAddress.Loopback, 4222);
        EndPointReportingProtocolHandler handler = new(
            new RecordingProtocolHandler("nats", _ =>
            {
                recorder.Record(new IPEndPoint(IPAddress.Loopback, 50000), remote, timings: new ConnectTimings(1, null, 5, null));
                time.Advance(7);
                throw new OperationCanceledException();
            }),
            recorder);

        OperationCanceledException cancelled = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await handler.ExecuteAsync(ContextOf(time)));

        TransferReport report = EndPointReportingProtocolHandler.ReportCarriedBy(cancelled)!;
        Assert.AreEqual(remote, report.RemoteEndPoint);
        Assert.IsNotNull(report.Timings);
        Assert.AreEqual(1, report.ConnectionCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelledBeforeConnecting_CarriesNoReport_BL5491()
    {
        ManualTimeProvider time = new();
        EndPointReportingProtocolHandler handler = new(
            new RecordingProtocolHandler("nats", _ => throw new OperationCanceledException()),
            new ConnectionEndPointRecorder());

        OperationCanceledException cancelled = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await handler.ExecuteAsync(ContextOf(time)));

        Assert.IsNull(EndPointReportingProtocolHandler.ReportCarriedBy(cancelled));
    }

    [TestMethod]
    public async Task ExecuteAsync_FailureWithReportAndNoConnection_ReportsNoUpload()
    {
        ManualTimeProvider time = new();
        EndPointReportingProtocolHandler handler = new(
            new RecordingProtocolHandler("nats", _ => ValueTask.FromResult(
                TransferResult.Failure(CurlExitCode.CouldntConnect, "x") with { Report = new TransferReport() })),
            new ConnectionEndPointRecorder());

        TransferResult result = await handler.ExecuteAsync(ContextOf(time, postData: [1]));

        Assert.AreEqual(0, result.Report!.UploadSize);
        Assert.IsNotNull(result.Report.Timings);
    }

    [TestMethod]
    public async Task ExecuteAsync_HandlerSentItsOwnUploadSize_KeepsIt()
    {
        ManualTimeProvider time = new();
        EndPointReportingProtocolHandler handler = new(
            new RecordingProtocolHandler("mqtt", _ => ValueTask.FromResult(
                TransferResult.Success(0) with { Report = new TransferReport { UploadSize = 8 } })),
            new ConnectionEndPointRecorder());

        TransferResult result = await handler.ExecuteAsync(ContextOf(time, postData: [1]));

        Assert.AreEqual(8, result.Report!.UploadSize);
        Assert.IsNotNull(result.Report.Timings);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectedNonHttpTransferFailsWithReport_ReportsNoUploadOfDashDData()
    {
        ManualTimeProvider time = new();
        ConnectionEndPointRecorder recorder = new();
        EndPointReportingProtocolHandler handler = new(
            new RecordingProtocolHandler("redis", _ =>
            {
                recorder.Record(null, Remote, false, new ConnectTimings(0, 1, 5, null));
                return ValueTask.FromResult(
                    TransferResult.Failure(CurlExitCode.LoginDenied, "x") with { Report = new TransferReport() });
            }),
            recorder);

        TransferResult result = await handler.ExecuteAsync(ContextOf(time, postData: [1, 2, 3]));

        Assert.AreEqual(0, result.Report!.UploadSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectedHttpTransferFailsWithReport_KeepsDashDDataAsUploaded()
    {
        ManualTimeProvider time = new();
        ConnectionEndPointRecorder recorder = new();
        EndPointReportingProtocolHandler handler = new(
            new RecordingProtocolHandler("http", _ =>
            {
                recorder.Record(null, Remote, false, new ConnectTimings(0, 1, 5, null));
                return ValueTask.FromResult(
                    TransferResult.Failure(CurlExitCode.OperationTimedOut, "x") with { Report = new TransferReport() });
            }),
            recorder);

        TransferResult result = await handler.ExecuteAsync(ContextOf(time, postData: [1, 2, 3], url: "http://127.0.0.1/"));

        Assert.AreEqual(3, result.Report!.UploadSize);
    }

    private static TransferContext ContextOf(TimeProvider time, byte[]? postData = null, Stream? upload = null, string url = "redis://127.0.0.1/") => new()
    {
        Url = CurlUrl.Parse(url),
        Output = new MemoryStream(),
        TimeProvider = time,
        PostData = postData is null ? null : (ReadOnlyMemory<byte>?)postData,
        Upload = upload,
    };
}
