using System.Text;

using Curl.Authentication;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="TcpConnector" /> through a proxy that answers a CONNECT's Digest answer with
/// a <c>407</c> whose Digest challenge carries <c>stale=true</c>: curl 8.21.0 answers again with
/// the new nonce on a new connection, at most five times, then fails with exit 7 (measured with
/// <c>Record-CurlExchange.ps1</c> as the proxy; the commands and bytes are in BL-864's Notes).
/// </summary>
public sealed partial class TcpConnectorTests
{
    // The cnonces real curl sent for nonces "a" and "b" in the measured exchange, so each
    // response hash below is the one it sent too.
    private const string MeasuredClientNonceForA = "d287019335fcd47238a8b68d206b7c2b";

    private const string MeasuredClientNonceForB = "9063b3b916723d2bbffbb44838396fe1";

    private const string DigestConnectWithNonceA =
        "CONNECT example.test:80 HTTP/1.1\r\nHost: example.test:80\r\n"
        + "Proxy-Authorization: Digest username=\"u\", realm=\"r\", nonce=\"a\", uri=\"example.test:80\", cnonce=\"" + MeasuredClientNonceForA + "\", nc=00000001, qop=auth, response=\"261498e9636d101495da39e5ec43444f\"\r\n"
        + "User-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n";

    private const string DigestConnectWithNonceB =
        "CONNECT example.test:80 HTTP/1.1\r\nHost: example.test:80\r\n"
        + "Proxy-Authorization: Digest username=\"u\", realm=\"r\", nonce=\"b\", uri=\"example.test:80\", cnonce=\"" + MeasuredClientNonceForB + "\", nc=00000001, qop=auth, response=\"c9f932550dc5be461ee0942fc8ad7d7d\"\r\n"
        + "User-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n";

    [TestMethod]
    public async Task ConnectAsync_WithProxyDigest_WhenTheAnswerIsChallengedStale_AnswersAgainWithTheNewNonce()
    {
        // curl -s -S -p -x http://127.0.0.1:18864 -U u:p --proxy-digest http://example.test/: 407 nonce "a",
        // 407 nonce "b" stale=true, 200 -> three connections, the third answering nonce "b"; the tunnel opens.
        var events = new RecordingTransferEvents();
        var first = new ScriptedConnection(Encoding.Latin1.GetBytes(DigestChallengeWithNonce("a", stale: false)));
        var second = new ScriptedConnection(Encoding.Latin1.GetBytes(DigestChallengeWithNonce("b", stale: true)));
        var third = new ScriptedConnection(Encoding.Latin1.GetBytes(EstablishedReply));
        var (connector, dialer) = CreateStaleDigestConnector([MeasuredClientNonceForA, MeasuredClientNonceForB], first, second, third);

        var result = await ConnectLoggedAsync(connector, AuthenticatingTarget with { Events = events });

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreSame(third, result.Connection);
        Assert.AreEqual(UnauthenticatedConnect, Encoding.Latin1.GetString([.. first.Written]));
        Assert.AreEqual(DigestConnectWithNonceA, Encoding.Latin1.GetString([.. second.Written]));
        Assert.AreEqual(DigestConnectWithNonceB, Encoding.Latin1.GetString([.. third.Written]));
        Assert.HasCount(3, dialer.DialedEndPoints);
        Assert.AreEqual(2, events.Transcript.Count(line => line == "* Connect me again please"));
        Assert.IsFalse(events.Transcript.Any(line => line.Contains("authentication problem", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ConnectAsync_WithProxyDigest_AfterFiveStaleChallengesOnNewConnections_FailsWithCouldNotConnect()
    {
        // The same with every 407 after the first stale=true: curl answers nonces "a" to "e" on six
        // connections, writes "Connect me again please" a sixth time and fails: curl: (7) Could not connect to server.
        var events = new RecordingTransferEvents();
        var connections = new[] { new ScriptedConnection(Encoding.Latin1.GetBytes(DigestChallengeWithNonce("a", stale: false))) }
            .Concat("bcdefg".Select(nonce => new ScriptedConnection(Encoding.Latin1.GetBytes(DigestChallengeWithNonce(nonce.ToString(), stale: true)))))
            .ToArray();
        var (connector, dialer) = CreateStaleDigestConnector(["1", "2", "3", "4", "5", "6"], connections);

        var result = await ConnectLoggedAsync(connector, AuthenticatingTarget with { Events = events });

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Could not connect to server", result.ErrorMessage);
        Assert.HasCount(6, dialer.DialedEndPoints);
        Assert.AreEqual(6, events.Transcript.Count(line => line == "* Connect me again please"));
        Assert.IsTrue(connections[..6].All(connection => connection.IsDisposed));
        Assert.IsEmpty(connections[6].Written);
        StringAssert.Contains(Encoding.Latin1.GetString([.. connections[5].Written]), "nonce=\"e\"");
    }

    [TestMethod]
    public async Task ConnectAsync_WithProxyDigest_WhenAStaleAnswerIsChallengedWithoutStale_FailsWithTheResponse407()
    {
        // 407 nonce "a", 407 nonce "b" stale=true, 407 nonce "c" -> "Digest authentication problem, ignoring."
        // then curl: (7) CONNECT tunnel failed, response 407, after three connections.
        var events = new RecordingTransferEvents();
        var (connector, dialer) = CreateStaleDigestConnector(
            [MeasuredClientNonceForA, MeasuredClientNonceForB],
            new ScriptedConnection(Encoding.Latin1.GetBytes(DigestChallengeWithNonce("a", stale: false))),
            new ScriptedConnection(Encoding.Latin1.GetBytes(DigestChallengeWithNonce("b", stale: true))),
            new ScriptedConnection(Encoding.Latin1.GetBytes(DigestChallengeWithNonce("c", stale: false))));

        var result = await ConnectLoggedAsync(connector, AuthenticatingTarget with { Events = events });

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("CONNECT tunnel failed, response 407", result.ErrorMessage);
        Assert.HasCount(3, dialer.DialedEndPoints);
        Assert.AreEqual(1, events.Transcript.Count(line => line == "* Digest authentication problem, ignoring."));
    }

    [TestMethod]
    public async Task ConnectAsync_WithProxyBasic_WhenTheChallengeIsAStaleDigest_FailsWithTheResponse407()
    {
        // stale=true renews only a Digest answer; a Basic one challenged again is given up on as before.
        var first = new ScriptedConnection(Encoding.Latin1.GetBytes(DigestChallengeWithNonce("a", stale: true)));
        var (connector, dialer) = CreateAuthenticatingConnector(HttpAuthSchemes.Basic, first);

        var result = await ConnectLoggedAsync(connector, AuthenticatingTarget);

        Diagnostics.Assert("error message", "CONNECT tunnel failed, response 407", result.ErrorMessage);
        Assert.AreEqual("CONNECT tunnel failed, response 407", result.ErrorMessage);
        Assert.HasCount(1, dialer.DialedEndPoints);
    }

    private static string DigestChallengeWithNonce(string nonce, bool stale) =>
        $"HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Digest realm=\"r\", nonce=\"{nonce}\", qop=\"auth\"{(stale ? ", stale=true" : string.Empty)}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";

    private static (TcpConnector Connector, FakeTcpDialer Dialer) CreateStaleDigestConnector(string[] clientNonces, params ScriptedConnection[] connections)
    {
        var nonces = new Queue<string>(clientNonces);
        var queue = new Queue<ScriptedConnection>(connections);
        var dialer = new FakeTcpDialer { DialOutcome = _ => queue.Dequeue() };
        var options = HttpProxyTunnelOptions.Default with
        {
            ProxyAuthSchemes = HttpAuthSchemes.Digest,
            ProxyAuthenticator = new RankedHttpAuthenticator(
                new BasicAndBearerAuthenticator(Encoding.UTF8),
                new DigestAuthenticator(Encoding.UTF8, nonces.Dequeue),
                new NegotiateHttpAuthenticator(new SystemSecurityContextFactory()),
                new NtlmHttpAuthenticator(new SystemSecurityContextFactory(), matchesSspiBuild: false)),
        };
        return (new TcpConnector(new FakeDnsResolver(ProxyAddress), dialer, new FakeTlsProvider(), new ManualTimeProvider(), options), dialer);
    }
}
