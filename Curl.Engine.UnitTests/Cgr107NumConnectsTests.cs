using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins CGR-107's <c>%{num_connects}</c> half (BL-3961): curl 8.22.0 counts the connections every
/// non-HTTP transfer opened - 1 for smtp, dict, gopher, ws, rtsp and sftp, 2 for an ftp download
/// (control and data connection), 1 after a refused ws upgrade - where the handlers report none,
/// so <see cref="ConnectionEndPointRecorder" /> puts the count of connections it saw on the report.
/// </summary>
[TestClass]
public sealed class Cgr107NumConnectsTests
{
    private static readonly IPEndPoint Local = new(IPAddress.Loopback, 64515);

    private static readonly IPEndPoint Remote = new(IPAddress.Loopback, 21);

    [TestMethod]
    public async Task ExecuteAsync_HandlerOpensOneConnection_ReportsOneConnection()
    {
        TransferResult result = await ExecuteOpeningAsync(1, TransferResult.Success(3));

        Assert.AreEqual(1, result.Report!.ConnectionCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_FtpOpensControlAndDataConnections_ReportsTwoConnections()
    {
        TransferResult result = await ExecuteOpeningAsync(2, TransferResult.Success(3));

        Assert.AreEqual(2, result.Report!.ConnectionCount);
        Assert.AreEqual(Remote, result.Report.RemoteEndPoint);
    }

    [TestMethod]
    public async Task ExecuteAsync_RefusedUpgradeAfterConnecting_ReportsOneConnection()
    {
        TransferResult result = await ExecuteOpeningAsync(1, TransferResult.Failure(CurlExitCode.HttpReturnedError, "x"));

        Assert.AreEqual(1, result.Report!.ConnectionCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_HandlerCountedItsConnections_KeepsItsCount()
    {
        TransferResult counted = TransferResult.Success(3) with { Report = new TransferReport { ConnectionCount = 1, RemoteEndPoint = Remote } };

        TransferResult result = await ExecuteOpeningAsync(2, counted);

        Assert.AreEqual(1, result.Report!.ConnectionCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReusedConnection_ReportsNoConnection()
    {
        TransferResult result = await ExecuteOpeningAsync(0, TransferResult.Success(3));

        Assert.IsNull(result.Report);
    }

    [TestMethod]
    public async Task ExecuteAsync_PooledConnectionReused_ReportsItsEndPointsAndNoConnection()
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointReportingProtocolHandler handler = new(
            new RecordingProtocolHandler("ftp", _ =>
            {
                recorder.Record(Local, Remote, reused: true);
                return ValueTask.FromResult(TransferResult.Success(3));
            }),
            recorder);

        TransferResult result = await handler.ExecuteAsync(ContextOf("ftp://127.0.0.1/f"));

        Assert.AreEqual(Remote, result.Report!.RemoteEndPoint);
        Assert.AreEqual(0, result.Report.ConnectionCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectionWithNoKnownPeer_CountsNothing()
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointReportingProtocolHandler handler = new(
            new RecordingProtocolHandler("dict", _ =>
            {
                recorder.Record(Local, null);
                return ValueTask.FromResult(TransferResult.Success(3));
            }),
            recorder);

        TransferResult result = await handler.ExecuteAsync(ContextOf("dict://127.0.0.1/"));

        Assert.IsNull(result.Report);
    }

    [TestMethod]
    public async Task ExecuteAsync_EarlierTransferOpenedConnections_CountsOnlyThisTransfers()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.Record(Local, Remote);
        recorder.Record(Local, Remote);
        EndPointReportingProtocolHandler handler = HandlerOpening(recorder, 1, TransferResult.Success(3));

        TransferResult result = await handler.ExecuteAsync(ContextOf("dict://127.0.0.1/"));

        Assert.AreEqual(1, result.Report!.ConnectionCount);
    }

    private static TransferContext ContextOf(string url) => new() { Url = CurlUrl.Parse(url), Output = new MemoryStream() };

    private static ValueTask<TransferResult> ExecuteOpeningAsync(int connections, TransferResult handlerResult)
    {
        ConnectionEndPointRecorder recorder = new();
        return HandlerOpening(recorder, connections, handlerResult).ExecuteAsync(ContextOf("ftp://127.0.0.1/f"));
    }

    private static EndPointReportingProtocolHandler HandlerOpening(ConnectionEndPointRecorder recorder, int connections, TransferResult handlerResult) =>
        new(
            new RecordingProtocolHandler("ftp", _ =>
            {
                for (var connection = 0; connection < connections; connection++)
                {
                    recorder.Record(Local, Remote);
                }

                return ValueTask.FromResult(handlerResult);
            }),
            recorder);
}
