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

    private const string IssueAnother = "Issue another request to this URL: 'http://127.0.0.1:18977/b'";

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", DisplayName = "Content-Length")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n2\r\nok\r\n0\r\n\r\n", DisplayName = "Chunked")]
    [DataRow("HTTP/1.0 200 OK\r\nConnection: keep-alive\r\nContent-Length: 2\r\n\r\nok", DisplayName = "HTTP/1.0 with keep-alive")]
    public async Task ExecuteAsync_ResponsePersists_MarksTheConnectionReusableAndLeavesItIntact(string response)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = Connection(response, chunkSize);
            RecordingTransferEvents events = new();

            TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(connection, null, connectionNumber: 0)))
                .ExecuteAsync(ReuseContext(events));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.IsTrue(connection.IsMarkedReusable, $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(InfoLines(response, "Connection #0 to host 127.0.0.1:18977 left intact"), events.Info, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 2\r\n\r\nok", DisplayName = "Connection: close")]
    [DataRow("HTTP/1.0 200 OK\r\nContent-Length: 2\r\n\r\nok", DisplayName = "HTTP/1.0 without keep-alive")]
    [DataRow("HTTP/1.1 200 OK\r\n\r\nok", DisplayName = "Body read to close")]
    [DataRow("HTTP/1.1 101 Switching Protocols\r\nUpgrade: h2c\r\n\r\nHTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", DisplayName = "101")]
    public async Task ExecuteAsync_ResponseDoesNotPersist_LeavesTheConnectionUnmarkedAndShutsItDown(string response)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = Connection(response, chunkSize);
            RecordingTransferEvents events = new();

            TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(connection, null, connectionNumber: 4)))
                .ExecuteAsync(ReuseContext(events));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.IsFalse(connection.IsMarkedReusable, $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(InfoLines(response, "shutting down connection #4"), events.Info, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ContentLengthOverMaxFileSize_LeavesTheConnectionUnmarkedAndClosesIt()
    {
        ScriptedConnection connection = Connection(KeepAliveResponse, 65536);
        RecordingTransferEvents events = new();

        TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(connection, null, connectionNumber: 2)))
            .ExecuteAsync(ReuseContext(events, maxFileSize: 1));

        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.IsFalse(connection.IsMarkedReusable);
        CollectionAssert.AreEqual(new[] { UsingHttp1, RequestSent, "closing connection #2" }, events.Info);
    }

    [TestMethod]
    public async Task ExecuteAsync_BodyCutShort_LeavesTheConnectionUnmarkedAndClosesIt()
    {
        ScriptedConnection connection = Connection("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nok", 65536);
        RecordingTransferEvents events = new();

        TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(connection, null, connectionNumber: 3)))
            .ExecuteAsync(ReuseContext(events));

        Assert.AreEqual(CurlExitCode.PartialFile, result.ExitCode);
        Assert.IsFalse(connection.IsMarkedReusable);
        CollectionAssert.AreEqual(new[] { UsingHttp1, RequestSent, "closing connection #3" }, events.Info);
    }

    [TestMethod]
    [DataRow(true, 0, DisplayName = "Reused")]
    [DataRow(false, 1, DisplayName = "Opened")]
    public async Task ExecuteAsync_ConnectorSaysReused_ReportsNoConnect(bool isReused, int connectionCount)
    {
        ScriptedConnection connection = Connection(KeepAliveResponse, 65536, ReuseRequest);

        TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(connection, null, isReused: isReused)))
            .ExecuteAsync(ReuseContext(new RecordingTransferEvents()));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
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

            TransferResult result = await Handler(connector).ExecuteAsync(ReuseContext(events, output));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("hi", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(2, connector.Targets.Count, $"Chunk size {chunkSize}");
            Assert.AreEqual(1, result.Report!.ConnectionCount, $"Chunk size {chunkSize}");
            Assert.AreEqual(2L * ReuseRequest.Length, result.Report.RequestSize, $"Chunk size {chunkSize}");
            Assert.IsTrue(dead.IsDisposed, $"Chunk size {chunkSize}");
            Assert.IsFalse(dead.IsMarkedReusable, $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(
                new[] { RequestSent, DiedRetrying, "shutting down connection #0", IssueAnother, UsingHttp1, RequestSent, "shutting down connection #1" },
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

        TransferResult result = await Handler(connector).ExecuteAsync(ReuseContext(events));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(2, connector.Targets.Count);
        CollectionAssert.AreEqual(
            new[] { RequestSent, DiedRetrying, "shutting down connection #0", IssueAnother, UsingHttp1, RequestSent, "Connection #1 to host 127.0.0.1:18977 left intact" },
            events.Info);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReusedConnectionFailsTheSend_SendsTheRequestAgain()
    {
        FailingSendConnection dead = new(new IOException("Connection reset."), 0);
        QueueConnector connector = new(
            ConnectResult.Connected(dead, null, isReused: true),
            ConnectResult.Connected(Connection(KeepAliveResponse, 65536, ReuseRequest), null));

        TransferResult result = await Handler(connector).ExecuteAsync(ReuseContext(new RecordingTransferEvents()));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(2, connector.Targets.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResentRequestFindsItsConnectionDeadToo_FailsWithoutAThirdTry()
    {
        RecordingTransferEvents events = new();
        QueueConnector connector = new(
            ConnectResult.Connected(Connection(string.Empty, 65536), null, isReused: true, connectionNumber: 0),
            ConnectResult.Connected(Connection(string.Empty, 65536), null, isReused: true, connectionNumber: 1));

        TransferResult result = await Handler(connector).ExecuteAsync(ReuseContext(events));

        Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode);
        Assert.AreEqual(2, connector.Targets.Count);
        CollectionAssert.AreEqual(
            new[] { RequestSent, DiedRetrying, "shutting down connection #0", IssueAnother, RequestSent, "closing connection #1" },
            events.Info);
    }

    [TestMethod]
    public async Task ExecuteAsync_OpenedConnectionGetsNoReply_FailsWithoutSendingAgain()
    {
        RecordingTransferEvents events = new();
        QueueConnector connector = new(ConnectResult.Connected(Connection(string.Empty, 65536), null));

        TransferResult result = await Handler(connector).ExecuteAsync(ReuseContext(events));

        Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode);
        CollectionAssert.AreEqual(new[] { UsingHttp1, RequestSent, "closing connection #0" }, events.Info);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReusedConnectionFailsPartwayThroughTheHead_FailsWithoutSendingAgain()
    {
        ScriptedConnection dead = new(Encoding.Latin1.GetBytes("HTTP/1.1 2"), 65536, failureAfterResponse: new IOException("Connection reset."));
        QueueConnector connector = new(ConnectResult.Connected(dead, null, isReused: true));

        TransferResult result = await Handler(connector).ExecuteAsync(ReuseContext(new RecordingTransferEvents()));

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(1, connector.Targets.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReusedConnectionFailsWithAnotherExitCode_FailsWithoutSendingAgain()
    {
        FailingReadStream stream = new(new byte[207], 65536, new IOException("Lock violation."));
        HttpRequestOptions options = new() { Body = new StreamBody(stream, 100207, "multipart/form-data; boundary=b") };
        QueueConnector connector = new(ConnectResult.Connected(Connection(NoContent, 65536), null, isReused: true));

        TransferResult result = await Handler(connector).ExecuteAsync(ReuseContext(new RecordingTransferEvents(), http: options));

        Assert.AreEqual(CurlExitCode.ReadError, result.ExitCode);
        Assert.AreEqual(1, connector.Targets.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReusedConnectionDiedAfterAStreamBody_FailsWithoutSendingAgain()
    {
        HttpRequestOptions options = new() { Body = new StreamBody(new MemoryStream(new byte[3]), 3, "application/octet-stream") };
        QueueConnector connector = new(ConnectResult.Connected(Connection(string.Empty, 65536), null, isReused: true));

        TransferResult result = await Handler(connector).ExecuteAsync(ReuseContext(new RecordingTransferEvents(), http: options));

        Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode);
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

        TransferResult result = await Handler(connector).ExecuteAsync(ReuseContext(events));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("closing connection #0", events.Info[^1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResolveFails_ReportsClosingConnection()
    {
        RecordingTransferEvents events = new();
        QueueConnector connector = new(ConnectResult.Failed(CurlExitCode.CouldntResolveHost, "Could not resolve host: nonexistent.invalid"));

        TransferResult result = await Handler(connector).ExecuteAsync(ReuseContext(events));

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "closing connection #0" }, events.Info);
    }

    private static string[] InfoLines(string response, string connectionEnd) =>
        response.StartsWith("HTTP/1.0", StringComparison.Ordinal)
            ? [UsingHttp1, RequestSent, "HTTP 1.0, assume close after body", connectionEnd]
            : [UsingHttp1, RequestSent, connectionEnd];

    private static TransferContext ReuseContext(ITransferEvents events, Stream? output = null, long? maxFileSize = null, HttpRequestOptions? http = null) =>
        new() { Url = CurlUrl.Parse(ReuseUrl), Output = output ?? new MemoryStream(), Events = events, MaxFileSize = maxFileSize, Http = http };
}
