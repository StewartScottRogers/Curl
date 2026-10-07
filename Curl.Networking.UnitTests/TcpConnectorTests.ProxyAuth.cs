using System.Net;
using System.Net.Sockets;
using System.Text;

using Curl.Authentication;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="TcpConnector" /> through a proxy that answers CONNECT with <c>407</c>:
/// Basic is sent up front, Digest and <c>--proxy-anyauth</c> answer the challenge on the same
/// connection or a new one, and a second <c>407</c> fails, as curl 8.21.0 does (measured with
/// <c>Record-CurlExchange.ps1 -Connections 3</c>; the commands and bytes are in BL-602's Notes).
/// </summary>
public sealed partial class TcpConnectorTests
{
    // The cnonce real curl sent in the measured --proxy-digest exchange, so the response hash
    // below is the one it sent too.
    private const string MeasuredClientNonce = "7f052e869469cb30acc85773266dfe11";

    private const string BasicChallengeClosing =
        "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Basic realm=\"r\"\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";

    private const string DigestChallengeClosing =
        "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Digest realm=\"r\", nonce=\"abc\", qop=\"auth\"\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";

    private const string UnauthenticatedConnect =
        "CONNECT example.test:80 HTTP/1.1\r\nHost: example.test:80\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n";

    // curl's own Digest code's format (ADR-0025) with the measured cnonce and response.
    private const string DigestConnect =
        "CONNECT example.test:80 HTTP/1.1\r\nHost: example.test:80\r\n"
        + "Proxy-Authorization: Digest username=\"u\", realm=\"r\", nonce=\"abc\", uri=\"example.test:80\", cnonce=\"" + MeasuredClientNonce + "\", nc=00000001, qop=auth, response=\"53a4df5585ad72bb9377dd4c05f58fc1\"\r\n"
        + "User-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n";

    private const string BasicConnect =
        "CONNECT example.test:80 HTTP/1.1\r\nHost: example.test:80\r\nProxy-Authorization: Basic dTpw\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n";

    private static readonly ProxyEndpoint AuthenticatingProxy = new(ProxyKind.Http, "127.0.0.1", 18602, new NetworkCredential("u", "p"));

    private static readonly ConnectTarget AuthenticatingTarget = new("example.test", 80, UseTls: false) { Proxy = AuthenticatingProxy };

    [TestMethod]
    public async Task ConnectAsync_WithProxyBasic_SendsBasicUpFrontAndFailsOnA407()
    {
        // curl -p -x http://127.0.0.1:18602 -U u:p --proxy-basic http://example.test/ -> curl: (7) CONNECT tunnel failed, response 407
        var first = new ScriptedConnection(Encoding.Latin1.GetBytes(BasicChallengeClosing));
        var (connector, dialer) = CreateAuthenticatingConnector(HttpAuthSchemes.Basic, first);

        var result = await ConnectLoggedAsync(connector, AuthenticatingTarget);

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("CONNECT tunnel failed, response 407", result.ErrorMessage);
        Assert.AreEqual(BasicConnect, Encoding.Latin1.GetString([.. first.Written]));
        Assert.IsTrue(first.IsDisposed);
        Assert.HasCount(1, dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_WithProxyDigest_AnswersTheChallengeOnANewConnectionWhenTheProxyCloses()
    {
        // curl -p -x http://127.0.0.1:18602 -U u:p --proxy-digest http://example.test/: "Connect me again please", then the tunnel opens.
        var first = new ScriptedConnection(Encoding.Latin1.GetBytes(DigestChallengeClosing));
        var second = new ScriptedConnection(Encoding.Latin1.GetBytes(EstablishedReply));
        var (connector, dialer) = CreateAuthenticatingConnector(HttpAuthSchemes.Digest, first, second);

        var result = await ConnectLoggedAsync(connector, AuthenticatingTarget);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreSame(second, result.Connection);
        Assert.AreEqual(200, result.ProxyConnectResponseCode);
        Assert.AreEqual(UnauthenticatedConnect, Encoding.Latin1.GetString([.. first.Written]));
        Assert.AreEqual(DigestConnect, Encoding.Latin1.GetString([.. second.Written]));
        Assert.IsTrue(first.IsDisposed);
        Assert.IsFalse(second.IsDisposed);
        Assert.HasCount(2, dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_WithProxyDigest_AnswersTheChallengeOnTheSameConnectionWhenItStaysOpen()
    {
        // Measured without Connection: close: curl sent the answer on the connection it had.
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(
            "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Digest realm=\"r\", nonce=\"abc\", qop=\"auth\"\r\nContent-Length: 6\r\n\r\ndenied"
            + EstablishedReply));
        var (connector, dialer) = CreateAuthenticatingConnector(HttpAuthSchemes.Digest, connection);

        var result = await ConnectLoggedAsync(connector, AuthenticatingTarget);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreSame(connection, result.Connection);
        Assert.AreEqual(UnauthenticatedConnect + DigestConnect, Encoding.Latin1.GetString([.. connection.Written]));
        Assert.AreEqual(0, connection.UnreadCount);
        Assert.HasCount(1, dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_WithProxyDigest_AfterAChunked407_AnswersTheChallengeOnTheSameConnection()
    {
        // Measured (BL-862): curl -p -x http://127.0.0.1:18862 -U u:p --proxy-digest http://example.test/ against a
        // chunked 407 with no Connection: close - "Ignore chunked response-body", one connection, both CONNECTs on it.
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(
            "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Digest realm=\"r\", nonce=\"abc\", qop=\"auth\"\r\nTransfer-Encoding: chunked\r\n\r\n"
            + "5;ext=1\r\nhello\r\n3\r\nabc\r\n0\r\nX-Trailer: t\r\n\r\n"
            + EstablishedReply));
        var (connector, dialer) = CreateAuthenticatingConnector(HttpAuthSchemes.Digest, connection);

        var result = await ConnectLoggedAsync(connector, AuthenticatingTarget);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreSame(connection, result.Connection);
        Assert.AreEqual(UnauthenticatedConnect + DigestConnect, Encoding.Latin1.GetString([.. connection.Written]));
        Assert.AreEqual(0, connection.UnreadCount);
        Assert.IsFalse(connection.IsDisposed);
        Assert.HasCount(1, dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_WithProxyDigest_AfterAMalformedChunked407_FailsWithExit56()
    {
        // Measured (BL-862): curl: (56) chunk hex-length char not a hex digit: 0x7a, no second CONNECT, no second connection.
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(
            "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Digest realm=\"r\", nonce=\"abc\", qop=\"auth\"\r\nTransfer-Encoding: chunked\r\n\r\n"
            + "zz\r\nhello\r\n0\r\n\r\n"));
        var (connector, dialer) = CreateAuthenticatingConnector(HttpAuthSchemes.Digest, connection);

        var result = await ConnectLoggedAsync(connector, AuthenticatingTarget);

        Diagnostics.Assert("exit code", CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("chunk hex-length char not a hex digit: 0x7a", result.ErrorMessage);
        Assert.AreEqual(UnauthenticatedConnect, Encoding.Latin1.GetString([.. connection.Written]));
        Assert.IsTrue(connection.IsDisposed);
        Assert.HasCount(1, dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenAKeptOpenChallengesBodyIsCutShort_AnswersOnANewConnection()
    {
        var first = new ScriptedConnection(Encoding.Latin1.GetBytes(
            "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Digest realm=\"r\", nonce=\"abc\", qop=\"auth\"\r\nContent-Length: 5\r\n\r\nden"));
        var second = new ScriptedConnection(Encoding.Latin1.GetBytes(EstablishedReply));
        var (connector, _) = CreateAuthenticatingConnector(HttpAuthSchemes.Digest, first, second);

        var result = await ConnectLoggedAsync(connector, AuthenticatingTarget);

        Diagnostics.Assert("connection is the second one", true, ReferenceEquals(second, result.Connection));
        Assert.AreSame(second, result.Connection);
        Assert.AreEqual(DigestConnect, Encoding.Latin1.GetString([.. second.Written]));
        Assert.IsTrue(first.IsDisposed);
    }

    [TestMethod]
    [DataRow(BasicChallengeClosing, BasicConnect)]
    [DataRow(DigestChallengeClosing, DigestConnect)]
    public async Task ConnectAsync_WithProxyAnyAuth_SendsNothingUpFrontThenAnswersTheOfferedScheme(string challenge, string expectedSecondConnect)
    {
        // curl -p -x http://127.0.0.1:18602 -U u:p --proxy-anyauth http://example.test/ against Basic and against Digest.
        var first = new ScriptedConnection(Encoding.Latin1.GetBytes(challenge));
        var second = new ScriptedConnection(Encoding.Latin1.GetBytes(EstablishedReply));
        var (connector, _) = CreateAuthenticatingConnector(HttpAuthSchemes.Any, first, second);

        var result = await ConnectLoggedAsync(connector, AuthenticatingTarget);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(UnauthenticatedConnect, Encoding.Latin1.GetString([.. first.Written]));
        Assert.AreEqual(expectedSecondConnect, Encoding.Latin1.GetString([.. second.Written]));
    }

    [TestMethod]
    [DataRow(SocketError.ConnectionReset)]
    [DataRow(SocketError.ConnectionAborted)]
    public async Task ConnectAsync_WithProxyAnyAuth_WhenTheSecondConnectsReplyReadFails_FailsWithTheRecvFailure(SocketError socketError)
    {
        // BL-1449: a 407 with no Content-Length leaves the connection reusable, so curl 8.21.0 sends the
        // answer on it; the proxy had closed it, and curl: (56) Recv failure: Connection was reset.
        var failure = new IOException("Unable to read data from the transport connection.", new SocketException((int)socketError));
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(
            "HTTP/1.1 407 Need\r\nProxy-Authenticate: Basic realm=\"r\"\r\n\r\n"))
        {
            ExceptionAfterScript = failure,
        };
        var (connector, dialer) = CreateAuthenticatingConnector(HttpAuthSchemes.Any, connection);

        var result = await ConnectLoggedAsync(connector, AuthenticatingTarget);

        Diagnostics.Assert("exit code", CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(CurlSocketErrorText.ReceiveFailure(failure), result.ErrorMessage);
        Assert.AreEqual(UnauthenticatedConnect + BasicConnect, Encoding.Latin1.GetString([.. connection.Written]));
        Assert.IsTrue(connection.IsDisposed);
        Assert.HasCount(1, dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_WithProxyDigest_WhenTheAnswerIsChallengedAgain_FailsWithTheSecond407()
    {
        // curl -p -x http://127.0.0.1:18602 -U u:p --proxy-digest http://example.test/, 407 twice ->
        // "Digest authentication problem, ignoring." then curl: (7) CONNECT tunnel failed, response 407
        var first = new ScriptedConnection(Encoding.Latin1.GetBytes(DigestChallengeClosing));
        var second = new ScriptedConnection(Encoding.Latin1.GetBytes(DigestChallengeClosing));
        var (connector, dialer) = CreateAuthenticatingConnector(HttpAuthSchemes.Digest, first, second);

        var result = await ConnectLoggedAsync(connector, AuthenticatingTarget);

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("CONNECT tunnel failed, response 407", result.ErrorMessage);
        Assert.AreEqual(DigestConnect, Encoding.Latin1.GetString([.. second.Written]));
        Assert.IsTrue(first.IsDisposed);
        Assert.IsTrue(second.IsDisposed);
        Assert.HasCount(2, dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_WithProxyDigestAndNoCredential_FailsWithTheFirst407()
    {
        var first = new ScriptedConnection(Encoding.Latin1.GetBytes(DigestChallengeClosing));
        var (connector, dialer) = CreateAuthenticatingConnector(HttpAuthSchemes.Digest, first);

        var result = await ConnectLoggedAsync(
            connector,
            AuthenticatingTarget with { Proxy = AuthenticatingProxy with { Credential = null } });

        Diagnostics.Assert("error message", "CONNECT tunnel failed, response 407", result.ErrorMessage);
        Assert.AreEqual("CONNECT tunnel failed, response 407", result.ErrorMessage);
        Assert.HasCount(1, dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughAnHttpsProxy_AnswersAChallengeOnANewTlsConnection()
    {
        var firstTls = new ScriptedConnection(Encoding.Latin1.GetBytes(DigestChallengeClosing));
        var secondTls = new ScriptedConnection(Encoding.Latin1.GetBytes(EstablishedReply));
        var tlsProvider = new SequencedTlsProvider(ConnectResult.Connected(firstTls), ConnectResult.Connected(secondTls));
        var dialer = new FakeTcpDialer { DialOutcome = _ => new ScriptedConnection([]) };
        var connector = new TcpConnector(new FakeDnsResolver(ProxyAddress), dialer, tlsProvider, new ManualTimeProvider(), AuthenticatingOptions(HttpAuthSchemes.Digest));

        var result = await ConnectLoggedAsync(
            connector,
            AuthenticatingTarget with { Proxy = new ProxyEndpoint(ProxyKind.Https, "localhost", 18602, new NetworkCredential("u", "p")) });

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreSame(secondTls, result.Connection);
        Assert.AreEqual(DigestConnect, Encoding.Latin1.GetString([.. secondTls.Written]));
        Assert.IsTrue(firstTls.IsDisposed);
        CollectionAssert.AreEqual(new[] { "localhost", "localhost" }, tlsProvider.ReceivedTargetHosts);
    }

    private static (TcpConnector Connector, FakeTcpDialer Dialer) CreateAuthenticatingConnector(HttpAuthSchemes schemes, params ScriptedConnection[] connections)
    {
        var queue = new Queue<ScriptedConnection>(connections);
        var dialer = new FakeTcpDialer { DialOutcome = _ => queue.Dequeue() };
        var connector = new TcpConnector(new FakeDnsResolver(ProxyAddress), dialer, new FakeTlsProvider(), new ManualTimeProvider(), AuthenticatingOptions(schemes));
        return (connector, dialer);
    }

    // The proxy authenticator the composition builds, with the measured cnonce.
    // The Schannel build's -v lines unless matchesSchannelBuild says otherwise, so a transcript pins one build on every platform.
    private static HttpProxyTunnelOptions AuthenticatingOptions(HttpAuthSchemes schemes, bool matchesSchannelBuild = true) =>
        HttpProxyTunnelOptions.Default with
        {
            ProxyAuthSchemes = schemes,
            MatchesSchannelBuild = matchesSchannelBuild,
            ProxyAuthenticator = new RankedHttpAuthenticator(
                new BasicAndBearerAuthenticator(Encoding.UTF8),
                new DigestAuthenticator(Encoding.UTF8, () => MeasuredClientNonce),
                new NegotiateHttpAuthenticator(new SystemSecurityContextFactory()),
                new NtlmHttpAuthenticator(new SystemSecurityContextFactory(), matchesSspiBuild: false)),
        };
}
