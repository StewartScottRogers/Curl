using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// CGR-202: the trailer lines decoded before a malformed trailer line are written to the
/// header output, as curl 8.22.0 does before it fails with exit 8.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_FoldedTrailerLine_WritesTheTrailerBeforeItThenFailsWithExit8()
    {
        string head = "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n";
        string response = head + "5\r\nhello\r\n0\r\nT: a\r\n b\r\n\r\n";
        MemoryStream headers = new();
        Diagnostics.Arrange("scripted response", "200 chunked, body hello, trailer T: a then a folded line ' b'");

        TransferResult result = await Handler(QueueConnector.For(Connection(response, 65536)))
            .ExecuteAsync(StoredHeadersContext(new MemoryStream(), headers, NoTransferEvents.Instance, storedBefore: 0));

        WriteResult(result);
        Diagnostics.Assert("header output", OneLine(head + "T: a\r\n"), OneLine(Latin1(headers.ToArray())));
        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual(head + "T: a\r\n", Latin1(headers.ToArray()));
    }
}
