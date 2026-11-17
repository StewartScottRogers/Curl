using System.Net;
using System.Text;
using Curl.Original.Protocol.Smb.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Smb;

/// <summary>
/// Pins that an <c>smb://</c> download reports the bytes it received as <c>%{size_download}</c>
/// (and so <c>%{size_delivered}</c> and a non-zero <c>%{speed_download}</c>) and that an upload is unchanged (BL-5389).
/// </summary>
[TestClass]
public sealed class SmbDownloadSizeReportTests
{
    private static readonly NetworkCredential User = new("User", "Password");

    [TestMethod]
    public async Task ExecuteAsync_Download_ReportsTheBytesReceivedAsTheDownloadSizeOverANonZeroTime()
    {
        var connection = new ScriptedConnection(
            SmbRecordedExchange.NegotiateResponse,
            SmbRecordedExchange.SessionSetupAccepted,
            SmbRecordedExchange.TreeConnectAccepted,
            SmbRecordedExchange.OpenAccepted,
            SmbRecordedExchange.ReadAccepted,
            SmbRecordedExchange.CloseAccepted,
            SmbRecordedExchange.TreeDisconnectAccepted);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(SmbRecordedExchange.DownloadUrl),
            Output = new MemoryStream(),
            Credentials = User,
            TimeProvider = new AdvancingTimeProvider(),
        };

        TransferResult result = await Run(context, connection);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual((long)SmbRecordedExchange.FileContent.Length, result.BytesTransferred);
        Assert.AreEqual(result.BytesTransferred, result.Report!.DownloadSize);
        Assert.AreEqual(0L, result.Report.UploadSize);
        Assert.IsGreaterThan(result.Report.Timings!.RequestReady!.Value, result.Report.Timings.Completed);
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_ReportsTheUploadSizeAndNoDownloadSize()
    {
        var connection = new ScriptedConnection(
            SmbRecordedExchange.NegotiateResponse,
            SmbRecordedExchange.SessionSetupAccepted,
            SmbRecordedExchange.TreeConnectAccepted,
            SmbRecordedExchange.UploadOpenCreated,
            SmbRecordedExchange.WriteAccepted(11),
            SmbRecordedExchange.CloseAccepted,
            SmbRecordedExchange.TreeDisconnectAccepted);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(SmbRecordedExchange.UploadUrl),
            Output = new MemoryStream(),
            Upload = new MemoryStream(Encoding.ASCII.GetBytes(SmbRecordedExchange.FileContent)),
            Credentials = User,
            TimeProvider = new AdvancingTimeProvider(),
        };

        TransferResult result = await Run(context, connection);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(11L, result.Report!.UploadSize);
        Assert.AreEqual(0L, result.Report.DownloadSize);
    }

    private static async Task<TransferResult> Run(TransferContext context, ScriptedConnection connection) =>
        await new SmbProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), SmbCurlOperatingSystem.Linux).ExecuteAsync(context);

    private sealed class AdvancingTimeProvider : TimeProvider
    {
        private long now;

        public override long GetTimestamp() => now += 1000;
    }
}
