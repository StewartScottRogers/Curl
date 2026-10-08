using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <content>
/// The <c>-v</c> lines BL-1430 added: <c>HTTP/1.0 proxy connection set to keep alive</c> and
/// <c>HTTP/1.1 proxy connection set close</c> before a <c>Proxy-Connection</c> header line of a
/// response received through an HTTP proxy, and <c>Got HTTP failure 417 while waiting for a 100</c>
/// or <c>... while sending data</c> before the resend a 417 leads to, against curl 8.21.0
/// (mingw, Schannel) measured on 2026-10-04 with <c>Record-CurlExchange.ps1</c>; the commands are
/// in the BL-1430 Notes.
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private const string ProxiedUrl = "http://example.invalid/a";

    private static readonly ProxyEndpoint ForwardingProxy = new(ProxyKind.Http, "127.0.0.1", 18431, null);

    /// <summary>
    /// Measured: <c>curl -v -x http://127.0.0.1:18431 http://example.invalid/a</c> answered
    /// <c>HTTP/1.0 200 OK</c> with <c>Proxy-Connection: Keep-Alive</c> writes the line right before
    /// that header line.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_Http10ProxyKeepAliveThroughAProxy_ReportsProxyKeepAliveBeforeTheHeader()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            RecordingTransferEvents events = new();
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("scripted response", "HTTP/1.0 200, Proxy-Connection: Keep-Alive, through a proxy");

            await Handler(QueueConnector.For(Connection("HTTP/1.0 200 OK\r\nProxy-Connection: Keep-Alive\r\nContent-Length: 2\r\n\r\nhi", chunkSize)))
                .ExecuteAsync(EventsContext(ProxiedUrl, events, new HttpRequestOptions { ForwardProxy = ForwardingProxy }));

            WriteEvents("events", events.Events);
            Diagnostics.Assert("proxy keep-alive line reported", true, events.Info.Contains("HTTP/1.0 proxy connection set to keep alive"));
            CollectionAssert.AreEqual(
                new[]
                {
                    "* HTTP 1.0, assume close after body",
                    "< HTTP/1.0 200 OK\r\n",
                    "* HTTP/1.0 proxy connection set to keep alive",
                    "< Proxy-Connection: Keep-Alive\r\n",
                    "< Content-Length: 2\r\n",
                },
                EventsFrom(events, "* HTTP 1.0, assume close after body", 5),
                $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: the same through the proxy, answered <c>HTTP/1.1 200 OK</c> with
    /// <c>Proxy-Connection: close</c>, writes <c>HTTP/1.1 proxy connection set close</c> right
    /// before that header line.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_Http11ProxyCloseThroughAProxy_ReportsProxyCloseBeforeTheHeader()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            RecordingTransferEvents events = new();
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("scripted response", "HTTP/1.1 200, Proxy-Connection: close, through a proxy");

            await Handler(QueueConnector.For(Connection("HTTP/1.1 200 OK\r\nProxy-Connection: close\r\nContent-Length: 2\r\n\r\nhi", chunkSize)))
                .ExecuteAsync(EventsContext(ProxiedUrl, events, new HttpRequestOptions { ForwardProxy = ForwardingProxy }));

            WriteEvents("events", events.Events);
            Diagnostics.Assert("proxy close line reported", true, events.Info.Contains("HTTP/1.1 proxy connection set close"));
            CollectionAssert.AreEqual(
                new[]
                {
                    "< HTTP/1.1 200 OK\r\n",
                    "* HTTP/1.1 proxy connection set close",
                    "< Proxy-Connection: close\r\n",
                    "< Content-Length: 2\r\n",
                },
                EventsFrom(events, "< HTTP/1.1 200 OK\r\n", 4),
                $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// curl 8.21.0 writes the proxy lines only through an HTTP proxy, and only for an HTTP/1.0
    /// <c>keep-alive</c> or an HTTP/1.1 <c>close</c> (<c>lib/http.c</c>).
    /// </summary>
    /// <param name="response">The response.</param>
    /// <param name="throughProxy">Whether the request goes through an HTTP proxy.</param>
    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nProxy-Connection: keep-alive\r\nContent-Length: 2\r\n\r\nhi", true, DisplayName = "1.1 keep-alive")]
    [DataRow("HTTP/1.0 200 OK\r\nProxy-Connection: close\r\nContent-Length: 2\r\n\r\nhi", true, DisplayName = "1.0 close")]
    [DataRow("HTTP/1.0 200 OK\r\nProxy-Connection: keep-alive\r\nContent-Length: 2\r\n\r\nhi", false, DisplayName = "1.0 keep-alive, direct")]
    [DataRow("HTTP/1.1 200 OK\r\nProxy-Connection: close\r\nContent-Length: 2\r\n\r\nhi", false, DisplayName = "1.1 close, direct")]
    [DataRow("HTTP/1.1 200 OK\r\nX-Proxy-Connection: close\r\nContent-Length: 2\r\n\r\nhi", true, DisplayName = "1.1 other header")]
    [DataRow("HTTP/1.1 200 OK\r\nNoColon close\r\nContent-Length: 2\r\n\r\nhi", true, DisplayName = "1.1 no colon")]
    public async Task ExecuteAsync_ProxyConnectionThatCurlDoesNotReport_ReportsNoProxyLine(string response, bool throughProxy)
    {
        RecordingTransferEvents events = new();
        HttpRequestOptions options = new() { ForwardProxy = throughProxy ? ForwardingProxy : null };
        Diagnostics.Arrange("scripted response", OneLine(response));
        Diagnostics.Arrange("through proxy", throughProxy);

        await Handler(QueueConnector.For(Connection(response, 65536))).ExecuteAsync(EventsContext(ProxiedUrl, events, options));

        WriteEvents("info lines", events.Info);
        Diagnostics.Assert("proxy connection lines", 0, events.Info.Count(line => line.Contains("proxy connection set", StringComparison.Ordinal)));
        CollectionAssert.DoesNotContain(events.Info.ToList(), "HTTP/1.0 proxy connection set to keep alive");
        CollectionAssert.DoesNotContain(events.Info.ToList(), "HTTP/1.1 proxy connection set close");
    }

    /// <summary>
    /// A 417 that arrives while the body still waits for <c>100 Continue</c> writes
    /// <c>Got HTTP failure 417 while waiting for a 100</c> after its header lines and before its
    /// empty line, as curl 8.21.0's <c>http_on_response</c> does, then the resend goes as before.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_417WhileWaitingForContinue_ReportsGotFailureWhileWaitingBeforeTheResend()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, ExpectationFailedHead, OkHead + "ok");
            RecordingTransferEvents events = new();
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("scripted responses", "417 while waiting for 100-continue, then 200 ok");

            TransferResult result = await Handler(QueueConnector.For(connection))
                .ExecuteAsync(ExpectEventsContext(BigBodyOptions(), events));

            WriteResult(result);
            WriteEvents("events", events.Events);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Diagnostics.Assert("request bytes written", (ExpectingHead + ResentHead + BigBody).Length, connection.Written.Length);
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(ExpectingHead + ResentHead + BigBody, connection.Written, $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(
                new[] { "< Content-Length: 0\r\n", "* Got HTTP failure 417 while waiting for a 100", "* Ignoring the response-body", "* setting size while ignoring", "< \r\n" },
                EventsFrom(events, "< HTTP/1.1 417 Expectation Failed\r\n", 6).Skip(1).ToArray(),
                $"Chunk size {chunkSize}");
            CollectionAssert.DoesNotContain(events.Info.ToList(), "Got HTTP failure 417 while sending data", $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured (BL-319 and BL-1446 Notes): a 417 that arrives once the wait ran out, while the
    /// body is being sent, writes <c>Got HTTP failure 417 while sending data</c>, <c>Need to rewind
    /// upload for next request</c> and <c>abort upload after having sent N bytes</c> before its
    /// empty line, no <c>Ignoring the response-body</c>, then shuts the connection down and issues
    /// the resend on a new one. The new connection's <c>Trying</c> and <c>Established</c> lines
    /// between them are the connector's, which the fake does not write.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_417WhileSendingTheBody_WritesCurlsLinesFromTheStatusLineToTheResend()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            GatedConnection first = FailingWhileSending(ExpectationFailedHead, chunkSize, ExpectingHead.Length);
            TurnTakingConnection second = new(chunkSize, OkHead + "ok");
            RecordingTransferEvents events = new();
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("scripted responses", "417 while sending the body, then 200 ok on a new connection");

            await RunPastTheWaitAsync(QueueConnector.For(first, second), ExpectEventsContext(BigBodyOptions(), events));

            string[] lines = events.Events.Where(line => line.StartsWith("* ", StringComparison.Ordinal) || line.StartsWith("< ", StringComparison.Ordinal) || line.StartsWith("> ", StringComparison.Ordinal)).ToArray();
            WriteEvents("verbose lines", lines);
            int start = Array.IndexOf(lines, "< HTTP/1.1 417 Expectation Failed\r\n");
            Diagnostics.Assert("417 status line found", true, start >= 0);
            Assert.IsGreaterThanOrEqualTo(0, start, string.Join(" | ", lines));
            int resend = Array.FindIndex(lines, start, line => line.StartsWith("> ", StringComparison.Ordinal));
            Assert.IsGreaterThan(start, resend, string.Join(" | ", lines));
            CollectionAssert.AreEqual(
                new[]
                {
                    "< HTTP/1.1 417 Expectation Failed\r\n",
                    "< Content-Length: 0\r\n",
                    "* Got HTTP failure 417 while sending data",
                    "* Need to rewind upload for next request",
                    $"* abort upload after having sent {FirstPiece} bytes",
                    "< \r\n",
                    "* shutting down connection #0",
                    $"* Issue another request to this URL: '{ExpectUrl}'",
                    "* using HTTP/1.x",
                },
                lines[start..resend],
                $"Chunk size {chunkSize}: {string.Join(" | ", lines[start..resend])}");
        }
    }

    /// <summary>
    /// Measured (BL-319 Notes): a 417 that arrives once the wait ran out, while the body is being
    /// sent, writes <c>Got HTTP failure 417 while sending data</c> before the resend on a new
    /// connection.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_417WhileSendingTheBody_ReportsGotFailureWhileSendingBeforeTheResend()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            GatedConnection first = FailingWhileSending(ExpectationFailedHead, chunkSize, ExpectingHead.Length);
            TurnTakingConnection second = new(chunkSize, OkHead + "ok");
            RecordingTransferEvents events = new();
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("scripted responses", "417 while sending the body, then 200 ok on a new connection");

            TransferResult result = await RunPastTheWaitAsync(QueueConnector.For(first, second), ExpectEventsContext(BigBodyOptions(), events));

            WriteResult(result);
            WriteEvents("events", events.Events);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Diagnostics.Assert("resent bytes written", (ResentHead + BigBody).Length, second.Written.Length);
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(ResentHead + BigBody, second.Written, $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(
                new[] { "< Content-Length: 0\r\n", "* Got HTTP failure 417 while sending data", "* Need to rewind upload for next request", $"* abort upload after having sent {FirstPiece} bytes", "< \r\n" },
                EventsFrom(events, "< HTTP/1.1 417 Expectation Failed\r\n", 6).Skip(1).ToArray(),
                $"Chunk size {chunkSize}");
            CollectionAssert.DoesNotContain(events.Info.ToList(), "Got HTTP failure 417 while waiting for a 100", $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// A 417 that leads to no resend - under <c>-f</c>, or one that closes the connection - writes
    /// neither line, as curl 8.21.0 reaches them only on the way to the resend (<c>lib/http.c</c>).
    /// </summary>
    /// <param name="response">The 417's head.</param>
    /// <param name="fail">The fail mode.</param>
    [TestMethod]
    [DataRow("HTTP/1.1 417 Expectation Failed\r\nContent-Length: 0\r\n\r\n", HttpFailMode.Fail, DisplayName = "-f")]
    [DataRow("HTTP/1.1 417 Expectation Failed\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", HttpFailMode.None, DisplayName = "Connection: close")]
    public async Task ExecuteAsync_417WithoutAResend_ReportsNoGotFailureLine(string response, HttpFailMode fail)
    {
        TurnTakingConnection connection = new(65536, response);
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("scripted response", OneLine(response));
        Diagnostics.Arrange("fail mode", fail);

        await Handler(QueueConnector.For(connection))
            .ExecuteAsync(ExpectEventsContext(BigBodyOptions() with { Fail = fail }, events));

        WriteEvents("info lines", events.Info);
        Diagnostics.Assert("got-failure lines", 0, events.Info.Count(line => line.StartsWith("Got HTTP failure 417", StringComparison.Ordinal)));
        CollectionAssert.DoesNotContain(events.Info.ToList(), "Got HTTP failure 417 while waiting for a 100");
        CollectionAssert.DoesNotContain(events.Info.ToList(), "Got HTTP failure 417 while sending data");
    }

    private static TransferContext ExpectEventsContext(HttpRequestOptions options, ITransferEvents events) =>
        new()
        {
            Url = CurlUrl.Parse(ExpectUrl),
            Output = new MemoryStream(),
            Http = options,
            Events = events,
            TimeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch),
        };

    private static string[] EventsFrom(RecordingTransferEvents events, string first, int count)
    {
        int start = events.Events.IndexOf(first);
        Assert.IsGreaterThanOrEqualTo(0, start, string.Join(" | ", events.Events));
        return events.Events.Skip(start).Take(count).ToArray();
    }
}
