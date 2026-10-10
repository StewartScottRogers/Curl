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

        WriteExpectedLines("events to the retry", ExpectedAuthRetryEvents("Digest", "WWW-Authenticate: Digest realm=\"r\", nonce=\"abc\""), AuthRetryLines(events));
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

        WriteExpectedLines("events to the retry", ExpectedAuthRetryEvents("NTLM", "WWW-Authenticate: NTLM BAUG"), AuthRetryLines(events));
        CollectionAssert.AreEqual(ExpectedAuthRetryEvents("NTLM", "WWW-Authenticate: NTLM BAUG"), AuthRetryLines(events));
    }

    [TestMethod]
    public async Task ExecuteAsync_DigestRetryVerbose_ReportsTheConnectionReusedByItsHostPortAndNumber()
    {
        RecordingTransferEvents events = await AuthRetryEventsAsync(
            new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Digest }, new ScriptedTokenSource(), KeepAliveDigestChallenge, OkHead + "ok");

        Diagnostics.Act("connections reused", string.Join(" | ", events.Reused));
        Diagnostics.Assert("connections reused", 1, events.Reused.Count);
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

        Diagnostics.Act("connections reused", string.Join(" | ", events.Reused));
        Diagnostics.Assert("connections reused", 1, events.Reused.Count);
        Assert.AreEqual(
            new ConnectionReusedEvent { Scheme = "http", IsProxy = true, HostName = "10.0.0.5", Port = 1080, ConnectionNumber = 0 },
            events.Reused.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_ProxyDigestRetryVerbose_ReportsTheForwardProxyConnectionReused()
    {
        TurnTakingConnection connection = new(65536, KeepAliveProxyDigestChallengeHead + "PPP", ProxyOkHead + "ok");
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("url, proxy, auth", $"{ProxyAuthUrl}, {ChallengingProxy.Host}:{ChallengingProxy.Port}, Digest");

        TransferResult result = await ProxyChallengeHandler(QueueConnector.For(connection), HttpAuthSchemes.Digest, "e395f7bf9cdabe6947113bd005a4ce2a")
            .ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse(ProxyAuthUrl),
                Output = new MemoryStream(),
                Http = new HttpRequestOptions { ForwardProxy = ChallengingProxy },
                Events = events,
            });

        WriteResult(result);
        WriteEvents("events", events.Events);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Act("connections reused", string.Join(" | ", events.Reused));
        Diagnostics.Assert("connections reused", 1, events.Reused.Count);
        Assert.AreEqual(
            new ConnectionReusedEvent { Scheme = "http", IsProxy = true, HostName = "127.0.0.1", Port = 18603, ConnectionNumber = 0 },
            events.Reused.Single());
        CollectionAssert.IsSubsetOf(
            new[] { "* Issue another request to this URL: 'http://example.invalid/'", "* Reusing existing http: connection with proxy 127.0.0.1" },
            events.Events);
    }

    [TestMethod]
    public async Task ExecuteAsync_DigestRetryWhenTheServerClosedTheKeptConnection_SendsTheRetryOnAFreshConnection()
    {
        // curl 8.21.0 --digest -u u:p -v against a 401 that half-closes after its body: the
        // connection is left intact, then found dead, and the retry goes out on connection #1
        // with no byte written to #0 (measured, BL-2018 Notes). The pool writes the dead lines.
        TurnTakingConnection closed = new(65536, KeepAliveDigestChallenge) { HasPeerClosed = true };
        TurnTakingConnection fresh = new(65536, OkHead + "ok");
        QueueConnector connector = QueueConnector.For(closed, fresh);
        RecordingTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(AuthUrl),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("u", "p"),
            Http = new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Digest },
            Events = events,
        };
        Diagnostics.Arrange("url, auth, first connection", $"{AuthUrl}, Digest, closed by the server after the 401");

        TransferResult result = await NegotiateHandler(connector, new ScriptedTokenSource()).ExecuteAsync(context);

        WriteResult(result);
        WriteEvents("events", events.Events);
        Diagnostics.Act("connects, requests on the closed connection", $"{connector.Targets.Count}, {closed.Written.Split("GET ").Length - 1}");
        Diagnostics.Assert("connects", 2, connector.Targets.Count);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.HasCount(2, connector.Targets);
        Assert.AreEqual(1, closed.Written.Split("GET ").Length - 1);
        StringAssert.Contains(fresh.Written, "Authorization: Digest");
        Assert.IsEmpty(events.Reused);
        CollectionAssert.IsSubsetOf(
            new[] { "* Issue another request to this URL: 'http://127.0.0.1:18183/a'" },
            events.Events);
        Assert.IsFalse(events.Events.Any(line => line.StartsWith("* Connection died", StringComparison.Ordinal)));
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
    private async Task<RecordingTransferEvents> AuthRetryEventsAsync(HttpRequestOptions options, ScriptedTokenSource tokens, params string[] responses)
    {
        TurnTakingConnection connection = new(65536, responses);
        RecordingTransferEvents events = new();
        TransferContext context = new() { Url = CurlUrl.Parse(AuthUrl), Output = new MemoryStream(), Credentials = new NetworkCredential("u", "p"), Http = options, Events = events };
        Diagnostics.Arrange("url, auth schemes, responses", $"{AuthUrl}, {options.AuthSchemes}, {responses.Length}");

        TransferResult result = await NegotiateHandler(QueueConnector.For(connection), tokens).ExecuteAsync(context);

        WriteResult(result);
        WriteEvents("events", events.Events);
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
