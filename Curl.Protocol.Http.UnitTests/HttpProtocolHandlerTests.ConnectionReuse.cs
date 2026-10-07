using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

public sealed partial class HttpProtocolHandlerTests
{
    private const string ReuseUrl = "http://127.0.0.1:18977/b";

    private const string ReuseRequest = "GET /b HTTP/1.1\r\nHost: 127.0.0.1:18977\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n";

    private const string KeepAliveResponse = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok";

    private const string DiedRetrying = "Connection died, retrying a fresh connect (retry count: 1)";

    private const string UsingHttp1 = "using HTTP/1.x";

    private const string RequestSent = "Request completely sent off";

    private const string AssumeClose = "HTTP 1.0, assume close after body";

    private const string Http10KeepAlive = "HTTP/1.0 connection set to keep alive";

    private const string NoEndOfMessage = "no chunk, no close, no size. Assume close to signal end";

    private const string IssueAnother = "Issue another request to this URL: 'http://127.0.0.1:18977/b'";

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", "", DisplayName = "Content-Length")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n2\r\nok\r\n0\r\n\r\n", "", DisplayName = "Chunked")]
    [DataRow("HTTP/1.0 200 OK\r\nConnection: keep-alive\r\nContent-Length: 2\r\n\r\nok", AssumeClose + "|" + Http10KeepAlive, DisplayName = "HTTP/1.0 with keep-alive")]
    public async Task ExecuteAsync_ResponsePersists_MarksTheConnectionReusableAndLeavesItIntact(string response, string headInfoLines)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = Connection(response, chunkSize);
            RecordingTransferEvents events = new();
            Diagnostics.Arrange("url, chunk size", $"{ReuseUrl}, {chunkSize}");
            Diagnostics.Arrange("response", OneLine(response));

            TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(connection, null, connectionNumber: 0)))
                .ExecuteAsync(ReuseContext(events));

            WriteResult(result);
            WriteEvents("info lines", events.Info);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Diagnostics.Assert("connection marked reusable", true, connection.IsMarkedReusable);
            Assert.IsTrue(connection.IsMarkedReusable, $"Chunk size {chunkSize}");
            string[] expectedInfo = InfoLines(headInfoLines, "Connection #0 to host 127.0.0.1:18977 left intact");
            WriteExpectedLines("info lines", expectedInfo, events.Info);
            CollectionAssert.AreEqual(expectedInfo, events.Info, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 2\r\n\r\nok", "", DisplayName = "Connection: close")]
    [DataRow("HTTP/1.0 200 OK\r\nContent-Length: 2\r\n\r\nok", AssumeClose, DisplayName = "HTTP/1.0 without keep-alive")]
    [DataRow("HTTP/1.1 200 OK\r\n\r\nok", NoEndOfMessage, DisplayName = "Body read to close")]
    [DataRow("HTTP/1.1 101 Switching Protocols\r\nUpgrade: h2c\r\n\r\nHTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", "", DisplayName = "101")]
    public async Task ExecuteAsync_ResponseDoesNotPersist_LeavesTheConnectionUnmarkedAndShutsItDown(string response, string headInfoLines)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = Connection(response, chunkSize);
            RecordingTransferEvents events = new();
            Diagnostics.Arrange("url, chunk size", $"{ReuseUrl}, {chunkSize}");
            Diagnostics.Arrange("response", OneLine(response));

            TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(connection, null, connectionNumber: 4)))
                .ExecuteAsync(ReuseContext(events));

            WriteResult(result);
            WriteEvents("info lines", events.Info);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Diagnostics.Assert("connection marked reusable", false, connection.IsMarkedReusable);
            Assert.IsFalse(connection.IsMarkedReusable, $"Chunk size {chunkSize}");
            string[] expectedInfo = InfoLines(headInfoLines, "shutting down connection #4");
            WriteExpectedLines("info lines", expectedInfo, events.Info);
            CollectionAssert.AreEqual(expectedInfo, events.Info, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_Http10KeepAliveBodyRunsToTheClose_MarksTheConnectionReusableAndReportsItLeftIntact()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = Connection("HTTP/1.0 200 OK\r\nConnection: keep-alive\r\n\r\nhi", chunkSize);
            RecordingTransferEvents events = new();
            Diagnostics.Arrange("url, chunk size", $"{ReuseUrl}, {chunkSize}");

            TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(connection, null, connectionNumber: 0)))
                .ExecuteAsync(ReuseContext(events));

            WriteResult(result);
            WriteEvents("info lines", events.Info);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Diagnostics.Assert("connection marked reusable", true, connection.IsMarkedReusable);
            Assert.IsTrue(connection.IsMarkedReusable, $"Chunk size {chunkSize}");
            string[] expectedInfo = InfoLines(AssumeClose + "|" + Http10KeepAlive, "Connection #0 to host 127.0.0.1:18977 left intact");
            WriteExpectedLines("info lines", expectedInfo, events.Info);
            CollectionAssert.AreEqual(
                expectedInfo,
                events.Info,
                $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_KeptAliveOverUnixSocket_ReportsTheLowerCasedSocketPathAndPort0LeftIntact()
    {
        // curl 8.21.0 -v --unix-socket "C:\Users\Public\BL794 Sock.sock" http://Example.COM:8080/x
        // ends "Connection #0 to host c:\users\public\bl794 sock.sock:0 left intact" (BL-794 Notes).
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = Connection("HTTP/1.0 200 OK\r\nConnection: keep-alive\r\n\r\nhi", chunkSize);
            RecordingTransferEvents events = new();
            Diagnostics.Arrange("url, chunk size", $"{ReuseUrl}, {chunkSize}");
            Diagnostics.Arrange("unix socket path", @"C:\Users\Public\BL794 Sock-Z.sock");

            TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(connection, null, unixSocketPath: @"C:\Users\Public\BL794 Sock-Z.sock")))
                .ExecuteAsync(ReuseContext(events));

            WriteResult(result);
            WriteEvents("info lines", events.Info);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            string[] expectedInfo = InfoLines(AssumeClose + "|" + Http10KeepAlive, @"Connection #0 to host c:\users\public\bl794 sock-z.sock:0 left intact");
            WriteExpectedLines("info lines", expectedInfo, events.Info);
            CollectionAssert.AreEqual(
                expectedInfo,
                events.Info,
                $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_SecondUrlAfterAnHttp10KeepAliveBodyRanToTheClose_ReportsThePooledConnectionDeadAndOpensConnection1()
    {
        // curl 8.21.0 -s -v http://127.0.0.1:P/a http://127.0.0.1:P/b, each answered
        // HTTP/1.0 200 with Connection: keep-alive and body "hi", then closed (BL-477 Notes).
        // curl's "Hostname ... was found in DNS cache" and "Trying" lines come from the TCP
        // connector, which this test replaces.
        const string response = "HTTP/1.0 200 OK\r\nConnection: keep-alive\r\n\r\nhi";
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection first = Connection(response, chunkSize);
            ScriptedConnection second = Connection(response, chunkSize, ReuseRequest);
            QueueConnector inner = new(ConnectResult.Connected(first, null), ConnectResult.Connected(second, null));
            await using Networking.PoolingConnector pool = new(inner, TimeProvider.System);
            HttpProtocolHandler handler = new(pool, new SilentAuthenticator());
            RecordingTransferEvents events = new();
            MemoryStream output = new();
            Diagnostics.Arrange("urls, chunk size", $"http://127.0.0.1:18977/a, {ReuseUrl}, {chunkSize}");
            Diagnostics.Arrange("response", OneLine(response));

            TransferResult a = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("http://127.0.0.1:18977/a"), Output = output, Events = events });
            TransferResult b = await handler.ExecuteAsync(ReuseContext(events, output));

            WriteResult(a);
            WriteResult(b);
            WriteEvents("info lines", events.Info);
            Diagnostics.Assert("first exit code", CurlExitCode.Ok, a.ExitCode);
            Assert.AreEqual(CurlExitCode.Ok, a.ExitCode, $"Chunk size {chunkSize}");
            Diagnostics.Assert("second exit code", CurlExitCode.Ok, b.ExitCode);
            Assert.AreEqual(CurlExitCode.Ok, b.ExitCode, $"Chunk size {chunkSize}");
            Diagnostics.Assert("output", "hihi", Latin1(output.ToArray()));
            Assert.AreEqual("hihi", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Diagnostics.Assert("first connection disposed", true, first.IsDisposed);
            Assert.IsTrue(first.IsDisposed, $"Chunk size {chunkSize}");
            Diagnostics.Assert("second connection count", 1, b.Report!.ConnectionCount);
            Assert.AreEqual(1, b.Report!.ConnectionCount, $"Chunk size {chunkSize}");
            string[] expectedInfo =
            [
                UsingHttp1, RequestSent, AssumeClose, Http10KeepAlive, "Connection #0 to host 127.0.0.1:18977 left intact",
                "Connection 0 seems to be dead", "shutting down connection #0",
                UsingHttp1, RequestSent, AssumeClose, Http10KeepAlive, "Connection #1 to host 127.0.0.1:18977 left intact",
            ];
            WriteExpectedLines("info lines", expectedInfo, events.Info);
            CollectionAssert.AreEqual(
                expectedInfo,
                events.Info,
                $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ContentLengthOverMaxFileSize_LeavesTheConnectionUnmarkedAndClosesIt()
    {
        ScriptedConnection connection = Connection(KeepAliveResponse, 65536);
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("url, max file size", $"{ReuseUrl}, 1");
        Diagnostics.Arrange("response", OneLine(KeepAliveResponse));

        TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(connection, null, connectionNumber: 2)))
            .ExecuteAsync(ReuseContext(events, maxFileSize: 1));

        WriteResult(result);
        WriteEvents("info lines", events.Info);
        Diagnostics.Assert("exit code", CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Diagnostics.Assert("connection marked reusable", false, connection.IsMarkedReusable);
        Assert.IsFalse(connection.IsMarkedReusable);
        string[] expectedInfo = [UsingHttp1, RequestSent, "closing connection #2"];
        WriteExpectedLines("info lines", expectedInfo, events.Info);
        CollectionAssert.AreEqual(expectedInfo, events.Info);
    }

    [TestMethod]
    public async Task ExecuteAsync_BodyCutShort_LeavesTheConnectionUnmarkedAndClosesIt()
    {
        ScriptedConnection connection = Connection("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nok", 65536);
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("url", ReuseUrl);
        Diagnostics.Arrange("response", OneLine("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nok"));

        TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(connection, null, connectionNumber: 3)))
            .ExecuteAsync(ReuseContext(events));

        WriteResult(result);
        WriteEvents("info lines", events.Info);
        Diagnostics.Assert("exit code", CurlExitCode.PartialFile, result.ExitCode);
        Assert.AreEqual(CurlExitCode.PartialFile, result.ExitCode);
        Diagnostics.Assert("connection marked reusable", false, connection.IsMarkedReusable);
        Assert.IsFalse(connection.IsMarkedReusable);
        string[] expectedInfo = [UsingHttp1, RequestSent, "closing connection #3"];
        WriteExpectedLines("info lines", expectedInfo, events.Info);
        CollectionAssert.AreEqual(expectedInfo, events.Info);
    }

    [TestMethod]
    [DataRow(true, 0, DisplayName = "Reused")]
    [DataRow(false, 1, DisplayName = "Opened")]
    public async Task ExecuteAsync_ConnectorSaysReused_ReportsNoConnect(bool isReused, int connectionCount)
    {
        ScriptedConnection connection = Connection(KeepAliveResponse, 65536, ReuseRequest);
        Diagnostics.Arrange("url, connector says reused", $"{ReuseUrl}, {isReused}");

        TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(connection, null, isReused: isReused)))
            .ExecuteAsync(ReuseContext(new RecordingTransferEvents()));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("connection count", connectionCount, result.Report!.ConnectionCount);
        Assert.AreEqual(connectionCount, result.Report!.ConnectionCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReusedConnectionDiedBeforeTheResponse_SendsTheRequestAgainOnceOnAFreshConnection()
    {
        // curl -v http://127.0.0.1:18977/a http://127.0.0.1:18977/b against a server that
        // answers /a with keep-alive, then closes on /b and answers it on a new connection
        // with Connection: close (BL-336 Notes). curl's %{num_connects} is 1 and its
        // %{size_request} counts the request on both connections.
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection dead = Connection(string.Empty, chunkSize, ReuseRequest);
            ScriptedConnection fresh = Connection("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nhi", chunkSize, ReuseRequest);
            RecordingTransferEvents events = new();
            MemoryStream output = new();
            QueueConnector connector = new(
                ConnectResult.Connected(dead, null, isReused: true, connectionNumber: 0),
                ConnectResult.Connected(fresh, null, connectionNumber: 1));
            Diagnostics.Arrange("url, chunk size", $"{ReuseUrl}, {chunkSize}");

            TransferResult result = await Handler(connector).ExecuteAsync(ReuseContext(events, output));

            WriteResult(result);
            WriteEvents("info lines", events.Info);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Diagnostics.Assert("output", "hi", Latin1(output.ToArray()));
            Assert.AreEqual("hi", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Diagnostics.Assert("connect targets", 2, connector.Targets.Count);
            Assert.AreEqual(2, connector.Targets.Count, $"Chunk size {chunkSize}");
            Diagnostics.Assert("connection count", 1, result.Report!.ConnectionCount);
            Assert.AreEqual(1, result.Report!.ConnectionCount, $"Chunk size {chunkSize}");
            Diagnostics.Assert("request size", 2L * ReuseRequest.Length, result.Report.RequestSize);
            Assert.AreEqual(2L * ReuseRequest.Length, result.Report.RequestSize, $"Chunk size {chunkSize}");
            Diagnostics.Assert("dead connection disposed", true, dead.IsDisposed);
            Assert.IsTrue(dead.IsDisposed, $"Chunk size {chunkSize}");
            Diagnostics.Assert("dead connection marked reusable", false, dead.IsMarkedReusable);
            Assert.IsFalse(dead.IsMarkedReusable, $"Chunk size {chunkSize}");
            string[] expectedInfo = [RequestSent, DiedRetrying, "shutting down connection #0", IssueAnother, UsingHttp1, RequestSent, "shutting down connection #1"];
            WriteExpectedLines("info lines", expectedInfo, events.Info);
            CollectionAssert.AreEqual(
                expectedInfo,
                events.Info,
                $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ReusedConnectionResetBeforeTheResponse_SendsTheRequestAgain()
    {
        ScriptedConnection dead = new([], 65536, failureAfterResponse: new IOException("Connection reset."));
        RecordingTransferEvents events = new();
        QueueConnector connector = new(
            ConnectResult.Connected(dead, null, isReused: true),
            ConnectResult.Connected(Connection(KeepAliveResponse, 65536, ReuseRequest), null, connectionNumber: 1));
        Diagnostics.Arrange("url, reused connection failure", $"{ReuseUrl}, Connection reset.");

        TransferResult result = await Handler(connector).ExecuteAsync(ReuseContext(events));

        WriteResult(result);
        WriteEvents("info lines", events.Info);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("connect targets", 2, connector.Targets.Count);
        Assert.AreEqual(2, connector.Targets.Count);
        string[] expectedInfo = [RequestSent, DiedRetrying, "shutting down connection #0", IssueAnother, UsingHttp1, RequestSent, "Connection #1 to host 127.0.0.1:18977 left intact"];
        WriteExpectedLines("info lines", expectedInfo, events.Info);
        CollectionAssert.AreEqual(
            expectedInfo,
            events.Info);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReusedConnectionFailsTheSend_SendsTheRequestAgain()
    {
        FailingSendConnection dead = new(new IOException("Connection reset."), 0);
        QueueConnector connector = new(
            ConnectResult.Connected(dead, null, isReused: true),
            ConnectResult.Connected(Connection(KeepAliveResponse, 65536, ReuseRequest), null));
        Diagnostics.Arrange("url, send failure", $"{ReuseUrl}, Connection reset.");

        TransferResult result = await Handler(connector).ExecuteAsync(ReuseContext(new RecordingTransferEvents()));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("connect targets", 2, connector.Targets.Count);
        Assert.AreEqual(2, connector.Targets.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResentRequestFindsItsConnectionDeadToo_FailsWithoutAThirdTry()
    {
        RecordingTransferEvents events = new();
        QueueConnector connector = new(
            ConnectResult.Connected(Connection(string.Empty, 65536), null, isReused: true, connectionNumber: 0),
            ConnectResult.Connected(Connection(string.Empty, 65536), null, isReused: true, connectionNumber: 1));
        Diagnostics.Arrange("url, response of both connections", $"{ReuseUrl}, (empty)");

        TransferResult result = await Handler(connector).ExecuteAsync(ReuseContext(events));

        WriteResult(result);
        WriteEvents("info lines", events.Info);
        Diagnostics.Assert("exit code", CurlExitCode.GotNothing, result.ExitCode);
        Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode);
        Diagnostics.Assert("connect targets", 2, connector.Targets.Count);
        Assert.AreEqual(2, connector.Targets.Count);
        string[] expectedInfo = [RequestSent, DiedRetrying, "shutting down connection #0", IssueAnother, RequestSent, "closing connection #1"];
        WriteExpectedLines("info lines", expectedInfo, events.Info);
        CollectionAssert.AreEqual(
            expectedInfo,
            events.Info);
    }

    [TestMethod]
    public async Task ExecuteAsync_OpenedConnectionGetsNoReply_FailsWithoutSendingAgain()
    {
        RecordingTransferEvents events = new();
        QueueConnector connector = new(ConnectResult.Connected(Connection(string.Empty, 65536), null));
        Diagnostics.Arrange("url, response", $"{ReuseUrl}, (empty)");

        TransferResult result = await Handler(connector).ExecuteAsync(ReuseContext(events));

        WriteResult(result);
        WriteEvents("info lines", events.Info);
        Diagnostics.Assert("exit code", CurlExitCode.GotNothing, result.ExitCode);
        Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode);
        string[] expectedInfo = [UsingHttp1, RequestSent, "closing connection #0"];
        WriteExpectedLines("info lines", expectedInfo, events.Info);
        CollectionAssert.AreEqual(expectedInfo, events.Info);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReusedConnectionFailsPartwayThroughTheHead_FailsWithoutSendingAgain()
    {
        ScriptedConnection dead = new(Encoding.Latin1.GetBytes("HTTP/1.1 2"), 65536, failureAfterResponse: new IOException("Connection reset."));
        QueueConnector connector = new(ConnectResult.Connected(dead, null, isReused: true));
        Diagnostics.Arrange("url, partial head, failure", $"{ReuseUrl}, HTTP/1.1 2, Connection reset.");

        TransferResult result = await Handler(connector).ExecuteAsync(ReuseContext(new RecordingTransferEvents()));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Diagnostics.Assert("connect targets", 1, connector.Targets.Count);
        Assert.AreEqual(1, connector.Targets.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReusedConnectionFailsWithAnotherExitCode_FailsWithoutSendingAgain()
    {
        FailingReadStream stream = new(new byte[207], 65536, new IOException("Lock violation."));
        HttpRequestOptions options = new() { Body = new StreamBody(stream, 100207, "multipart/form-data; boundary=b") };
        QueueConnector connector = new(ConnectResult.Connected(Connection(NoContent, 65536), null, isReused: true));
        Diagnostics.Arrange("url, body length, body read failure", $"{ReuseUrl}, 100207, Lock violation.");

        TransferResult result = await Handler(connector).ExecuteAsync(ReuseContext(new RecordingTransferEvents(), http: options));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.ReadError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.ReadError, result.ExitCode);
        Diagnostics.Assert("connect targets", 1, connector.Targets.Count);
        Assert.AreEqual(1, connector.Targets.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReusedConnectionDiedAfterAStreamBody_FailsWithoutSendingAgain()
    {
        HttpRequestOptions options = new() { Body = new StreamBody(new MemoryStream(new byte[3]), 3, "application/octet-stream") };
        QueueConnector connector = new(ConnectResult.Connected(Connection(string.Empty, 65536), null, isReused: true));
        Diagnostics.Arrange("url, stream body length", $"{ReuseUrl}, 3");

        TransferResult result = await Handler(connector).ExecuteAsync(ReuseContext(new RecordingTransferEvents(), http: options));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.GotNothing, result.ExitCode);
        Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode);
        Diagnostics.Assert("connect targets", 1, connector.Targets.Count);
        Assert.AreEqual(1, connector.Targets.Count);
    }

    /// <summary>
    /// The info lines of one request on a new connection: <c>using HTTP/1.x</c>, <c>Request
    /// completely sent off</c>, <c>HTTP 1.0, assume close after body</c> before an HTTP/1.0
    /// status line (measured, BL-449 Notes), and the connection's end.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_ConnectRefused_ReportsClosingConnectionLast()
    {
        RecordingTransferEvents events = new();
        QueueConnector connector = new(ConnectResult.Refused("Failed to connect to 127.0.0.1:1 after 0 ms: Could not connect to server"));
        Diagnostics.Arrange("url, connect result", $"{ReuseUrl}, refused");

        TransferResult result = await Handler(connector).ExecuteAsync(ReuseContext(events));

        WriteResult(result);
        WriteEvents("info lines", events.Info);
        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Diagnostics.Assert("last info line", "closing connection #0", events.Info[^1]);
        Assert.AreEqual("closing connection #0", events.Info[^1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResolveFails_ReportsClosingConnection()
    {
        RecordingTransferEvents events = new();
        QueueConnector connector = new(ConnectResult.Failed(CurlExitCode.CouldntResolveHost, "Could not resolve host: nonexistent.invalid"));
        Diagnostics.Arrange("url, connect result", $"{ReuseUrl}, could not resolve host");

        TransferResult result = await Handler(connector).ExecuteAsync(ReuseContext(events));

        WriteResult(result);
        WriteEvents("info lines", events.Info);
        Diagnostics.Assert("exit code", CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        string[] expectedInfo = ["closing connection #0"];
        WriteExpectedLines("info lines", expectedInfo, events.Info);
        CollectionAssert.AreEqual(expectedInfo, events.Info);
    }

    /// <summary>
    /// The info lines of one exchange on a fresh connection: the head's own lines, given as
    /// one string separated by <c>|</c>, between the request's and the connection's end.
    /// </summary>
    private static string[] InfoLines(string headInfoLines, string connectionEnd) =>
        [UsingHttp1, RequestSent, .. headInfoLines.Split('|', StringSplitOptions.RemoveEmptyEntries), connectionEnd];

    private static TransferContext ReuseContext(ITransferEvents events, Stream? output = null, long? maxFileSize = null, HttpRequestOptions? http = null) =>
        new() { Url = CurlUrl.Parse(ReuseUrl), Output = output ?? new MemoryStream(), Events = events, MaxFileSize = maxFileSize, Http = http };
}
