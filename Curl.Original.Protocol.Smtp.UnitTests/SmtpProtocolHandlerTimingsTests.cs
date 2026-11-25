using System.Text;
using Curl.Original.Protocol.Smtp.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Original.Protocol.Smtp;

/// <summary>
/// Pins that a successful Smtp transfer reports the stamps behind <c>%{time_pretransfer}</c>,
/// <c>%{time_posttransfer}</c> and <c>%{time_starttransfer}</c> (BL-5473, STD-152), in order
/// and none left at zero.
/// </summary>
[TestClass]
public sealed class SmtpProtocolHandlerTimingsTests
{
    [TestMethod]
    public async Task ExecuteAsync_Success_StampsPreTransferPostTransferAndStartTransferInOrder()
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes("220 localhost ESMTP\r\n250-localhost\r\n250 OK\r\n" + SmtpRun.HelpReply + "221 Bye\r\n"));
        var handler = new SmtpProtocolHandler(new QueuedConnector(ConnectResult.Connected(connection)), new QueuedTlsProvider());

        TransferResult result = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("smtp://127.0.0.1:18025/x"), Output = Stream.Null });
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
