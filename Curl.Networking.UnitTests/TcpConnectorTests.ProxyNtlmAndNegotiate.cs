using System.Net;
using System.Text;

using Curl.Authentication;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="TcpConnector" /> through a proxy that answers CONNECT with an NTLM or
/// Negotiate <c>407</c>, the handshake going on over the same connection, as curl 8.21.0 does
/// (measured with <c>Record-CurlExchange.ps1 -Script</c> for
/// <c>curl -s -S -v -p -x http://127.0.0.1:18604 --proxy-ntlm -U u:p http://example.test/</c>;
/// the bytes are in BL-604's Notes). The contexts come from a <see cref="ScriptedTokenSource" />
/// handing out curl's own Type 1 and Type 3 messages (the Type 3 curl 8.18.0 answered the same
/// Type 2 with, BL-526), so each CONNECT is byte for byte the one curl writes around them.
/// </summary>
public sealed partial class TcpConnectorTests
{
    private const string TunnelNtlmType1 = "TlRMTVNTUAABAAAAB4IIogAAAAAAAAAAAAAAAAAAAAAKAPRlAAAADw==";

    private const string TunnelNtlmType2 = "TlRMTVNTUAACAAAADAAMADgAAAAzgoriASNFZ4mrze8AAAAAAAAAACQAJABEAAAABgBwFwAAAA9TAGUAcgB2AGUAcgACAAwARABvAG0AYQBpAG4AAQAMAFMAZQByAHYAZQByAAAAAAAA";

    private const string TunnelNtlmType3 =
        "TlRMTVNTUAADAAAAGAAYAEAAAABUAFQAWAAAAAAAAACsAAAAAgACAKwAAAAWABYArgAAAAAAAAAAAAAAM4KK4iWAoKMN+kq8Eimq1kt9xV8afTTK0zM6hWRUG/gzNnfh/LfhEpJFHEgBAQAAAAAAAIAkZ3HbT90BGn00ytMzOoUAAAAAAgAMAEQAbwBtAGEAaQBuAAEADABTAGUAcgB2AGUAcgAAAAAAAAAAAHUAVwBPAFIASwBTAFQAQQBUAEkATwBOAA==";

    private const string NtlmChallenge =
        "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: NTLM " + TunnelNtlmType2 + "\r\nContent-Length: 0\r\n\r\n";

    private const string NegotiateNoCredentialsSspiLine =
        "InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package";

    private const string NtlmRejection =
        "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: NTLM\r\nContent-Length: 0\r\n\r\n";

    [TestMethod]
    public async Task ConnectAsync_WithProxyNtlm_SendsType1ThenType3OnTheSameConnection()
    {
        // Measured: Type 1 on the first CONNECT, Type 3 for the 407's Type 2 on the same connection, then the tunnel opens.
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(NtlmChallenge + EstablishedReply));
        var tokens = NtlmTunnelTokens();
        var (connector, dialer) = CreateTokenConnector(HttpAuthSchemes.Ntlm, tokens, matchesSspiBuild: false, connection);

        var result = await ConnectLoggedAsync(connector, AuthenticatingTarget);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreSame(connection, result.Connection);
        Assert.AreEqual(TokenConnect("NTLM " + TunnelNtlmType1) + TokenConnect("NTLM " + TunnelNtlmType3), Encoding.Latin1.GetString([.. connection.Written]));
        Assert.HasCount(1, dialer.DialedEndPoints);
        CollectionAssert.AreEqual(Convert.FromBase64String(TunnelNtlmType2), tokens.IncomingTokens[2]);
        Assert.IsTrue(tokens.Requests.All(request => request is { Mechanism: SecurityMechanism.Ntlm, ServiceName: "HTTP", HostName: "127.0.0.1", UserName: "u" }));
    }

    [TestMethod]
    public async Task ConnectAsync_WithProxyNtlm_WhenType3IsRejected_FailsWithTheSecond407()
    {
        // Measured: "NTLM handshake rejected", then curl: (7) CONNECT tunnel failed, response 407.
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(NtlmChallenge + NtlmRejection));
        var (connector, _) = CreateTokenConnector(HttpAuthSchemes.Ntlm, NtlmTunnelTokens(), matchesSspiBuild: false, connection);

        var result = await ConnectLoggedAsync(connector, AuthenticatingTarget);

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("CONNECT tunnel failed, response 407", result.ErrorMessage);
        Assert.AreEqual(TokenConnect("NTLM " + TunnelNtlmType1) + TokenConnect("NTLM " + TunnelNtlmType3), Encoding.Latin1.GetString([.. connection.Written]));
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ConnectAsync_WithProxyNtlm_WhenTheType2ClosesTheConnection_SendsType3OnANewOne()
    {
        var first = new ScriptedConnection(Encoding.Latin1.GetBytes(
            "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: NTLM " + TunnelNtlmType2 + "\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"));
        var second = new ScriptedConnection(Encoding.Latin1.GetBytes(NtlmRejection));
        var (connector, dialer) = CreateTokenConnector(HttpAuthSchemes.Ntlm, NtlmTunnelTokens(), matchesSspiBuild: false, first, second);

        var result = await ConnectLoggedAsync(connector, AuthenticatingTarget);

        // The Type 3 went out as an answer, so the bare NTLM 407 to it ends the handshake.
        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(TokenConnect("NTLM " + TunnelNtlmType3), Encoding.Latin1.GetString([.. second.Written]));
        Assert.IsTrue(first.IsDisposed);
        Assert.HasCount(2, dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenSspiCannotAnswerTheType2_FailsWithTheAuthenticatorsExitCode()
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(NtlmChallenge));
        var tokens = new ScriptedTokenSource(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, Convert.FromBase64String(TunnelNtlmType1)),
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, Convert.FromBase64String(TunnelNtlmType1)),
            new SecurityContextStep(SecurityContextStatus.Refused, []));
        var (connector, _) = CreateTokenConnector(HttpAuthSchemes.Ntlm, tokens, matchesSspiBuild: true, connection);

        var result = await ConnectLoggedAsync(connector, AuthenticatingTarget);

        Diagnostics.Assert("exit code", CurlExitCode.AuthError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.AuthError, result.ExitCode);
        Assert.AreEqual(NtlmHttpAuthenticator.AuthErrorMessage, result.ErrorMessage);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ConnectAsync_WithProxyNegotiate_SendsTheContextsNextTokenOnTheSameConnection()
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(
            "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Negotiate BAUG\r\nContent-Length: 0\r\n\r\n" + EstablishedReply));
        var tokens = new ScriptedTokenSource(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [1, 2, 3]),
            new SecurityContextStep(SecurityContextStatus.Completed, [7, 8, 9]));
        var (connector, _) = CreateTokenConnector(HttpAuthSchemes.Negotiate, tokens, matchesSspiBuild: false, connection);

        var result = await ConnectLoggedAsync(connector, AuthenticatingTarget);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(TokenConnect("Negotiate AQID") + TokenConnect("Negotiate BwgJ"), Encoding.Latin1.GetString([.. connection.Written]));
        CollectionAssert.AreEqual(new byte[] { 4, 5, 6 }, tokens.IncomingTokens[1]);
        Assert.AreEqual(new SecurityContextRequest(SecurityMechanism.Negotiate, "HTTP", "127.0.0.1") { UserName = "u", Password = "p" }, tokens.Requests.Single() with { Delegation = default });
        Assert.AreEqual(1, tokens.ContextsDisposed);
    }

    [TestMethod]
    public async Task ConnectAsync_WithProxyNegotiate_WhenTheTunnelOpensAtOnce_EndsTheKeptContext()
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(EstablishedReply));
        var tokens = new ScriptedTokenSource(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [1, 2, 3]));
        var (connector, _) = CreateTokenConnector(HttpAuthSchemes.Negotiate, tokens, matchesSspiBuild: false, connection);

        var result = await ConnectLoggedAsync(connector, AuthenticatingTarget);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(TokenConnect("Negotiate AQID"), Encoding.Latin1.GetString([.. connection.Written]));
        Assert.AreEqual(1, tokens.ContextsDisposed);
    }

    [TestMethod]
    public async Task ConnectAsync_WithProxyNegotiate_WhenANon407RefusesTheTunnel_EndsTheKeptContext()
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes("HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\n\r\n"));
        var tokens = new ScriptedTokenSource(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [1, 2, 3]));
        var (connector, _) = CreateTokenConnector(HttpAuthSchemes.Negotiate, tokens, matchesSspiBuild: false, connection);

        var result = await ConnectLoggedAsync(connector, AuthenticatingTarget);

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("CONNECT tunnel failed, response 403", result.ErrorMessage);
        Assert.AreEqual(1, tokens.ContextsDisposed);
    }

    [TestMethod]
    public async Task ConnectAsync_WithProxyAnyAuth_WhenNegotiateMakesNoToken_FailsWithThe407()
    {
        // The authenticator's empty answer (ADR-0232) sends no second CONNECT through a tunnel.
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(
            "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Negotiate\r\nContent-Length: 0\r\n\r\n"));
        var tokens = new ScriptedTokenSource(new SecurityContextStep(SecurityContextStatus.NoCredentials, []));
        var (connector, dialer) = CreateTokenConnector(HttpAuthSchemes.Any, tokens, matchesSspiBuild: false, connection);

        var result = await ConnectLoggedAsync(connector, AuthenticatingTarget);

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(UnauthenticatedConnect, Encoding.Latin1.GetString([.. connection.Written]));
        Assert.HasCount(1, dialer.DialedEndPoints);
        Assert.AreEqual(1, tokens.ContextsMade);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ConnectAsync_WithProxyNegotiate_WhenTheContextHasNoCredentials_FailsWithTheSspiFailureLine()
    {
        // curl 8.21.0's SSPI build writes the failure with failf, the error buffer's first (BL-1033 Notes).
        var (result, connection, events) = await ConnectRefusedNegotiateTunnelAsync();

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(NegotiateNoCredentialsSspiLine, result.ErrorMessage);
        Assert.AreEqual(UnauthenticatedConnect, Encoding.Latin1.GetString([.. connection.Written]));
        Assert.AreEqual(2, events.Info.Count(line => line == NegotiateNoCredentialsSspiLine));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ConnectAsync_WithProxyNegotiate_WhenTheContextHasNoCredentials_FailsWithThe407()
    {
        // curl 8.18.0's GSS-API build writes the failure with infof, so the 407 stays the message (BL-1033 Notes).
        var (result, connection, events) = await ConnectRefusedNegotiateTunnelAsync();

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("CONNECT tunnel failed, response 407", result.ErrorMessage);
        Assert.AreEqual(UnauthenticatedConnect, Encoding.Latin1.GetString([.. connection.Written]));
        Assert.AreEqual(2, events.Info.Count(line => line.StartsWith("gss_init_sec_context() failed: ", StringComparison.Ordinal)));
    }

    // curl -s -S -v -p -x http://127.0.0.1:18733 --proxy-negotiate -U : http://example.test/ against
    // a proxy answering 407 with a bare Negotiate challenge, the context wording failures as the platform's curl.
    private async Task<(ConnectResult Result, ScriptedConnection Connection, RecordingTransferEvents Events)> ConnectRefusedNegotiateTunnelAsync()
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(
            "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Negotiate\r\nContent-Length: 4\r\n\r\ndeny"));
        var tokens = new ScriptedTokenSource(
            new SecurityContextStep(SecurityContextStatus.NoCredentials, []),
            new SecurityContextStep(SecurityContextStatus.NoCredentials, []));
        var dialer = new FakeTcpDialer { DialOutcome = _ => connection };
        var options = HttpProxyTunnelOptions.Default with
        {
            ProxyAuthSchemes = HttpAuthSchemes.Negotiate,
            ProxyAuthenticator = new RankedHttpAuthenticator(
                new BasicAndBearerAuthenticator(Encoding.UTF8),
                new DigestAuthenticator(Encoding.UTF8, () => MeasuredClientNonce),
                new NegotiateHttpAuthenticator(tokens),
                new NtlmHttpAuthenticator(tokens, matchesSspiBuild: false)),
        };
        var connector = new TcpConnector(new FakeDnsResolver(ProxyAddress), dialer, new FakeTlsProvider(), new ManualTimeProvider(), options);
        var events = new RecordingTransferEvents();
        var proxy = AuthenticatingProxy with { Credential = new NetworkCredential(string.Empty, string.Empty) };

        var result = await ConnectLoggedAsync(connector, AuthenticatingTarget with { Proxy = proxy, Events = events });
        return (result, connection, events);
    }

    private static ScriptedTokenSource NtlmTunnelTokens() => new(
        new SecurityContextStep(SecurityContextStatus.ContinueNeeded, Convert.FromBase64String(TunnelNtlmType1)),
        new SecurityContextStep(SecurityContextStatus.ContinueNeeded, Convert.FromBase64String(TunnelNtlmType1)),
        new SecurityContextStep(SecurityContextStatus.Completed, Convert.FromBase64String(TunnelNtlmType3)));

    private static string TokenConnect(string proxyAuthorization) =>
        "CONNECT example.test:80 HTTP/1.1\r\nHost: example.test:80\r\nProxy-Authorization: " + proxyAuthorization
        + "\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n";

    // The proxy authenticator the composition builds, its NTLM and Negotiate contexts made by tokens.
    private static (TcpConnector Connector, FakeTcpDialer Dialer) CreateTokenConnector(
        HttpAuthSchemes schemes,
        ScriptedTokenSource tokens,
        bool matchesSspiBuild,
        params ScriptedConnection[] connections)
    {
        var queue = new Queue<ScriptedConnection>(connections);
        var dialer = new FakeTcpDialer { DialOutcome = _ => queue.Dequeue() };
        var options = HttpProxyTunnelOptions.Default with
        {
            ProxyAuthSchemes = schemes,
            ProxyAuthenticator = new RankedHttpAuthenticator(
                new BasicAndBearerAuthenticator(Encoding.UTF8),
                new DigestAuthenticator(Encoding.UTF8, () => MeasuredClientNonce),
                new NegotiateHttpAuthenticator(tokens, null, wordsFailuresAsSspi: true),
                new NtlmHttpAuthenticator(tokens, matchesSspiBuild)),
        };
        var connector = new TcpConnector(new FakeDnsResolver(ProxyAddress), dialer, new FakeTlsProvider(), new ManualTimeProvider(), options);
        return (connector, dialer);
    }
}
