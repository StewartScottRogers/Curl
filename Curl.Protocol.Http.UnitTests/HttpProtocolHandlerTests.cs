using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Drives <see cref="HttpProtocolHandler" /> through <see cref="QueueConnector" /> and
/// <see cref="ScriptedConnection" />, never a socket. Every exchange that reads a response is
/// replayed with 1-byte reads and with one read, and must come out the same.
/// </summary>
[TestClass]
public sealed partial class HttpProtocolHandlerTests
{
    private const string RootRequest = "GET / HTTP/1.1\r\nHost: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n";

    private const string PathRequest = "GET /path?q=1 HTTP/1.1\r\nHost: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n";

    private const string Head = "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nX-Folded: a\r\n  b\r\nContent-Length: 5\r\n\r\n";

    private const string FoldedHead = "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nX-Folded: a b\r\nContent-Length: 5\r\n\r\n";

    private const string NoContent = "HTTP/1.1 204 No Content\r\n\r\n";

    private static readonly int[] ChunkSizes = [1, 65536];

    [TestMethod]
    public void SupportedSchemes_AreHttpAndHttps()
    {
        HttpProtocolHandler handler = Handler(new QueueConnector());

        CollectionAssert.AreEqual(new[] { "http", "https" }, handler.SupportedSchemes.ToArray());
    }

    [TestMethod]
    public void Constructor_KeepsTheAuthenticatorAndCookieStore()
    {
        SilentAuthenticator authenticator = new();

        HttpProtocolHandler handler = new(new QueueConnector(), authenticator);

        Assert.AreSame(authenticator, handler.Authenticator);
        Assert.IsNull(handler.CookieStore);
    }

    [TestMethod]
    public void Constructor_NullConnector_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new HttpProtocolHandler(null!, new SilentAuthenticator()));

    [TestMethod]
    public void Constructor_NullAuthenticator_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new HttpProtocolHandler(new QueueConnector(), null!));

    [TestMethod]
    public async Task ExecuteAsync_NullContext_Throws()
    {
        HttpProtocolHandler handler = Handler(new QueueConnector());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await handler.ExecuteAsync(null!));
    }

    [TestMethod]
    [DataRow("http://example.com/", "example.com", 80, false, DisplayName = "http defaults to 80")]
    [DataRow("https://example.com/", "example.com", 443, true, DisplayName = "https defaults to 443 with TLS")]
    [DataRow("HTTPS://Example.com:8443/", "Example.com", 8443, true, DisplayName = "https with a port")]
    [DataRow("http://example.com:8080/", "example.com", 8080, false, DisplayName = "http with a port")]
    [DataRow("http://[::1]/", "::1", 80, false, DisplayName = "IPv6 literal without brackets")]
    public async Task ExecuteAsync_Url_ConnectsToItsHostPortAndTls(string url, string host, int port, bool useTls)
    {
        QueueConnector connector = QueueConnector.For(Connection(NoContent, 65536));

        TransferResult result = await Handler(connector).ExecuteAsync(Context(url, new MemoryStream()));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(new ConnectTarget(host, port, useTls), connector.Targets.Single());
    }

    [TestMethod]
    [DataRow(CurlExitCode.CouldntResolveHost, "Could not resolve host: example.com", DisplayName = "6")]
    [DataRow(CurlExitCode.CouldntConnect, "Failed to connect to example.com port 80 after 0 ms: Could not connect to server", DisplayName = "7")]
    [DataRow(CurlExitCode.SslConnectError, "schannel: failed to receive handshake, SSL/TLS connection failed", DisplayName = "35")]
    [DataRow(CurlExitCode.PeerFailedVerification, "SSL certificate problem: unable to get local issuer certificate", DisplayName = "60")]
    public async Task ExecuteAsync_ConnectFails_ReturnsTheConnectorsExitCodeAndMessage(CurlExitCode exitCode, string message)
    {
        MemoryStream output = new();
        MemoryStream headerOutput = new();

        TransferResult result = await Handler(new QueueConnector(ConnectResult.Failed(exitCode, message)))
            .ExecuteAsync(Context("https://example.com/", output, headerOutput));

        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
        Assert.AreEqual(0L, result.BytesTransferred);
        Assert.AreEqual(0L, output.Length);
        Assert.AreEqual(0L, headerOutput.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_Exchange_SendsTheRequestAndWritesHeadersThenBody()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = Connection(Head + "hello", chunkSize, PathRequest);
            MemoryStream output = new();
            MemoryStream headerOutput = new();

            TransferResult result = await Handler(QueueConnector.For(connection))
                .ExecuteAsync(Context("http://example.com/path?q=1", output, headerOutput));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(5L, result.BytesTransferred, $"Chunk size {chunkSize}");
            Assert.AreEqual(FoldedHead, Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual("hello", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.IsTrue(connection.IsDisposed, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_HeaderOutputIsTheOutput_WritesHeadersBeforeTheBody()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();

            await Handler(QueueConnector.For(Connection(Head + "hello", chunkSize)))
                .ExecuteAsync(Context("http://example.com/", output, output));

            Assert.AreEqual(FoldedHead + "hello", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_InformationalAndChunkedResponse_WritesEveryHeadThenTheTrailersAfterTheBody()
    {
        const string informational = "HTTP/1.1 100 Continue\r\n\r\n";
        const string final = "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            MemoryStream headerOutput = new();

            TransferResult result = await Handler(QueueConnector.For(Connection(informational + final + "5\r\nhello\r\n0\r\nX-Trailer: t\r\n\r\n", chunkSize)))
                .ExecuteAsync(Context("http://example.com/", output, headerOutput));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(informational + final + "X-Trailer: t\r\n", Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual("hello", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual((long)(informational + final).Length, result.Report!.HeaderSize, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_NoHeaderOutput_WritesOnlyTheBody()
    {
        MemoryStream output = new();

        TransferResult result = await Handler(QueueConnector.For(Connection(Head + "hello", 65536)))
            .ExecuteAsync(Context("http://example.com/", output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("hello", Latin1(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_Exchange_ReportsWhatTheResponseSaid()
    {
        IPEndPoint local = new(IPAddress.Loopback, 50000);
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = Connection(Head + "hello", chunkSize);
            QueueConnector connector = new(ConnectResult.Connected(connection, null, local, 200));

            TransferResult result = await Handler(connector).ExecuteAsync(Context("http://example.com/path?q=1", new MemoryStream()));

            TransferReport report = result.Report!;
            Assert.AreEqual(200, report.ResponseCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(HttpVersion.Version11, report.HttpVersion, $"Chunk size {chunkSize}");
            Assert.AreEqual("GET", report.Method, $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(
                new[]
                {
                    KeyValuePair.Create("Content-Type", "text/plain"),
                    KeyValuePair.Create("X-Folded", "a b"),
                    KeyValuePair.Create("Content-Length", "5"),
                },
                report.ResponseHeaders.ToArray(),
                $"Chunk size {chunkSize}");
            Assert.AreEqual("text/plain", report.ContentType, $"Chunk size {chunkSize}");
            Assert.AreEqual((long)FoldedHead.Length, report.HeaderSize, $"Chunk size {chunkSize}");
            Assert.AreEqual((long)PathRequest.Length, report.RequestSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(5L, report.DownloadSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(1, report.ConnectionCount, $"Chunk size {chunkSize}");
            Assert.AreEqual(200, report.ProxyConnectResponseCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(local, report.LocalEndPoint, $"Chunk size {chunkSize}");
            Assert.IsNull(report.RemoteEndPoint, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_SeveralContentTypes_ReportsTheLast()
    {
        TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.0 200 OK\r\ncontent-type: a\r\nCONTENT-TYPE: b\r\n\r\n", 65536)))
            .ExecuteAsync(Context("http://example.com/", new MemoryStream()));

        Assert.AreEqual("b", result.Report!.ContentType);
        Assert.AreEqual(HttpVersion.Version10, result.Report.HttpVersion);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoContentType_ReportsNone()
    {
        TransferResult result = await Handler(QueueConnector.For(Connection(NoContent, 65536)))
            .ExecuteAsync(Context("http://example.com/", new MemoryStream()));

        Assert.IsNull(result.Report!.ContentType);
        Assert.AreEqual(204, result.Report.ResponseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_CustomMethod_IsSentAndReported()
    {
        const string request = "DELETE / HTTP/1.1\r\nHost: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n";
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://example.com/"),
            Output = new MemoryStream(),
            Http = new HttpRequestOptions { CustomMethod = "DELETE" },
        };

        TransferResult result = await Handler(QueueConnector.For(Connection(NoContent, 65536, request))).ExecuteAsync(context);

        Assert.AreEqual("DELETE", result.Report!.Method);
    }

    [TestMethod]
    public async Task ExecuteAsync_HttpOptionsWithoutMethod_SendsAndReportsGet()
    {
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://example.com/"),
            Output = new MemoryStream(),
            Http = new HttpRequestOptions(),
        };

        TransferResult result = await Handler(QueueConnector.For(Connection(NoContent, 65536, RootRequest))).ExecuteAsync(context);

        Assert.AreEqual("GET", result.Report!.Method);
    }

    [TestMethod]
    public async Task ExecuteAsync_PeerClosesBeforeAHead_FailsWithExit52AndAReportWithoutAResponse()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = Connection(string.Empty, chunkSize);

            TransferResult result = await Handler(QueueConnector.For(connection))
                .ExecuteAsync(Context("http://example.com/", new MemoryStream()));

            Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("Empty reply from server", result.ErrorMessage, $"Chunk size {chunkSize}");
            Assert.AreEqual(0, result.Report!.ResponseCode, $"Chunk size {chunkSize}");
            Assert.IsNull(result.Report.HttpVersion, $"Chunk size {chunkSize}");
            Assert.AreEqual(0L, result.Report.HeaderSize, $"Chunk size {chunkSize}");
            Assert.IsEmpty(result.Report.ResponseHeaders, $"Chunk size {chunkSize}");
            Assert.AreEqual((long)RootRequest.Length, result.Report.RequestSize, $"Chunk size {chunkSize}");
            Assert.IsTrue(connection.IsDisposed, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_BodyCutShort_FailsWithExit18AndReportsTheBytesWritten()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();

            TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 200 OK\r\nContent-Length: 10\r\n\r\nhel", chunkSize)))
                .ExecuteAsync(Context("http://example.com/", output));

            Assert.AreEqual(CurlExitCode.PartialFile, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("end of response with 7 bytes missing", result.ErrorMessage, $"Chunk size {chunkSize}");
            Assert.AreEqual(3L, result.BytesTransferred, $"Chunk size {chunkSize}");
            Assert.AreEqual(200, result.Report!.ResponseCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(3L, result.Report.DownloadSize, $"Chunk size {chunkSize}");
            Assert.AreEqual("hel", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_HeaderOutputThrowsIOException_FailsWithExit23BeforeTheBody()
    {
        FailingWriteStream headerOutput = new(1, new IOException("Disk full."));
        MemoryStream output = new();

        TransferResult result = await Handler(QueueConnector.For(Connection(Head + "hello", 65536)))
            .ExecuteAsync(Context("http://example.com/", output, headerOutput));

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual($"Failure writing output to destination, passed {FoldedHead.Length} returned 0", result.ErrorMessage);
        Assert.AreEqual(0L, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeaderOutputRefusesPartOfTheTrailers_FailsWithExit23()
    {
        const string response = "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n2\r\nhi\r\n0\r\nX-Trailer: t\r\n\r\n";
        FailingWriteStream headerOutput = new(2, new OutputWriteFailedException(4, "Refused."));

        TransferResult result = await Handler(QueueConnector.For(Connection(response, 65536)))
            .ExecuteAsync(Context("http://example.com/", new MemoryStream(), headerOutput));

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual("Failure writing output to destination, passed 14 returned 4", result.ErrorMessage);
        Assert.AreEqual(2L, result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_Cancelled_Throws()
    {
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://example.com/"),
            Output = new MemoryStream(),
            CancellationToken = cancellation.Token,
        };

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await Handler(QueueConnector.For(Connection(Head, 1))).ExecuteAsync(context));
    }

    [TestMethod]
    public async Task ExecuteAsync_FormBody_SendsTheMeasuredPostAndReportsItsSize()
    {
        // curl -d x=1 http://127.0.0.1:18081/ (BL-175 Notes); -w reported size_request 151, size_upload 3.
        const string request = "POST / HTTP/1.1\r\nHost: 127.0.0.1:18081\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "Content-Length: 3\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\nx=1";
        foreach (int chunkSize in ChunkSizes)
        {
            HttpRequestOptions options = new() { Body = new BytesBody("x=1"u8.ToArray(), "application/x-www-form-urlencoded") };

            TransferResult result = await Handler(QueueConnector.For(Connection(NoContent, chunkSize, request)))
                .ExecuteAsync(BodyContext("http://127.0.0.1:18081/", options));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("POST", result.Report!.Method, $"Chunk size {chunkSize}");
            Assert.AreEqual(151L, result.Report.RequestSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(3L, result.Report.UploadSize, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_JsonBody_SendsTheMeasuredContentTypeAndAccept()
    {
        // curl --json {"a":1} http://127.0.0.1:18081/ (BL-175 Notes).
        const string request = "POST / HTTP/1.1\r\nHost: 127.0.0.1:18081\r\nUser-Agent: curl/8.21.0\r\n"
            + "Content-Type: application/json\r\nAccept: application/json\r\nContent-Length: 7\r\n\r\n{\"a\":1}";
        HttpRequestOptions options = new()
        {
            Headers = ["Content-Type: application/json", "Accept: application/json"],
            Body = new BytesBody("{\"a\":1}"u8.ToArray(), "application/json"),
        };

        TransferResult result = await Handler(QueueConnector.For(Connection(NoContent, 65536, request)))
            .ExecuteAsync(BodyContext("http://127.0.0.1:18081/", options));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_BodyWithCustomMethod_ReportsThatMethod()
    {
        // curl -X PUT -d x=1: -w reported method PUT, size_request 150.
        HttpRequestOptions options = new() { CustomMethod = "PUT", Body = new BytesBody("x=1"u8.ToArray(), "application/x-www-form-urlencoded") };

        TransferResult result = await Handler(QueueConnector.For(Connection(NoContent, 65536)))
            .ExecuteAsync(BodyContext("http://127.0.0.1:18081/", options));

        Assert.AreEqual("PUT", result.Report!.Method);
        Assert.AreEqual(150L, result.Report.RequestSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_BodyAboveOneMebibyteAndNoReply_SendsItAfterTheMeasuredOneSecondWait()
    {
        // curl --data-binary @b with 1048577 bytes: Expect: 100-continue, body sent 1.005 s later; size_request 1048753.
        const string head = "POST / HTTP/1.1\r\nHost: 127.0.0.1:18081\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "Content-Length: 1048577\r\nContent-Type: application/x-www-form-urlencoded\r\nExpect: 100-continue\r\n\r\n";
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        GatedConnection connection = new(Encoding.Latin1.GetBytes(NoContent), 65536, head.Length + 1048577);
        HttpRequestOptions options = new() { Body = new BytesBody(new byte[1048577], "application/x-www-form-urlencoded") };

        Task<TransferResult> transfer = Handler(QueueConnector.For(connection))
            .ExecuteAsync(BodyContext("http://127.0.0.1:18081/", options, time)).AsTask();
        await time.TimerCreatedAsync(HttpContinueWaitConnection.ContinueWait);
        time.Advance(TimeSpan.FromMilliseconds(999));
        Assert.AreEqual(head, Latin1(connection.Written), "The body was sent before one second.");
        time.Advance(TimeSpan.FromMilliseconds(1));
        TransferResult result = await transfer;

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.HasCount(head.Length + 1048577, connection.Written);
        Assert.AreEqual(1048753L, result.Report!.RequestSize);
        Assert.AreEqual(1048577L, result.Report.UploadSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_ContinueArrives_SendsTheBodyAndWritesBothHeads()
    {
        const string response = "HTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok";
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            MemoryStream headers = new();
            GatedConnection connection = new(Encoding.Latin1.GetBytes(response), chunkSize, 0);
            HttpRequestOptions options = new() { Headers = ["Expect: 100-continue"], Body = new BytesBody("x=1"u8.ToArray(), "a/b") };
            TransferContext context = new()
            {
                Url = CurlUrl.Parse("http://127.0.0.1:18081/"),
                Output = output,
                HeaderOutput = headers,
                Http = options,
                TimeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch),
            };

            TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(context);

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.EndsWith("\r\n\r\nx=1", Latin1(connection.Written), $"Chunk size {chunkSize}");
            Assert.AreEqual("HTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\n", Latin1(headers.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual("ok", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_FinalStatusArrivesDuringTheWait_LeavesTheBodyUnsent()
    {
        // A server that answered 401 at once: curl sent no body byte and wrote the 401 body.
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            GatedConnection connection = new(Encoding.Latin1.GetBytes("HTTP/1.1 401 No\r\nContent-Length: 3\r\n\r\nno!"), chunkSize, 0);
            HttpRequestOptions options = new() { Body = new BytesBody(new byte[1048577], "application/x-www-form-urlencoded") };
            TransferContext context = new()
            {
                Url = CurlUrl.Parse("http://127.0.0.1:18081/"),
                Output = output,
                Http = options,
                TimeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch),
            };

            TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(context);

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.EndsWith("Expect: 100-continue\r\n\r\n", Latin1(connection.Written), $"Chunk size {chunkSize}");
            Assert.AreEqual("no!", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(0L, result.Report!.UploadSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(401, result.Report.ResponseCode, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_StreamBodyReadFails_FailsWithExit26AndTheMeasuredMessage()
    {
        // curl -F f=@locked: exit 26, "client mime read EOF fail, only 207/100207 of needed bytes read".
        FailingReadStream stream = new(new byte[207], 65536, new IOException("Lock violation."));
        HttpRequestOptions options = new() { Body = new StreamBody(stream, 100207, "multipart/form-data; boundary=b") };

        TransferResult result = await Handler(QueueConnector.For(Connection(NoContent, 65536)))
            .ExecuteAsync(BodyContext("http://127.0.0.1:18081/", options));

        Assert.AreEqual(CurlExitCode.ReadError, result.ExitCode);
        Assert.AreEqual("client mime read EOF fail, only 207/100207 of needed bytes read", result.ErrorMessage);
        Assert.AreEqual(207L, result.Report!.UploadSize);
        Assert.AreEqual(0, result.Report.ResponseCode);
    }

    [TestMethod]
    [DataRow(null, "HEAD", DisplayName = "-I")]
    [DataRow("GET", "GET", DisplayName = "-I -X GET")]
    public async Task ExecuteAsync_NoBody_SendsTheMeasuredRequestAndWritesOnlyTheHead(string? customMethod, string method)
    {
        const string head = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\n";
        string request = method + " /a?b HTTP/1.1\r\nHost: 127.0.0.1:18276\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = Connection(head + "hello", chunkSize, request);
            MemoryStream output = new();
            MemoryStream headerOutput = new();

            TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(
                FailContext("http://127.0.0.1:18276/a?b", HttpFailMode.None, output, headerOutput, customMethod, noBody: true));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(0L, result.BytesTransferred, $"Chunk size {chunkSize}");
            Assert.AreEqual(head, Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(0L, output.Length, $"Chunk size {chunkSize}");
            Assert.AreEqual(method, result.Report!.Method, $"Chunk size {chunkSize}");
            Assert.AreEqual(0L, result.Report.DownloadSize, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow(400, DisplayName = "400")]
    [DataRow(401, DisplayName = "401")]
    [DataRow(404, DisplayName = "404")]
    [DataRow(407, DisplayName = "407")]
    [DataRow(500, DisplayName = "500")]
    [DataRow(599, DisplayName = "599")]
    public async Task ExecuteAsync_FailAtOrAbove400_Returns22AndWritesTheHeadButNoBody(int status)
    {
        string head = $"HTTP/1.1 {status} X\r\nContent-Length: 5\r\n\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            MemoryStream headerOutput = new();

            TransferResult result = await Handler(QueueConnector.For(Connection(head + "nope!", chunkSize, RootRequest)))
                .ExecuteAsync(FailContext("http://example.com/", HttpFailMode.Fail, output, headerOutput));

            Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual($"The requested URL returned error: {status}", result.ErrorMessage, $"Chunk size {chunkSize}");
            Assert.AreEqual(0L, result.BytesTransferred, $"Chunk size {chunkSize}");
            Assert.AreEqual(head, Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(0L, output.Length, $"Chunk size {chunkSize}");
            Assert.AreEqual(status, result.Report!.ResponseCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(0L, result.Report.DownloadSize, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow(HttpFailMode.Fail, DisplayName = "-f")]
    [DataRow(HttpFailMode.FailWithBody, DisplayName = "--fail-with-body")]
    public async Task ExecuteAsync_FailBelow400_WritesTheBodyAndSucceeds(HttpFailMode fail)
    {
        MemoryStream output = new();

        TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 399 X\r\nContent-Length: 5\r\n\r\nnope!", 65536)))
            .ExecuteAsync(FailContext("http://example.com/", fail, output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("nope!", Latin1(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_FailWithCredentialsOn401_Returns22()
    {
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://example.com/"),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("a", "b"),
            Http = new HttpRequestOptions { Fail = HttpFailMode.Fail },
        };

        TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 401 Unauthorized\r\nContent-Length: 5\r\n\r\nnope!", 65536)))
            .ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("The requested URL returned error: 401", result.ErrorMessage);
        Assert.AreEqual(0L, context.Output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailWithBodyOn404_WritesTheBodyThenReturns22()
    {
        const string head = "HTTP/1.1 404 Not Found\r\nContent-Length: 5\r\n\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            MemoryStream headerOutput = new();

            TransferResult result = await Handler(QueueConnector.For(Connection(head + "nope!", chunkSize, RootRequest)))
                .ExecuteAsync(FailContext("http://example.com/", HttpFailMode.FailWithBody, output, headerOutput));

            Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("The requested URL returned error: 404", result.ErrorMessage, $"Chunk size {chunkSize}");
            Assert.AreEqual(5L, result.BytesTransferred, $"Chunk size {chunkSize}");
            Assert.AreEqual(head, Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual("nope!", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(5L, result.Report!.DownloadSize, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow(HttpFailMode.Fail, DisplayName = "-I -f")]
    [DataRow(HttpFailMode.FailWithBody, DisplayName = "-I --fail-with-body")]
    public async Task ExecuteAsync_NoBodyAndFailOn404_WritesTheHeadAndReturns22(HttpFailMode fail)
    {
        const string head = "HTTP/1.1 404 Not Found\r\nContent-Length: 5\r\n\r\n";
        MemoryStream output = new();
        MemoryStream headerOutput = new();

        TransferResult result = await Handler(QueueConnector.For(Connection(head, 65536)))
            .ExecuteAsync(FailContext("http://example.com/", fail, output, headerOutput, noBody: true));

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual(head, Latin1(headerOutput.ToArray()));
        Assert.AreEqual(0L, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_RedirectWithoutFollowing_ReportsTheTargetAndWritesTheBody()
    {
        const string head = "HTTP/1.1 302 Found\r\nLocation: ../next?x=1\r\nContent-Length: 5\r\n\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            MemoryStream headerOutput = new();

            TransferResult result = await Handler(QueueConnector.For(Connection(head + "moved", chunkSize)))
                .ExecuteAsync(Context("http://example.com/a/b/c", output, headerOutput));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("http://example.com/a/next?x=1", result.Report!.RedirectUrl, $"Chunk size {chunkSize}");
            Assert.AreEqual(head, Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual("moved", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow(
        "HTTP/1.1 301 Moved Permanently\r\nLocation: //other.example/x\r\nContent-Length: 5\r\n\r\n",
        "moved",
        "",
        DisplayName = "Content-Length body")]
    [DataRow(
        "HTTP/1.1 301 Moved Permanently\r\nLocation: //other.example/x\r\nTransfer-Encoding: chunked\r\n\r\n",
        "5\r\nmoved\r\n0\r\nX-Trailer: t\r\n\r\n",
        "X-Trailer: t\r\n",
        DisplayName = "chunked body with a trailer")]
    public async Task ExecuteAsync_RedirectWhileFollowing_DrainsTheBodyAndStillWritesTheHeaders(string head, string body, string trailers)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            MemoryStream headerOutput = new();
            ScriptedConnection connection = Connection(head + body, chunkSize);

            TransferResult result = await Handler(QueueConnector.For(connection))
                .ExecuteAsync(FollowContext("http://example.com/", output, headerOutput));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("http://other.example/x", result.Report!.RedirectUrl, $"Chunk size {chunkSize}");
            Assert.AreEqual(head + trailers, Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(0L, output.Length, $"Chunk size {chunkSize}");
            Assert.AreEqual(5L, result.Report.DownloadSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(0, await connection.ReadAsync(new byte[1], CancellationToken.None), $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured with curl 8.21.0 <c>-L --max-redirs 1 --compressed</c> against a 302 with
    /// <c>Content-Encoding: foo</c> and body <c>AB</c> (BL-177 Notes): curl follows it and
    /// ends with exit 47, not 61, so the discarded body is not decoded.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_CompressedWhileFollowing_DoesNotDecodeTheDiscardedBody()
    {
        MemoryStream output = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://example.com/"),
            Output = output,
            Http = new HttpRequestOptions { FollowRedirects = true, Compressed = true },
        };

        TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 302 Found\r\nLocation: /b\r\nContent-Encoding: foo\r\nContent-Length: 2\r\n\r\nAB", 65536)))
            .ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("http://example.com/b", result.Report!.RedirectUrl);
        Assert.AreEqual(0L, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_3xxWithoutLocationWhileFollowing_WritesTheBodyAndReportsNoTarget()
    {
        MemoryStream output = new();

        TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 300 Multiple Choices\r\nContent-Length: 5\r\n\r\nlist!", 65536)))
            .ExecuteAsync(FollowContext("http://example.com/", output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.Report!.RedirectUrl);
        Assert.AreEqual("list!", Latin1(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_LocationOn200WhileFollowing_WritesTheBodyAndReportsNoTarget()
    {
        MemoryStream output = new();

        TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 200 OK\r\nLocation: /x\r\nContent-Length: 5\r\n\r\nhello", 65536)))
            .ExecuteAsync(FollowContext("http://example.com/", output));

        Assert.IsNull(result.Report!.RedirectUrl);
        Assert.AreEqual("hello", Latin1(output.ToArray()));
    }

    /// <summary>
    /// Measured with curl 8.21.0 <c>-s -S --compressed</c> and <c>--raw --compressed</c>
    /// (BL-177 Notes): both send Accept-Encoding; <c>--compressed</c> writes <c>hello</c>, and
    /// <c>--raw</c> writes the 25 gzip bytes untouched. Either way the download size is 25.
    /// </summary>
    [TestMethod]
    [DataRow(false, DisplayName = "--compressed decodes")]
    [DataRow(true, DisplayName = "--raw --compressed does not")]
    public async Task ExecuteAsync_Compressed_SendsAcceptEncodingAndDecodesUnlessRaw(bool raw)
    {
        byte[] gzip = HttpContentDecoderTests.Bytes(HttpContentDecoderTests.Gzip);
        string response = "HTTP/1.1 200 OK\r\nContent-Encoding: gzip\r\nContent-Length: 25\r\n\r\n" + Latin1(gzip);
        string request = "GET / HTTP/1.1\r\nHost: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nAccept-Encoding: deflate, gzip, br\r\n\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            TransferContext context = new()
            {
                Url = CurlUrl.Parse("http://example.com/"),
                Output = output,
                Http = new HttpRequestOptions { Compressed = true, Raw = raw },
            };

            TransferResult result = await Handler(QueueConnector.For(Connection(response, chunkSize, request))).ExecuteAsync(context);

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(raw ? gzip : "hello"u8.ToArray(), output.ToArray(), $"Chunk size {chunkSize}");
            Assert.AreEqual(25L, result.Report!.DownloadSize, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: a gzip body starting <c>00 01 02</c> gives
    /// <c>curl: (61) Error while processing content unencoding: incorrect header check</c>
    /// and writes nothing.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_CompressedAndCorruptGzip_ReturnsExit61()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            TransferContext context = new()
            {
                Url = CurlUrl.Parse("http://example.com/"),
                Output = output,
                Http = new HttpRequestOptions { Compressed = true },
            };
            string response = "HTTP/1.1 200 OK\r\nContent-Encoding: gzip\r\nContent-Length: 12\r\n\r\n"
                + Latin1(HttpContentDecoderTests.Bytes("000102030405060708090A0B"));

            TransferResult result = await Handler(QueueConnector.For(Connection(response, chunkSize))).ExecuteAsync(context);

            Assert.AreEqual(CurlExitCode.BadContentEncoding, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("Error while processing content unencoding: incorrect header check", result.ErrorMessage, $"Chunk size {chunkSize}");
            Assert.AreEqual(0L, output.Length, $"Chunk size {chunkSize}");
        }
    }

    private static TransferContext FollowContext(string url, Stream output, Stream? headerOutput = null) =>
        new()
        {
            Url = CurlUrl.Parse(url),
            Output = output,
            HeaderOutput = headerOutput,
            Http = new HttpRequestOptions { FollowRedirects = true },
        };

    private static TransferContext FailContext(
        string url,
        HttpFailMode fail,
        Stream output,
        Stream? headerOutput = null,
        string? customMethod = null,
        bool noBody = false) =>
        new()
        {
            Url = CurlUrl.Parse(url),
            Output = output,
            HeaderOutput = headerOutput,
            NoBody = noBody,
            Http = new HttpRequestOptions { Fail = fail, CustomMethod = customMethod },
        };

    private static TransferContext BodyContext(string url, HttpRequestOptions options, TimeProvider? timeProvider = null) =>
        new()
        {
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
            Http = options,
            TimeProvider = timeProvider ?? new FakeTimeProvider(DateTimeOffset.UnixEpoch),
        };

    private static HttpProtocolHandler Handler(QueueConnector connector) => new(connector, new SilentAuthenticator());

    private static TransferContext Context(string url, Stream output, Stream? headerOutput = null) =>
        new() { Url = CurlUrl.Parse(url), Output = output, HeaderOutput = headerOutput };

    private static ScriptedConnection Connection(string response, int chunkSize, string? expectedRequest = null) =>
        new(Encoding.Latin1.GetBytes(response), chunkSize, expectedRequest is null ? null : Encoding.Latin1.GetBytes(expectedRequest));

    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);
}
