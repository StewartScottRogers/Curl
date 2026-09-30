using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <content>
/// Pins the <c>-v</c> lines curl 8.21.0 writes between a <c>401</c> or <c>407</c> it answers
/// and the request it sends again on the same connection (measured with
/// <c>Record-CurlExchange.ps1 -HoldOpenMilliseconds</c>, BL-959 Notes): the connection left
/// intact, <c>Issue another request to this URL: '...'</c>, <c>Reusing existing http:
/// connection with host ...</c>, then the retry's <c>Server auth using</c> line and head. Each
/// request head is given by its first line.
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private const string KeepAliveDigestChallenge = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Digest realm=\"r\", nonce=\"abc\"\r\nContent-Length: 4\r\n\r\ndeny";

    private const string KeepAliveNtlmChallenge = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: NTLM BAUG\r\nContent-Length: 4\r\n\r\ndeny";

    [TestMethod]
    public async Task ExecuteAsync_DigestRetryVerbose_WritesIssueAnotherRequestAndReusingBeforeTheRetry()
    {
        RecordingTransferEvents events = await AuthRetryEventsAsync(
            new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Digest }, new ScriptedTokenSource(), KeepAliveDigestChallenge, OkHead + "ok");

        CollectionAssert.AreEqual(ExpectedAuthRetryEvents("Digest", "WWW-Authenticate: Digest realm=\"r\", nonce=\"abc\""), AuthRetryLines(events));
    }

    [TestMethod]
    public async Task ExecuteAsync_NtlmRetryVerbose_WritesIssueAnotherRequestAndReusingBeforeTheRetry()
    {
        ScriptedTokenSource tokens = new(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [1, 2, 3]),
            new SecurityContextStep(SecurityContextStatus.Completed, [7, 8, 9]));

        RecordingTransferEvents events = await AuthRetryEventsAsync(
            new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Ntlm }, tokens, KeepAliveNtlmChallenge, OkHead + "ok");

        CollectionAssert.AreEqual(ExpectedAuthRetryEvents("NTLM", "WWW-Authenticate: NTLM BAUG"), AuthRetryLines(events));
    }

    [TestMethod]
    public async Task ExecuteAsync_DigestRetryVerbose_ReportsTheConnectionReusedByItsHostPortAndNumber()
    {
        RecordingTransferEvents events = await AuthRetryEventsAsync(
            new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Digest }, new ScriptedTokenSource(), KeepAliveDigestChallenge, OkHead + "ok");

        Assert.AreEqual(
            new ConnectionReusedEvent { Scheme = "http", IsProxy = false, HostName = "127.0.0.1", Port = 18183, ConnectionNumber = 0 },
            events.Reused.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_DigestRetryThroughATunnellingProxy_ReportsTheConnectionReusedWithTheProxy()
    {
        HttpRequestOptions options = new()
        {
            AuthSchemes = HttpAuthSchemes.Digest,
            ForwardProxy = new ProxyEndpoint(ProxyKind.Socks5, "10.0.0.5", 1080, null),
        };

        RecordingTransferEvents events = await AuthRetryEventsAsync(options, new ScriptedTokenSource(), KeepAliveDigestChallenge, OkHead + "ok");

        Assert.AreEqual(
            new ConnectionReusedEvent { Scheme = "http", IsProxy = true, HostName = "10.0.0.5", Port = 1080, ConnectionNumber = 0 },
            events.Reused.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_ProxyDigestRetryVerbose_ReportsTheForwardProxyConnectionReused()
    {
        TurnTakingConnection connection = new(65536, KeepAliveProxyDigestChallengeHead + "PPP", ProxyOkHead + "ok");
        RecordingTransferEvents events = new();

        TransferResult result = await ProxyChallengeHandler(QueueConnector.For(connection), HttpAuthSchemes.Digest, "e395f7bf9cdabe6947113bd005a4ce2a")
            .ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse(ProxyAuthUrl),
                Output = new MemoryStream(),
                Http = new HttpRequestOptions { ForwardProxy = ChallengingProxy },
                Events = events,
            });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(
            new ConnectionReusedEvent { Scheme = "http", IsProxy = true, HostName = "127.0.0.1", Port = 18603, ConnectionNumber = 0 },
            events.Reused.Single());
        CollectionAssert.IsSubsetOf(
            new[] { "* Issue another request to this URL: 'http://example.invalid/'", "* Reusing existing http: connection with proxy 127.0.0.1" },
            events.Events);
    }

    /// <summary>
    /// Gives curl 8.21.0's <c>-v</c> lines for <c>--digest</c> or <c>--ntlm -u u:p -v</c>
    /// against <paramref name="challenge" /> on a connection kept open, then a <c>200</c>, as
    /// measured to the retry's request head (BL-959 Notes).
    /// </summary>
    private static string[] ExpectedAuthRetryEvents(string scheme, string challenge) =>
    [
        "* using HTTP/1.x",
        $"* Server auth using {scheme} with user 'u'",
        "> GET /a HTTP/1.1",
        "* Request completely sent off",
        "< HTTP/1.1 401 Unauthorized",
        "< " + challenge,
        "< Content-Length: 4",
        "* Ignoring the response-body",
        "* setting size while ignoring",
        "< ",
        "* Connection #0 to host 127.0.0.1:18183 left intact",
        "* Issue another request to this URL: 'http://127.0.0.1:18183/a'",
        "* Reusing existing http: connection with host 127.0.0.1",
        $"* Server auth using {scheme} with user 'u'",
        "> GET /a HTTP/1.1",
        "* Request completely sent off",
    ];

    /// <summary>
    /// Runs one <c>-u u:p -v</c> transfer of <see cref="AuthUrl" /> against
    /// <paramref name="responses" /> on one connection and gives its events.
    /// </summary>
    private static async Task<RecordingTransferEvents> AuthRetryEventsAsync(HttpRequestOptions options, ScriptedTokenSource tokens, params string[] responses)
    {
        TurnTakingConnection connection = new(65536, responses);
        RecordingTransferEvents events = new();
        TransferContext context = new() { Url = CurlUrl.Parse(AuthUrl), Output = new MemoryStream(), Credentials = new NetworkCredential("u", "p"), Http = options, Events = events };

        TransferResult result = await NegotiateHandler(QueueConnector.For(connection), tokens).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        return events;
    }

    /// <summary>
    /// Gives the events up to the second request's <c>Request completely sent off</c>, each
    /// header by its first line.
    /// </summary>
    private static List<string> AuthRetryLines(RecordingTransferEvents events)
    {
        List<string> lines = [.. events.Events.Select(line => line.Split("\r\n")[0])];
        int secondSent = lines.FindIndex(lines.IndexOf("* Request completely sent off") + 1, line => line == "* Request completely sent off");
        return lines[..(secondSent + 1)];
    }
}
