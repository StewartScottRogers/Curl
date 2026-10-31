using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// The <c>-v</c> line of a refused Transfer-Encoding. Every case was measured on curl 8.22.0
/// with <c>-sv</c> and the options named, against a loopback server with
/// <c>Record-CurlExchange.ps1</c> (BL-3283 Notes): curl writes the head lines before the
/// refused header, then its exit 61 message as a <c>*</c> line, as <c>failf</c> does.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: gzip, chunked\r\n\r\n3\r\nabc\r\n0\r\n\r\n", "--compressed", "Unsolicited Transfer-Encoding (gzip) found", new[] { "< HTTP/1.1 200 OK\r\n" }, DisplayName = "Unsolicited, with --compressed (differential srv-te-gzip-chunked-v)")]
    [DataRow("HTTP/1.1 200 OK\r\nX-Before: 1\r\nTransfer-Encoding: chunked, identity\r\n\r\n0\r\n\r\n", "", "A Transfer-Encoding (identity) was listed after chunked", new[] { "< HTTP/1.1 200 OK\r\n", "< X-Before: 1\r\n" }, DisplayName = "Listed after chunked")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked, gzip\r\n\r\n0\r\n\r\n", "--tr-encoding", "Reject response due to 'chunked' not being the last Transfer-Encoding", new[] { "< HTTP/1.1 200 OK\r\n" }, DisplayName = "Chunked not last, with --tr-encoding")]
    public async Task ExecuteAsync_RefusedTransferEncodingVerbose_WritesTheMessageAfterTheHeadLinesBeforeIt(string response, string options, string message, string[] headLines)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            RecordingTransferEvents events = new();
            TransferContext context = RefusedHeaderContext(new MemoryStream(), null, options, events);
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("scripted response", OneLine(response));
            Diagnostics.Arrange("options", options);

            TransferResult result = await Handler(QueueConnector.For(Connection(response, chunkSize))).ExecuteAsync(context);

            WriteResult(result);
            WriteEvents("head events", HeadEvents(events));
            int lastReceived = events.Events.FindLastIndex(line => line[0] == '<');
            Diagnostics.Assert("line after the head lines", "* " + message, events.Events.ElementAtOrDefault(lastReceived + 1) ?? "(none)");
            Assert.AreEqual(CurlExitCode.BadContentEncoding, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(message, result.ErrorMessage, $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(headLines, HeadEvents(events), $"Chunk size {chunkSize}");
            Assert.AreEqual("* " + message, events.Events[lastReceived + 1], $"Chunk size {chunkSize}");
            Assert.AreEqual(1, events.Events.Count(line => line == "* " + message), $"Chunk size {chunkSize}");
        }
    }
}
