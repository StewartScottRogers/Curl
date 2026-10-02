using System.Net;
using System.Text;
using Curl.Authentication;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <content>
/// Drives <see cref="HttpProtocolHandler" /> through a forward proxy that answers <c>407</c>,
/// with the <see cref="RankedHttpAuthenticator" /> the composition builds, so every
/// <c>Proxy-Authorization</c> is the real Basic or Digest answer. Each case is what curl 8.21.0
/// sent <c>Record-CurlExchange.ps1 -Connections N</c> as the proxy for
/// <c>curl -s -S -v -x http://127.0.0.1:18603 -U u:p &lt;switches&gt; http://example.invalid/</c>;
/// the bytes are in BL-603's Notes. Digest answers inject the cnonce curl sent, so each hash
/// is the one curl sent too, in curl's own-code format (ADR-0025). Each exchange is replayed
/// with 1-byte reads and with one read.
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private const string ProxyAuthUrl = "http://example.invalid/";

    private const string ProxyBasicChallengeHead =
        "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Basic realm=\"r\"\r\nContent-Length: 3\r\nConnection: close\r\n\r\n";

    private const string ProxyDigestChallengeHead =
        "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Digest realm=\"r\", nonce=\"abc\", qop=\"auth\"\r\nContent-Length: 3\r\nConnection: close\r\n\r\n";

    private const string KeepAliveProxyDigestChallengeHead =
        "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Digest realm=\"r\", nonce=\"abc\", qop=\"auth\"\r\nContent-Length: 3\r\n\r\n";

    private const string OriginBasicChallengeHead =
        "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"s\"\r\nContent-Length: 3\r\nConnection: close\r\n\r\n";

    private const string OriginDigestChallengeHead =
        "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Digest realm=\"s\", nonce=\"xyz\", qop=\"auth\"\r\nContent-Length: 3\r\nConnection: close\r\n\r\n";

    private const string ClosingOkHead = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\n";

    private const string ProxyRequestStart = "GET http://example.invalid/ HTTP/1.1\r\nHost: example.invalid\r\n";

    private const string ProxyRequestEnd = "User-Agent: curl/8.21.0\r\nAccept: */*\r\nProxy-Connection: Keep-Alive\r\n\r\n";

    private const string ProxyBasic = "Proxy-Authorization: Basic dTpw\r\n";

    private const string OriginBasic = "Authorization: Basic YTpi\r\n";

    private static readonly ProxyEndpoint ChallengingProxy = new(ProxyKind.Http, "127.0.0.1", 18603, new NetworkCredential("u", "p"));

    /// <summary>
    /// Measured: <c>--proxy-basic</c> against a <c>407</c> sends <c>Basic dTpw</c> up front and
    /// makes no second request; exit 0 with the 407's body <c>PPP</c> as the output.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_ProxyBasicRefused_ReturnsThe407WithoutRetrying()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, ProxyBasicChallengeHead + "PPP");
            MemoryStream output = new();

            TransferResult result = await ProxyChallengeHandler(QueueConnector.For(connection), HttpAuthSchemes.Basic)
                .ExecuteAsync(ProxyChallengeContext(output));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(ProxyRequestStart + ProxyBasic + ProxyRequestEnd, connection.Written, $"Chunk size {chunkSize}");
            Assert.AreEqual("PPP", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(407, result.Report!.ResponseCode, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: <c>-f --proxy-basic</c> against a <c>407</c> fails with
    /// <c>curl: (22) The requested URL returned error: 407</c> and writes no body.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_ProxyBasicRefusedUnderFail_FailsWith22()
    {
        TurnTakingConnection connection = new(65536, ProxyBasicChallengeHead + "PPP");
        MemoryStream output = new();

        TransferResult result = await ProxyChallengeHandler(QueueConnector.For(connection), HttpAuthSchemes.Basic)
            .ExecuteAsync(ProxyChallengeContext(output, fail: true));

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("The requested URL returned error: 407", result.ErrorMessage);
        Assert.AreEqual(ProxyRequestStart + ProxyBasic + ProxyRequestEnd, connection.Written);
        Assert.IsEmpty(output.ToArray());
    }

    /// <summary>
    /// Measured: <c>--proxy-digest</c> sends no <c>Proxy-Authorization</c> first, answers the
    /// 407's Digest challenge on a new connection, as the 407 closes its own, with
    /// <c>uri="/"</c>, the origin form, and gets the 200's <c>ok</c>.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_ProxyDigestChallengeThatCloses_AnswersItOnANewConnection()
    {
        const string digest = "Proxy-Authorization: Digest username=\"u\", realm=\"r\", nonce=\"abc\", uri=\"/\", "
            + "cnonce=\"8c2728ea340ca45e0fc1411a44d914b4\", nc=00000001, qop=auth, response=\"7c30003a2d4e99796b67ba30c0234996\"\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection first = new(chunkSize, ProxyDigestChallengeHead + "PPP");
            TurnTakingConnection second = new(chunkSize, ClosingOkHead + "ok");
            QueueConnector connector = QueueConnector.For(first, second);
            MemoryStream output = new();
            MemoryStream headerOutput = new();

            TransferResult result = await ProxyChallengeHandler(connector, HttpAuthSchemes.Digest, "8c2728ea340ca45e0fc1411a44d914b4")
                .ExecuteAsync(ProxyChallengeContext(output, headerOutput: headerOutput));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(ProxyRequestStart + ProxyRequestEnd, first.Written, $"Chunk size {chunkSize}");
            Assert.AreEqual(ProxyRequestStart + digest + ProxyRequestEnd, second.Written, $"Chunk size {chunkSize}");
            Assert.AreEqual(ProxyDigestChallengeHead + ClosingOkHead, Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual("ok", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.HasCount(2, connector.Targets, $"Chunk size {chunkSize}");
            Assert.AreEqual(2, result.Report!.ConnectionCount, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: <c>--proxy-digest</c> against a 407 that keeps the connection open sends the
    /// Digest answer on the same connection.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_ProxyDigestChallengeOnKeepAlive_AnswersItOnTheSameConnection()
    {
        const string digest = "Proxy-Authorization: Digest username=\"u\", realm=\"r\", nonce=\"abc\", uri=\"/\", "
            + "cnonce=\"e395f7bf9cdabe6947113bd005a4ce2a\", nc=00000001, qop=auth, response=\"688727ce4be512c2aaae3c137d924d87\"\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, KeepAliveProxyDigestChallengeHead + "PPP", ProxyOkHead + "ok");
            QueueConnector connector = QueueConnector.For(connection);
            MemoryStream output = new();

            TransferResult result = await ProxyChallengeHandler(connector, HttpAuthSchemes.Digest, "e395f7bf9cdabe6947113bd005a4ce2a")
                .ExecuteAsync(ProxyChallengeContext(output));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(ProxyRequestStart + ProxyRequestEnd + ProxyRequestStart + digest + ProxyRequestEnd, connection.Written, $"Chunk size {chunkSize}");
            Assert.AreEqual("ok", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.HasCount(1, connector.Targets, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: <c>--proxy-digest</c> answered by a second 407 makes no third request; exit 0
    /// with the second 407's body, or under <c>-f</c> exit 22 on the second 407.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_ProxyDigestAnswerRefused_ReturnsTheSecond407()
    {
        const string digest = "Proxy-Authorization: Digest username=\"u\", realm=\"r\", nonce=\"abc\", uri=\"/\", "
            + "cnonce=\"95f7476d91514799667a1c895ce15714\", nc=00000001, qop=auth, response=\"039df8ae965737f3ebb7361b5ac12705\"\r\n";
        foreach (bool fail in new[] { false, true })
        {
            TurnTakingConnection first = new(65536, ProxyDigestChallengeHead + "PPP");
            TurnTakingConnection second = new(65536, ProxyDigestChallengeHead + "PPP");
            QueueConnector connector = QueueConnector.For(first, second);
            MemoryStream output = new();

            TransferResult result = await ProxyChallengeHandler(connector, HttpAuthSchemes.Digest, "95f7476d91514799667a1c895ce15714")
                .ExecuteAsync(ProxyChallengeContext(output, fail: fail));

            Assert.AreEqual(fail ? CurlExitCode.HttpReturnedError : CurlExitCode.Ok, result.ExitCode, $"Fail {fail}");
            Assert.AreEqual(ProxyRequestStart + digest + ProxyRequestEnd, second.Written, $"Fail {fail}");
            Assert.AreEqual(fail ? string.Empty : "PPP", Latin1(output.ToArray()), $"Fail {fail}");
            Assert.HasCount(2, connector.Targets, $"Fail {fail}");
        }
    }

    /// <summary>
    /// Measured: <c>--proxy-digest</c> against a 407 offering only Basic makes no second
    /// request; exit 0 with the 407's body.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_ProxyDigestAgainstABasicChallenge_ReturnsThe407()
    {
        TurnTakingConnection connection = new(65536, ProxyBasicChallengeHead + "PPP");
        MemoryStream output = new();

        TransferResult result = await ProxyChallengeHandler(QueueConnector.For(connection), HttpAuthSchemes.Digest)
            .ExecuteAsync(ProxyChallengeContext(output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(ProxyRequestStart + ProxyRequestEnd, connection.Written);
        Assert.AreEqual("PPP", Latin1(output.ToArray()));
    }

    /// <summary>
    /// Measured: <c>--proxy-anyauth</c> sends nothing first and answers a Basic 407 with
    /// <c>Basic dTpw</c>; a second 407 is the result.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_ProxyAnyAuthAgainstBasic_AnswersWithBasicOnce()
    {
        foreach (string last in new[] { ClosingOkHead + "ok", ProxyBasicChallengeHead + "PPP" })
        {
            TurnTakingConnection first = new(65536, ProxyBasicChallengeHead + "PPP");
            TurnTakingConnection second = new(65536, last);
            MemoryStream output = new();

            TransferResult result = await ProxyChallengeHandler(QueueConnector.For(first, second), HttpAuthSchemes.Any)
                .ExecuteAsync(ProxyChallengeContext(output));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
            Assert.AreEqual(ProxyRequestStart + ProxyRequestEnd, first.Written);
            Assert.AreEqual(ProxyRequestStart + ProxyBasic + ProxyRequestEnd, second.Written);
            Assert.AreEqual(last.EndsWith("ok", StringComparison.Ordinal) ? "ok" : "PPP", Latin1(output.ToArray()));
        }
    }

    /// <summary>
    /// Measured: <c>--proxy-anyauth</c> answers a Digest 407 with Digest.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_ProxyAnyAuthAgainstDigest_AnswersWithDigest()
    {
        const string digest = "Proxy-Authorization: Digest username=\"u\", realm=\"r\", nonce=\"abc\", uri=\"/\", "
            + "cnonce=\"ac9d38277a649bc7d1b93233b9bcf15c\", nc=00000001, qop=auth, response=\"9a1961b82b2ff80d0f088b26d553c8d5\"\r\n";
        TurnTakingConnection first = new(65536, ProxyDigestChallengeHead + "PPP");
        TurnTakingConnection second = new(65536, ClosingOkHead + "ok");
        MemoryStream output = new();

        TransferResult result = await ProxyChallengeHandler(QueueConnector.For(first, second), HttpAuthSchemes.Any, "ac9d38277a649bc7d1b93233b9bcf15c")
            .ExecuteAsync(ProxyChallengeContext(output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(ProxyRequestStart + ProxyRequestEnd, first.Written);
        Assert.AreEqual(ProxyRequestStart + digest + ProxyRequestEnd, second.Written);
        Assert.AreEqual("ok", Latin1(output.ToArray()));
    }

    /// <summary>
    /// Measured: <c>-U u:p -u a:b</c>, Basic for both, sends both up front; the 407 is the
    /// result.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_ProxyAndOriginBasicRefusedByTheProxy_ReturnsThe407()
    {
        TurnTakingConnection connection = new(65536, ProxyBasicChallengeHead + "PPP");
        MemoryStream output = new();

        TransferResult result = await ProxyChallengeHandler(QueueConnector.For(connection), HttpAuthSchemes.Basic)
            .ExecuteAsync(ProxyChallengeContext(output, originCredential: new NetworkCredential("a", "b")));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(ProxyRequestStart + ProxyBasic + OriginBasic + ProxyRequestEnd, connection.Written);
        Assert.AreEqual("PPP", Latin1(output.ToArray()));
    }

    /// <summary>
    /// Measured: <c>--proxy-anyauth -u a:b</c> sends the origin's Basic up front, keeps it on
    /// the retry that answers the 407 with Basic, and takes the 401 that follows as the
    /// result, as the origin's credential was sent and refused.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_ProxyAnyAuthThenOriginRefusal_KeepsTheOriginBasicAndReturnsThe401()
    {
        TurnTakingConnection first = new(65536, ProxyBasicChallengeHead + "PPP");
        TurnTakingConnection second = new(65536, OriginBasicChallengeHead + "UUU");
        QueueConnector connector = QueueConnector.For(first, second);
        MemoryStream output = new();

        TransferResult result = await ProxyChallengeHandler(connector, HttpAuthSchemes.Any)
            .ExecuteAsync(ProxyChallengeContext(output, originCredential: new NetworkCredential("a", "b")));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(ProxyRequestStart + OriginBasic + ProxyRequestEnd, first.Written);
        Assert.AreEqual(ProxyRequestStart + ProxyBasic + OriginBasic + ProxyRequestEnd, second.Written);
        Assert.AreEqual("UUU", Latin1(output.ToArray()));
        Assert.AreEqual(401, result.Report!.ResponseCode);
    }

    /// <summary>
    /// Measured: <c>--proxy-digest -u a:b --digest</c> against a 407, then a 401, then a 200
    /// answers each challenge once, the 401's retry keeping the proxy's Digest answer with its
    /// nonce counted on to <c>nc=00000002</c>, the same cnonce and curl's hash for that count
    /// (BL-603 Notes, BL-869).
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_ProxyDigestThenOriginDigest_AnswersEachChallengeOnce()
    {
        const string proxyDigest = "Proxy-Authorization: Digest username=\"u\", realm=\"r\", nonce=\"abc\", uri=\"/\", "
            + "cnonce=\"063231b54c58aa830f9917b0665bdaf8\", nc=00000001, qop=auth, response=\"8146a82aefc2f845325c0b151b67e80d\"\r\n";
        const string keptProxyDigest = "Proxy-Authorization: Digest username=\"u\", realm=\"r\", nonce=\"abc\", uri=\"/\", "
            + "cnonce=\"063231b54c58aa830f9917b0665bdaf8\", nc=00000002, qop=auth, response=\"924da41f0f75d705a8c76efb5ad7d596\"\r\n";
        const string originDigest = "Authorization: Digest username=\"a\", realm=\"s\", nonce=\"xyz\", uri=\"/\", "
            + "cnonce=\"582287d88c3c0940fe2e132942e332bc\", nc=00000001, qop=auth, response=\"1f89777280a8403ca258416cad303d5b\"\r\n";
        TurnTakingConnection first = new(65536, ProxyDigestChallengeHead + "PPP");
        TurnTakingConnection second = new(65536, OriginDigestChallengeHead + "UUU");
        TurnTakingConnection third = new(65536, ClosingOkHead + "ok");
        QueueConnector connector = QueueConnector.For(first, second, third);
        MemoryStream output = new();

        TransferResult result = await ProxyChallengeHandler(connector, HttpAuthSchemes.Digest, "063231b54c58aa830f9917b0665bdaf8", "582287d88c3c0940fe2e132942e332bc")
            .ExecuteAsync(ProxyChallengeContext(output, originCredential: new NetworkCredential("a", "b"), originSchemes: HttpAuthSchemes.Digest));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(ProxyRequestStart + ProxyRequestEnd, first.Written);
        Assert.AreEqual(ProxyRequestStart + proxyDigest + ProxyRequestEnd, second.Written);
        Assert.AreEqual(ProxyRequestStart + keptProxyDigest + originDigest + ProxyRequestEnd, third.Written);
        Assert.AreEqual("ok", Latin1(output.ToArray()));
        Assert.AreEqual(3, result.Report!.ConnectionCount);
    }

    /// <summary>
    /// Measured: <c>--proxy-anyauth -u a:b --digest</c> against a 401, then a 407, then a 200
    /// answers the 401 first and keeps that answer when it answers the 407, counted on to
    /// <c>nc=00000002</c> with the same cnonce (BL-869).
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_OriginDigestThenProxyDigest_AnswersEachChallengeOnce()
    {
        const string originDigest = "Authorization: Digest username=\"a\", realm=\"s\", nonce=\"xyz\", uri=\"/\", "
            + "cnonce=\"f7604464c2453c62e1f5077435011686\", nc=00000001, qop=auth, response=\"9355f1a62b7602de98380cd8232120b8\"\r\n";
        const string keptOriginDigest = "Authorization: Digest username=\"a\", realm=\"s\", nonce=\"xyz\", uri=\"/\", "
            + "cnonce=\"f7604464c2453c62e1f5077435011686\", nc=00000002, qop=auth, response=\"19b392bfec0f8a5d87a9d959622271e4\"\r\n";
        const string proxyDigest = "Proxy-Authorization: Digest username=\"u\", realm=\"r\", nonce=\"abc\", uri=\"/\", "
            + "cnonce=\"c06ed45dc85f0a3e7b4671f765ef1672\", nc=00000001, qop=auth, response=\"b54dfbe77d0102f798b83b924efa82c5\"\r\n";
        TurnTakingConnection first = new(65536, OriginDigestChallengeHead + "UUU");
        TurnTakingConnection second = new(65536, ProxyDigestChallengeHead + "PPP");
        TurnTakingConnection third = new(65536, ClosingOkHead + "ok");
        MemoryStream output = new();

        TransferResult result = await ProxyChallengeHandler(QueueConnector.For(first, second, third), HttpAuthSchemes.Any, "f7604464c2453c62e1f5077435011686", "c06ed45dc85f0a3e7b4671f765ef1672")
            .ExecuteAsync(ProxyChallengeContext(output, originCredential: new NetworkCredential("a", "b"), originSchemes: HttpAuthSchemes.Digest));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(ProxyRequestStart + ProxyRequestEnd, first.Written);
        Assert.AreEqual(ProxyRequestStart + originDigest + ProxyRequestEnd, second.Written);
        Assert.AreEqual(ProxyRequestStart + proxyDigest + keptOriginDigest + ProxyRequestEnd, third.Written);
        Assert.AreEqual("ok", Latin1(output.ToArray()));
    }

    /// <summary>
    /// A 407 from a server reached without a forward proxy has no proxy to answer: it is the
    /// result, and the authenticator is never asked about a proxy.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_407WithoutAForwardProxy_ReturnsIt()
    {
        TurnTakingConnection connection = new(65536, ProxyBasicChallengeHead + "PPP");
        MemoryStream output = new();
        TransferContext context = new() { Url = CurlUrl.Parse(ProxyAuthUrl), Output = output };

        TransferResult result = await ProxyChallengeHandler(QueueConnector.For(connection), HttpAuthSchemes.Any).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("PPP", Latin1(output.ToArray()));
        Assert.DoesNotContain("Proxy-Authorization", connection.Written);
    }

    /// <summary>
    /// A 407 to a request whose body is a stream is the result, as a 401 to one is (ADR-0034):
    /// the stream cannot be sent again.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_407ToAStreamBody_ReturnsIt()
    {
        TurnTakingConnection connection = new(65536, ProxyBasicChallengeHead + "PPP");
        MemoryStream output = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(ProxyAuthUrl),
            Output = output,
            Http = new HttpRequestOptions { ForwardProxy = ChallengingProxy, Body = new StreamBody(new MemoryStream("hi"u8.ToArray()), 2, "application/octet-stream") },
        };

        TransferResult result = await ProxyChallengeHandler(QueueConnector.For(connection), HttpAuthSchemes.Any).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("PPP", Latin1(output.ToArray()));
    }

    /// <summary>
    /// The handler keeps the proxy schemes it was given, Basic when given none.
    /// </summary>
    [TestMethod]
    public void ProxyAuthSchemes_IsWhatTheHandlerWasGivenOrBasic()
    {
        SilentAuthenticator authenticator = new();

        Assert.AreEqual(HttpAuthSchemes.Basic, new HttpProtocolHandler(QueueConnector.For(), authenticator).ProxyAuthSchemes);
        Assert.AreEqual(HttpAuthSchemes.Digest, new HttpProtocolHandler(QueueConnector.For(), authenticator, null, HttpAuthSchemes.Digest).ProxyAuthSchemes);
    }

    /// <summary>
    /// Builds the handler over <paramref name="connector" /> with the authenticator the
    /// composition builds, answering the proxy with <paramref name="proxySchemes" /> and drawing
    /// each Digest cnonce from <paramref name="clientNonces" /> in turn.
    /// </summary>
    private static HttpProtocolHandler ProxyChallengeHandler(QueueConnector connector, HttpAuthSchemes proxySchemes, params string[] clientNonces)
    {
        Queue<string> nonces = new(clientNonces);
        RankedHttpAuthenticator authenticator = new(
            new BasicAndBearerAuthenticator(Encoding.UTF8),
            new DigestAuthenticator(Encoding.UTF8, nonces.Dequeue),
            new NegotiateHttpAuthenticator(new SystemSecurityContextFactory()),
            new NtlmHttpAuthenticator(new SystemSecurityContextFactory(), matchesSspiBuild: false));
        return new HttpProtocolHandler(connector, authenticator, null, proxySchemes);
    }

    private static TransferContext ProxyChallengeContext(
        Stream output,
        Stream? headerOutput = null,
        bool fail = false,
        NetworkCredential? originCredential = null,
        HttpAuthSchemes originSchemes = HttpAuthSchemes.Basic) =>
        new()
        {
            Url = CurlUrl.Parse(ProxyAuthUrl),
            Output = output,
            HeaderOutput = headerOutput,
            Credentials = originCredential,
            Http = new HttpRequestOptions
            {
                ForwardProxy = ChallengingProxy,
                Fail = fail ? HttpFailMode.Fail : HttpFailMode.None,
                AuthSchemes = originSchemes,
            },
        };
}
