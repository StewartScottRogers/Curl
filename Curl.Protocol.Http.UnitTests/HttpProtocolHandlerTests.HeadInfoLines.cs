using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// The <c>-v</c> info lines BL-467 added - an HTTP/1.0 connection kept alive and an HTTP/1.1
/// body with no end-of-message indicator - and where each falls among the header events,
/// against curl 8.21.0 (mingw, Schannel) measured on 2026-09-27 with
/// <c>Record-CurlExchange.ps1</c>; the commands are in the BL-467 Notes.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    private const string KeepAliveLine = "* HTTP/1.0 connection set to keep alive";

    private const string NoEndOfMessageLine = "* no chunk, no close, no size. Assume close to signal end";

    [TestMethod]
    public async Task ExecuteAsync_Http10KeepAliveHeaders_ReportsTheKeepAliveLineBeforeEachOne()
    {
        // curl -s -v against an HTTP/1.0 200 with two Connection: keep-alive headers.
        foreach (int chunkSize in ChunkSizes)
        {
            RecordingTransferEvents events = new();
            Diagnostics.Arrange("url, chunk size", $"http://127.0.0.1:18467/a, {chunkSize}");
            Diagnostics.Arrange("response head", "HTTP/1.0 200 OK, Connection: keep-alive, Connection: Foo, Keep-Alive, Content-Length: 2");

            TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.0 200 OK\r\nConnection: keep-alive\r\nConnection: Foo, Keep-Alive\r\nContent-Length: 2\r\n\r\nok", chunkSize)))
                .ExecuteAsync(EventsContext("http://127.0.0.1:18467/a", events));

            WriteResult(result);
            WriteEvents("events", events.Events);
            WriteExpectedLines(
                "events 3 to 10",
                [
                    "* HTTP 1.0, assume close after body",
                    "< HTTP/1.0 200 OK\r\n",
                    KeepAliveLine,
                    "< Connection: keep-alive\r\n",
                    KeepAliveLine,
                    "< Connection: Foo, Keep-Alive\r\n",
                    "< Content-Length: 2\r\n",
                    "< \r\n",
                ],
                events.Events.Skip(3).Take(8).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "* HTTP 1.0, assume close after body",
                    "< HTTP/1.0 200 OK\r\n",
                    KeepAliveLine,
                    "< Connection: keep-alive\r\n",
                    KeepAliveLine,
                    "< Connection: Foo, Keep-Alive\r\n",
                    "< Content-Length: 2\r\n",
                    "< \r\n",
                },
                events.Events.Skip(3).Take(8).ToArray(),
                $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow("HTTP/1.0 200 OK\r\nConnection: keep-alive, close\r\nContent-Length: 2\r\n\r\nok", DisplayName = "1.0 keep-alive, close")]
    [DataRow("HTTP/1.1 200 OK\r\nConnection: keep-alive\r\nContent-Length: 2\r\n\r\nok", DisplayName = "1.1 keep-alive")]
    public async Task ExecuteAsync_KeepAliveThatDoesNotKeepAnHttp10ConnectionAlive_ReportsNoKeepAliveLine(string response)
    {
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("url, response", $"http://127.0.0.1:18467/a, {OneLine(response)}");

        TransferResult result = await Handler(QueueConnector.For(Connection(response, 65536))).ExecuteAsync(EventsContext("http://127.0.0.1:18467/a", events));

        WriteResult(result);
        Diagnostics.Act("info lines", string.Join(" | ", events.Info));
        Diagnostics.Assert("info contains keep alive line", false, events.Info.Contains("HTTP/1.0 connection set to keep alive"));
        CollectionAssert.DoesNotContain(events.Info.ToList(), "HTTP/1.0 connection set to keep alive");
    }

    [TestMethod]
    public async Task ExecuteAsync_Http11BodyWithNoLength_ReportsNoEndOfMessageIndicatorBeforeTheHeadsEmptyLine()
    {
        // curl -s -v against an HTTP/1.1 200 with no length, not chunked, not closing.
        foreach (int chunkSize in ChunkSizes)
        {
            RecordingTransferEvents events = new();
            Diagnostics.Arrange("url, chunk size", $"http://127.0.0.1:18467/a, {chunkSize}");
            Diagnostics.Arrange("response", "HTTP/1.1 200 OK, X: y, body ok");

            TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 200 OK\r\nX: y\r\n\r\nok", chunkSize)))
                .ExecuteAsync(EventsContext("http://127.0.0.1:18467/a", events));

            WriteResult(result);
            WriteEvents("events", events.Events);
            WriteExpectedLines(
                "events 3 to 6",
                ["< HTTP/1.1 200 OK\r\n", "< X: y\r\n", NoEndOfMessageLine, "< \r\n"],
                events.Events.Skip(3).Take(4).ToArray());
            CollectionAssert.AreEqual(
                new[] { "< HTTP/1.1 200 OK\r\n", "< X: y\r\n", NoEndOfMessageLine, "< \r\n" },
                events.Events.Skip(3).Take(4).ToArray(),
                $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_RedirectWithNoLengthFollowedPast_ReportsNoEndOfMessageIndicatorAndNoIgnoredBody()
    {
        // curl -s -L -v against a 302 with Location: /b and no length.
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("url, follow redirects", "http://127.0.0.1:18467/a, True");
        Diagnostics.Arrange("response", "HTTP/1.1 302 Found, Location: /b, no length");

        TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 302 Found\r\nLocation: /b\r\n\r\n", 65536)))
            .ExecuteAsync(EventsContext("http://127.0.0.1:18467/a", events, new HttpRequestOptions { FollowRedirects = true }));

        WriteResult(result);
        WriteEvents("events", events.Events);
        WriteExpectedLines(
            "events 3 to 6",
            ["< HTTP/1.1 302 Found\r\n", "< Location: /b\r\n", NoEndOfMessageLine, "< \r\n"],
            events.Events.Skip(3).Take(4).ToArray());
        CollectionAssert.AreEqual(
            new[] { "< HTTP/1.1 302 Found\r\n", "< Location: /b\r\n", NoEndOfMessageLine, "< \r\n" },
            events.Events.Skip(3).Take(4).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_FailOn404WithNoLength_ReportsNoEndOfMessageIndicatorBeforeTheHeadsEmptyLine()
    {
        // curl -s -v -f against an HTTP/1.1 404 with no length.
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("url, fail mode", "http://127.0.0.1:18467/a, Fail");
        Diagnostics.Arrange("response", "HTTP/1.1 404 Not Found, X: y, body no");

        TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 404 Not Found\r\nX: y\r\n\r\nno", 65536)))
            .ExecuteAsync(EventsContext("http://127.0.0.1:18467/a", events, new HttpRequestOptions { Fail = HttpFailMode.Fail }));

        WriteResult(result);
        WriteEvents("events", events.Events);
        Diagnostics.Assert("exit code", CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        WriteExpectedLines(
            "events 4 to 6",
            ["< X: y\r\n", NoEndOfMessageLine, "< \r\n"],
            events.Events.Skip(4).Take(3).ToArray());
        CollectionAssert.AreEqual(
            new[] { "< X: y\r\n", NoEndOfMessageLine, "< \r\n" },
            events.Events.Skip(4).Take(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_IgnoreContentLength_ReportsNoEndOfMessageIndicator()
    {
        // curl -s -v --ignore-content-length against an HTTP/1.1 200 with Content-Length: 2.
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("url, ignore content length", "http://127.0.0.1:18467/a, True");
        Diagnostics.Arrange("response", "HTTP/1.1 200 OK, Content-Length: 2, body ok");

        TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", 65536)))
            .ExecuteAsync(EventsContext("http://127.0.0.1:18467/a", events, new HttpRequestOptions { IgnoreContentLength = true }));

        WriteResult(result);
        WriteEvents("events", events.Events);
        WriteExpectedLines(
            "events 4 to 6",
            ["< Content-Length: 2\r\n", NoEndOfMessageLine, "< \r\n"],
            events.Events.Skip(4).Take(3).ToArray());
        CollectionAssert.AreEqual(
            new[] { "< Content-Length: 2\r\n", NoEndOfMessageLine, "< \r\n" },
            events.Events.Skip(4).Take(3).ToArray());
    }

    [TestMethod]
    [DataRow("HTTP/1.0 200 OK\r\nConnection: keep-alive\r\n\r\nok", DisplayName = "HTTP/1.0 keep-alive")]
    [DataRow("HTTP/1.1 200 OK\r\nConnection: close\r\n\r\nok", DisplayName = "Connection: close")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n2\r\nok\r\n0\r\n\r\n", DisplayName = "Chunked")]
    [DataRow("HTTP/1.1 204 No Content\r\n\r\n", DisplayName = "204")]
    [DataRow("HTTP/1.1 200 OK\r\nX: y", DisplayName = "Closed before the head's end")]
    public async Task ExecuteAsync_BodyWithAnEndOrNoBody_ReportsNoEndOfMessageIndicator(string response)
    {
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("url, response", $"http://127.0.0.1:18467/a, {OneLine(response)}");

        TransferResult result = await Handler(QueueConnector.For(Connection(response, 65536))).ExecuteAsync(EventsContext("http://127.0.0.1:18467/a", events));

        WriteResult(result);
        Diagnostics.Act("info lines", string.Join(" | ", events.Info));
        Diagnostics.Assert("info contains no-end-of-message line", false, events.Info.Contains("no chunk, no close, no size. Assume close to signal end"));
        CollectionAssert.DoesNotContain(events.Info.ToList(), "no chunk, no close, no size. Assume close to signal end");
    }
}
