using System.Text;
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

        Diagnostics.Assert("read lines", 3, events.Info.Count(line => line.StartsWith("[READ]", StringComparison.Ordinal)));
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

        Diagnostics.Assert("read lines", 0, events.Info.Count(line => line.StartsWith("[READ]", StringComparison.Ordinal)));
        Diagnostics.Assert("traces readers by default", false, Handler(QueueConnector.For()).TracesClientReaders);
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
        Diagnostics.Arrange("upload", "-T file, abc (3 bytes)");
        Diagnostics.Arrange("traces readers", true);

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator()) { TracesClientReaders = true }
            .ExecuteAsync(context);

        WriteResult(result);
        WriteEvents("events", events.Events);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("read lines", 3, events.Info.Count(line => line.StartsWith("[READ]", StringComparison.Ordinal)));
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
    public async Task ExecuteAsync_StdinUploadTracingReaders_WritesTheHeldBackReadsAroundTheHeadAndTheChunkedReadsAfterTheWait()
    {
        // printf abc | curl -sv --trace-config read -T - http://127.0.0.1:47811/up (BL-1214 Notes).
        const string head = "PUT /up HTTP/1.1\r\nHost: 127.0.0.1:47811\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "Transfer-Encoding: chunked\r\nExpect: 100-continue\r\n\r\n";
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        GatedConnection connection = new(Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhi"), 65536, head.Length + 13);
        RecordingTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://127.0.0.1:47811/up"),
            Output = new MemoryStream(),
            Upload = StandardInput("abc"u8.ToArray()),
            TimeProvider = time,
            Events = events,
        };
        Diagnostics.Arrange("upload", "-T - from standard input, abc, chunked, Expect: 100-continue");
        Diagnostics.Arrange("traces readers", true);

        Task<TransferResult> transfer = new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator()) { TracesClientReaders = true }
            .ExecuteAsync(context).AsTask();
        await time.TimerCreatedAsync(HttpRequestOptions.DefaultContinueWait);
        time.Advance(HttpRequestOptions.DefaultContinueWait);
        TransferResult result = await transfer;

        WriteResult(result);
        WriteEvents("events", events.Events);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("upload sent line", true, events.Info.Contains("upload completely sent off: 13 bytes"));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "* using HTTP/1.x",
                "* [READ] add fread reader, len=-1 -> 0",
                "* [READ] client_read(len=65405) -> 0, nread=0, eos=0",
                "> " + head,
                "* [READ] client_read(len=65536) -> 0, nread=0, eos=0",
                "* Done waiting for 100-continue",
                "* [READ] cr_in_read(len=65524, total=-1, read=3) -> 0, nread=3, eos=0",
                "* [READ] http_chunk, made chunk of 3 bytes -> 0",
                "* [READ] client_read(len=65536) -> 0, nread=8, eos=0",
                "} 3\r\nabc\r\n",
                "* [READ] cr_in_read(len=65524, total=-1, read=3) -> 0, nread=0, eos=1",
                "* [READ] http_chunk, added last, empty chunk",
                "* [READ] client_read(len=65536) -> 0, nread=5, eos=1",
                "} 0\r\n\r\n",
                "* upload completely sent off: 13 bytes",
            },
            events.Events.Take(15).ToArray(),
            string.Join('\n', events.Events));
    }

    [TestMethod]
    public async Task ExecuteAsync_MultipartPostTracingReaders_WritesTheMimeReadersLinesBeforeTheHead()
    {
        // curl -sv --trace-config read -F a=b http://127.0.0.1:47811/ (BL-1214 Notes).
        const string contentType = "multipart/form-data; boundary=------------------------Q8BOgKaJAf5dPmHW2bVJZk";
        const string head = "POST / HTTP/1.1\r\nHost: 127.0.0.1:47811\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "Content-Length: 149\r\nContent-Type: " + contentType + "\r\n\r\n";
        byte[] form = new byte[149];
        ScriptedConnection connection = Connection("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhi", 65536);
        RecordingTransferEvents events = new();
        HttpRequestOptions options = new() { Body = new StreamBody(new MemoryStream(form), form.Length, contentType) };
        Diagnostics.Arrange("request body", $"multipart stream, {form.Length} bytes");
        Diagnostics.Arrange("traces readers", true);

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator()) { TracesClientReaders = true }
            .ExecuteAsync(EventsContext("http://127.0.0.1:47811/", events, options));

        WriteResult(result);
        WriteEvents("events", events.Events);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("first mime read line", "* [READ] cr_mime_read(len=149), mime_read() -> 149", events.Events.ElementAtOrDefault(1));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "* using HTTP/1.x",
                "* [READ] cr_mime_read(len=149), mime_read() -> 149",
                "* [READ] cr_mime_read(len=149, total=149, read=149) -> 0, 149, 1",
                "* [READ] client_read(len=65343) -> 0, nread=149, eos=1",
                "> " + head,
            },
            events.Events.Take(5).ToArray(),
            string.Join('\n', events.Events));
    }

    [TestMethod]
    public async Task ExecuteAsync_StdinUploadNotTracingReaders_WritesNoReadLine()
    {
        RecordingTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://127.0.0.1:47811/up"),
            Output = new MemoryStream(),
            Upload = StandardInput("abc"u8.ToArray()),
            Http = new HttpRequestOptions { Headers = ["Expect:"] },
            Events = events,
        };
        Diagnostics.Arrange("upload", "-T - from standard input, abc, Expect: removed");
        Diagnostics.Arrange("traces readers", false);

        TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n", 65536))).ExecuteAsync(context);

        WriteResult(result);
        WriteEvents("events", events.Events);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("read lines", 0, events.Info.Count(line => line.StartsWith("[READ]", StringComparison.Ordinal)));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsFalse(events.Info.Any(line => line.StartsWith("[READ]", StringComparison.Ordinal)), string.Join('\n', events.Events));
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
        Diagnostics.Arrange("request", "HTTP/2 prior knowledge, POST ab");
        Diagnostics.Arrange("traces readers", true);

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(new ScriptedConnection(Http2HelloResponse(), 65536)), new SilentAuthenticator()) { TracesClientReaders = true }
            .ExecuteAsync(context);

        WriteResult(result);
        WriteEvents("events", events.Events);
        Diagnostics.Assert("read lines", 0, events.Info.Count(line => line.StartsWith("[READ]", StringComparison.Ordinal)));
        Assert.IsFalse(events.Info.Any(line => line.StartsWith("[READ]", StringComparison.Ordinal)), string.Join('\n', events.Events));
    }

    private async Task<RecordingTransferEvents> PostWithDataAsync(bool tracesReaders)
    {
        ScriptedConnection connection = Connection("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhi", 65536, ReadTracePostHead + "ab");
        RecordingTransferEvents events = new();
        HttpRequestOptions options = new() { Body = new BytesBody("ab"u8.ToArray(), "application/x-www-form-urlencoded") };
        Diagnostics.Arrange("request", "POST -d ab");
        Diagnostics.Arrange("traces readers", tracesReaders);

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator()) { TracesClientReaders = tracesReaders }
            .ExecuteAsync(EventsContext("http://127.0.0.1:47811/", events, options));

        WriteResult(result);
        WriteEvents("events", events.Events);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        return events;
    }
}
