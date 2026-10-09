using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using Curl.Testing;
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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void SupportedSchemes_AreHttpAndHttps()
    {
        HttpProtocolHandler handler = Handler(new QueueConnector());
        Diagnostics.Arrange("handler", "HttpProtocolHandler with an empty QueueConnector");

        string[] schemes = handler.SupportedSchemes.ToArray();

        Diagnostics.Act("supported schemes", string.Join(", ", schemes));
        Diagnostics.Assert("supported schemes", "http, https", string.Join(", ", schemes));
        CollectionAssert.AreEqual(new[] { "http", "https" }, handler.SupportedSchemes.ToArray());
    }

    [TestMethod]
    public void Constructor_KeepsTheAuthenticatorAndCookieStore()
    {
        SilentAuthenticator authenticator = new();
        Diagnostics.Arrange("authenticator, cookie store", "SilentAuthenticator, (none)");

        HttpProtocolHandler handler = new(new QueueConnector(), authenticator);

        Diagnostics.Act("authenticator kept, cookie store", $"{ReferenceEquals(authenticator, handler.Authenticator)}, {handler.CookieStore?.ToString() ?? "(null)"}");
        Diagnostics.Assert("same authenticator", true, ReferenceEquals(authenticator, handler.Authenticator));
        Diagnostics.Assert("cookie store", "(null)", handler.CookieStore?.ToString() ?? "(null)");
        Assert.AreSame(authenticator, handler.Authenticator);
        Assert.IsNull(handler.CookieStore);
    }

    [TestMethod]
    public void Constructor_NullConnector_Throws()
    {
        Diagnostics.Arrange("connector, authenticator", "(null), SilentAuthenticator");

        ArgumentNullException thrown = Assert.ThrowsExactly<ArgumentNullException>(() => new HttpProtocolHandler(null!, new SilentAuthenticator()));

        Diagnostics.Act("thrown", $"{thrown.GetType().Name} for {thrown.ParamName}");
        Diagnostics.Assert("exception type", nameof(ArgumentNullException), thrown.GetType().Name);
    }

    [TestMethod]
    public void Constructor_NullAuthenticator_Throws()
    {
        Diagnostics.Arrange("connector, authenticator", "QueueConnector, (null)");

        ArgumentNullException thrown = Assert.ThrowsExactly<ArgumentNullException>(() => new HttpProtocolHandler(new QueueConnector(), null!));

        Diagnostics.Act("thrown", $"{thrown.GetType().Name} for {thrown.ParamName}");
        Diagnostics.Assert("exception type", nameof(ArgumentNullException), thrown.GetType().Name);
    }

    [TestMethod]
    public async Task ExecuteAsync_NullContext_Throws()
    {
        HttpProtocolHandler handler = Handler(new QueueConnector());
        Diagnostics.Arrange("context", "(null)");

        ArgumentNullException thrown = await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await handler.ExecuteAsync(null!));

        Diagnostics.Act("thrown", $"{thrown.GetType().Name} for {thrown.ParamName}");
        Diagnostics.Assert("exception type", nameof(ArgumentNullException), thrown.GetType().Name);
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
        Diagnostics.Arrange("url", url);
        Diagnostics.Arrange("scripted response", "204 No Content");

        TransferResult result = await Handler(connector).ExecuteAsync(Context(url, new MemoryStream()));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("connect target", $"{host}:{port} tls {useTls}", $"{connector.Targets.Single().Host}:{connector.Targets.Single().Port} tls {connector.Targets.Single().UseTls}");
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(new ConnectTarget(host, port, useTls) { PoolScheme = useTls ? "https" : "http" }, connector.Targets.Single());
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
        Diagnostics.Arrange("url", "https://example.com/");
        Diagnostics.Arrange("connect failure", $"{exitCode} ({(int)exitCode}): {message}");

        TransferResult result = await Handler(new QueueConnector(ConnectResult.Failed(exitCode, message)))
            .ExecuteAsync(Context("https://example.com/", output, headerOutput));

        WriteResult(result);
        Diagnostics.Assert("exit code", exitCode, result.ExitCode);
        Diagnostics.Assert("error text", message, result.ErrorMessage);
        Diagnostics.Assert("bytes transferred, output, header output", "0, 0, 0", $"{result.BytesTransferred}, {output.Length}, {headerOutput.Length}");
        Diagnostics.Assert("used proxy, refused", "False, False", $"{result.Report!.UsedProxy}, {result.IsConnectionRefused}");
        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
        Assert.AreEqual(0L, result.BytesTransferred);
        Assert.AreEqual(0L, output.Length);
        Assert.AreEqual(0L, headerOutput.Length);
        Assert.IsFalse(result.Report!.UsedProxy);
        Assert.IsFalse(result.IsConnectionRefused);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectRefused_ReturnsCouldntConnectMarkedRefused()
    {
        const string message = "Failed to connect to 127.0.0.1:1 after 0 ms: Could not connect to server";
        Diagnostics.Arrange("url, connect result", $"http://127.0.0.1:1/, refused: {message}");

        TransferResult result = await Handler(new QueueConnector(ConnectResult.Refused(message)))
            .ExecuteAsync(Context("http://127.0.0.1:1/", new MemoryStream()));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Diagnostics.Assert("error text", message, result.ErrorMessage);
        Diagnostics.Assert("refused", true, result.IsConnectionRefused);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    public async Task ExecuteAsync_Exchange_SendsTheRequestAndWritesHeadersThenBody()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = Connection(Head + "hello", chunkSize, PathRequest);
            MemoryStream output = new();
            MemoryStream headerOutput = new();
            Diagnostics.Arrange("chunk size, url", $"{chunkSize}, http://example.com/path?q=1");
            Diagnostics.Arrange("expected request", OneLine(PathRequest));
            Diagnostics.Arrange("scripted response", OneLine(Head + "hello"));

            TransferResult result = await Handler(QueueConnector.For(connection))
                .ExecuteAsync(Context("http://example.com/path?q=1", output, headerOutput));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Diagnostics.Assert("bytes transferred", 5L, result.BytesTransferred);
            Diagnostics.Assert("header output", OneLine(FoldedHead), OneLine(Latin1(headerOutput.ToArray())));
            Diagnostics.Assert("body", "hello", Latin1(output.ToArray()));
            Diagnostics.Assert("connection disposed", true, connection.IsDisposed);
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
            Diagnostics.Arrange("chunk size, header output", $"{chunkSize}, the output stream");
            Diagnostics.Arrange("scripted response", OneLine(Head + "hello"));

            TransferResult result = await Handler(QueueConnector.For(Connection(Head + "hello", chunkSize)))
                .ExecuteAsync(Context("http://example.com/", output, output));

            WriteResult(result);
            Diagnostics.Assert("output", OneLine(FoldedHead + "hello"), OneLine(Latin1(output.ToArray())));
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
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("scripted response", "100 Continue, then 200 chunked: hello, trailer X-Trailer: t");

            TransferResult result = await Handler(QueueConnector.For(Connection(informational + final + "5\r\nhello\r\n0\r\nX-Trailer: t\r\n\r\n", chunkSize)))
                .ExecuteAsync(Context("http://example.com/", output, headerOutput));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Diagnostics.Assert("header output", OneLine(informational + final + "X-Trailer: t\r\n"), OneLine(Latin1(headerOutput.ToArray())));
            Diagnostics.Assert("body", "hello", Latin1(output.ToArray()));
            Diagnostics.Assert("header size", (long)(informational + final).Length, result.Report!.HeaderSize);
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
        Diagnostics.Arrange("header output, scripted response", $"(none), {OneLine(Head + "hello")}");

        TransferResult result = await Handler(QueueConnector.For(Connection(Head + "hello", 65536)))
            .ExecuteAsync(Context("http://example.com/", output));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("output", "hello", Latin1(output.ToArray()));
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
            Diagnostics.Arrange("chunk size, local end point, proxy connect code", $"{chunkSize}, {local}, 200");
            Diagnostics.Arrange("scripted response", OneLine(Head + "hello"));

            TransferResult result = await Handler(connector).ExecuteAsync(Context("http://example.com/path?q=1", new MemoryStream()));

            TransferReport report = result.Report!;
            WriteResult(result);
            Diagnostics.Assert("status, version, method", "200, 1.1, GET", $"{report.ResponseCode}, {report.HttpVersion}, {report.Method}");
            Diagnostics.Assert("headers", "Content-Type: text/plain | X-Folded: a b | Content-Length: 5", string.Join(" | ", report.ResponseHeaders.Select(header => $"{header.Key}: {header.Value}")));
            Diagnostics.Assert("header size, request size, download size", $"{FoldedHead.Length}, {PathRequest.Length}, 5", $"{report.HeaderSize}, {report.RequestSize}, {report.DownloadSize}");
            Diagnostics.Assert("local end point", local, report.LocalEndPoint);
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
    public async Task ExecuteAsync_ConnectWithoutLocalEndPoint_ReportsTheConnectionsOwn()
    {
        IPEndPoint local = new(IPAddress.Loopback, 49152);
        ScriptedConnection connection = new(Encoding.Latin1.GetBytes(Head + "hello"), 4096, null) { LocalEndPoint = local };
        QueueConnector connector = new(ConnectResult.Connected(connection));
        Diagnostics.Arrange("connection local end point, connect local end point, remote end point", $"{local}, none, none");

        TransferResult result = await Handler(connector).ExecuteAsync(Context("http://example.com/path?q=1", new MemoryStream()));

        WriteResult(result);
        Diagnostics.Assert("local end point", local, result.Report!.LocalEndPoint);
        Assert.AreEqual(local, result.Report.LocalEndPoint);
        Assert.IsNull(result.Report.RemoteEndPoint);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectWithPeerCertificates_ReportsTheSameChainInOrder()
    {
        ReadOnlyMemory<byte>[] chain = [new byte[] { 0x30, 0x01 }, new byte[] { 0x30, 0x02 }];
        QueueConnector connector = new(ConnectResult.Connected(Connection(Head + "hello", 65536), null, peerCertificates: chain));
        Diagnostics.Arrange("peer certificates", "30 01 | 30 02");

        TransferResult result = await Handler(connector).ExecuteAsync(Context("https://example.com/", new MemoryStream()));

        IReadOnlyList<ReadOnlyMemory<byte>> reported = result.Report!.PeerCertificates;
        WriteResult(result);
        Diagnostics.Assert("reported certificates", "30 01 | 30 02", string.Join(" | ", reported.Select(certificate => Convert.ToHexString(certificate.Span).Insert(2, " "))));
        Assert.AreEqual(2, reported.Count);
        CollectionAssert.AreEqual(chain[0].ToArray(), reported[0].ToArray());
        CollectionAssert.AreEqual(chain[1].ToArray(), reported[1].ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_PlainHttp_ReportsNoPeerCertificates()
    {
        Diagnostics.Arrange("url, peer certificates", "http://example.com/, (none)");

        TransferResult result = await Handler(QueueConnector.For(Connection(Head + "hello", 65536)))
            .ExecuteAsync(Context("http://example.com/", new MemoryStream()));

        WriteResult(result);
        Diagnostics.Assert("reported certificate count", 0, result.Report!.PeerCertificates.Count);
        Assert.AreEqual(0, result.Report!.PeerCertificates.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_SeveralContentTypes_ReportsTheLast()
    {
        Diagnostics.Arrange("scripted response", "HTTP/1.0 200, content-type: a, CONTENT-TYPE: b");

        TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.0 200 OK\r\ncontent-type: a\r\nCONTENT-TYPE: b\r\n\r\n", 65536)))
            .ExecuteAsync(Context("http://example.com/", new MemoryStream()));

        WriteResult(result);
        Diagnostics.Assert("content type", "b", result.Report!.ContentType);
        Diagnostics.Assert("version", HttpVersion.Version10, result.Report.HttpVersion);
        Assert.AreEqual("b", result.Report!.ContentType);
        Assert.AreEqual(HttpVersion.Version10, result.Report.HttpVersion);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoContentType_ReportsNone()
    {
        Diagnostics.Arrange("scripted response", "204 No Content, no Content-Type");

        TransferResult result = await Handler(QueueConnector.For(Connection(NoContent, 65536)))
            .ExecuteAsync(Context("http://example.com/", new MemoryStream()));

        WriteResult(result);
        Diagnostics.Assert("content type", "(null)", result.Report!.ContentType ?? "(null)");
        Diagnostics.Assert("status", 204, result.Report.ResponseCode);
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
        Diagnostics.Arrange("custom method", "DELETE");
        Diagnostics.Arrange("expected request", OneLine(request));

        TransferResult result = await Handler(QueueConnector.For(Connection(NoContent, 65536, request))).ExecuteAsync(context);

        WriteResult(result);
        Diagnostics.Assert("method", "DELETE", result.Report!.Method);
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
        Diagnostics.Arrange("custom method", "(none)");
        Diagnostics.Arrange("expected request", OneLine(RootRequest));

        TransferResult result = await Handler(QueueConnector.For(Connection(NoContent, 65536, RootRequest))).ExecuteAsync(context);

        WriteResult(result);
        Diagnostics.Assert("method", "GET", result.Report!.Method);
        Assert.AreEqual("GET", result.Report!.Method);
    }

    [TestMethod]
    public async Task ExecuteAsync_PeerClosesBeforeAHead_FailsWithExit52AndAReportWithoutAResponse()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = Connection(string.Empty, chunkSize);
            Diagnostics.Arrange("chunk size, scripted response", $"{chunkSize}, (empty: peer closes)");

            TransferResult result = await Handler(QueueConnector.For(connection))
                .ExecuteAsync(Context("http://example.com/", new MemoryStream()));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.GotNothing, result.ExitCode);
            Diagnostics.Assert("error text", "Empty reply from server", result.ErrorMessage);
            Diagnostics.Assert("status, header size, header count", "0, 0, 0", $"{result.Report!.ResponseCode}, {result.Report.HeaderSize}, {result.Report.ResponseHeaders.Count}");
            Diagnostics.Assert("request size", (long)RootRequest.Length, result.Report.RequestSize);
            Diagnostics.Assert("connection disposed", true, connection.IsDisposed);
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
            Diagnostics.Arrange("chunk size, scripted response", $"{chunkSize}, 200 Content-Length: 10, body hel then close");

            TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 200 OK\r\nContent-Length: 10\r\n\r\nhel", chunkSize)))
                .ExecuteAsync(Context("http://example.com/", output));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.PartialFile, result.ExitCode);
            Diagnostics.Assert("error text", "end of response with 7 bytes missing", result.ErrorMessage);
            Diagnostics.Assert("bytes transferred, status, download size", "3, 200, 3", $"{result.BytesTransferred}, {result.Report!.ResponseCode}, {result.Report.DownloadSize}");
            Diagnostics.Assert("body", "hel", Latin1(output.ToArray()));
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
        Diagnostics.Arrange("header output", "fails its first write with IOException: Disk full.");

        TransferResult result = await Handler(QueueConnector.For(Connection(Head + "hello", 65536)))
            .ExecuteAsync(Context("http://example.com/", output, headerOutput));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.WriteError, result.ExitCode);
        Diagnostics.Assert("error text", $"Failure writing output to destination, passed {FoldedHead.Length} returned 0", result.ErrorMessage);
        Diagnostics.Assert("output length", 0L, output.Length);
        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual($"Failure writing output to destination, passed {FoldedHead.Length} returned 0", result.ErrorMessage);
        Assert.AreEqual(0L, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeaderOutputRefusesPartOfTheTrailers_FailsWithExit23()
    {
        const string response = "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n2\r\nhi\r\n0\r\nX-Trailer: t\r\n\r\n";
        FailingWriteStream headerOutput = new(2, new OutputWriteFailedException(4, "Refused."));
        Diagnostics.Arrange("scripted response", OneLine(response));
        Diagnostics.Arrange("header output", "fails its second write after 4 bytes");

        TransferResult result = await Handler(QueueConnector.For(Connection(response, 65536)))
            .ExecuteAsync(Context("http://example.com/", new MemoryStream(), headerOutput));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.WriteError, result.ExitCode);
        Diagnostics.Assert("error text", "Failure writing output to destination, passed 14 returned 4", result.ErrorMessage);
        Diagnostics.Assert("bytes transferred", 2L, result.BytesTransferred);
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
        Diagnostics.Arrange("cancellation token", "already cancelled");

        OperationCanceledException thrown = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await Handler(QueueConnector.For(Connection(Head, 1))).ExecuteAsync(context));

        Diagnostics.Act("thrown", thrown.GetType().Name);
        Diagnostics.Assert("exception type", nameof(OperationCanceledException), thrown.GetType().Name);
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
            Diagnostics.Arrange("chunk size, body", $"{chunkSize}, x=1 as application/x-www-form-urlencoded");
            Diagnostics.Arrange("expected request", OneLine(request));

            TransferResult result = await Handler(QueueConnector.For(Connection(NoContent, chunkSize, request)))
                .ExecuteAsync(BodyContext("http://127.0.0.1:18081/", options));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Diagnostics.Assert("method, request size, upload size", "POST, 151, 3", $"{result.Report!.Method}, {result.Report.RequestSize}, {result.Report.UploadSize}");
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
        Diagnostics.Arrange("body", "{\"a\":1} as application/json");
        Diagnostics.Arrange("expected request", OneLine(request));

        TransferResult result = await Handler(QueueConnector.For(Connection(NoContent, 65536, request)))
            .ExecuteAsync(BodyContext("http://127.0.0.1:18081/", options));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_BodyWithCustomMethod_ReportsThatMethod()
    {
        // curl -X PUT -d x=1: -w reported method PUT, size_request 150.
        HttpRequestOptions options = new() { CustomMethod = "PUT", Body = new BytesBody("x=1"u8.ToArray(), "application/x-www-form-urlencoded") };
        Diagnostics.Arrange("custom method, body", "PUT, x=1");

        TransferResult result = await Handler(QueueConnector.For(Connection(NoContent, 65536)))
            .ExecuteAsync(BodyContext("http://127.0.0.1:18081/", options));

        WriteResult(result);
        Diagnostics.Assert("method, request size", "PUT, 150", $"{result.Report!.Method}, {result.Report.RequestSize}");
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
        Diagnostics.Arrange("body length, continue wait", $"1048577, {HttpRequestOptions.DefaultContinueWait.TotalMilliseconds} ms");

        Task<TransferResult> transfer = Handler(QueueConnector.For(connection))
            .ExecuteAsync(BodyContext("http://127.0.0.1:18081/", options, time)).AsTask();
        await time.TimerCreatedAsync(HttpRequestOptions.DefaultContinueWait);
        time.Advance(TimeSpan.FromMilliseconds(999));
        Diagnostics.Assert("written at 999 ms", head.Length, connection.Written.Length);
        Assert.AreEqual(head, Latin1(connection.Written), "The body was sent before one second.");
        time.Advance(TimeSpan.FromMilliseconds(1));
        TransferResult result = await transfer;

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("written", head.Length + 1048577, connection.Written.Length);
        Diagnostics.Assert("request size, upload size", "1048753, 1048577", $"{result.Report!.RequestSize}, {result.Report.UploadSize}");
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.HasCount(head.Length + 1048577, connection.Written);
        Assert.AreEqual(1048753L, result.Report!.RequestSize);
        Assert.AreEqual(1048577L, result.Report.UploadSize);
    }

    [TestMethod]
    [DataRow(200, DisplayName = "--expect100-timeout 0.2")]
    [DataRow(3000, DisplayName = "--expect100-timeout 3")]
    public async Task ExecuteAsync_ContinueWaitSetAndNoReply_SendsTheBodyWhenThatWaitHasPassed(int milliseconds)
    {
        // curl --data-binary @b (2,000,000 bytes) --expect100-timeout 0.2 and 3: "Done waiting
        // for 100-continue" and then the body 200 ms and 3 s after the head (BL-624 Notes).
        const string head = "POST / HTTP/1.1\r\nHost: 127.0.0.1:18081\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "Content-Length: 1048577\r\nContent-Type: application/x-www-form-urlencoded\r\nExpect: 100-continue\r\n\r\n";
        TimeSpan continueWait = TimeSpan.FromMilliseconds(milliseconds);
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        GatedConnection connection = new(Encoding.Latin1.GetBytes(NoContent), 65536, head.Length + 1048577);
        HttpRequestOptions options = new() { Body = new BytesBody(new byte[1048577], "application/x-www-form-urlencoded"), ContinueWait = continueWait };
        Diagnostics.Arrange("body length, continue wait", $"1048577, {milliseconds} ms");

        Task<TransferResult> transfer = Handler(QueueConnector.For(connection))
            .ExecuteAsync(BodyContext("http://127.0.0.1:18081/", options, time)).AsTask();
        await time.TimerCreatedAsync(continueWait);
        time.Advance(continueWait - TimeSpan.FromMilliseconds(1));
        Diagnostics.Assert("written 1 ms before the wait ends", head.Length, connection.Written.Length);
        Assert.AreEqual(head, Latin1(connection.Written), "The body was sent before the wait ran out.");
        time.Advance(TimeSpan.FromMilliseconds(1));
        TransferResult result = await transfer;

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("written", head.Length + 1048577, connection.Written.Length);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.HasCount(head.Length + 1048577, connection.Written);
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
            Diagnostics.Arrange("chunk size, header, body", $"{chunkSize}, Expect: 100-continue, x=1");
            Diagnostics.Arrange("scripted response", OneLine(response));

            TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(context);

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Diagnostics.Assert("request ends with body", true, Latin1(connection.Written).EndsWith("\r\n\r\nx=1", StringComparison.Ordinal));
            Diagnostics.Assert("header output", OneLine("HTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\n"), OneLine(Latin1(headers.ToArray())));
            Diagnostics.Assert("body", "ok", Latin1(output.ToArray()));
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
            Diagnostics.Arrange("chunk size, body length", $"{chunkSize}, 1048577");
            Diagnostics.Arrange("scripted response", "401 No, Content-Length: 3, body no!");

            TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(context);

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Diagnostics.Assert("request ends with the head", true, Latin1(connection.Written).EndsWith("Expect: 100-continue\r\n\r\n", StringComparison.Ordinal));
            Diagnostics.Assert("body", "no!", Latin1(output.ToArray()));
            Diagnostics.Assert("upload size, status", "0, 401", $"{result.Report!.UploadSize}, {result.Report.ResponseCode}");
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
        Diagnostics.Arrange("body", "stream of 100207 bytes that fails after 207 with IOException");

        TransferResult result = await Handler(QueueConnector.For(Connection(NoContent, 65536)))
            .ExecuteAsync(BodyContext("http://127.0.0.1:18081/", options));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.ReadError, result.ExitCode);
        Diagnostics.Assert("error text", "client mime read EOF fail, only 207/100207 of needed bytes read", result.ErrorMessage);
        Diagnostics.Assert("upload size, status", "207, 0", $"{result.Report!.UploadSize}, {result.Report.ResponseCode}");
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
            Diagnostics.Arrange("chunk size, custom method, no body", $"{chunkSize}, {customMethod ?? "(none)"}, True");
            Diagnostics.Arrange("expected request", OneLine(request));

            TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(
                FailContext("http://127.0.0.1:18276/a?b", HttpFailMode.None, output, headerOutput, customMethod, noBody: true));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Diagnostics.Assert("header output", OneLine(head), OneLine(Latin1(headerOutput.ToArray())));
            Diagnostics.Assert("bytes transferred, output, download size", "0, 0, 0", $"{result.BytesTransferred}, {output.Length}, {result.Report!.DownloadSize}");
            Diagnostics.Assert("method", method, result.Report.Method);
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
            Diagnostics.Arrange("chunk size, fail mode", $"{chunkSize}, Fail");
            Diagnostics.Arrange("scripted response", $"{status} X, Content-Length: 5, body nope!");

            TransferResult result = await Handler(QueueConnector.For(Connection(head + "nope!", chunkSize, RootRequest)))
                .ExecuteAsync(FailContext("http://example.com/", HttpFailMode.Fail, output, headerOutput));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.HttpReturnedError, result.ExitCode);
            Diagnostics.Assert("error text", $"The requested URL returned error: {status}", result.ErrorMessage);
            Diagnostics.Assert("header output", OneLine(head), OneLine(Latin1(headerOutput.ToArray())));
            Diagnostics.Assert("bytes transferred, output, download size", "0, 0, 0", $"{result.BytesTransferred}, {output.Length}, {result.Report!.DownloadSize}");
            Diagnostics.Assert("status", status, result.Report.ResponseCode);
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
        Diagnostics.Arrange("fail mode, scripted response", $"{fail}, 399 X with body nope!");

        TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 399 X\r\nContent-Length: 5\r\n\r\nnope!", 65536)))
            .ExecuteAsync(FailContext("http://example.com/", fail, output));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("body", "nope!", Latin1(output.ToArray()));
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
        Diagnostics.Arrange("fail mode, credentials", "Fail, a:(secret)");
        Diagnostics.Arrange("scripted response", "401 Unauthorized with body nope!");

        TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 401 Unauthorized\r\nContent-Length: 5\r\n\r\nnope!", 65536)))
            .ExecuteAsync(context);

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.HttpReturnedError, result.ExitCode);
        Diagnostics.Assert("error text", "The requested URL returned error: 401", result.ErrorMessage);
        Diagnostics.Assert("output length", 0L, context.Output.Length);
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
            Diagnostics.Arrange("chunk size, fail mode", $"{chunkSize}, FailWithBody");
            Diagnostics.Arrange("scripted response", "404 Not Found, Content-Length: 5, body nope!");

            TransferResult result = await Handler(QueueConnector.For(Connection(head + "nope!", chunkSize, RootRequest)))
                .ExecuteAsync(FailContext("http://example.com/", HttpFailMode.FailWithBody, output, headerOutput));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.HttpReturnedError, result.ExitCode);
            Diagnostics.Assert("error text", "The requested URL returned error: 404", result.ErrorMessage);
            Diagnostics.Assert("header output", OneLine(head), OneLine(Latin1(headerOutput.ToArray())));
            Diagnostics.Assert("body", "nope!", Latin1(output.ToArray()));
            Diagnostics.Assert("bytes transferred, download size", "5, 5", $"{result.BytesTransferred}, {result.Report!.DownloadSize}");
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
        Diagnostics.Arrange("fail mode, no body", $"{fail}, True");
        Diagnostics.Arrange("scripted response", OneLine(head));

        TransferResult result = await Handler(QueueConnector.For(Connection(head, 65536)))
            .ExecuteAsync(FailContext("http://example.com/", fail, output, headerOutput, noBody: true));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.HttpReturnedError, result.ExitCode);
        Diagnostics.Assert("header output", OneLine(head), OneLine(Latin1(headerOutput.ToArray())));
        Diagnostics.Assert("output length", 0L, output.Length);
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
            Diagnostics.Arrange("chunk size, url", $"{chunkSize}, http://example.com/a/b/c");
            Diagnostics.Arrange("scripted response", "302 Found, Location: ../next?x=1, body moved");

            TransferResult result = await Handler(QueueConnector.For(Connection(head + "moved", chunkSize)))
                .ExecuteAsync(Context("http://example.com/a/b/c", output, headerOutput));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Diagnostics.Assert("redirect url", "http://example.com/a/next?x=1", result.Report!.RedirectUrl);
            Diagnostics.Assert("header output", OneLine(head), OneLine(Latin1(headerOutput.ToArray())));
            Diagnostics.Assert("body", "moved", Latin1(output.ToArray()));
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
            Diagnostics.Arrange("chunk size, follow", $"{chunkSize}, True");
            Diagnostics.Arrange("scripted response", OneLine(head + body));

            TransferResult result = await Handler(QueueConnector.For(connection))
                .ExecuteAsync(FollowContext("http://example.com/", output, headerOutput));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Diagnostics.Assert("redirect url", "http://other.example/x", result.Report!.RedirectUrl);
            Diagnostics.Assert("header output", OneLine(head + trailers), OneLine(Latin1(headerOutput.ToArray())));
            Diagnostics.Assert("output, download size", "0, 5", $"{output.Length}, {result.Report.DownloadSize}");
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("http://other.example/x", result.Report!.RedirectUrl, $"Chunk size {chunkSize}");
            Assert.AreEqual(head + trailers, Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(0L, output.Length, $"Chunk size {chunkSize}");
            Assert.AreEqual(5L, result.Report.DownloadSize, $"Chunk size {chunkSize}");
            int bytesLeft = await connection.ReadAsync(new byte[1], CancellationToken.None);
            Diagnostics.Assert("bytes left on the connection", 0, bytesLeft);
            Assert.AreEqual(0, bytesLeft, $"Chunk size {chunkSize}");
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
        Diagnostics.Arrange("follow, compressed", "True, True");
        Diagnostics.Arrange("scripted response", "302 Found, Location: /b, Content-Encoding: foo, body AB");

        TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 302 Found\r\nLocation: /b\r\nContent-Encoding: foo\r\nContent-Length: 2\r\n\r\nAB", 65536)))
            .ExecuteAsync(context);

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("redirect url", "http://example.com/b", result.Report!.RedirectUrl);
        Diagnostics.Assert("output length", 0L, output.Length);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("http://example.com/b", result.Report!.RedirectUrl);
        Assert.AreEqual(0L, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_3xxWithoutLocationWhileFollowing_WritesTheBodyAndReportsNoTarget()
    {
        MemoryStream output = new();
        Diagnostics.Arrange("follow, scripted response", "True, 300 Multiple Choices without Location, body list!");

        TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 300 Multiple Choices\r\nContent-Length: 5\r\n\r\nlist!", 65536)))
            .ExecuteAsync(FollowContext("http://example.com/", output));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("redirect url", "(null)", result.Report!.RedirectUrl ?? "(null)");
        Diagnostics.Assert("body", "list!", Latin1(output.ToArray()));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.Report!.RedirectUrl);
        Assert.AreEqual("list!", Latin1(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_LocationOn200WhileFollowing_WritesTheBodyAndReportsNoTarget()
    {
        MemoryStream output = new();
        Diagnostics.Arrange("follow, scripted response", "True, 200 OK with Location: /x, body hello");

        TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 200 OK\r\nLocation: /x\r\nContent-Length: 5\r\n\r\nhello", 65536)))
            .ExecuteAsync(FollowContext("http://example.com/", output));

        WriteResult(result);
        Diagnostics.Assert("redirect url", "(null)", result.Report!.RedirectUrl ?? "(null)");
        Diagnostics.Assert("body", "hello", Latin1(output.ToArray()));
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
        string request = "GET / HTTP/1.1\r\nHost: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nAccept-Encoding: deflate, gzip, br, zstd\r\n\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            TransferContext context = new()
            {
                Url = CurlUrl.Parse("http://example.com/"),
                Output = output,
                Http = new HttpRequestOptions { Compressed = true, Raw = raw },
            };
            Diagnostics.Arrange("chunk size, raw", $"{chunkSize}, {raw}");
            Diagnostics.Arrange("expected request", OneLine(request));
            Diagnostics.Bytes("gzip body", gzip);

            TransferResult result = await Handler(QueueConnector.For(Connection(response, chunkSize, request))).ExecuteAsync(context);

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Diagnostics.Assert("output", Convert.ToHexString(raw ? gzip : "hello"u8.ToArray()), Convert.ToHexString(output.ToArray()));
            Diagnostics.Assert("download size", 25L, result.Report!.DownloadSize);
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
            Diagnostics.Arrange("chunk size, compressed", $"{chunkSize}, True");
            Diagnostics.Arrange("gzip body", "000102030405060708090A0B (corrupt)");

            TransferResult result = await Handler(QueueConnector.For(Connection(response, chunkSize))).ExecuteAsync(context);

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.BadContentEncoding, result.ExitCode);
            Diagnostics.Assert("error text", "Error while processing content unencoding: incorrect header check", result.ErrorMessage);
            Diagnostics.Assert("output length", 0L, output.Length);
            Assert.AreEqual(CurlExitCode.BadContentEncoding, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("Error while processing content unencoding: incorrect header check", result.ErrorMessage, $"Chunk size {chunkSize}");
            Assert.AreEqual(0L, output.Length, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured with curl 8.21.0 <c>-s -S --compressed</c> (BL-281 Notes): a gzip body
    /// followed by <c>41 42</c>, both inside Content-Length, writes <c>hello</c> and gives
    /// <c>curl: (23) Failed writing received data to disk/application</c>.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_CompressedBodyWithBytesAfterItsStream_WritesTheStreamThenReturnsExit23()
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
            string response = "HTTP/1.1 200 OK\r\nContent-Encoding: gzip\r\nContent-Length: 27\r\n\r\n"
                + Latin1(HttpContentDecoderTests.Bytes(HttpContentDecoderTests.Gzip + "4142"));
            Diagnostics.Arrange("chunk size, compressed", $"{chunkSize}, True");
            Diagnostics.Arrange("gzip body", HttpContentDecoderTests.Gzip + "4142 (two bytes after the stream)");

            TransferResult result = await Handler(QueueConnector.For(Connection(response, chunkSize))).ExecuteAsync(context);

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.WriteError, result.ExitCode);
            Diagnostics.Assert("error text", "Failed writing received data to disk/application", result.ErrorMessage);
            Diagnostics.Assert("body", "hello", Latin1(output.ToArray()));
            Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("Failed writing received data to disk/application", result.ErrorMessage, $"Chunk size {chunkSize}");
            Assert.AreEqual("hello", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
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

    /// <summary>Shows CR and LF as <c>\r</c> and <c>\n</c>, so a diagnostic stays on one line.</summary>
    private static string OneLine(string text) => text.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

    /// <summary>Writes the ACT line for a transfer's exit code and error text.</summary>
    private void WriteResult(TransferResult result) =>
        Diagnostics.Act("exit code", $"{result.ExitCode} ({(int)result.ExitCode}), error: {result.ErrorMessage ?? "(none)"}");

    /// <summary>Writes an ACT line with every recorded event, CR and LF shown escaped.</summary>
    private void WriteEvents(string label, IEnumerable<string> lines) =>
        Diagnostics.Act(label, OneLine(string.Join(" | ", lines)));

    /// <summary>Writes the ASSERT line for two sequences of lines, CR and LF shown escaped.</summary>
    private void WriteExpectedLines(string label, IEnumerable<string> expected, IEnumerable<string> actual) =>
        Diagnostics.Assert(label, OneLine(string.Join(" | ", expected)), OneLine(string.Join(" | ", actual)));
}
