using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Rtsp.Fakes;

namespace Curl.Original.Protocol.Rtsp;

/// <summary>
/// BL-4934: for the tool's <c>OPTIONS *</c> whose reply body is discarded, curl 8.22.0 (Schannel) prints
/// <c>%{size_download}</c> 0 and the reply's <c>%{content_type}</c> (outside review STD-024).
/// </summary>
[TestClass]
public sealed class RtspWriteOutTests
{
    [TestMethod]
    public async Task ExecuteAsync_DiscardedBody_ReportsZeroSizeDownloadAndContentType()
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 10\r\nContent-Type: text/plain\r\n\r\n0123456789"));
        var handler = new RtspProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), new RecordingAuthenticator());

        TransferResult result = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("rtsp://127.0.0.1:47951/media"), Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0L, result.Report!.DownloadSize);
        Assert.AreEqual("text/plain", result.Report.ContentType);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoContentType_ReportsNullContentType()
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\n\r\n"));
        var handler = new RtspProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), new RecordingAuthenticator());

        TransferResult result = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("rtsp://127.0.0.1:47951/media"), Output = new MemoryStream() });

        Assert.IsNull(result.Report!.ContentType);
    }
}
