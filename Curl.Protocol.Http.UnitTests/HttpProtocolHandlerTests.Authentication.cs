using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <content>
/// Drives <see cref="HttpProtocolHandler" />'s authentication through
/// <see cref="ScriptedAuthenticator" />, <see cref="TurnTakingConnection" /> and
/// <see cref="QueueConnector" />, never a socket. Every request, header output and report
/// value is what curl 8.21.0 produced against a loopback server; the commands are in the
/// BL-181 Notes. Each exchange is replayed with 1-byte reads and with one read.
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private const string AuthUrl = "http://127.0.0.1:18183/a";

    private const string DigestValue = "Digest username=\"u\",realm=\"r\",nonce=\"n\",uri=\"/a\",response=\"544c035f0f40d9ebf0295157d8041f8c\"";

    private const string Challenge = "Digest realm=\"r\", nonce=\"n\"";

    private const string PlainRequest = "GET /a HTTP/1.1\r\nHost: 127.0.0.1:18183\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n";

    private const string DigestRequest = "GET /a HTTP/1.1\r\nHost: 127.0.0.1:18183\r\nAuthorization: " + DigestValue + "\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n";

    private const string BasicRequest = "GET /a HTTP/1.1\r\nHost: 127.0.0.1:18183\r\nAuthorization: Basic dTpw\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n";

    private const string ChallengeHead = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: " + Challenge + "\r\nContent-Length: 4\r\n\r\n";

    private const string ClosingChallengeHead = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: " + Challenge + "\r\nConnection: close\r\nContent-Length: 4\r\n\r\n";

    private const string RefusedHead = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: " + Challenge + "\r\nContent-Length: 5\r\n\r\n";

    private const string OkHead = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\n";

    private const string FormBody = "Content-Length: 5\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\nhello";

    /// <summary>
    /// Measured: <c>curl -s -u u:p http://127.0.0.1:18181/a</c> sends Basic on the first
    /// request, straight after <c>Host</c>.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_BasicUpFront_SendsItAfterHost()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, OkHead + "ok");
            ScriptedAuthenticator authenticator = new("Basic dTpw", null);
            MemoryStream output = new();
            NetworkCredential credential = new("u", "p");
            TransferContext context = new() { Url = new Uri(AuthUrl), Output = output, Credentials = credential };

            TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), authenticator).ExecuteAsync(context);

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(BasicRequest, connection.Written, $"Chunk size {chunkSize}");
            Assert.AreEqual("ok", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            (HttpAuthRequest request, IReadOnlyList<string> challenges) = authenticator.Calls.Single();
            Assert.AreEqual(new HttpAuthRequest("GET", new Uri(AuthUrl), "/a", credential, null, HttpAuthSchemes.Basic, false), request);
            Assert.IsEmpty(challenges, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: <c>curl -s -i -u u:p</c> against a 401 with a Basic challenge sends no
    /// second request and exits 0 with the 401 as the output.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_UpFrontCredentialRefused_ReturnsThe401WithoutRetrying()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            const string basicChallenge = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"x\"\r\nContent-Length: 4\r\n\r\n";
            TurnTakingConnection connection = new(chunkSize, basicChallenge + "nope", OkHead + "ok");
            ScriptedAuthenticator authenticator = new("Basic dTpw", "Basic dTpw");
            MemoryStream output = new();
            MemoryStream headerOutput = new();

            TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), authenticator)
                .ExecuteAsync(AuthContext(output, headerOutput));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(BasicRequest, connection.Written, $"Chunk size {chunkSize}");
            Assert.AreEqual(basicChallenge, Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual("nope", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.HasCount(1, authenticator.Calls, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: <c>curl -s -i --digest -u u:p -w "%{http_code} %{num_connects}
    /// %{size_request} %{size_header} %{size_download}"</c> against a keep-alive 401 retries
    /// on the same connection, writes both heads and only the 200's body, and reports
    /// <c>200 1 269 133 2</c>.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_ChallengeOnKeepAlive_RetriesOnTheSameConnection()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, ChallengeHead + "nope", OkHead + "ok");
            QueueConnector connector = QueueConnector.For(connection);
            ScriptedAuthenticator authenticator = new(null, DigestValue);
            MemoryStream output = new();
            MemoryStream headerOutput = new();

            TransferResult result = await new HttpProtocolHandler(connector, authenticator).ExecuteAsync(AuthContext(output, headerOutput));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(PlainRequest + DigestRequest, connection.Written, $"Chunk size {chunkSize}");
            Assert.AreEqual(ChallengeHead + OkHead, Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual("ok", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.HasCount(1, connector.Targets, $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(new[] { Challenge }, authenticator.Calls[1].Challenges.ToArray(), $"Chunk size {chunkSize}");
            AssertReport(result, 200, 1, 269, 133, 2);
        }
    }

    /// <summary>
    /// Measured: the same run against a 401 with <c>Connection: close</c> retries on a new
    /// connection and reports <c>200 2 269 152 2</c>.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_ChallengeWithConnectionClose_RetriesOnANewConnection()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection first = new(chunkSize, ClosingChallengeHead + "nope");
            TurnTakingConnection second = new(chunkSize, OkHead + "ok");
            QueueConnector connector = QueueConnector.For(first, second);
            MemoryStream output = new();
            MemoryStream headerOutput = new();

            TransferResult result = await new HttpProtocolHandler(connector, new ScriptedAuthenticator(null, DigestValue))
                .ExecuteAsync(AuthContext(output, headerOutput));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(PlainRequest, first.Written, $"Chunk size {chunkSize}");
            Assert.AreEqual(DigestRequest, second.Written, $"Chunk size {chunkSize}");
            Assert.IsTrue(first.IsDisposed, $"Chunk size {chunkSize}");
            Assert.IsTrue(second.IsDisposed, $"Chunk size {chunkSize}");
            Assert.AreEqual(ClosingChallengeHead + OkHead, Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual("ok", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(connector.Targets[0], connector.Targets[1], $"Chunk size {chunkSize}");
            AssertReport(result, 200, 2, 269, 152, 2);
        }
    }

    /// <summary>
    /// Measured: when the retry draws a second 401, curl 8.21.0 sends nothing more and exits
    /// 0 with that 401's body, reporting <c>401 1 269 190 5</c>.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_RetryRefused_ReturnsTheSecond401()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, ChallengeHead + "nope", RefusedHead + "nope2", OkHead + "ok");
            ScriptedAuthenticator authenticator = new(null, DigestValue);
            MemoryStream output = new();
            MemoryStream headerOutput = new();

            TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), authenticator)
                .ExecuteAsync(AuthContext(output, headerOutput));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(PlainRequest + DigestRequest, connection.Written, $"Chunk size {chunkSize}");
            Assert.AreEqual(ChallengeHead + RefusedHead, Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual("nope2", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.HasCount(2, authenticator.Calls, $"Chunk size {chunkSize}");
            AssertReport(result, 401, 1, 269, 190, 5);
        }
    }

    /// <summary>
    /// Measured: under <c>-f</c> the 401 the retry answers does not fail the transfer; the
    /// second one does, with <c>curl: (22) The requested URL returned error: 401</c> after
    /// both heads are written.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_FailAndRetryRefused_FailsOnTheSecond401()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, ChallengeHead + "nope", RefusedHead + "nope2");
            MemoryStream output = new();
            MemoryStream headerOutput = new();
            TransferContext context = AuthContext(output, headerOutput, new HttpRequestOptions { Fail = HttpFailMode.Fail });

            TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new ScriptedAuthenticator(null, DigestValue))
                .ExecuteAsync(context);

            Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("The requested URL returned error: 401", result.ErrorMessage, $"Chunk size {chunkSize}");
            Assert.AreEqual(PlainRequest + DigestRequest, connection.Written, $"Chunk size {chunkSize}");
            Assert.AreEqual(ChallengeHead + RefusedHead, Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(0L, output.Length, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: <c>curl -sS -f -u u:p</c> against a 401 exits 22 at once.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_FailAndUpFrontCredentialRefused_Fails()
    {
        TurnTakingConnection connection = new(65536, "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"x\"\r\nContent-Length: 4\r\n\r\nnope");
        TransferContext context = AuthContext(new MemoryStream(), null, new HttpRequestOptions { Fail = HttpFailMode.Fail });

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new ScriptedAuthenticator("Basic dTpw", "Basic dTpw"))
            .ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("The requested URL returned error: 401", result.ErrorMessage);
        Assert.AreEqual(BasicRequest, connection.Written);
    }

    /// <summary>
    /// Measured: <c>curl --digest -u u:p</c> against a 401 with no <c>WWW-Authenticate</c>
    /// sends nothing more and reports <c>401 1 80 48 4</c>. An authenticator that cannot
    /// answer the challenge ends the transfer the same way.
    /// </summary>
    [TestMethod]
    [DataRow("HTTP/1.1 401 Unauthorized\r\nContent-Length: 4\r\n\r\n", DigestValue, 1, DisplayName = "No challenge")]
    [DataRow(ChallengeHead, null, 2, DisplayName = "Challenge the authenticator cannot answer")]
    public async Task ExecuteAsync_NoAnswerToThe401_ReturnsIt(string head, string? answer, int calls)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, head + "nope", OkHead + "ok");
            ScriptedAuthenticator authenticator = new(null, answer);
            MemoryStream output = new();

            TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), authenticator).ExecuteAsync(AuthContext(output));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(PlainRequest, connection.Written, $"Chunk size {chunkSize}");
            Assert.AreEqual("nope", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.HasCount(calls, authenticator.Calls, $"Chunk size {chunkSize}");
            Assert.AreEqual(401, result.Report!.ResponseCode, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: <c>curl --anyauth -u u:p -d hello</c> against a Basic challenge sends the
    /// body with both requests.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_ChallengeToABytesBody_SendsTheBodyAgain()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            const string basicChallenge = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"x\"\r\nContent-Length: 4\r\n\r\n";
            TurnTakingConnection connection = new(chunkSize, basicChallenge + "nope", OkHead + "ok");
            MemoryStream output = new();
            TransferContext context = AuthContext(output, null, new HttpRequestOptions { Body = new BytesBody("hello"u8.ToArray(), "application/x-www-form-urlencoded") });

            TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new ScriptedAuthenticator(null, "Basic dTpw"))
                .ExecuteAsync(context);

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(
                "POST /a HTTP/1.1\r\nHost: 127.0.0.1:18183\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n" + FormBody
                + "POST /a HTTP/1.1\r\nHost: 127.0.0.1:18183\r\nAuthorization: Basic dTpw\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n" + FormBody,
                connection.Written,
                $"Chunk size {chunkSize}");
            Assert.AreEqual("ok", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// A stream body has been read by the time the 401 arrives and cannot be sent again, so
    /// the 401 is the result (BL-181 Notes, ADR-0032).
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_ChallengeToAStreamBody_ReturnsThe401()
    {
        TurnTakingConnection connection = new(65536, ChallengeHead + "nope", OkHead + "ok");
        ScriptedAuthenticator authenticator = new(null, DigestValue);
        MemoryStream output = new();
        TransferContext context = AuthContext(output, null, new HttpRequestOptions { Body = new StreamBody(new MemoryStream("hello"u8.ToArray()), 5, "application/octet-stream") });

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), authenticator).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("nope", Latin1(output.ToArray()));
        Assert.HasCount(1, authenticator.Calls);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReconnectFails_ReturnsTheConnectFailure()
    {
        TurnTakingConnection first = new(65536, ClosingChallengeHead + "nope");
        QueueConnector connector = new(
            ConnectResult.Connected(first),
            ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect to 127.0.0.1 port 18183 after 0 ms: Could not connect to server"));
        MemoryStream output = new();

        TransferResult result = await new HttpProtocolHandler(connector, new ScriptedAuthenticator(null, DigestValue)).ExecuteAsync(AuthContext(output));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to 127.0.0.1 port 18183 after 0 ms: Could not connect to server", result.ErrorMessage);
        Assert.AreEqual(0L, output.Length);
    }

    private static void AssertReport(TransferResult result, int responseCode, int connections, long requestSize, long headerSize, long downloadSize)
    {
        TransferReport report = result.Report!;
        Assert.AreEqual(responseCode, report.ResponseCode);
        Assert.AreEqual(connections, report.ConnectionCount);
        Assert.AreEqual(requestSize, report.RequestSize);
        Assert.AreEqual(headerSize, report.HeaderSize);
        Assert.AreEqual(downloadSize, report.DownloadSize);
    }

    private static TransferContext AuthContext(Stream output, Stream? headerOutput = null, HttpRequestOptions? options = null) =>
        new() { Url = new Uri(AuthUrl), Output = output, HeaderOutput = headerOutput, Http = options };
}
