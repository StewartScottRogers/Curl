using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins the <c>-v</c> lines <see cref="TcpConnector" /> reports for the BL-602 CONNECT <c>407</c>
/// cases, in curl 8.21.0's order and text (measured with <c>Record-CurlExchange.ps1</c> as the
/// proxy; BL-863 Notes): <c>Proxy auth using</c>, <c>Establishing HTTP proxy tunnel to</c>, the
/// CONNECT head, the reply's header lines, <c>Connect me again please</c> and
/// <c>&lt;scheme&gt; authentication problem, ignoring.</c>, and after a <c>2xx</c> the
/// <c>CONNECT phase completed</c> and <c>CONNECT tunnel established</c> lines (BL-964), in the
/// Schannel build unless a test says otherwise, each dial's <c>Trying</c> followed by <c>CONNECT: no ALPN
/// negotiated</c> (BL-1145).
/// </summary>
public sealed partial class TcpConnectorTests
{
    private const string Trying = "*   Trying 192.0.2.10:18602...";

    private const string NoAlpnNegotiated = "* CONNECT: no ALPN negotiated";

    private const string Establishing = "* Establishing HTTP proxy tunnel to example.test:80";

    private static readonly string[] EstablishedLines =
        ["< HTTP/1.1 200 Connection established", "< ", "* CONNECT phase completed for HTTP proxy", "* CONNECT tunnel established, response 200"];

    private static readonly string[] BasicChallengeLines =
        ["< HTTP/1.1 407 Proxy Authentication Required", "< Proxy-Authenticate: Basic realm=\"r\"", "< Content-Length: 0", "< Connection: close", "< "];

    private static readonly string[] DigestChallengeLines =
        ["< HTTP/1.1 407 Proxy Authentication Required", "< Proxy-Authenticate: Digest realm=\"r\", nonce=\"abc\", qop=\"auth\"", "< Content-Length: 0", "< Connection: close", "< "];

    private const string ChunkedDigestChallenge =
        "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Digest realm=\"r\", nonce=\"abc\", qop=\"auth\"\r\nTransfer-Encoding: chunked\r\n\r\n";

    private static readonly string[] ChunkedDigestChallengeLines =
    [
        "< HTTP/1.1 407 Proxy Authentication Required",
        "< Proxy-Authenticate: Digest realm=\"r\", nonce=\"abc\", qop=\"auth\"",
        "< Transfer-Encoding: chunked",
        "* CONNECT responded chunked",
        "< ",
    ];

    [TestMethod]
    public async Task ConnectAsync_WithProxyBasic_ReportsTheBasicLinesAndTheProblemAfterTheChallenge()
    {
        var events = new RecordingTransferEvents();
        var (connector, _) = CreateAuthenticatingConnector(HttpAuthSchemes.Basic, new ScriptedConnection(Encoding.Latin1.GetBytes(BasicChallengeClosing)));

        await connector.ConnectAsync(AuthenticatingTarget with { Events = events }, CancellationToken.None);

        AssertTranscript(
            Lines(
                [Trying, NoAlpnNegotiated, "* Proxy auth using Basic with user 'u'", Establishing],
                RequestLines(BasicConnect),
                BasicChallengeLines[..2],
                ["* Basic authentication problem, ignoring."],
                BasicChallengeLines[2..]),
            events.Transcript);
    }

    [TestMethod]
    public async Task ConnectAsync_WithProxyDigest_ReportsDigestBeforeBothConnectsAndConnectsAgainAfterTheClose()
    {
        var events = new RecordingTransferEvents();
        var (connector, _) = CreateAuthenticatingConnector(
            HttpAuthSchemes.Digest,
            new ScriptedConnection(Encoding.Latin1.GetBytes(DigestChallengeClosing)),
            new ScriptedConnection(Encoding.Latin1.GetBytes(EstablishedReply)));

        await connector.ConnectAsync(AuthenticatingTarget with { Events = events }, CancellationToken.None);

        AssertTranscript(
            Lines(
                [Trying, NoAlpnNegotiated, "* Proxy auth using Digest with user 'u'", Establishing],
                RequestLines(UnauthenticatedConnect),
                DigestChallengeLines,
                ["* Connect me again please", Trying, NoAlpnNegotiated, "* Proxy auth using Digest with user 'u'", Establishing],
                RequestLines(DigestConnect),
                EstablishedLines),
            events.Transcript);
    }

    [TestMethod]
    public async Task ConnectAsync_WithProxyDigestOnAKeptOpenConnection_ReportsBothConnectsWithoutConnectingAgain()
    {
        // Measured (BL-863, BL-1146): curl 8.21.0 writes "Ignore 6 bytes of response-body" after the 407's blank line.
        var events = new RecordingTransferEvents();
        var (connector, _) = CreateAuthenticatingConnector(
            HttpAuthSchemes.Digest,
            new ScriptedConnection(Encoding.Latin1.GetBytes(
                "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Digest realm=\"r\", nonce=\"abc\", qop=\"auth\"\r\nContent-Length: 6\r\n\r\ndenied"
                + EstablishedReply)));

        await connector.ConnectAsync(AuthenticatingTarget with { Events = events }, CancellationToken.None);

        AssertTranscript(
            Lines(
                [Trying, NoAlpnNegotiated, "* Proxy auth using Digest with user 'u'", Establishing],
                RequestLines(UnauthenticatedConnect),
                ["< HTTP/1.1 407 Proxy Authentication Required", "< Proxy-Authenticate: Digest realm=\"r\", nonce=\"abc\", qop=\"auth\"", "< Content-Length: 6", "< "],
                ["* Ignore 6 bytes of response-body", "* Proxy auth using Digest with user 'u'", Establishing],
                RequestLines(DigestConnect),
                EstablishedLines),
            events.Transcript);
    }

    [TestMethod]
    public async Task ConnectAsync_WithProxyDigestAfterAnEmpty407Body_ReportsNoIgnoreLine()
    {
        // Measured (BL-1146): curl 8.21.0 writes no Ignore line for Content-Length: 0.
        var events = new RecordingTransferEvents();
        var (connector, _) = CreateAuthenticatingConnector(
            HttpAuthSchemes.Digest,
            new ScriptedConnection(Encoding.Latin1.GetBytes(
                "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Digest realm=\"r\", nonce=\"abc\", qop=\"auth\"\r\nContent-Length: 0\r\n\r\n"
                + EstablishedReply)));

        await connector.ConnectAsync(AuthenticatingTarget with { Events = events }, CancellationToken.None);

        AssertTranscript(
            Lines(
                [Trying, NoAlpnNegotiated, "* Proxy auth using Digest with user 'u'", Establishing],
                RequestLines(UnauthenticatedConnect),
                ["< HTTP/1.1 407 Proxy Authentication Required", "< Proxy-Authenticate: Digest realm=\"r\", nonce=\"abc\", qop=\"auth\"", "< Content-Length: 0", "< "],
                ["* Proxy auth using Digest with user 'u'", Establishing],
                RequestLines(DigestConnect),
                EstablishedLines),
            events.Transcript);
    }

    [TestMethod]
    public async Task ConnectAsync_WithProxyDigestAfterAChunked407_ReportsRespondedChunkedIgnoreAndChunkReadingDone()
    {
        // Measured (BL-1144): curl 8.21.0 -v -p -x ... -U u:p --proxy-digest against a chunked 407 then a 200.
        var events = new RecordingTransferEvents();
        var (connector, _) = CreateAuthenticatingConnector(
            HttpAuthSchemes.Digest,
            new ScriptedConnection(Encoding.Latin1.GetBytes(
                ChunkedDigestChallenge + "5;ext=1\r\nhello\r\n3\r\nabc\r\n0\r\nX-Trailer: t\r\n\r\n" + EstablishedReply)));

        await connector.ConnectAsync(AuthenticatingTarget with { Events = events }, CancellationToken.None);

        AssertTranscript(
            Lines(
                [Trying, NoAlpnNegotiated, "* Proxy auth using Digest with user 'u'", Establishing],
                RequestLines(UnauthenticatedConnect),
                ChunkedDigestChallengeLines,
                ["* Ignore chunked response-body", "* chunk reading DONE", "* Proxy auth using Digest with user 'u'", Establishing],
                RequestLines(DigestConnect),
                EstablishedLines),
            events.Transcript);
    }

    [TestMethod]
    [DataRow("zz\r\nhello\r\n0\r\n\r\n", new[] { "* chunk hex-length char not a hex digit: 0x7a" }, DisplayName = "malformed")]
    [DataRow("5\r\nhel", new[] { "* Proxy CONNECT aborted" }, DisplayName = "cut short")]
    [DataRow("5\r\nhelloX", new string[0], DisplayName = "no CRLF after the data")]
    public async Task ConnectAsync_WithProxyDigestAfterABadChunked407_ReportsTheFailureLineCurlWrites(string body, string[] expectedEnd)
    {
        // Measured (BL-1144): the line before curl's "* closing connection #0", which the HTTP handler writes.
        var events = new RecordingTransferEvents();
        var (connector, _) = CreateAuthenticatingConnector(
            HttpAuthSchemes.Digest,
            new ScriptedConnection(Encoding.Latin1.GetBytes(ChunkedDigestChallenge + body)));

        await connector.ConnectAsync(AuthenticatingTarget with { Events = events }, CancellationToken.None);

        AssertTranscript(
            Lines(
                [Trying, NoAlpnNegotiated, "* Proxy auth using Digest with user 'u'", Establishing],
                RequestLines(UnauthenticatedConnect),
                ChunkedDigestChallengeLines,
                ["* Ignore chunked response-body"],
                expectedEnd),
            events.Transcript);
    }

    [TestMethod]
    [DataRow("Basic", DisplayName = "--proxy-anyauth vs Basic")]
    [DataRow("Digest", DisplayName = "--proxy-anyauth vs Digest")]
    public async Task ConnectAsync_WithProxyAnyAuth_ReportsNoSchemeBeforeTheFirstConnectAndTheOfferedOneAfter(string scheme)
    {
        var events = new RecordingTransferEvents();
        var isBasic = scheme == "Basic";
        var (connector, _) = CreateAuthenticatingConnector(
            HttpAuthSchemes.Any,
            new ScriptedConnection(Encoding.Latin1.GetBytes(isBasic ? BasicChallengeClosing : DigestChallengeClosing)),
            new ScriptedConnection(Encoding.Latin1.GetBytes(EstablishedReply)));

        await connector.ConnectAsync(AuthenticatingTarget with { Events = events }, CancellationToken.None);

        AssertTranscript(
            Lines(
                [Trying, NoAlpnNegotiated, Establishing],
                RequestLines(UnauthenticatedConnect),
                isBasic ? BasicChallengeLines : DigestChallengeLines,
                ["* Connect me again please", Trying, NoAlpnNegotiated, $"* Proxy auth using {scheme} with user 'u'", Establishing],
                RequestLines(isBasic ? BasicConnect : DigestConnect),
                EstablishedLines),
            events.Transcript);
    }

    [TestMethod]
    [DataRow("Digest", DisplayName = "--proxy-digest, 407 twice")]
    [DataRow("Basic", DisplayName = "--proxy-anyauth vs Basic twice")]
    public async Task ConnectAsync_WhenTheAnswerIsChallengedAgain_ReportsTheProblemAfterTheSecondChallenge(string scheme)
    {
        var events = new RecordingTransferEvents();
        var isBasic = scheme == "Basic";
        var challenge = isBasic ? BasicChallengeClosing : DigestChallengeClosing;
        var challengeLines = isBasic ? BasicChallengeLines : DigestChallengeLines;
        var (connector, _) = CreateAuthenticatingConnector(
            isBasic ? HttpAuthSchemes.Any : HttpAuthSchemes.Digest,
            new ScriptedConnection(Encoding.Latin1.GetBytes(challenge)),
            new ScriptedConnection(Encoding.Latin1.GetBytes(challenge)));

        await connector.ConnectAsync(AuthenticatingTarget with { Events = events }, CancellationToken.None);

        AssertTranscript(
            Lines(
                isBasic ? [Trying, NoAlpnNegotiated, Establishing] : [Trying, NoAlpnNegotiated, "* Proxy auth using Digest with user 'u'", Establishing],
                RequestLines(UnauthenticatedConnect),
                challengeLines,
                ["* Connect me again please", Trying, NoAlpnNegotiated, $"* Proxy auth using {scheme} with user 'u'", Establishing],
                RequestLines(isBasic ? BasicConnect : DigestConnect),
                challengeLines[..2],
                [$"* {scheme} authentication problem, ignoring."],
                challengeLines[2..]),
            events.Transcript);
    }


    [TestMethod]
    public async Task ConnectAsync_InTheOpenSslBuild_ReportsAllocateConnectBufferOnEachProxyConnection()
    {
        // curl 8.18.0 (OpenSSL) -v -p -x http://127.0.0.1:18966 --proxy-anyauth -U u:p: allocate
        // connect buffer before the first CONNECT's lines; a redial opens a new tunnel filter, so
        // it comes again (BL-964 Notes).
        var events = new RecordingTransferEvents();
        var queue = new Queue<ScriptedConnection>([
            new ScriptedConnection(Encoding.Latin1.GetBytes(DigestChallengeClosing)),
            new ScriptedConnection(Encoding.Latin1.GetBytes(EstablishedReply))]);
        var connector = new TcpConnector(
            new FakeDnsResolver(ProxyAddress),
            new FakeTcpDialer { DialOutcome = _ => queue.Dequeue() },
            new FakeTlsProvider(),
            new ManualTimeProvider(),
            AuthenticatingOptions(HttpAuthSchemes.Digest, matchesSchannelBuild: false));

        await connector.ConnectAsync(AuthenticatingTarget with { Events = events }, CancellationToken.None);

        AssertTranscript(
            Lines(
                [Trying, NoAlpnNegotiated, "* allocate connect buffer", "* Proxy auth using Digest with user 'u'", Establishing],
                RequestLines(UnauthenticatedConnect),
                DigestChallengeLines,
                ["* Connect me again please", Trying, NoAlpnNegotiated, "* allocate connect buffer", "* Proxy auth using Digest with user 'u'", Establishing],
                RequestLines(DigestConnect),
                EstablishedLines),
            events.Transcript);
    }

    [TestMethod]
    public async Task ConnectAsync_InTheOpenSslBuildOnAKeptOpenConnection_ReportsAllocateConnectBufferOnce()
    {
        // A second CONNECT on the same connection reuses curl's tunnel state (tunnel_reinit), so
        // allocate connect buffer is not written again (curl 8.21.0's lib/cf-h1-proxy.c).
        var events = new RecordingTransferEvents();
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(
            "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Digest realm=\"r\", nonce=\"abc\", qop=\"auth\"\r\nContent-Length: 0\r\n\r\n"
            + EstablishedReply));
        var connector = new TcpConnector(
            new FakeDnsResolver(ProxyAddress),
            new FakeTcpDialer { DialOutcome = _ => connection },
            new FakeTlsProvider(),
            new ManualTimeProvider(),
            AuthenticatingOptions(HttpAuthSchemes.Digest, matchesSchannelBuild: false));

        await connector.ConnectAsync(AuthenticatingTarget with { Events = events }, CancellationToken.None);

        Assert.AreEqual(1, events.Transcript.Count(line => line == "* allocate connect buffer"));
        Assert.AreEqual("* allocate connect buffer", events.Transcript[2]);
        CollectionAssert.AreEqual(EstablishedLines, events.Transcript.TakeLast(4).ToArray());
    }

    private static void AssertTranscript(string[] expected, List<string> transcript) =>
        Assert.AreEqual(string.Join("\n", expected), string.Join("\n", transcript));

    private static string[] RequestLines(string head) => [.. head[..^2].Split("\r\n").Select(line => "> " + line)];

    private static string[] Lines(params string[][] parts) => [.. parts.SelectMany(part => part)];
}
