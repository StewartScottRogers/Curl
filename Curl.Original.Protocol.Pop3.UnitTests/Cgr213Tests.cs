using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Pop3.Fakes;

namespace Curl.Original.Protocol.Pop3;

/// <summary>
/// Pins CGR-213 (BL-3742): a POP3 transfer reports itself started as soon as its TCP connect is
/// over, so <c>-m</c> passing during the greeting ends with curl 8.22.0's
/// <c>Operation timed out after N milliseconds with 0 bytes received</c>, not
/// <c>Connection timed out after N milliseconds</c>.
/// </summary>
[TestClass]
public sealed class Cgr213Tests
{
    [TestMethod]
    public async Task ExecuteAsync_GreetingRefusedAfterConnect_HasReportedTheTransferStarted()
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes("-ERR go away\r\n"));
        var connector = new QueuedConnector(ConnectResult.Connected(connection, null));
        var handler = new Pop3ProtocolHandler(connector, new QueuedTlsProvider(), new ScriptedSaslAuthenticator());
        var progress = new RecordingProgress();

        await handler.ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("pop3://127.0.0.1:18632/1"),
            Output = Stream.Null,
            Progress = progress,
        });

        Assert.IsTrue(progress.Started);
    }
}
