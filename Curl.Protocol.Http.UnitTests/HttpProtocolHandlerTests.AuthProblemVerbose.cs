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

        CollectionAssert.AreEqual(
            new[] { "< HTTP/1.1 401 Unauthorized", BasicProblem, "< WWW-Authenticate: Basic realm=\"x\"", "< Content-Length: 4" },
            LastHeadLines(lines));
        Assert.AreEqual(1, lines.Count(line => line.StartsWith("> GET", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_Bearer401Verbose_WritesBearerProblemOnlyForTheBearerChallenge()
    {
        TurnTakingConnection connection = new(65536, "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Bearer realm=\"x\", basic x\r\nContent-Length: 4\r\n\r\nnope");

        List<string> lines = await AuthProblemLinesAsync(connection, new HttpRequestOptions { BearerToken = "tok", AuthSchemes = HttpAuthSchemes.Bearer }, credential: null, AuthUrl);

        CollectionAssert.AreEqual(
            new[] { "< HTTP/1.1 401 Unauthorized", "* Bearer authentication problem, ignoring.", "< WWW-Authenticate: Bearer realm=\"x\", basic x", "< Content-Length: 4" },
            LastHeadLines(lines));
    }

    [TestMethod]
    public async Task ExecuteAsync_Basic401WithAnAuthorizationHeaderVerbose_WritesBasicProblemForEachBasicChallenge()
    {
        TurnTakingConnection connection = new(
            65536, "HTTP/1.1 401 U\r\nWWW-Authenticate: Basic realm=\"x\", Basic y\r\nwww-authenticate: basic z\r\nContent-Length: 4\r\n\r\nnope");

        List<string> lines = await AuthProblemLinesAsync(connection, new HttpRequestOptions { Headers = ["Authorization: Foo"] }, new NetworkCredential("u", "p"), AuthUrl);

        CollectionAssert.AreEqual(
            new[] { "< HTTP/1.1 401 U", BasicProblem, BasicProblem, "< WWW-Authenticate: Basic realm=\"x\", Basic y", BasicProblem, "< www-authenticate: basic z", "< Content-Length: 4" },
            LastHeadLines(lines));
    }

    [TestMethod]
    public async Task ExecuteAsync_Basic403Verbose_WritesNoProblemLine()
    {
        TurnTakingConnection connection = new(65536, "HTTP/1.1 403 Forbidden\r\nWWW-Authenticate: Basic realm=\"x\"\r\nContent-Length: 4\r\n\r\nnope");

        List<string> lines = await AuthProblemLinesAsync(connection, new HttpRequestOptions(), new NetworkCredential("u", "p"), AuthUrl);

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
        Assert.AreEqual("< WWW-Authenticate: Basic realm=\"x\"", lines[firstStatus + 1]);
        CollectionAssert.AreEqual(
            new[] { "< HTTP/1.1 401 Unauthorized", BasicProblem, "< WWW-Authenticate: Basic realm=\"x\"", "< Content-Length: 4" },
            LastHeadLines(lines));
    }

    [TestMethod]
    public async Task ExecuteAsync_DigestOnly401AfterBasicVerbose_WritesNoProblemLine()
    {
        TurnTakingConnection connection = new(65536, "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Digest realm=\"r\", nonce=\"n\"\r\nContent-Length: 4\r\n\r\nnope");

        List<string> lines = await AuthProblemLinesAsync(connection, new HttpRequestOptions(), new NetworkCredential("u", "p"), AuthUrl);

        Assert.IsFalse(lines.Any(line => line.Contains("authentication problem", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_ProxyBasic407Verbose_WritesBasicProblemBeforeTheProxyChallengeHeader()
    {
        TurnTakingConnection connection = new(65536, "HTTP/1.1 407 Proxy Auth\r\nProxy-Authenticate: Basic realm=\"x\", Basic y\r\nContent-Length: 4\r\n\r\nnope");

        List<string> lines = await AuthProblemLinesAsync(connection, new HttpRequestOptions { ForwardProxy = ChallengingProxy }, credential: null, ProxyAuthUrl);

        CollectionAssert.AreEqual(
            new[] { "< HTTP/1.1 407 Proxy Auth", BasicProblem, BasicProblem, "< Proxy-Authenticate: Basic realm=\"x\", Basic y", "< Content-Length: 4" },
            LastHeadLines(lines));
    }

    /// <summary>Runs one <c>-v</c> transfer of <paramref name="url" /> on <paramref name="connection" />, checks it succeeds, and gives its events by first line.</summary>
    private static async Task<List<string>> AuthProblemLinesAsync(TurnTakingConnection connection, HttpRequestOptions options, NetworkCredential? credential, string url)
    {
        RecordingTransferEvents events = new();
        TransferContext context = new() { Url = CurlUrl.Parse(url), Output = new MemoryStream(), Credentials = credential, Http = options, Events = events };

        TransferResult result = await NegotiateHandler(QueueConnector.For(connection), new ScriptedTokenSource()).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        return FirstLinesOf(events);
    }
}
