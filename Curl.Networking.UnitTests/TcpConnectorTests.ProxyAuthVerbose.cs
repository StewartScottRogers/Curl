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
/// Schannel build unless a test says otherwise. The <c>CONNECT: no ALPN negotiated</c> line
/// before a plain proxy's CONNECT is not reported yet.
/// </summary>
public sealed partial class TcpConnectorTests
{
    private const string Trying = "*   Trying 192.0.2.10:18602...";

    private const string Establishing = "* Establishing HTTP proxy tunnel to example.test:80";

    private static readonly string[] EstablishedLines =
        ["< HTTP/1.1 200 Connection established", "< ", "* CONNECT phase completed for HTTP proxy", "* CONNECT tunnel established, response 200"];

    private static readonly string[] BasicChallengeLines =
        ["< HTTP/1.1 407 Proxy Authentication Required", "< Proxy-Authenticate: Basic realm=\"r\"", "< Content-Length: 0", "< Connection: close", "< "];

    private static readonly string[] DigestChallengeLines =
        ["< HTTP/1.1 407 Proxy Authentication Required", "< Proxy-Authenticate: Digest realm=\"r\", nonce=\"abc\", qop=\"auth\"", "< Content-Length: 0", "< Connection: close", "< "];

    [TestMethod]
    public async Task ConnectAsync_WithProxyBasic_ReportsTheBasicLinesAndTheProblemAfterTheChallenge()
    {
        var events = new RecordingTransferEvents();
        var (connector, _) = CreateAuthenticatingConnector(HttpAuthSchemes.Basic, new ScriptedConnection(Encoding.Latin1.GetBytes(BasicChallengeClosing)));

        await connector.ConnectAsync(AuthenticatingTarget with { Events = events }, CancellationToken.None);

        AssertTranscript(
            Lines(
                [Trying, "* Proxy auth using Basic with user 'u'", Establishing],
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
                [Trying, "* Proxy auth using Digest with user 'u'", Establishing],
                RequestLines(UnauthenticatedConnect),
                DigestChallengeLines,
                ["* Connect me again please", Trying, "* Proxy auth using Digest with user 'u'", Establishing],
                RequestLines(DigestConnect),
                EstablishedLines),
            events.Transcript);
    }

    [TestMethod]
    public async Task ConnectAsync_WithProxyDigestOnAKeptOpenConnection_ReportsBothConnectsWithoutConnectingAgain()
    {
        var events = new RecordingTransferEvents();
        var (connector, _) = CreateAuthenticatingConnector(
            HttpAuthSchemes.Digest,
            new ScriptedConnection(Encoding.Latin1.GetBytes(
                "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Digest realm=\"r\", nonce=\"abc\", qop=\"auth\"\r\nContent-Length: 6\r\n\r\ndenied"
                + EstablishedReply)));

        await connector.ConnectAsync(AuthenticatingTarget with { Events = events }, CancellationToken.None);

        AssertTranscript(
            Lines(
                [Trying, "* Proxy auth using Digest with user 'u'", Establishing],
                RequestLines(UnauthenticatedConnect),
                ["< HTTP/1.1 407 Proxy Authentication Required", "< Proxy-Authenticate: Digest realm=\"r\", nonce=\"abc\", qop=\"auth\"", "< Content-Length: 6", "< "],
                ["* Proxy auth using Digest with user 'u'", Establishing],
                RequestLines(DigestConnect),
                EstablishedLines),
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
                [Trying, Establishing],
                RequestLines(UnauthenticatedConnect),
                isBasic ? BasicChallengeLines : DigestChallengeLines,
                ["* Connect me again please", Trying, $"* Proxy auth using {scheme} with user 'u'", Establishing],
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
                isBasic ? [Trying, Establishing] : [Trying, "* Proxy auth using Digest with user 'u'", Establishing],
                RequestLines(UnauthenticatedConnect),
                challengeLines,
                ["* Connect me again please", Trying, $"* Proxy auth using {scheme} with user 'u'", Establishing],
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
                [Trying, "* allocate connect buffer", "* Proxy auth using Digest with user 'u'", Establishing],
                RequestLines(UnauthenticatedConnect),
                DigestChallengeLines,
                ["* Connect me again please", Trying, "* allocate connect buffer", "* Proxy auth using Digest with user 'u'", Establishing],
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
        Assert.AreEqual("* allocate connect buffer", events.Transcript[1]);
        CollectionAssert.AreEqual(EstablishedLines, events.Transcript.TakeLast(4).ToArray());
    }

    private static void AssertTranscript(string[] expected, List<string> transcript) =>
        Assert.AreEqual(string.Join("\n", expected), string.Join("\n", transcript));

    private static string[] RequestLines(string head) => [.. head[..^2].Split("\r\n").Select(line => "> " + line)];

    private static string[] Lines(params string[][] parts) => [.. parts.SelectMany(part => part)];
}
