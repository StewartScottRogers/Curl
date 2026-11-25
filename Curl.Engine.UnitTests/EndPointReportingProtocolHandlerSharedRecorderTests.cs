using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Pins that transfers of one parallel run sharing the connection recorder keep the HTTP handler's own connection
/// count: one connection multiplexed by three transfers is counted for the transfer that opened it alone (BL-5337).
/// </summary>
[TestClass]
public sealed class EndPointReportingProtocolHandlerSharedRecorderTests
{
    private static readonly IPEndPoint Local = new(IPAddress.Loopback, 64515);

    private static readonly IPEndPoint Remote = new(IPAddress.Loopback, 2628);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_HttpTransferReusedAConnectionAnotherTransferOpened_CountsNone()
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointReportingProtocolHandler handler = HandlerOf("http", recorder, connectionCount: 0);

        TransferResult result = await handler.ExecuteAsync(ContextOf("http://h/"));
        Diagnostics.Act("reported connection count", result.Report!.ConnectionCount);

        Diagnostics.Assert("reported connection count", 0, result.Report.ConnectionCount);
        Assert.AreEqual(0, result.Report.ConnectionCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_HttpsTransferOpenedTheConnection_KeepsTheHandlersCountOfOne()
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointReportingProtocolHandler handler = HandlerOf("https", recorder, connectionCount: 1);

        TransferResult result = await handler.ExecuteAsync(ContextOf("https://h/"));
        Diagnostics.Act("reported connection count", result.Report!.ConnectionCount);

        Diagnostics.Assert("reported connection count", 1, result.Report.ConnectionCount);
        Assert.AreEqual(1, result.Report.ConnectionCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_ThreeTransfersShareOneRecorderAndOneConnection_CountsOneInTotal()
    {
        ConnectionEndPointRecorder recorder = new();
        int[] counts =
        [
            (await HandlerOf("http", recorder, connectionCount: 1).ExecuteAsync(ContextOf("http://h/a"))).Report!.ConnectionCount,
            (await HandlerOf("http", recorder, connectionCount: 0).ExecuteAsync(ContextOf("http://h/b"))).Report!.ConnectionCount,
            (await HandlerOf("http", recorder, connectionCount: 0).ExecuteAsync(ContextOf("http://h/c"))).Report!.ConnectionCount,
        ];
        Diagnostics.Act("connection counts", string.Join(",", counts));

        Diagnostics.Assert("connection counts", "1,0,0", string.Join(",", counts));
        CollectionAssert.AreEqual(new[] { 1, 0, 0 }, counts);
    }

    [TestMethod]
    public async Task ExecuteAsync_NonHttpTransferOpenedAConnection_CountsItFromTheRecorder()
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointReportingProtocolHandler handler = new(
            new RecordingProtocolHandler("dict", _ =>
            {
                recorder.Record(Local, Remote);
                return ValueTask.FromResult(TransferResult.Success(3));
            }),
            recorder);

        TransferResult result = await handler.ExecuteAsync(ContextOf("dict://h/"));
        Diagnostics.Act("reported connection count", result.Report!.ConnectionCount);

        Diagnostics.Assert("reported connection count", 1, result.Report.ConnectionCount);
        Assert.AreEqual(1, result.Report.ConnectionCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_HttpTransferFailedWithNoReport_CountsTheConnectionFromTheRecorder()
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointReportingProtocolHandler handler = new(
            new RecordingProtocolHandler("http", _ =>
            {
                recorder.Record(Local, Remote);
                return ValueTask.FromResult(TransferResult.Failure(CurlExitCode.OperationTimedOut, "timed out"));
            }),
            recorder);

        TransferResult result = await handler.ExecuteAsync(ContextOf("http://h/"));
        Diagnostics.Act("reported connection count", result.Report!.ConnectionCount);

        Diagnostics.Assert("reported connection count", 1, result.Report.ConnectionCount);
        Assert.AreEqual(1, result.Report.ConnectionCount);
    }

    private static EndPointReportingProtocolHandler HandlerOf(string scheme, ConnectionEndPointRecorder recorder, int connectionCount) =>
        new(
            new RecordingProtocolHandler(scheme, _ =>
            {
                // Another transfer of the run opened a connection through the shared recorder meanwhile.
                recorder.Record(Local, Remote);
                return ValueTask.FromResult(TransferResult.Success(3) with { Report = new TransferReport { DownloadSize = 3, ConnectionCount = connectionCount } });
            }),
            recorder);

    private static TransferContext ContextOf(string url) => new()
    {
        Url = CurlUrl.Parse(url),
        Output = new MemoryStream(),
    };
}
