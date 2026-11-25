using Curl.Original.Protocol.Gopher.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Gopher;

/// <summary>
/// Pins the timings behind <c>%{time_connect}</c>, <c>%{time_starttransfer}</c>, <c>%{time_total}</c>
/// and <c>%{speed_download}</c> that a Gopher transfer reports (BL-5390).
/// </summary>
[TestClass]
public sealed class GopherWriteOutTimingsTests
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
    public async Task ExecuteAsync_Transfer_ReportsPretransferAndPosttransferAfterConnectAndStartTransferBeforeTotal()
    {
        TransferResult result = await RunAsync();

        TransferTimings timings = result.Report!.Timings!;
        Assert.IsNotNull(timings.RequestReady);
        Assert.IsNotNull(timings.RequestSent);
        Assert.IsGreaterThan(timings.Started, timings.RequestReady.Value);
        Assert.IsGreaterThanOrEqualTo(timings.RequestReady.Value, timings.RequestSent.Value);
        Assert.IsLessThan(timings.Completed, timings.FirstByteReceived!.Value);
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
        Assert.AreEqual(17, result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailedTransfer_ReportsNoTimings()
    {
        var context = new TransferContext { Url = CurlUrl.Parse("gopher://127.0.0.1:41070/0%00"), Output = new MemoryStream(), TimeProvider = new AdvancingTimeProvider() };

        TransferResult result = await new GopherProtocolHandler(new FakeConnector(ConnectResult.Connected(new ScriptedConnection(), Reached))).ExecuteAsync(context);

        Assert.AreNotEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.Report?.Timings);
    }

    private static async Task<TransferResult> RunAsync()
    {
        var connection = new ScriptedConnection("hello gopher\r\n.\r\n"u8.ToArray());
        var context = new TransferContext { Url = CurlUrl.Parse("gopher://127.0.0.1:41070/0x"), Output = new MemoryStream(), TimeProvider = new AdvancingTimeProvider() };
        return await new GopherProtocolHandler(new FakeConnector(ConnectResult.Connected(connection, Reached))).ExecuteAsync(context);
    }

    // A clock that moves 1000 ticks on every reading, so every later stamp is greater than every earlier one.
    private sealed class AdvancingTimeProvider : TimeProvider
    {
        private long now;

        public override long GetTimestamp() => now += 1000;
    }
}
