using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// The transfer events the handler reports for <c>-v</c> and <c>--trace</c> (ADR-0046), against
/// curl 8.21.0 (mingw, Schannel) measured on 2026-09-27 with <c>Record-CurlExchange.ps1</c>; the
/// commands are in the BL-407 Notes.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    private const string EventsGetHead = "GET /f.txt HTTP/1.1\r\nHost: 127.0.0.1:18441\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n";

    [TestMethod]
    public async Task ExecuteAsync_MeasuredVerboseGet_ReportsTheMeasuredEventsInOrder()
    {
        // curl -s -v http://127.0.0.1:18441/f.txt -o o: after the connect, using HTTP/1.x, one
        // 84-byte head, Request completely sent off, four header lines, one 6-byte data block.
        ScriptedConnection connection = Connection(
            "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 6\r\n\r\nhello\n", 65536, EventsGetHead);
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("url, chunk size", "http://127.0.0.1:18441/f.txt, 65536");
        Diagnostics.Arrange("request head", OneLine(EventsGetHead));
        Diagnostics.Arrange("response", "HTTP/1.1 200 OK, Content-Type: text/plain, Content-Length: 6, body hello");

        TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(connection, null, connectionNumber: 0)))
            .ExecuteAsync(EventsContext("http://127.0.0.1:18441/f.txt", events));

        WriteResult(result);
        WriteEvents("events", events.Events);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("request head length", 84, EventsGetHead.Length);
        Assert.AreEqual(84, EventsGetHead.Length);
        WriteExpectedLines(
            "events",
            [
                "* using HTTP/1.x",
                "> " + EventsGetHead,
                "* Request completely sent off",
                "< HTTP/1.1 200 OK\r\n",
                "< Content-Type: text/plain\r\n",
                "< Content-Length: 6\r\n",
                "< \r\n",
                "{ hello\n",
                "* Connection #0 to host 127.0.0.1:18441 left intact",
            ],
            events.Events);
        CollectionAssert.AreEqual(
            new[]
            {
                "* using HTTP/1.x",
                "> " + EventsGetHead,
                "* Request completely sent off",
                "< HTTP/1.1 200 OK\r\n",
                "< Content-Type: text/plain\r\n",
                "< Content-Length: 6\r\n",
                "< \r\n",
                "{ hello\n",
                "* Connection #0 to host 127.0.0.1:18441 left intact",
            },
            events.Events);
    }

    [TestMethod]
    public async Task ExecuteAsync_BodyArrivingInPieces_ReportsOneDataEventPerRead()
    {
        ScriptedConnection connection = Connection("HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nabc", 1);
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("url, chunk size", "http://127.0.0.1:18441/f.txt, 1");
        Diagnostics.Arrange("response", "HTTP/1.1 200 OK, Content-Length: 3, body abc");

        TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(EventsContext("http://127.0.0.1:18441/f.txt", events));

        WriteResult(result);
        WriteEvents("events", events.Events);
        WriteExpectedLines("data events", ["{ a", "{ b", "{ c"], events.Events.Where(e => e.StartsWith('{')).ToArray());
        CollectionAssert.AreEqual(new[] { "{ a", "{ b", "{ c" }, events.Events.Where(e => e.StartsWith('{')).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_PostWithData_ReportsTheBodySentAfterTheHead()
    {
        // curl -s -v -d hi http://127.0.0.1:18473/p -o o3: } [2 bytes data], then
        // "upload completely sent off: 2 bytes" instead of "Request completely sent off".
        const string head = "POST /p HTTP/1.1\r\nHost: 127.0.0.1:18473\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "Content-Length: 2\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\n";
        ScriptedConnection connection = Connection("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", 65536, head + "hi");
        RecordingTransferEvents events = new();
        HttpRequestOptions options = new() { Body = new BytesBody("hi"u8.ToArray(), "application/x-www-form-urlencoded") };

        Diagnostics.Arrange("url, body", "http://127.0.0.1:18473/p, hi");
        Diagnostics.Arrange("request head", OneLine(head));

        TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(connection, null, connectionNumber: 0)))
            .ExecuteAsync(EventsContext("http://127.0.0.1:18473/p", events, options));

        WriteResult(result);
        WriteEvents("events", events.Events);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        WriteExpectedLines(
            "events",
            [
                "* using HTTP/1.x",
                "> " + head,
                "} hi",
                "* upload completely sent off: 2 bytes",
                "< HTTP/1.1 200 OK\r\n",
                "< Content-Length: 2\r\n",
                "< \r\n",
                "{ ok",
                "* Connection #0 to host 127.0.0.1:18473 left intact",
            ],
            events.Events);
        CollectionAssert.AreEqual(
            new[]
            {
                "* using HTTP/1.x",
                "> " + head,
                "} hi",
                "* upload completely sent off: 2 bytes",
                "< HTTP/1.1 200 OK\r\n",
                "< Content-Length: 2\r\n",
                "< \r\n",
                "{ ok",
                "* Connection #0 to host 127.0.0.1:18473 left intact",
            },
            events.Events);
    }

    [TestMethod]
    public async Task ExecuteAsync_PostWithEmptyData_ReportsRequestCompletelySentOff()
    {
        // curl -s -v -d '' http://127.0.0.1:18216/p: no data line, then "Request completely
        // sent off", not "upload completely sent off: 0 bytes" (BL-1216 Notes).
        const string head = "POST /p HTTP/1.1\r\nHost: 127.0.0.1:18216\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "Content-Length: 0\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\n";
        ScriptedConnection connection = Connection("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", 65536, head);
        RecordingTransferEvents events = new();
        HttpRequestOptions options = new() { Body = new BytesBody(ReadOnlyMemory<byte>.Empty, "application/x-www-form-urlencoded") };

        Diagnostics.Arrange("url, body", "http://127.0.0.1:18216/p, (empty)");
        Diagnostics.Arrange("request head", OneLine(head));

        TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(connection, null, connectionNumber: 0)))
            .ExecuteAsync(EventsContext("http://127.0.0.1:18216/p", events, options));

        WriteResult(result);
        WriteEvents("events", events.Events);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        WriteExpectedLines(
            "first 4 events",
            ["* using HTTP/1.x", "> " + head, "* Request completely sent off", "< HTTP/1.1 200 OK\r\n"],
            events.Events.Take(4).ToArray());
        CollectionAssert.AreEqual(
            new[] { "* using HTTP/1.x", "> " + head, "* Request completely sent off", "< HTTP/1.1 200 OK\r\n" },
            events.Events.Take(4).ToArray(),
            string.Join('\n', events.Events));
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadOfAnEmptyFile_ReportsRequestCompletelySentOff()
    {
        // curl -s -v -T empty.txt http://127.0.0.1:18217/u: "Content-Length: 0", no data
        // line, then "Request completely sent off" (BL-1216 Notes).
        const string head = "PUT /u HTTP/1.1\r\nHost: 127.0.0.1:18217\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nContent-Length: 0\r\n\r\n";
        ScriptedConnection connection = Connection("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", 65536, head);
        RecordingTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://127.0.0.1:18217/u"),
            Output = new MemoryStream(),
            Upload = new MemoryStream(),
            Events = events,
        };

        Diagnostics.Arrange("url, upload", "http://127.0.0.1:18217/u, (empty stream)");
        Diagnostics.Arrange("request head", OneLine(head));

        TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(context);

        WriteResult(result);
        WriteEvents("events", events.Events);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        WriteExpectedLines(
            "first 4 events",
            ["* using HTTP/1.x", "> " + head, "* Request completely sent off", "< HTTP/1.1 200 OK\r\n"],
            events.Events.Take(4).ToArray());
        CollectionAssert.AreEqual(
            new[] { "* using HTTP/1.x", "> " + head, "* Request completely sent off", "< HTTP/1.1 200 OK\r\n" },
            events.Events.Take(4).ToArray(),
            string.Join('\n', events.Events));
    }

    [TestMethod]
    public async Task ExecuteAsync_ChunkedUploadAfterTheContinueWait_ReportsEachChunkWithItsFramingAndTheBytesOnTheWire()
    {
        // printf abcde | curl -s --trace-ascii - -T - http://127.0.0.1:18475/u: "Done waiting
        // for 100-continue", Send data 10 bytes (5 CRLF abcde CRLF), Send data 5 bytes (0 CRLF
        // CRLF), "upload completely sent off: 15 bytes" (BL-407 and BL-449 Notes).
        const string head = "PUT /u HTTP/1.1\r\nHost: 127.0.0.1:18475\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "Transfer-Encoding: chunked\r\nExpect: 100-continue\r\n\r\n";
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        GatedConnection connection = new(Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok"), 65536, head.Length + 15);
        RecordingTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://127.0.0.1:18475/u"),
            Output = new MemoryStream(),
            Upload = StandardInput("abcde"u8.ToArray()),
            TimeProvider = time,
            Events = events,
        };

        Diagnostics.Arrange("url, upload", "http://127.0.0.1:18475/u, abcde");
        Diagnostics.Arrange("request head", OneLine(head));
        Diagnostics.Arrange("continue wait", HttpRequestOptions.DefaultContinueWait);

        Task<TransferResult> transfer = Handler(QueueConnector.For(connection)).ExecuteAsync(context).AsTask();
        await time.TimerCreatedAsync(HttpRequestOptions.DefaultContinueWait);
        time.Advance(HttpRequestOptions.DefaultContinueWait);
        TransferResult result = await transfer;

        WriteResult(result);
        WriteEvents("events", events.Events);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        WriteExpectedLines(
            "events 1 to 5",
            ["> " + head, "* Done waiting for 100-continue", "} 5\r\nabcde\r\n", "} 0\r\n\r\n", "* upload completely sent off: 15 bytes"],
            events.Events.Skip(1).Take(5).ToArray());
        CollectionAssert.AreEqual(
            new[] { "> " + head, "* Done waiting for 100-continue", "} 5\r\nabcde\r\n", "} 0\r\n\r\n", "* upload completely sent off: 15 bytes" },
            events.Events.Skip(1).Take(5).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_ChunkedUploadWithoutExpect_ReportsEachChunkWithItsFraming()
    {
        ScriptedConnection connection = Connection("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n", 65536);
        RecordingTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://127.0.0.1:18475/u"),
            Output = new MemoryStream(),
            Upload = StandardInput("abcde"u8.ToArray()),
            Http = new HttpRequestOptions { Headers = ["Expect:"] },
            Events = events,
        };

        Diagnostics.Arrange("url, upload, headers", "http://127.0.0.1:18475/u, abcde, Expect:");

        TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(context);

        WriteResult(result);
        WriteEvents("events", events.Events);
        WriteExpectedLines(
            "events 2 to 4",
            ["} 5\r\nabcde\r\n", "} 0\r\n\r\n", "* upload completely sent off: 15 bytes"],
            events.Events.Skip(2).Take(3).ToArray());
        CollectionAssert.AreEqual(
            new[] { "} 5\r\nabcde\r\n", "} 0\r\n\r\n", "* upload completely sent off: 15 bytes" },
            events.Events.Skip(2).Take(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_ChunkedResponseWithTrailer_ReportsTheRawBodyAsDataAndNoTrailerHeader()
    {
        // curl -s --trace-ascii - http://127.0.0.1:18471/c: one 21-byte Recv data block holding
        // the chunk framing and the trailer, and no Recv header for the trailer.
        const string body = "3\r\nabc\r\n0\r\nX-T: 1\r\n\r\n";
        ScriptedConnection connection = Connection("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nTrailer: X-T\r\n\r\n" + body, 65536);
        RecordingTransferEvents events = new();

        Diagnostics.Arrange("url, chunk size", "http://127.0.0.1:18471/c, 65536");
        Diagnostics.Arrange("chunked body with trailer", OneLine(body));

        TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(EventsContext("http://127.0.0.1:18471/c", events));

        WriteResult(result);
        WriteEvents("events", events.Events);
        WriteExpectedLines(
            "events 5 to 7",
            ["< Trailer: X-T\r\n", "< \r\n", "{ " + body],
            events.Events.Skip(5).Take(3).ToArray());
        CollectionAssert.AreEqual(
            new[] { "< Trailer: X-T\r\n", "< \r\n", "{ " + body },
            events.Events.Skip(5).Take(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_ChunkedResponseArrivingInPieces_ReportsEveryRead()
    {
        const string body = "3\r\nabc\r\n0\r\n\r\n";
        ScriptedConnection connection = Connection("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n" + body, 1);
        RecordingTransferEvents events = new();

        Diagnostics.Arrange("url, chunk size", "http://127.0.0.1:18471/c, 1");
        Diagnostics.Arrange("chunked body", OneLine(body));

        TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(EventsContext("http://127.0.0.1:18471/c", events));

        WriteResult(result);
        WriteEvents("events", events.Events);
        Diagnostics.Diff("concatenated data events", OneLine(body), OneLine(string.Concat(events.Events.Where(e => e.StartsWith('{')).Select(e => e[2..]))));
        Assert.AreEqual(body, string.Concat(events.Events.Where(e => e.StartsWith('{')).Select(e => e[2..])));
    }

    [TestMethod]
    public async Task ExecuteAsync_InformationalHeadBeforeTheFinalOne_ReportsBothHeadsLineByLine()
    {
        ScriptedConnection connection = Connection("HTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 204 No Content\r\n\r\n", 65536);
        RecordingTransferEvents events = new();

        Diagnostics.Arrange("url, chunk size", "http://127.0.0.1:18441/f.txt, 65536");
        Diagnostics.Arrange("response", "HTTP/1.1 100 Continue, then HTTP/1.1 204 No Content");

        TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(EventsContext("http://127.0.0.1:18441/f.txt", events));

        WriteResult(result);
        WriteEvents("events", events.Events);
        WriteExpectedLines(
            "head events",
            ["< HTTP/1.1 100 Continue\r\n", "< \r\n", "< HTTP/1.1 204 No Content\r\n", "< \r\n"],
            events.Events.Where(e => e.StartsWith('<')).ToArray());
        CollectionAssert.AreEqual(
            new[] { "< HTTP/1.1 100 Continue\r\n", "< \r\n", "< HTTP/1.1 204 No Content\r\n", "< \r\n" },
            events.Events.Where(e => e.StartsWith('<')).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_RedirectBodyFollowedPast_ReportsNoDataForIt()
    {
        // curl -s -L -v http://127.0.0.1:18472/a: the 302's 4-byte body is ignored, so no
        // { [4 bytes data] line.
        ScriptedConnection connection = Connection("HTTP/1.1 302 Found\r\nLocation: /b\r\nContent-Length: 4\r\n\r\nmove", 65536);
        RecordingTransferEvents events = new();
        TransferContext context = EventsContext("http://127.0.0.1:18472/a", events, new HttpRequestOptions { FollowRedirects = true });

        Diagnostics.Arrange("url, follow redirects", "http://127.0.0.1:18472/a, True");
        Diagnostics.Arrange("response", "HTTP/1.1 302 Found, Location: /b, Content-Length: 4, body move");

        TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(context);

        WriteResult(result);
        WriteEvents("events", events.Events);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("any data event", false, events.Events.Any(e => e.StartsWith('{')));
        Assert.IsFalse(events.Events.Any(e => e.StartsWith('{')));
    }

    private static TransferContext EventsContext(string url, ITransferEvents events, HttpRequestOptions? http = null) =>
        new() { Url = CurlUrl.Parse(url), Output = new MemoryStream(), Events = events, Http = http };
}
