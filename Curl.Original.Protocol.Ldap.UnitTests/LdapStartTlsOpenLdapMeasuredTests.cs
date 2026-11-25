using System.Net;
using Curl.Original.Protocol.Ldap.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// <c>ldap://</c> StartTLS on the OpenLDAP dialect against a scripted server (BL-5908 and BL-5931, ADR-1366):
/// refusals, closes, failed handshakes and binds as curl 8.21.0 with OpenLDAP 2.6.15 answered them; the
/// malformed-reply case pins the port's own answer, not curl's.
/// </summary>
[TestClass]
public sealed class LdapStartTlsOpenLdapMeasuredTests
{
    private const string StartTlsRequest = "301d02010177188016312e332e362e312e342e312e313436362e3230303337";

    private const string BindV2AsMessage2 = "3012020102600d0201020404636e3d6180027077";

    private const string BindV3AsMessage2 = "3012020102600d0201030404636e3d6180027077";

    private const string UnbindAsMessage2 = "30050201024200";

    [TestMethod]
    public async Task ExecuteAsync_SslReqdServerWithoutACertificate_ExitsWith1UnsupportedProtocol()
    {
        var server = new ScriptedConnection(StartTlsResponse("02"));

        TransferResult result = await RunAsync(server, TransportSecurityLevel.Required);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UnsupportedProtocol, "Unsupported protocol"), result);
        Assert.AreEqual(StartTlsRequest + UnbindAsMessage2, Sent(server));
    }

    [TestMethod]
    public async Task ExecuteAsync_SslServerWithoutACertificate_BindsAsLdapV2InClearAndExitsWith0()
    {
        var server = new ScriptedConnection(StartTlsResponse("02"), BindResponse(2, "00"), SearchDone(3));

        TransferResult result = await RunAsync(server, TransportSecurityLevel.Try);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.StartsWith(StartTlsRequest + BindV2AsMessage2, Sent(server));
        Assert.AreEqual(0x02, server.Sent[40]);
    }

    [TestMethod]
    public async Task ExecuteAsync_UntrustedCertificate_ExitsWith64AndTheVerifyResultAfterAnUnbind()
    {
        const string message = "SSL certificate OpenSSL verify result: self-signed certificate (18)";
        var server = new ScriptedConnection(StartTlsResponse("00"));

        TransferResult result = await RunAsync(server, TransportSecurityLevel.Required, ConnectResult.Failed(CurlExitCode.PeerFailedVerification, message));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UseSslFailed, message), result);
        Assert.AreEqual(StartTlsRequest + UnbindAsMessage2, Sent(server));
    }

    [TestMethod]
    [DataRow(CurlExitCode.SslConnectError)]
    [DataRow(CurlExitCode.SslCipher)]
    public async Task ExecuteAsync_AnyHandshakeFailure_ExitsWith64(CurlExitCode handshakeExitCode)
    {
        var server = new ScriptedConnection(StartTlsResponse("00"));

        TransferResult result = await RunAsync(server, TransportSecurityLevel.Try, ConnectResult.Failed(handshakeExitCode, "TLS connect error: alert"));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UseSslFailed, "TLS connect error: alert"), result);
        Assert.AreEqual(StartTlsRequest + UnbindAsMessage2, Sent(server));
    }

    [TestMethod]
    public async Task ExecuteAsync_HandshakeFailsAndTheProviderDisposesItsConnection_UnbindsInPlaintextOnTheStillOpenConnection()
    {
        const string alert = "15030300020228";
        var server = new ScriptedConnection(StartTlsResponse("00"), Hex.Bytes(alert));
        var tls = new DisposingTlsProvider(server, Hex.Bytes(alert));

        TransferResult result = await RunAsync(server, TransportSecurityLevel.Required, tls, new NetworkCredential("cn=a", "pw"));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UseSslFailed, "TLS connect error: alert"), result);
        Assert.AreEqual(StartTlsRequest + alert + UnbindAsMessage2, Sent(server));
        Assert.AreEqual(alert, tls.Received);
        Assert.IsFalse(tls.ServerDisposedByTheProvider);
        Assert.IsTrue(server.IsDisposed);
    }

    [TestMethod]
    [DataRow(TransportSecurityLevel.Try, DisplayName = "--ssl")]
    [DataRow(TransportSecurityLevel.Required, DisplayName = "--ssl-reqd")]
    public async Task ExecuteAsync_StartTlsSucceedsWithoutCredentials_SearchesWithoutBinding(TransportSecurityLevel level)
    {
        var server = new ScriptedConnection(StartTlsResponse("00"));
        var secured = new ScriptedConnection(SearchDone(2));

        TransferResult result = await RunAsync(server, level, new FixedTlsProvider(ConnectResult.Connected(secured)), null);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(StartTlsRequest, Sent(server));
        Assert.StartsWith("30", Sent(secured));
        Assert.AreEqual("02010263", Sent(secured).Substring(4, 8));
        Assert.EndsWith("30050201034200", Sent(secured));
    }

    [TestMethod]
    public async Task ExecuteAsync_StartTlsSucceedsWithCredentials_BindsAsLdapV3OverTls()
    {
        var server = new ScriptedConnection(StartTlsResponse("00"));
        var secured = new ScriptedConnection(BindResponse(2, "00"), SearchDone(3));

        TransferResult result = await RunAsync(server, TransportSecurityLevel.Required, new FixedTlsProvider(ConnectResult.Connected(secured)), new NetworkCredential("cn=a", "pw"));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.StartsWith(BindV3AsMessage2, Sent(secured));
    }

    [TestMethod]
    public async Task ExecuteAsync_StartTlsSucceedsForASaslBindWithoutCredentials_StillAsksForTheMechanisms()
    {
        var server = new ScriptedConnection(StartTlsResponse("00"));
        var secured = new ScriptedConnection();
        var handler = new LdapProtocolHandler(
            new RecordingConnector(ConnectResult.Connected(server)),
            LdapDialect.OpenLdap,
            new FakeLogonTokenSource(0),
            new FixedTlsProvider(ConnectResult.Connected(secured)));

        await handler.ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("ldap://127.0.0.1:21900/dc=x"),
            Output = new MemoryStream(),
            Mail = new MailRequestOptions { LoginOptions = "AUTH=PLAIN" },
            SslLevel = TransportSecurityLevel.Required,
        });

        Assert.Contains(Convert.ToHexStringLower("supportedSASLMechanisms"u8), Sent(secured));
    }

    [TestMethod]
    public async Task ExecuteAsync_SslStartTlsRefusedWithoutCredentials_BindsAnonymously()
    {
        var server = new ScriptedConnection(StartTlsResponse("34"), BindResponse(2, "00"), SearchDone(3));

        TransferResult result = await RunAsync(server, TransportSecurityLevel.Try, new FixedTlsProvider(ConnectResult.Failed(CurlExitCode.SslConnectError, "unused")), null);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.StartsWith(StartTlsRequest + "300c020102600702010304008000", Sent(server));
    }

    [TestMethod]
    [DataRow("01", CurlExitCode.UseSslFailed, "Requested SSL level failed")]
    [DataRow("0c", CurlExitCode.UseSslFailed, "Requested SSL level failed")]
    [DataRow("34", CurlExitCode.UseSslFailed, "Requested SSL level failed")]
    [DataRow("35", CurlExitCode.UseSslFailed, "Requested SSL level failed")]
    [DataRow("31", CurlExitCode.LoginDenied, "Login denied")]
    [DataRow("32", CurlExitCode.RemoteAccessDenied, "Access denied to remote resource")]
    public async Task ExecuteAsync_SslReqdStartTlsRefused_FailsAsTheResultCodeMapsAfterAnUnbind(string resultCode, CurlExitCode exitCode, string message)
    {
        var server = new ScriptedConnection(StartTlsResponse(resultCode));

        TransferResult result = await RunAsync(server, TransportSecurityLevel.Required);

        Assert.AreEqual(TransferResult.Failure(exitCode, message), result);
        Assert.AreEqual(StartTlsRequest + UnbindAsMessage2, Sent(server));
    }

    [TestMethod]
    public async Task ExecuteAsync_SslStartTlsUnavailable_BindsAsLdapV3()
    {
        var server = new ScriptedConnection(StartTlsResponse("34"), BindResponse(2, "00"), SearchDone(3));

        TransferResult result = await RunAsync(server, TransportSecurityLevel.Try);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.StartsWith(StartTlsRequest + BindV3AsMessage2, Sent(server));
    }

    [TestMethod]
    [DataRow("34", BindV3AsMessage2, DisplayName = "unavailable")]
    [DataRow("02", BindV2AsMessage2, DisplayName = "protocolError")]
    public async Task ExecuteAsync_SslStartTlsRefusedByABindResponse_BindsAsAfterAnExtendedResponse(string resultCode, string bind)
    {
        var server = new ScriptedConnection(BindResponse(1, resultCode), BindResponse(2, "00"), SearchDone(3));

        TransferResult result = await RunAsync(server, TransportSecurityLevel.Try);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.StartsWith(StartTlsRequest + bind, Sent(server));
    }

    [TestMethod]
    [DataRow("34", CurlExitCode.UseSslFailed, "Requested SSL level failed")]
    [DataRow("02", CurlExitCode.UnsupportedProtocol, "Unsupported protocol")]
    public async Task ExecuteAsync_SslReqdStartTlsRefusedByABindResponse_FailsAsTheResultCodeMapsAfterAnUnbind(string resultCode, CurlExitCode exitCode, string message)
    {
        var server = new ScriptedConnection(BindResponse(1, resultCode));

        TransferResult result = await RunAsync(server, TransportSecurityLevel.Required);

        Assert.AreEqual(TransferResult.Failure(exitCode, message), result);
        Assert.AreEqual(StartTlsRequest + UnbindAsMessage2, Sent(server));
    }

    [TestMethod]
    [DataRow(TransportSecurityLevel.Try, DisplayName = "--ssl")]
    [DataRow(TransportSecurityLevel.Required, DisplayName = "--ssl-reqd")]
    public async Task ExecuteAsync_StartTlsAnsweredBySuccessInABindResponse_StartsTheHandshake(TransportSecurityLevel level)
    {
        var server = new ScriptedConnection(BindResponse(1, "00"));

        TransferResult result = await RunAsync(server, level, ConnectResult.Failed(CurlExitCode.SslConnectError, "TLS connect error: eof"));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UseSslFailed, "TLS connect error: eof"), result);
        Assert.AreEqual(StartTlsRequest + UnbindAsMessage2, Sent(server));
    }

    [TestMethod]
    [DataRow("300a02010164050401783000", DisplayName = "SearchResultEntry")]
    [DataRow("30080201017303040178", DisplayName = "SearchResultReference")]
    public async Task ExecuteAsync_SslReqdStartTlsAnsweredByASearchReply_AbandonsUnbindsAndExitsWith64(string reply)
    {
        var server = new ScriptedConnection(Hex.Bytes(reply));

        TransferResult result = await RunAsync(server, TransportSecurityLevel.Required);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UseSslFailed, "Requested SSL level failed"), result);
        Assert.AreEqual(StartTlsRequest + "3006020102500101" + "30050201034200", Sent(server));
    }

    [TestMethod]
    public async Task ExecuteAsync_SslStartTlsAnsweredByASearchReply_BindsAsLdapV3()
    {
        var server = new ScriptedConnection(Hex.Bytes("300a02010164050401783000"), BindResponse(2, "00"), SearchDone(3));

        TransferResult result = await RunAsync(server, TransportSecurityLevel.Try);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.StartsWith(StartTlsRequest + BindV3AsMessage2, Sent(server));
    }

    [TestMethod]
    [DataRow("3006020101040178", DisplayName = "a universal protocolOp")]
    [DataRow("30050201014200", DisplayName = "a primitive protocolOp")]
    public async Task ExecuteAsync_SslReqdStartTlsAnsweredByAMessageWithNoResult_FailsWithExit64SendingNothingMore(string reply)
    {
        var server = new ScriptedConnection(Hex.Bytes(reply));

        TransferResult result = await RunAsync(server, TransportSecurityLevel.Required);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UseSslFailed, "LDAP local: Can't contact LDAP server"), result);
        Assert.AreEqual(StartTlsRequest, Sent(server));
    }

    [TestMethod]
    [DataRow(TransportSecurityLevel.Try, DisplayName = "--ssl")]
    [DataRow(TransportSecurityLevel.Required, DisplayName = "--ssl-reqd")]
    public async Task ExecuteAsync_ServerClosesInsteadOfAnsweringStartTls_ExitsWith7SendingNothingMore(TransportSecurityLevel level)
    {
        var server = new ScriptedConnection();

        TransferResult result = await RunAsync(server, level);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.CouldntConnect, "LDAP local: connecting ldap_result Can't contact LDAP server"), result);
        Assert.AreEqual(StartTlsRequest, Sent(server));
    }

    [TestMethod]
    [DataRow("02", CurlExitCode.UnsupportedProtocol)]
    [DataRow("31", CurlExitCode.LoginDenied)]
    [DataRow("32", CurlExitCode.RemoteAccessDenied)]
    [DataRow("35", CurlExitCode.LdapCannotBind)]
    public async Task ExecuteAsync_TheLdapV2BindRefused_FailsAsTheResultCodeMapsAfterAnUnbind(string resultCode, CurlExitCode exitCode)
    {
        var server = new ScriptedConnection(StartTlsResponse("02"), BindResponse(2, resultCode));

        TransferResult result = await RunAsync(server, TransportSecurityLevel.Try);

        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual(StartTlsRequest + BindV2AsMessage2 + "30050201034200", Sent(server));
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutSslAnLdapV3BindAnsweredProtocolError_BindsAgainAsLdapV2()
    {
        var server = new ScriptedConnection(BindResponse(1, "02"), BindResponse(2, "00"), SearchDone(3));

        TransferResult result = await RunAsync(server, TransportSecurityLevel.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.StartsWith("3012020101600d0201030404636e3d6180027077" + BindV2AsMessage2, Sent(server));
    }

    [TestMethod]
    public async Task ExecuteAsync_SslStartTlsUnavailableThenProtocolErrorToTheLdapV3Bind_BindsAgainAsLdapV2()
    {
        var server = new ScriptedConnection(StartTlsResponse("34"), BindResponse(2, "02"), BindResponse(3, "00"), SearchDone(4));

        TransferResult result = await RunAsync(server, TransportSecurityLevel.Try);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.StartsWith(StartTlsRequest + BindV3AsMessage2 + "3012020103600d0201020404636e3d6180027077", Sent(server));
    }

    private static string Sent(ScriptedConnection server) => Convert.ToHexStringLower(server.Sent);

    private static byte[] StartTlsResponse(string resultCode) => Hex.Bytes($"300c02010178070a01{resultCode}04000400");

    private static byte[] BindResponse(int messageId, string resultCode) => Hex.Bytes($"300c02010{messageId}61070a01{resultCode}04000400");

    private static byte[] SearchDone(int messageId) => Hex.Bytes($"300c02010{messageId}65070a010004000400");

    private static Task<TransferResult> RunAsync(ScriptedConnection server, TransportSecurityLevel level, ConnectResult? handshake = null) =>
        RunAsync(server, level, new FixedTlsProvider(handshake ?? ConnectResult.Failed(CurlExitCode.SslConnectError, "unused")), new NetworkCredential("cn=a", "pw"));

    private static async Task<TransferResult> RunAsync(ScriptedConnection server, TransportSecurityLevel level, ITlsProvider tls, NetworkCredential? credentials)
    {
        var handler = new LdapProtocolHandler(new RecordingConnector(ConnectResult.Connected(server)), LdapDialect.OpenLdap, new FakeLogonTokenSource(0), tls);
        return await handler.ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("ldap://127.0.0.1:21900/dc=x"),
            Output = new MemoryStream(),
            Credentials = credentials,
            SslLevel = level,
        });
    }

    /// <summary>
    /// Fails the handshake as the real providers do: reads the server's bytes, writes <c>alert</c> and
    /// disposes the connection it was given.
    /// </summary>
    private sealed class DisposingTlsProvider(ScriptedConnection server, byte[] alert) : ITlsProvider
    {
        public string Received { get; private set; } = string.Empty;

        public bool ServerDisposedByTheProvider { get; private set; }

        public async ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, CancellationToken cancellationToken = default)
        {
            Assert.IsFalse(plaintext.IsSecure);
            Assert.IsNull(plaintext.RemoteEndPoint);
            Assert.IsNull(plaintext.LocalEndPoint);
            Assert.IsNull(plaintext.SocketHandle);
            var buffer = new byte[64];
            int read = await plaintext.ReadAsync(buffer, cancellationToken);
            Received = Convert.ToHexStringLower(buffer, 0, read);
            await plaintext.WriteAsync(alert, cancellationToken);
            await plaintext.FlushAsync(cancellationToken);
            await plaintext.DisposeAsync();
            ServerDisposedByTheProvider = server.IsDisposed;
            return ConnectResult.Failed(CurlExitCode.SslConnectError, "TLS connect error: alert");
        }
    }

    /// <summary>Answers every handshake with one fixed result.</summary>
    private sealed class FixedTlsProvider(ConnectResult result) : ITlsProvider
    {
        public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(result);
    }
}
