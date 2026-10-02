using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins the <c>[READ]</c> lines an HTTP/1.x request body's reads write when the handler traces client
/// readers (<c>-v --trace-config read</c>, BL-1189): after <c>using HTTP/1.x</c> and before the request
/// head, as curl 8.21.0 wrote them for <c>-d ab</c> and <c>-T</c> of a 3-byte file (BL-1189 Notes), and
/// none of them otherwise.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    private const string ReadTracePostHead = "POST / HTTP/1.1\r\nHost: 127.0.0.1:47811\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
        + "Content-Length: 2\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\n";

    [TestMethod]
    public async Task ExecuteAsync_PostWithDataTracingReaders_WritesTheBufferReadersLinesBeforeTheHead()
    {
        RecordingTransferEvents events = await PostWithDataAsync(tracesReaders: true);

        CollectionAssert.AreEqual(
            new[]
            {
                "* using HTTP/1.x",
                "* [READ] add buf reader, len=2 -> 0",
                "* [READ] cr_buf_read(len=65388) -> 0, nread=2, eos=1",
                "* [READ] client_read(len=65388) -> 0, nread=2, eos=1",
                "> " + ReadTracePostHead,
                "} ab",
                "* upload completely sent off: 2 bytes",
            },
            events.Events.Take(7).ToArray(),
            string.Join('\n', events.Events));
    }

    [TestMethod]
    public async Task ExecuteAsync_PostWithDataNotTracingReaders_WritesNoReadLine()
    {
        RecordingTransferEvents events = await PostWithDataAsync(tracesReaders: false);

        Assert.IsFalse(events.Info.Any(line => line.StartsWith("[READ]", StringComparison.Ordinal)), string.Join('\n', events.Events));
        Assert.IsFalse(Handler(QueueConnector.For()).TracesClientReaders);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadTracingReaders_WritesTheUploadReadersLinesBeforeTheHead()
    {
        const string head = "PUT /up.txt HTTP/1.1\r\nHost: 127.0.0.1:47811\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nContent-Length: 3\r\n\r\n";
        ScriptedConnection connection = Connection("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhi", 65536, head + "abc");
        RecordingTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://127.0.0.1:47811/up.txt"),
            Output = new MemoryStream(),
            Upload = new MemoryStream("abc"u8.ToArray()),
            Events = events,
        };

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator()) { TracesClientReaders = true }
            .ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "* using HTTP/1.x",
                "* [READ] add fread reader, len=3 -> 0",
                "* [READ] cr_in_read(len=3, total=3, read=3) -> 0, nread=3, eos=1",
                "* [READ] client_read(len=65432) -> 0, nread=3, eos=1",
                "> " + head,
                "} abc",
                "* upload completely sent off: 3 bytes",
            },
            events.Events.Take(7).ToArray(),
            string.Join('\n', events.Events));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2PostTracingReaders_WritesNoReadLine()
    {
        RecordingTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(LogUrl),
            Output = new MemoryStream(),
            Http = new HttpRequestOptions { Version = HttpVersionPreference.Http2PriorKnowledge, Body = new BytesBody("ab"u8.ToArray(), "a/b") },
            Events = events,
            TimeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch),
        };

        await new HttpProtocolHandler(QueueConnector.For(new ScriptedConnection(Http2HelloResponse(), 65536)), new SilentAuthenticator()) { TracesClientReaders = true }
            .ExecuteAsync(context);

        Assert.IsFalse(events.Info.Any(line => line.StartsWith("[READ]", StringComparison.Ordinal)), string.Join('\n', events.Events));
    }

    private static async Task<RecordingTransferEvents> PostWithDataAsync(bool tracesReaders)
    {
        ScriptedConnection connection = Connection("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhi", 65536, ReadTracePostHead + "ab");
        RecordingTransferEvents events = new();
        HttpRequestOptions options = new() { Body = new BytesBody("ab"u8.ToArray(), "application/x-www-form-urlencoded") };

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator()) { TracesClientReaders = tracesReaders }
            .ExecuteAsync(EventsContext("http://127.0.0.1:47811/", events, options));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        return events;
    }
}
