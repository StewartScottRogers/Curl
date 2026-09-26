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
public sealed class HttpProtocolHandlerTests
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
    [DataRow("HTTPS://Example.com:8443/", "example.com", 8443, true, DisplayName = "https with a port")]
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
            Url = new Uri("http://example.com/"),
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
            Url = new Uri("http://example.com/"),
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
            Url = new Uri("http://example.com/"),
            Output = new MemoryStream(),
            CancellationToken = cancellation.Token,
        };

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await Handler(QueueConnector.For(Connection(Head, 1))).ExecuteAsync(context));
    }

    private static HttpProtocolHandler Handler(QueueConnector connector) => new(connector, new SilentAuthenticator());

    private static TransferContext Context(string url, Stream output, Stream? headerOutput = null) =>
        new() { Url = new Uri(url), Output = output, HeaderOutput = headerOutput };

    private static ScriptedConnection Connection(string response, int chunkSize, string? expectedRequest = null) =>
        new(Encoding.Latin1.GetBytes(response), chunkSize, expectedRequest is null ? null : Encoding.Latin1.GetBytes(expectedRequest));

    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);
}
