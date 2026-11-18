using System.Text;
using Curl.Original.Protocol.Pop3.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Original.Protocol.Pop3;

/// <summary>
/// Pins that a successful Pop3 transfer reports the stamps behind <c>%{time_pretransfer}</c>,
/// <c>%{time_posttransfer}</c> and <c>%{time_starttransfer}</c> (BL-5473, STD-152), in order
/// and none left at zero.
/// </summary>
[TestClass]
public sealed class Pop3ProtocolHandlerTimingsTests
{
    [TestMethod]
    public async Task ExecuteAsync_Success_StampsPreTransferPostTransferAndStartTransferInOrder()
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes("+OK ready\r\n+OK Capability list follows\r\nUSER\r\n.\r\n+OK 4 octets\r\nabc\r\n.\r\n"), Encoding.Latin1.GetBytes("+OK Bye\r\n"));
        var handler = new Pop3ProtocolHandler(new QueuedConnector(ConnectResult.Connected(connection)), new QueuedTlsProvider());

        TransferResult result = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("pop3://127.0.0.1:18110/1"), Output = Stream.Null });
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
