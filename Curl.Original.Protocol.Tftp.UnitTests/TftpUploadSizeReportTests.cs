using System.Globalization;
using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Tftp.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Tftp;

/// <summary>
/// Pins that a <c>tftp://</c> upload (<c>-T</c>) reports the bytes it sent as
/// <c>%{size_upload}</c> through <see cref="TransferReport.UploadSize" />, with no download
/// size, and that a download still reports what it received.
/// </summary>
[TestClass]
public sealed class TftpUploadSizeReportTests
{
    private static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 69);

    private static readonly IPEndPoint TransferEndPoint = new(IPAddress.Loopback, 50123);

    [TestMethod]
    [DataRow(6)]
    [DataRow(300000)]
    public async Task ExecuteAsync_Upload_ReportsTheBytesSentAsUploadSizeAndNoDownloadSize(int length)
    {
        var channel = new AcknowledgingDatagramChannel(ServerEndPoint, TransferEndPoint);
        var result = await RunAsync(channel, new MemoryStream(new byte[length]), upload: true);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNotNull(result.Report);
        Assert.AreEqual(length, result.Report.UploadSize);
        Assert.AreEqual(0, result.Report.DownloadSize);
        Assert.AreEqual(
            string.Create(CultureInfo.InvariantCulture, $"up={length} down=0"),
            string.Create(CultureInfo.InvariantCulture, $"up={result.Report.UploadSize} down={result.Report.DownloadSize}"));
    }

    [TestMethod]
    public async Task ExecuteAsync_Download_ReportsTheBytesReceivedAndNoUploadSize()
    {
        var channel = new FallsSilentDatagramChannel(ServerEndPoint, new ManualTimeProvider(), [([0, 3, 0, 1, .. "hello"u8.ToArray()], TransferEndPoint)]);
        var result = await RunAsync(channel, new MemoryStream(), upload: false);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(5, result.BytesTransferred);
        Assert.AreEqual(0, result.Report?.UploadSize ?? 0);
    }

    private static async Task<TransferResult> RunAsync(IDatagramChannel channel, MemoryStream stream, bool upload) =>
        await new TftpProtocolHandler(new RecordingDatagramConnector(DatagramOpenResult.Opened(channel)))
            .ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse("tftp://h/dest.txt"),
                Output = upload ? new MemoryStream() : stream,
                Upload = upload ? stream : null,
            });
}
