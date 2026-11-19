using System.Text;
using Curl.Original.Protocol.Rtsp.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Original.Protocol.Rtsp;

/// <summary>
/// Pins that a successful Rtsp transfer reports the stamps behind <c>%{time_pretransfer}</c>,
/// <c>%{time_posttransfer}</c> and <c>%{time_starttransfer}</c> (BL-5473, STD-152), in order
/// and none left at zero.
/// </summary>
[TestClass]
public sealed class RtspProtocolHandlerTimingsTests
{
    [TestMethod]
    public async Task ExecuteAsync_Success_StampsPreTransferPostTransferAndStartTransferInOrder()
    {
        var server = new ScriptedConnection(Encoding.Latin1.GetBytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\n\r\n"));
        var handler = new RtspProtocolHandler(new RecordingConnector(ConnectResult.Connected(server)), new RecordingAuthenticator());

        TransferResult result = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("rtsp://127.0.0.1:47950/media"), Output = Stream.Null });
        Assert.IsTrue(result.IsSuccess);
        TransferTimings timings = result.Report!.Timings!;
        Assert.IsNotNull(timings.RequestReady);
        Assert.IsNotNull(timings.RequestSent);
        Assert.IsNotNull(timings.FirstByteReceived);
        Assert.IsGreaterThanOrEqualTo(timings.Started, timings.RequestReady.Value);
        Assert.IsGreaterThanOrEqualTo(timings.RequestReady.Value, timings.RequestSent.Value);
        Assert.IsGreaterThanOrEqualTo(timings.RequestSent.Value, timings.FirstByteReceived.Value);
        Assert.IsGreaterThanOrEqualTo(timings.FirstByteReceived.Value, timings.Completed);
    }
}
