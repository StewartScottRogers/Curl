using System.Net;
using Curl.Original.Protocol.Ldap.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// <c>ldap://</c> StartTLS on the OpenLDAP dialect against a scripted server (BL-5908, ADR-1366): refusals,
/// closes and binds as curl 8.21.0 with OpenLDAP 2.6.15 answered them; the two non-ExtendedResponse
/// cases pin the port's own answer, not curl's.
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
    public async Task ExecuteAsync_UntrustedCertificate_ExitsWith64AndTheVerifyResultWithoutBinding()
    {
        const string message = "SSL certificate OpenSSL verify result: self-signed certificate (18)";
        var server = new ScriptedConnection(StartTlsResponse("00"));

        TransferResult result = await RunAsync(server, TransportSecurityLevel.Required, ConnectResult.Failed(CurlExitCode.PeerFailedVerification, message));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UseSslFailed, message), result);
        Assert.AreEqual(StartTlsRequest, Sent(server));
    }

    [TestMethod]
    [DataRow(CurlExitCode.SslConnectError)]
    [DataRow(CurlExitCode.SslCipher)]
    public async Task ExecuteAsync_AnyHandshakeFailure_ExitsWith64(CurlExitCode handshakeExitCode)
    {
        var server = new ScriptedConnection(StartTlsResponse("00"));

        TransferResult result = await RunAsync(server, TransportSecurityLevel.Try, ConnectResult.Failed(handshakeExitCode, "TLS connect error: alert"));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UseSslFailed, "TLS connect error: alert"), result);
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
    public async Task ExecuteAsync_SslStartTlsAnsweredByANonExtendedResponse_BindsAsLdapV3()
    {
        var server = new ScriptedConnection(BindResponse(1, "00"), BindResponse(2, "00"), SearchDone(3));

        TransferResult result = await RunAsync(server, TransportSecurityLevel.Try);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.StartsWith(StartTlsRequest + BindV3AsMessage2, Sent(server));
    }

    [TestMethod]
    public async Task ExecuteAsync_SslReqdStartTlsAnsweredByANonExtendedResponse_FailsWithExit64SendingNothingMore()
    {
        var server = new ScriptedConnection(BindResponse(1, "00"));

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

    private static async Task<TransferResult> RunAsync(ScriptedConnection server, TransportSecurityLevel level, ConnectResult? handshake = null)
    {
        var tls = new FixedTlsProvider(handshake ?? ConnectResult.Failed(CurlExitCode.SslConnectError, "unused"));
        var handler = new LdapProtocolHandler(new RecordingConnector(ConnectResult.Connected(server)), LdapDialect.OpenLdap, new FakeLogonTokenSource(0), tls);
        return await handler.ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("ldap://127.0.0.1:21900/dc=x"),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("cn=a", "pw"),
            SslLevel = level,
        });
    }

    /// <summary>Answers every handshake with one fixed result.</summary>
    private sealed class FixedTlsProvider(ConnectResult result) : ITlsProvider
    {
        public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(result);
    }
}
