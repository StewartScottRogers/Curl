using System.Net.Sockets;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// The <c>-v</c> info lines BL-449 added - a body ignored past a redirect, the
/// <c>100 Continue</c> wait running out, a read the peer reset and an HTTP/1.0 status line -
/// and where each falls among the header and data events, against curl 8.21.0 (mingw,
/// Schannel) measured on 2026-09-27 with <c>Record-CurlExchange.ps1</c>; the commands are in
/// the BL-449 Notes.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    private const string IgnoredGetHead = "GET /a HTTP/1.1\r\nHost: 127.0.0.1:18601\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n";

    [TestMethod]
    public async Task ExecuteAsync_RedirectWithContentLengthFollowedPast_ReportsTheIgnoredBodyBeforeTheHeadsEmptyLine()
    {
        // curl -s -L -v http://127.0.0.1:18601/a, the 302 with Content-Length: 4 and "move".
        foreach (int chunkSize in ChunkSizes)
        {
            RecordingTransferEvents events = new();

            await Handler(QueueConnector.For(Connection("HTTP/1.1 302 Found\r\nLocation: /b\r\nContent-Length: 4\r\n\r\nmove", chunkSize)))
                .ExecuteAsync(EventsContext("http://127.0.0.1:18601/a", events, new HttpRequestOptions { FollowRedirects = true }));

            CollectionAssert.AreEqual(
                new[]
                {
                    "* using HTTP/1.x",
                    "> " + IgnoredGetHead,
                    "* Request completely sent off",
                    "< HTTP/1.1 302 Found\r\n",
                    "< Location: /b\r\n",
                    "< Content-Length: 4\r\n",
                    "* Ignoring the response-body",
                    "* setting size while ignoring",
                    "< \r\n",
                    "* Connection #0 to host 127.0.0.1:18601 left intact",
                },
                events.Events.Take(10).ToArray(),
                $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ChunkedRedirectFollowedPast_ReportsTheIgnoredBodyWithoutASize()
    {
        // curl -s -L -v against a chunked 302: "Ignoring the response-body" and no size line.
        RecordingTransferEvents events = new();

        await Handler(QueueConnector.For(Connection("HTTP/1.1 302 Found\r\nLocation: /b\r\nTransfer-Encoding: chunked\r\n\r\n4\r\nmove\r\n0\r\n\r\n", 65536)))
            .ExecuteAsync(EventsContext("http://127.0.0.1:18601/a", events, new HttpRequestOptions { FollowRedirects = true }));

        CollectionAssert.AreEqual(
            new[] { "< Transfer-Encoding: chunked\r\n", "* Ignoring the response-body", "< \r\n" },
            events.Events.Skip(5).Take(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadRedirectFollowedPast_ReportsTheIgnoredBodyAndItsSize()
    {
        // curl -s -I -L -v against a 302 with Content-Length: 4 and no body.
        RecordingTransferEvents events = new();

        await Handler(QueueConnector.For(Connection("HTTP/1.1 302 Found\r\nLocation: /b\r\nContent-Length: 4\r\n\r\n", 65536)))
            .ExecuteAsync(HeadRedirectContext(events, ignoresContentLength: false));

        CollectionAssert.AreEqual(
            new[] { "< Content-Length: 4\r\n", "* Ignoring the response-body", "* setting size while ignoring", "< \r\n" },
            events.Events.Skip(5).Take(4).ToArray());
    }

    [TestMethod]
    [DataRow("HTTP/1.1 302 Found\r\nLocation: /b\r\nTransfer-Encoding: chunked\r\nContent-Length: 4\r\n\r\n", false, DisplayName = "Chunked")]
    [DataRow("HTTP/1.1 302 Found\r\nLocation: /b\r\nContent-Length: 4\r\n\r\n", true, DisplayName = "--ignore-content-length")]
    public async Task ExecuteAsync_HeadRedirectWithoutAUsableLengthFollowedPast_ReportsTheIgnoredBodyWithoutASize(string response, bool ignoresContentLength)
    {
        // A chunked body's length is unknown, and --ignore-content-length never reads it.
        RecordingTransferEvents events = new();

        await Handler(QueueConnector.For(Connection(response, 65536))).ExecuteAsync(HeadRedirectContext(events, ignoresContentLength));

        Assert.AreEqual(1, events.Info.Count(line => line == "Ignoring the response-body"));
        CollectionAssert.DoesNotContain(events.Info.ToList(), "setting size while ignoring");
    }

    [TestMethod]
    [DataRow("HTTP/1.1 302 Found\r\nLocation: /b\r\nConnection: close\r\nContent-Length: 4\r\n\r\nmove", DisplayName = "Connection: close")]
    [DataRow("HTTP/1.1 302 Found\r\nLocation: /b\r\n\r\nmove", DisplayName = "No length")]
    public async Task ExecuteAsync_RedirectOnAClosingConnectionFollowedPast_ReportsNoIgnoredBody(string response)
    {
        // curl -s -L -v: a 302 whose connection closes after it prints neither line.
        RecordingTransferEvents events = new();

        await Handler(QueueConnector.For(Connection(response, 65536)))
            .ExecuteAsync(EventsContext("http://127.0.0.1:18601/a", events, new HttpRequestOptions { FollowRedirects = true }));

        Assert.IsFalse(events.Info.Any(line => line.Contains("ignoring", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task ExecuteAsync_FailOn404_StillReportsTheHeadsEmptyLine()
    {
        RecordingTransferEvents events = new();

        TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 404 Not Found\r\nContent-Length: 4\r\n\r\nnope", 65536)))
            .ExecuteAsync(EventsContext("http://127.0.0.1:18601/a", events, new HttpRequestOptions { Fail = HttpFailMode.Fail }));

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "< HTTP/1.1 404 Not Found\r\n", "< Content-Length: 4\r\n", "< \r\n" },
            events.Events.Where(e => e.StartsWith('<')).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_ContinueArrivesBeforeTheWaitRunsOut_ReportsNoDoneWaitingLine()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        RecordingTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://127.0.0.1:18604/u"),
            Output = new MemoryStream(),
            Upload = StandardInput("abcde"u8.ToArray()),
            TimeProvider = time,
            Events = events,
        };

        TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", 65536)))
            .ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.DoesNotContain(events.Info.ToList(), "Done waiting for 100-continue");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_ReusedConnectionResetByThePeer_ReportsTheWinsockReceiveFailureBeforeTheRetry()
    {
        // curl -v on two URLs, the server closing after the first: "Recv failure: Connection
        // was reset" before "Connection died, retrying a fresh connect (retry count: 1)".
        await AssertReusedConnectionResetReportsAsync(new SocketException((int)SocketError.ConnectionReset), "Recv failure: Connection was reset");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_ReusedConnectionResetByThePeer_ReportsTheSocketErrorsOwnWordsBeforeTheRetry()
    {
        SocketException socketError = new((int)SocketError.ConnectionReset);

        await AssertReusedConnectionResetReportsAsync(socketError, "Recv failure: " + socketError.Message);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_FreshConnectionResetByThePeer_ReportsTheWinsockReceiveFailureBeforeClosing()
    {
        await AssertFreshConnectionFailureReportsAsync(new SocketException((int)SocketError.ConnectionReset), "Recv failure: Connection was reset");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_FreshConnectionResetByThePeer_ReportsTheSocketErrorsOwnWordsBeforeClosing()
    {
        SocketException socketError = new((int)SocketError.ConnectionReset);

        await AssertFreshConnectionFailureReportsAsync(socketError, "Recv failure: " + socketError.Message);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_FreshConnectionAbortedUnderTheRead_ReportsTheWinsockReceiveFailureBeforeClosing()
    {
        await AssertFreshConnectionFailureReportsAsync(new SocketException((int)SocketError.ConnectionAborted), "Recv failure: Connection was aborted");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_FreshConnectionAbortedUnderTheRead_ReportsTheSocketErrorsOwnWordsBeforeClosing()
    {
        SocketException socketError = new((int)SocketError.ConnectionAborted);

        await AssertFreshConnectionFailureReportsAsync(socketError, "Recv failure: " + socketError.Message);
    }

    private static async Task AssertReusedConnectionResetReportsAsync(SocketException socketError, string expectedLine)
    {
        ScriptedConnection dead = new([], 65536, failureAfterResponse: new IOException("Connection reset.", socketError));
        RecordingTransferEvents events = new();
        QueueConnector connector = new(
            ConnectResult.Connected(dead, null, isReused: true),
            ConnectResult.Connected(Connection(KeepAliveResponse, 65536, ReuseRequest), null, connectionNumber: 1));

        TransferResult result = await Handler(connector).ExecuteAsync(ReuseContext(events));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { RequestSent, expectedLine, DiedRetrying, "shutting down connection #0", IssueAnother },
            events.Info.Take(5).ToArray());
    }

    private static async Task AssertFreshConnectionFailureReportsAsync(SocketException socketError, string expectedLine)
    {
        IOException failure = new("Connection failed.", socketError);
        RecordingTransferEvents events = new();

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection([], 65536, failureAfterResponse: failure)))
            .ExecuteAsync(ReuseContext(events));

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(expectedLine, result.ErrorMessage);
        CollectionAssert.AreEqual(
            new[] { UsingHttp1, RequestSent, expectedLine, "closing connection #0" },
            events.Info.ToArray());
    }

    [TestMethod]
    [DataRow("HTTP/1.0 200 OK\r\nContent-Length: 2\r\n\r\nok", "< Content-Length: 2\r\n", DisplayName = "Without keep-alive")]
    [DataRow("HTTP/1.0 200 OK\r\nConnection: keep-alive\r\nContent-Length: 2\r\n\r\nok", "* HTTP/1.0 connection set to keep alive", DisplayName = "With keep-alive")]
    public async Task ExecuteAsync_Http10Response_ReportsAssumeCloseBeforeTheStatusLine(string response, string firstHeader)
    {
        // curl -s -v against an HTTP/1.0 200: "HTTP 1.0, assume close after body" right after
        // "Request completely sent off", keep-alive or not.
        RecordingTransferEvents events = new();

        await Handler(QueueConnector.For(Connection(response, 1))).ExecuteAsync(EventsContext("http://127.0.0.1:18602/a", events));

        CollectionAssert.AreEqual(
            new[] { "* Request completely sent off", "* HTTP 1.0, assume close after body", "< HTTP/1.0 200 OK\r\n", firstHeader },
            events.Events.Skip(2).Take(4).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_Http11Response_ReportsNoAssumeCloseLine()
    {
        RecordingTransferEvents events = new();

        await Handler(QueueConnector.For(Connection(KeepAliveResponse, 65536))).ExecuteAsync(EventsContext("http://127.0.0.1:18602/a", events));

        CollectionAssert.DoesNotContain(events.Info.ToList(), "HTTP 1.0, assume close after body");
    }

    private static TransferContext HeadRedirectContext(ITransferEvents events, bool ignoresContentLength) =>
        new()
        {
            Url = CurlUrl.Parse("http://127.0.0.1:18601/a"),
            Output = new MemoryStream(),
            Events = events,
            NoBody = true,
            Http = new HttpRequestOptions { FollowRedirects = true, IgnoreContentLength = ignoresContentLength },
        };
}
