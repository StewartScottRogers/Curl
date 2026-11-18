using System.Text;
using Curl.Original.Protocol.Imap.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Original.Protocol.Imap;

/// <summary>
/// Pins that a successful Imap transfer reports the stamps behind <c>%{time_pretransfer}</c>,
/// <c>%{time_posttransfer}</c> and <c>%{time_starttransfer}</c> (BL-5473, STD-152), in order
/// and none left at zero.
/// </summary>
[TestClass]
public sealed class ImapProtocolHandlerTimingsTests
{
    [TestMethod]
    public async Task ExecuteAsync_Success_StampsPreTransferPostTransferAndStartTransferInOrder()
    {
        string replies = "* OK [CAPABILITY IMAP4rev1 AUTH=PLAIN] ready\r\n* CAPABILITY IMAP4rev1 AUTH=PLAIN\r\nA001 OK CAPABILITY completed\r\n"
            + "* 2 EXISTS\r\nA002 OK [READ-WRITE] SELECT completed\r\n* 1 FETCH (UID 1 BODY[] {3}\r\nabc)\r\nA003 OK FETCH completed\r\n"
            + "* BYE Logging out\r\nA004 OK LOGOUT completed\r\n";
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(replies));
        var handler = new ImapProtocolHandler(new QueuedConnector(ConnectResult.Connected(connection)), new QueuedTlsProvider());

        TransferResult result = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("imap://127.0.0.1:18155/INBOX;UID=1"), Output = Stream.Null });
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
