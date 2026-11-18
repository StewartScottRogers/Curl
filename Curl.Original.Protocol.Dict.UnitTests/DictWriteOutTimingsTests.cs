using Curl.Original.Protocol.Dict.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Dict;

/// <summary>
/// Pins the timings behind <c>%{time_connect}</c>, <c>%{time_starttransfer}</c>, <c>%{time_total}</c>
/// and <c>%{speed_download}</c> that a DICT transfer reports (BL-5390).
/// </summary>
[TestClass]
public sealed class DictWriteOutTimingsTests
{
    private static readonly ConnectTimings Reached = new(1, 2, 3, null);

    [TestMethod]
    public async Task ExecuteAsync_Transfer_ReportsConnectStartTransferAndTotalTimes()
    {
        TransferResult result = await RunAsync();

        TransferTimings timings = result.Report!.Timings!;
        Assert.AreEqual(Reached, timings.Connect);
        Assert.IsGreaterThan(timings.Started, timings.FirstByteReceived!.Value);
        Assert.IsGreaterThanOrEqualTo(timings.FirstByteReceived.Value, timings.Completed);
        Assert.IsGreaterThan(timings.Started, timings.Completed);
    }

    [TestMethod]
    public async Task ExecuteAsync_Transfer_ReportsBytesAndElapsedTimeThatGiveANonZeroSpeed()
    {
        TransferResult result = await RunAsync();

        TransferTimings timings = result.Report!.Timings!;
        Assert.IsGreaterThan(0, result.Report.DownloadSize);
        Assert.IsGreaterThan(0, timings.Completed - timings.Started);
    }

    [TestMethod]
    public async Task ExecuteAsync_Transfer_KeepsTheDownloadSizeItAlreadyReported()
    {
        TransferResult result = await RunAsync();

        Assert.AreEqual(result.BytesTransferred, result.Report!.DownloadSize);
        Assert.AreEqual(54, result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailedTransfer_ReportsNoTimings()
    {
        var context = new TransferContext { Url = CurlUrl.Parse("dict://127.0.0.1:41628/d:%01"), Output = new MemoryStream(), TimeProvider = new AdvancingTimeProvider() };

        TransferResult result = await new DictProtocolHandler(new RecordingConnector(ConnectResult.Connected(new ScriptedConnection(), Reached))).ExecuteAsync(context);

        Assert.AreNotEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.Report?.Timings);
    }

    private static async Task<TransferResult> RunAsync()
    {
        var connection = new ScriptedConnection("150 1 definitions\r\n151 \"x\" db \"Db\"\r\nhello\r\n.\r\n250 ok\r\n"u8.ToArray());
        var context = new TransferContext { Url = CurlUrl.Parse("dict://127.0.0.1:41628/d:x"), Output = new MemoryStream(), TimeProvider = new AdvancingTimeProvider() };
        return await new DictProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection, Reached))).ExecuteAsync(context);
    }

    // A clock that moves 1000 ticks on every reading, so every later stamp is greater than every earlier one.
    private sealed class AdvancingTimeProvider : TimeProvider
    {
        private long now;

        public override long GetTimestamp() => now += 1000;
    }
}
