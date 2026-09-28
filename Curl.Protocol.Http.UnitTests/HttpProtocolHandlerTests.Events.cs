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

        TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(connection, null, connectionNumber: 0)))
            .ExecuteAsync(EventsContext("http://127.0.0.1:18441/f.txt", events));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(84, EventsGetHead.Length);
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

        await Handler(QueueConnector.For(connection)).ExecuteAsync(EventsContext("http://127.0.0.1:18441/f.txt", events));

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

        TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(connection, null, connectionNumber: 0)))
            .ExecuteAsync(EventsContext("http://127.0.0.1:18473/p", events, options));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
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

        Task<TransferResult> transfer = Handler(QueueConnector.For(connection)).ExecuteAsync(context).AsTask();
        await time.TimerCreatedAsync(HttpContinueWaitConnection.ContinueWait);
        time.Advance(HttpContinueWaitConnection.ContinueWait);
        TransferResult result = await transfer;

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
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

        await Handler(QueueConnector.For(connection)).ExecuteAsync(context);

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

        await Handler(QueueConnector.For(connection)).ExecuteAsync(EventsContext("http://127.0.0.1:18471/c", events));

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

        await Handler(QueueConnector.For(connection)).ExecuteAsync(EventsContext("http://127.0.0.1:18471/c", events));

        Assert.AreEqual(body, string.Concat(events.Events.Where(e => e.StartsWith('{')).Select(e => e[2..])));
    }

    [TestMethod]
    public async Task ExecuteAsync_InformationalHeadBeforeTheFinalOne_ReportsBothHeadsLineByLine()
    {
        ScriptedConnection connection = Connection("HTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 204 No Content\r\n\r\n", 65536);
        RecordingTransferEvents events = new();

        await Handler(QueueConnector.For(connection)).ExecuteAsync(EventsContext("http://127.0.0.1:18441/f.txt", events));

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

        TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsFalse(events.Events.Any(e => e.StartsWith('{')));
    }

    private static TransferContext EventsContext(string url, ITransferEvents events, HttpRequestOptions? http = null) =>
        new() { Url = CurlUrl.Parse(url), Output = new MemoryStream(), Events = events, Http = http };
}
