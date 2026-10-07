using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <content>
/// Pins the <c>Basic authentication problem, ignoring.</c> and <c>Bearer ...</c> <c>-v</c> lines
/// curl 8.21.0 writes just before a challenge header refusing the Basic or Bearer value it sent,
/// once for each challenge offering that scheme, and that it sends no second request (measured
/// with <c>Record-CurlExchange.ps1</c>, BL-1040 Notes). Each event is given by its first line.
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private const string BasicProblem = "* Basic authentication problem, ignoring.";

    [TestMethod]
    public async Task ExecuteAsync_Basic401Verbose_WritesBasicProblemBeforeTheChallengeHeaderAndSendsOneRequest()
    {
        TurnTakingConnection connection = new(65536, "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"x\"\r\nContent-Length: 4\r\n\r\nnope");

        List<string> lines = await AuthProblemLinesAsync(connection, new HttpRequestOptions(), new NetworkCredential("u", "p"), AuthUrl);

        string[] expectedHead = ["< HTTP/1.1 401 Unauthorized", BasicProblem, "< WWW-Authenticate: Basic realm=\"x\"", "< Content-Length: 4"];
        WriteExpectedLines("last head lines", expectedHead, LastHeadLines(lines));
        CollectionAssert.AreEqual(
            expectedHead,
            LastHeadLines(lines));
        Assert.AreEqual(1, lines.Count(line => line.StartsWith("> GET", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_Bearer401Verbose_WritesBearerProblemOnlyForTheBearerChallenge()
    {
        TurnTakingConnection connection = new(65536, "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Bearer realm=\"x\", basic x\r\nContent-Length: 4\r\n\r\nnope");

        List<string> lines = await AuthProblemLinesAsync(connection, new HttpRequestOptions { BearerToken = "tok", AuthSchemes = HttpAuthSchemes.Bearer }, credential: null, AuthUrl);

        string[] expectedHead = ["< HTTP/1.1 401 Unauthorized", "* Bearer authentication problem, ignoring.", "< WWW-Authenticate: Bearer realm=\"x\", basic x", "< Content-Length: 4"];
        WriteExpectedLines("last head lines", expectedHead, LastHeadLines(lines));
        CollectionAssert.AreEqual(
            expectedHead,
            LastHeadLines(lines));
    }

    [TestMethod]
    public async Task ExecuteAsync_Basic401WithAnAuthorizationHeaderVerbose_WritesBasicProblemForEachBasicChallenge()
    {
        TurnTakingConnection connection = new(
            65536, "HTTP/1.1 401 U\r\nWWW-Authenticate: Basic realm=\"x\", Basic y\r\nwww-authenticate: basic z\r\nContent-Length: 4\r\n\r\nnope");

        List<string> lines = await AuthProblemLinesAsync(connection, new HttpRequestOptions { Headers = ["Authorization: Foo"] }, new NetworkCredential("u", "p"), AuthUrl);

        string[] expectedHead = ["< HTTP/1.1 401 U", BasicProblem, BasicProblem, "< WWW-Authenticate: Basic realm=\"x\", Basic y", BasicProblem, "< www-authenticate: basic z", "< Content-Length: 4"];
        WriteExpectedLines("last head lines", expectedHead, LastHeadLines(lines));
        CollectionAssert.AreEqual(
            expectedHead,
            LastHeadLines(lines));
    }

    [TestMethod]
    public async Task ExecuteAsync_Basic403Verbose_WritesNoProblemLine()
    {
        TurnTakingConnection connection = new(65536, "HTTP/1.1 403 Forbidden\r\nWWW-Authenticate: Basic realm=\"x\"\r\nContent-Length: 4\r\n\r\nnope");

        List<string> lines = await AuthProblemLinesAsync(connection, new HttpRequestOptions(), new NetworkCredential("u", "p"), AuthUrl);

        Diagnostics.Assert("authentication problem lines", 0, lines.Count(line => line.Contains("authentication problem", StringComparison.Ordinal)));
        Assert.IsFalse(lines.Any(line => line.Contains("authentication problem", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Measured: <c>--anyauth</c> picks no scheme for its first request, so the first 401 writes
    /// no line; the retry it sends with Basic picked writes one before the second 401's challenge.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_AnyAuth401Verbose_WritesNoProblemLineBeforeTheFirstChallengeOnlyAfterBasicWasPicked()
    {
        const string Challenge = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"x\"\r\nContent-Length: 4\r\n\r\nnope";
        TurnTakingConnection connection = new(65536, Challenge, Challenge);

        List<string> lines = await AuthProblemLinesAsync(
            connection, new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Any }, new NetworkCredential("u", "p"), AuthUrl);

        int firstStatus = lines.IndexOf("< HTTP/1.1 401 Unauthorized");
        Diagnostics.Assert("line after the first status", "< WWW-Authenticate: Basic realm=\"x\"", lines[firstStatus + 1]);
        Assert.AreEqual("< WWW-Authenticate: Basic realm=\"x\"", lines[firstStatus + 1]);
        string[] expectedHead = ["< HTTP/1.1 401 Unauthorized", BasicProblem, "< WWW-Authenticate: Basic realm=\"x\"", "< Content-Length: 4"];
        WriteExpectedLines("last head lines", expectedHead, LastHeadLines(lines));
        CollectionAssert.AreEqual(
            expectedHead,
            LastHeadLines(lines));
    }

    [TestMethod]
    public async Task ExecuteAsync_DigestOnly401AfterBasicVerbose_WritesNoProblemLine()
    {
        TurnTakingConnection connection = new(65536, "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Digest realm=\"r\", nonce=\"n\"\r\nContent-Length: 4\r\n\r\nnope");

        List<string> lines = await AuthProblemLinesAsync(connection, new HttpRequestOptions(), new NetworkCredential("u", "p"), AuthUrl);

        Diagnostics.Assert("authentication problem lines", 0, lines.Count(line => line.Contains("authentication problem", StringComparison.Ordinal)));
        Assert.IsFalse(lines.Any(line => line.Contains("authentication problem", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_ProxyBasic407Verbose_WritesBasicProblemBeforeTheProxyChallengeHeader()
    {
        TurnTakingConnection connection = new(65536, "HTTP/1.1 407 Proxy Auth\r\nProxy-Authenticate: Basic realm=\"x\", Basic y\r\nContent-Length: 4\r\n\r\nnope");


        List<string> lines = await AuthProblemLinesAsync(connection, new HttpRequestOptions { ForwardProxy = ChallengingProxy }, credential: null, ProxyAuthUrl);

        string[] expectedHead = ["< HTTP/1.1 407 Proxy Auth", BasicProblem, BasicProblem, "< Proxy-Authenticate: Basic realm=\"x\", Basic y", "< Content-Length: 4"];
        WriteExpectedLines("last head lines", expectedHead, LastHeadLines(lines));
        CollectionAssert.AreEqual(
            expectedHead,
            LastHeadLines(lines));
    }

    /// <summary>
    /// Measured (BL-1179 Notes): <c>curl -s -S -v -x http://127.0.0.1:P http://example.invalid/</c>
    /// with no <c>-U</c> against a <c>407</c> carrying two Digest challenges in one header writes
    /// the duplicate line before that header and no problem line.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_ProxyDigest407WithoutProxyCredentialsVerbose_WritesDuplicateDigestLineBeforeTheProxyChallengeHeader()
    {
        TurnTakingConnection connection = new(65536, "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Digest realm=\"r\", nonce=\"a\", Digest realm=\"s\", nonce=\"c\"\r\nContent-Length: 0\r\n\r\n");
        ProxyEndpoint proxyWithoutCredential = new(ProxyKind.Http, "127.0.0.1", 18603, null);

        List<string> lines = await AuthProblemLinesAsync(connection, new HttpRequestOptions { ForwardProxy = proxyWithoutCredential }, credential: null, ProxyAuthUrl);

        string[] expectedHead =
        [
            "< HTTP/1.1 407 Proxy Authentication Required",
            "* Ignoring duplicate digest auth header.",
            "< Proxy-Authenticate: Digest realm=\"r\", nonce=\"a\", Digest realm=\"s\", nonce=\"c\"",
            "< Content-Length: 0",
        ];
        WriteExpectedLines("last head lines", expectedHead, LastHeadLines(lines));
        CollectionAssert.AreEqual(
            expectedHead,
            LastHeadLines(lines));
    }

    /// <summary>
    /// Measured (BL-1175 Notes): <c>curl -s -S -v --digest -u u:p</c> against a Digest
    /// <c>401</c> and then a second one without <c>stale=true</c> writes the Digest problem line,
    /// then the duplicate line for the header's second Digest challenge, before that header.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_DigestAnswerRefusedVerbose_WritesDigestProblemBeforeTheChallengeHeader()
    {
        const string Refusal = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Digest realm=\"r\", nonce=\"b\", qop=\"auth\", Digest realm=\"s\", nonce=\"c\"\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
        TurnTakingConnection first = new(65536, StaleChallenge("a", stale: false, close: true));
        TurnTakingConnection second = new(65536, Refusal);

        List<string> lines = await DigestVerboseLinesAsync(first, second);

        string[] expectedHead =
        [
            "< HTTP/1.1 401 Unauthorized",
            "* Digest authentication problem, ignoring.",
            "* Ignoring duplicate digest auth header.",
            "< WWW-Authenticate: Digest realm=\"r\", nonce=\"b\", qop=\"auth\", Digest realm=\"s\", nonce=\"c\"",
            "< Content-Length: 0",
        ];
        WriteExpectedLines("last head lines", expectedHead, LastHeadLines(lines));
        CollectionAssert.AreEqual(
            expectedHead,
            LastHeadLines(lines));
        Assert.AreEqual(1, lines.Count(line => line.Contains("Digest authentication problem", StringComparison.Ordinal)));
    }

    /// <summary>Measured (BL-1175 Notes): a stale Digest challenge writes no problem line; curl answers it afresh.</summary>
    [TestMethod]
    public async Task ExecuteAsync_StaleDigestChallengeVerbose_WritesNoProblemLine()
    {
        TurnTakingConnection first = new(65536, StaleChallenge("a", stale: false, close: true));
        TurnTakingConnection second = new(65536, StaleChallenge("b", stale: true, close: true));
        TurnTakingConnection third = new(65536, StaleOk);

        List<string> lines = await DigestVerboseLinesAsync(first, second, third);

        Diagnostics.Assert("authentication problem lines", 0, lines.Count(line => line.Contains("authentication problem", StringComparison.Ordinal)));
        Assert.IsFalse(lines.Any(line => line.Contains("authentication problem", StringComparison.Ordinal)));
        Assert.Contains("< HTTP/1.1 200 OK", lines);
    }

    /// <summary>Runs one <c>-v --digest -u u:p</c> transfer on <paramref name="connections" />, checks it succeeds, and gives its events by first line.</summary>
    private async Task<List<string>> DigestVerboseLinesAsync(params TurnTakingConnection[] connections)
    {
        RecordingTransferEvents events = new();
        TransferContext context = new() { Url = CurlUrl.Parse(StaleUrl), Output = new MemoryStream(), Credentials = new NetworkCredential("u", "p"), Http = new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Digest }, Events = events };
        Diagnostics.Arrange("url, user, connections", $"{StaleUrl} --digest, u, {connections.Length}");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connections), StaleAuthenticator("370cf856b91684edfd74ca6d21b5bebb", "fa452aa0c29c5f74b6287443cc695e23"))
            .ExecuteAsync(context);

        WriteResult(result);
        WriteEvents("events by first line", FirstLinesOf(events));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        return FirstLinesOf(events);
    }

    /// <summary>Runs one <c>-v</c> transfer of <paramref name="url" /> on <paramref name="connection" />, checks it succeeds, and gives its events by first line.</summary>
    private async Task<List<string>> AuthProblemLinesAsync(TurnTakingConnection connection, HttpRequestOptions options, NetworkCredential? credential, string url)
    {
        RecordingTransferEvents events = new();
        TransferContext context = new() { Url = CurlUrl.Parse(url), Output = new MemoryStream(), Credentials = credential, Http = options, Events = events };
        Diagnostics.Arrange("url, user, auth schemes", $"{url}, {credential?.UserName ?? "(none)"}, {options.AuthSchemes}");

        TransferResult result = await NegotiateHandler(QueueConnector.For(connection), new ScriptedTokenSource()).ExecuteAsync(context);

        WriteResult(result);
        WriteEvents("events by first line", FirstLinesOf(events));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        return FirstLinesOf(events);
    }
}
