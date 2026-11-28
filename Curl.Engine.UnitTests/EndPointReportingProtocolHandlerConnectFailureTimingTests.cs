using Curl.Console;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Checks that a connect that failed to resolve or timed out, which leaves no report, is given its elapsed
/// total time and no connection, as curl 8.22.0 reports <c>%{time_total}</c> and <c>%{num_connects}</c> (BL-6041).
/// </summary>
[TestClass]
public sealed class EndPointReportingProtocolHandlerConnectFailureTimingTests
{
    [TestMethod]
    [DataRow("ftp", CurlExitCode.CouldntResolveProxy)]
    [DataRow("ftp", CurlExitCode.CouldntResolveHost)]
    [DataRow("ftp", CurlExitCode.OperationTimedOut)]
    [DataRow("ws", CurlExitCode.CouldntResolveHost)]
    [DataRow("wss", CurlExitCode.CouldntResolveHost)]
    [DataRow("wss", CurlExitCode.OperationTimedOut)]
    public async Task ExecuteAsync_ConnectFailsWithNoReport_ReportsTheElapsedTotalAndNoConnection(string scheme, CurlExitCode exitCode)
    {
        TransferResult result = await FailAfterSevenTicksAsync(scheme, exitCode);

        TransferTimings timings = result.Report!.Timings!;
        Assert.AreEqual(7, timings.Completed - timings.Started);
        Assert.IsNull(timings.Connect);
        Assert.IsNull(timings.RequestReady);
        Assert.AreEqual(0, result.Report.ConnectionCount);
        Assert.AreEqual(0, result.Report.UploadSize);
        Assert.AreEqual(exitCode, result.ExitCode);
    }

    [TestMethod]
    [DataRow("http", CurlExitCode.CouldntResolveHost)]
    [DataRow("https", CurlExitCode.OperationTimedOut)]
    [DataRow("ftp", CurlExitCode.LoginDenied)]
    public async Task ExecuteAsync_HttpFailureOrAnotherExitCodeWithNoReport_LeavesNoReport(string scheme, CurlExitCode exitCode)
    {
        TransferResult result = await FailAfterSevenTicksAsync(scheme, exitCode);

        Assert.IsNull(result.Report);
    }

    private static async Task<TransferResult> FailAfterSevenTicksAsync(string scheme, CurlExitCode exitCode)
    {
        ManualTimeProvider time = new();
        EndPointReportingProtocolHandler handler = new(
            new RecordingProtocolHandler(scheme, _ =>
            {
                time.Advance(7);
                return ValueTask.FromResult(TransferResult.Failure(exitCode, "x"));
            }),
            new ConnectionEndPointRecorder());

        return await handler.ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse($"{scheme}://example.test/"),
            Output = Stream.Null,
            PostData = new byte[] { 1, 2 },
            TimeProvider = time,
        });
    }
}
