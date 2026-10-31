using System.Text;

namespace Curl.Extended.Protocol.McpClient.StreamableHttp;

/// <summary>
/// Each test reads a recorded <c>text/event-stream</c> body against one rule of the HTML Living Standard,
/// "Server-sent events &gt; Interpreting an event stream", which "Transports &gt; Streamable HTTP" carries messages in.
/// </summary>
[TestClass]
public sealed class McpEventStreamReaderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadEventDataAsync_DataFieldForms_ReadsEachValue()
    {
        // A "data" line with no colon has an empty value; one space after the colon is dropped, and only one.
        var events = await ReadAll("data\ndata:x\ndata:  y\n\ndatax: z\nretry: 5\n\n\n");

        CollectionAssert.AreEqual(new[] { "\nx\n y" }, events);
    }

    [TestMethod]
    public async Task ReadEventDataAsync_CrLfSplitAcrossReads_IsOneLineEnd()
    {
        // CRLF, LF and CR each end a line; a CR at the end of one read and LF at the start of the next are one line end.
        var events = await ReadAll("data: a\r\n\r\ndata: b\r\rdata: c\n\n", readSize: 8);

        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, events);
    }

    [TestMethod]
    public async Task ReadEventDataAsync_StreamEndsInsideAnEvent_DiscardsIt()
    {
        // An event not followed by a blank line before the end of the stream is not dispatched.
        var events = await ReadAll("data: a\n\ndata: b\n");

        CollectionAssert.AreEqual(new[] { "a" }, events);
    }

    [TestMethod]
    public async Task ReadEventDataAsync_LineLongerThanTheLimit_Throws()
    {
        var exception = await Assert.ThrowsExactlyAsync<McpHttpException>(() => ReadAll("data: " + new string('a', McpHttpConnectionReader.MaxLineLength)));

        Assert.AreEqual(McpHttpFailure.LineTooLong, exception.Failure);
    }

    private async Task<List<string>> ReadAll(string stream, int readSize = int.MaxValue)
    {
        var reader = new McpEventStreamReader(new McpCloseDelimitedBody(new McpHttpConnectionReader(new RecordedConnection(stream, readSize))));
        var events = new List<string>();
        for (var data = await reader.ReadEventDataAsync(TestContext.CancellationToken); data is not null; data = await reader.ReadEventDataAsync(TestContext.CancellationToken))
        {
            events.Add(Encoding.UTF8.GetString(data));
        }

        return events;
    }
}
