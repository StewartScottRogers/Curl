using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ldap.Fakes;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Pins CGR-013 (BL-3542): <c>--ssl-reqd</c> and <c>--ssl</c> on a plaintext <c>ldap</c> URL never
/// send the simple bind in clear where curl does not. The WinLDAP build refuses <c>--ssl-reqd</c>
/// with <c>curl: (4) LDAP local: explicit TLS not supported</c> after connecting and before it
/// sends a byte (curl 8.22.0 Schannel, measured 2026-10-26); the OpenLDAP build sends the
/// StartTLS extended request in plaintext and the bind, search and unbind inside TLS.
/// </summary>
[TestClass]
public sealed class Cgr013Tests
{
    /// <summary>The StartTLS ExtendedRequest, messageID 1, as curl 8.18.0's OpenLDAP build sent it (measured 2026-10-26).</summary>
    private const string StartTlsRequest = "301d02010177188016312e332e362e312e342e312e313436362e3230303337";

    private static readonly byte[] StartTlsAccepted = Hex.Bytes("300c02010178070a010004000400");

    private static readonly byte[] StartTlsUnavailable = Hex.Bytes("300c02010178070a013404000400");

    private static readonly byte[] BindSucceeded = Hex.Bytes("300c02010261070a010004000400");

    private static readonly byte[] SearchDone = Hex.Bytes("300c02010365070a010004000400");

    [TestMethod]
    [DataRow(TransportSecurityLevel.Required, DisplayName = "--ssl-reqd")]
    public async Task ExecuteAsync_WinLdapWithSslRequired_FailsWithExit4BeforeSendingAByte(TransportSecurityLevel level)
    {
        var connection = new ScriptedConnection();
        var events = new RecordingTransferEvents();

        TransferResult result = await Handler(LdapDialect.WinLdap, connection, null).ExecuteAsync(Context("ldap", level, events));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.NotBuiltIn, "LDAP local: explicit TLS not supported"), result);
        Assert.IsEmpty(connection.Sent);
        Assert.IsTrue(connection.IsDisposed);
        CollectionAssert.IsSubsetOf(new[] { "LDAP local: trying to establish cleartext connection", "LDAP local: explicit TLS not supported" }, events.Lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapWithSslTry_BindsInPlaintextAsWinLdapDoes()
    {
        var connection = new ScriptedConnection();

        TransferResult result = await Handler(LdapDialect.WinLdap, connection, null).ExecuteAsync(Context("ldap", TransportSecurityLevel.Try, new RecordingTransferEvents()));

        Assert.AreNotEqual(CurlExitCode.NotBuiltIn, result.ExitCode);
        Assert.Contains("636e3d61646d696e8006736563726574", Convert.ToHexStringLower(connection.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapLdapsWithSslRequired_IsNotRefused()
    {
        var connection = new ScriptedConnection();

        TransferResult result = await Handler(LdapDialect.WinLdap, connection, null).ExecuteAsync(Context("ldaps", TransportSecurityLevel.Required, new RecordingTransferEvents()));

        Assert.AreNotEqual(CurlExitCode.NotBuiltIn, result.ExitCode);
        Assert.IsNotEmpty(connection.Sent);
    }

    [TestMethod]
    [DataRow(TransportSecurityLevel.Required, DisplayName = "--ssl-reqd")]
    [DataRow(TransportSecurityLevel.Try, DisplayName = "--ssl")]
    public async Task ExecuteAsync_OpenLdapWithSsl_SendsStartTlsThenBindsInsideTls(TransportSecurityLevel level)
    {
        var plaintext = new ScriptedConnection(StartTlsAccepted);
        var secured = new ScriptedConnection(BindSucceeded, SearchDone);
        var tls = new FakeTlsProvider(ConnectResult.Connected(secured));

        TransferResult result = await Handler(LdapDialect.OpenLdap, plaintext, tls).ExecuteAsync(Context("ldap", level, new RecordingTransferEvents()));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(StartTlsRequest, Convert.ToHexStringLower(plaintext.Sent));
        Assert.AreSame(plaintext, tls.Upgraded);
        Assert.AreEqual("127.0.0.1", tls.TargetHost);
        Assert.AreEqual(0x60, secured.Sent[5]);
        Assert.IsTrue(secured.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_OpenLdapWithSslRequiredAndStartTlsRefused_FailsWithExit64WithoutBinding()
    {
        var plaintext = new ScriptedConnection(StartTlsUnavailable);

        TransferResult result = await Handler(LdapDialect.OpenLdap, plaintext, new FakeTlsProvider(ConnectResult.Failed(CurlExitCode.SslConnectError, "unused"))).ExecuteAsync(Context("ldap", TransportSecurityLevel.Required, new RecordingTransferEvents()));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UseSslFailed, "LDAP local: Server is unavailable"), result);
        Assert.AreEqual(StartTlsRequest, Convert.ToHexStringLower(plaintext.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_OpenLdapWithSslRequiredAndServerClosed_FailsWithExit64()
    {
        var plaintext = new ScriptedConnection();

        TransferResult result = await Handler(LdapDialect.OpenLdap, plaintext, null).ExecuteAsync(Context("ldap", TransportSecurityLevel.Required, new RecordingTransferEvents()));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UseSslFailed, "LDAP local: Can't contact LDAP server"), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_OpenLdapWithSslTryAndStartTlsRefused_BindsInPlaintext()
    {
        var plaintext = new ScriptedConnection(StartTlsUnavailable, BindSucceeded, SearchDone);

        TransferResult result = await Handler(LdapDialect.OpenLdap, plaintext, null).ExecuteAsync(Context("ldap", TransportSecurityLevel.Try, new RecordingTransferEvents()));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.StartsWith(StartTlsRequest + "30", Convert.ToHexStringLower(plaintext.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_OpenLdapWhenTheTlsHandshakeFails_ReturnsItsFailure()
    {
        var plaintext = new ScriptedConnection(StartTlsAccepted);
        var tls = new FakeTlsProvider(ConnectResult.Failed(CurlExitCode.SslConnectError, "handshake failed"));

        TransferResult result = await Handler(LdapDialect.OpenLdap, plaintext, tls).ExecuteAsync(Context("ldap", TransportSecurityLevel.Required, new RecordingTransferEvents()));

        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual("handshake failed", result.ErrorMessage);
        Assert.AreEqual(StartTlsRequest, Convert.ToHexStringLower(plaintext.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_OpenLdapStartTlsAcceptedWithoutATlsProvider_FailsWithExit64WithoutBinding()
    {
        var plaintext = new ScriptedConnection(StartTlsAccepted);

        TransferResult result = await Handler(LdapDialect.OpenLdap, plaintext, null).ExecuteAsync(Context("ldap", TransportSecurityLevel.Required, new RecordingTransferEvents()));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UseSslFailed, "LDAP local: no TLS provider to start TLS with"), result);
        Assert.AreEqual(StartTlsRequest, Convert.ToHexStringLower(plaintext.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_OpenLdapWithoutSsl_SendsNoStartTls()
    {
        var plaintext = new ScriptedConnection(BindSucceeded, SearchDone);

        await Handler(LdapDialect.OpenLdap, plaintext, null).ExecuteAsync(Context("ldap", TransportSecurityLevel.None, new RecordingTransferEvents()));

        Assert.AreEqual(0x60, plaintext.Sent[5]);
    }

    [TestMethod]
    [DataRow("300c02010278070a010004000400", DisplayName = "another messageID")]
    [DataRow("300c02010161070a010004000400", DisplayName = "a BindResponse")]
    [DataRow("300c02010178070a01ff04000400", DisplayName = "a negative result code")]
    [DataRow("3010020101780b0a05008000000004000400", DisplayName = "a result code past int.MaxValue")]
    [DataRow("300702050080000000", DisplayName = "a messageID past int.MaxValue")]
    [DataRow("300c0201", DisplayName = "truncated")]
    [DataRow("0400", DisplayName = "not a sequence")]
    public void Decode_NotAnExtendedResponseToTheRequest_IsMalformed(string hex)
    {
        Assert.AreEqual(LdapBindReplyStatus.Malformed, LdapExtendedResponse.Decode(Hex.Bytes(hex), 1).Status);
    }

    [TestMethod]
    public void Decode_ExtendedResponse_ReturnsItsResultCode()
    {
        Assert.AreEqual(new LdapBindReply(LdapBindReplyStatus.Answered, 52), LdapExtendedResponse.Decode(StartTlsUnavailable, 1));
    }

    private static LdapProtocolHandler Handler(LdapDialect dialect, ScriptedConnection connection, ITlsProvider? tls) =>
        new(new RecordingConnector(ConnectResult.Connected(connection)), dialect, new FakeLogonTokenSource(0), tls);

    private static TransferContext Context(string scheme, TransportSecurityLevel level, RecordingTransferEvents events) => new()
    {
        Url = CurlUrl.Parse($"{scheme}://127.0.0.1:21900/dc=test"),
        Output = new MemoryStream(),
        Credentials = new NetworkCredential("cn=admin", "secret"),
        SslLevel = level,
        Events = events,
    };

    /// <summary>Records the connection it is asked to upgrade and answers with a fixed result.</summary>
    private sealed class FakeTlsProvider(ConnectResult result) : ITlsProvider
    {
        public IConnection? Upgraded { get; private set; }

        public string? TargetHost { get; private set; }

        public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, CancellationToken cancellationToken)
        {
            Upgraded = plaintext;
            TargetHost = targetHost;
            return ValueTask.FromResult(result);
        }
    }
}
