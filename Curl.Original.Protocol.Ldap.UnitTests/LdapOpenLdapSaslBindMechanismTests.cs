using System.Net;
using System.Text;
using Curl.Authentication;
using Curl.Original.Protocol.Ldap.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Pins each SASL mechanism of the OpenLDAP bind, run by the shared authenticator, against the requests
/// curl 8.18.0 (OpenLDAP 2.6.10, Linux) sent a scripted server, measured with
/// <c>Record-CurlExchange.ps1 -Script</c> (BL-5907).
/// </summary>
[TestClass]
public sealed class LdapOpenLdapSaslBindMechanismTests
{
    private const string Url = "ldap://172.26.96.1:22394/dc=x";

    private const string RootDseSearch =
        "30 3e 02 01 01 63 39 04 00 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 87 0b 6f 62 6a 65 63 74 63 6c 61 73 73 "
        + "30 19 04 17 73 75 70 70 6f 72 74 65 64 53 41 53 4c 4d 65 63 68 61 6e 69 73 6d 73";

    private const string RootDseDone = "30 0c 02 01 01 65 07 0a 01 00 04 00 04 00";

    private const string PlainBind = "30 19 02 01 02 60 14 02 01 03 04 00 a3 0d 04 05 50 4c 41 49 4e 04 04 00 75 00 70";

    private const string LoginBind = "30 16 02 01 02 60 11 02 01 03 04 00 a3 0a 04 05 4c 4f 47 49 4e 04 01 75";

    private const string LoginPasswordBind = "30 16 02 01 03 60 11 02 01 03 04 00 a3 0a 04 05 4c 4f 47 49 4e 04 01 70";

    private const string CramBind = "30 16 02 01 02 60 11 02 01 03 04 00 a3 0a 04 08 43 52 41 4d 2d 4d 44 35";

    private const string CramAnswerBind =
        "30 3a 02 01 03 60 35 02 01 03 04 00 a3 2e 04 08 43 52 41 4d 2d 4d 44 35 04 22 75 20 63 31 33 35 32 39 62 33 37 34 63 62 "
        + "37 64 66 61 34 35 34 32 36 35 61 31 38 30 61 39 38 34 37 30";

    private const string OAuthBearerBind =
        "30 4f 02 01 02 60 4a 02 01 03 04 00 a3 43 04 0b 4f 41 55 54 48 42 45 41 52 45 52 04 34 6e 2c 61 3d 75 2c 01 68 6f 73 74 3d "
        + "31 37 32 2e 32 36 2e 39 36 2e 31 01 70 6f 72 74 3d 32 32 33 39 34 01 61 75 74 68 3d 42 65 61 72 65 72 20 74 6f 6b 01 01";

    private const string OAuthBearerAcknowledgement = "30 1c 02 01 03 60 17 02 01 03 04 00 a3 10 04 0b 4f 41 55 54 48 42 45 41 52 45 52 04 01 01";

    private const string ExternalAsUserBind = "30 19 02 01 02 60 14 02 01 03 04 00 a3 0d 04 08 45 58 54 45 52 4e 41 4c 04 01 75";

    private const string ExternalWithNoUserBind = "30 18 02 01 02 60 13 02 01 03 04 00 a3 0c 04 08 45 58 54 45 52 4e 41 4c 04 00";

    private const string AuthorizationIdentityBind = "30 1a 02 01 02 60 15 02 01 03 04 00 a3 0e 04 05 50 4c 41 49 4e 04 05 7a 00 75 00 70";

    private const string CancelBind = "30 0e 02 01 03 60 09 02 01 03 04 00 a3 02 04 00";

    private const string NtlmType1Bind =
        "30 34 02 01 02 60 2f 02 01 03 04 00 a3 28 04 04 4e 54 4c 4d 04 20 4e 54 4c 4d 53 53 50 00 01 00 00 00 06 82 08 00 "
        + "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00";

    private const string DigestBind = "30 18 02 01 02 60 13 02 01 03 04 00 a3 0c 04 0a 44 49 47 45 53 54 2d 4d 44 35";

    // The framing of the other measured binds around a scripted token: a real one differs with every ticket.
    private const string GssapiBind = "30 1b 02 01 02 60 16 02 01 03 04 00 a3 0f 04 06 47 53 53 41 50 49 04 05 66 69 72 73 74";

    private static readonly byte[] NtlmType1 = Hex.Bytes("4e 54 4c 4d 53 53 50 00 01 00 00 00 06 82 08 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00");

    private static TransferResult LoginDenied => TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied");

    private static TransferResult Cancelled => TransferResult.Failure(CurlExitCode.LoginDenied, "Authentication cancelled");

    [TestMethod]
    [DataRow("AUTH=PLAIN", HttpAuthSchemes.Basic)]
    [DataRow(null, HttpAuthSchemes.Any)]
    public async Task ExecuteAsync_Plain_SendsCurlsBindAndSearches(string? loginOptions, HttpAuthSchemes schemes)
    {
        var transfer = new Transfer { LoginOptions = loginOptions, Schemes = schemes };

        await transfer.RunAsync(Mechanisms("PLAIN", "LOGIN", "CRAM-MD5"), RootDseDone, Bind(2, 0), SearchDone(3));

        transfer.AssertBound(RootDseSearch, PlainBind);
    }

    [TestMethod]
    public async Task ExecuteAsync_Login_SendsTheUserThenThePassword()
    {
        var transfer = new Transfer { LoginOptions = "AUTH=LOGIN" };

        await transfer.RunAsync(Mechanisms("LOGIN"), RootDseDone, Bind(2, 14, "Password:"), Bind(3, 0), SearchDone(4));

        transfer.AssertBound(RootDseSearch, LoginBind, LoginPasswordBind);
    }

    [TestMethod]
    public async Task ExecuteAsync_AnyLoginOption_PicksCramMd5AndAnswersItsChallenge()
    {
        var transfer = new Transfer { LoginOptions = "AUTH=*" };

        await transfer.RunAsync(Mechanisms("PLAIN", "LOGIN", "CRAM-MD5"), RootDseDone, Bind(2, 14, "<1.2@x>"), Bind(3, 0), SearchDone(4));

        transfer.AssertBound(RootDseSearch, CramBind, CramAnswerBind);
    }

    [TestMethod]
    public async Task ExecuteAsync_RefusedDigestMd5_SendsABindWithoutCredentialsAndFailsWith67()
    {
        var transfer = new Transfer { Schemes = HttpAuthSchemes.Digest };

        await transfer.RunAsync(Mechanisms("DIGEST-MD5"), RootDseDone, Bind(2, 49));

        transfer.AssertFailed(LoginDenied, RootDseSearch, DigestBind, Unbind(3));
    }

    [TestMethod]
    public async Task ExecuteAsync_Ntlm_SendsType1ThenTheAnswerToTheChallenge()
    {
        var transfer = new Transfer { Schemes = HttpAuthSchemes.Ntlm, Contexts = new ScriptedContexts(NtlmType1, "three"u8.ToArray()) };

        await transfer.RunAsync(Mechanisms("NTLM"), RootDseDone, Bind(2, 14, "two"), Bind(3, 0), SearchDone(4));

        transfer.AssertBound(RootDseSearch, NtlmType1Bind);
        Assert.IsTrue(transfer.Sent.AsSpan().IndexOf("three"u8) >= 0);
    }

    [TestMethod]
    public async Task ExecuteAsync_NtlmChallengeItCannotAnswer_CancelsAndFailsWith67AuthenticationCancelled()
    {
        var transfer = new Transfer { Schemes = HttpAuthSchemes.Ntlm, Contexts = new ScriptedContexts(NtlmType1) };

        await transfer.RunAsync(Mechanisms("NTLM"), RootDseDone, Bind(2, 14, "junk"), Bind(3, 0));

        transfer.AssertFailed(Cancelled, RootDseSearch, NtlmType1Bind, CancelBind, Unbind(4));
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelledMechanism_GivesWayToTheNextOne()
    {
        var transfer = new Transfer { LoginOptions = "AUTH=*", Contexts = new ScriptedContexts(NtlmType1) };

        await transfer.RunAsync(Mechanisms("NTLM", "PLAIN"), RootDseDone, Bind(2, 14, "junk"), Bind(3, 0), Bind(4, 0), SearchDone(5));

        transfer.AssertBound(RootDseSearch, NtlmType1Bind, CancelBind, "30 19 02 01 04 60 14 02 01 03 04 00 a3 0d 04 05 50 4c 41 49 4e 04 04 00 75 00 70");
    }

    [TestMethod]
    public async Task ExecuteAsync_Gssapi_AnswersTheTokenAndTheSecurityLayerOffer()
    {
        var transfer = new Transfer { User = "u@R", Schemes = HttpAuthSchemes.Negotiate, Contexts = new ScriptedContexts("first"u8.ToArray(), "second"u8.ToArray()) };

        await transfer.RunAsync(Mechanisms("GSSAPI"), RootDseDone, Bind(2, 14, "reply"), Bind(3, 14, "\u0001\0\0\0"), Bind(4, 0), SearchDone(5));

        transfer.AssertBound(RootDseSearch, GssapiBind);
        Assert.IsTrue(transfer.Sent.AsSpan().IndexOf("second"u8) >= 0);
    }

    [TestMethod]
    public async Task ExecuteAsync_GssapiSuccessBeforeTheSecurityLayer_FailsWith67()
    {
        var transfer = new Transfer { User = "u@R", Schemes = HttpAuthSchemes.Negotiate, Contexts = new ScriptedContexts("first"u8.ToArray()) };

        await transfer.RunAsync(Mechanisms("GSSAPI"), RootDseDone, Bind(2, 0));

        Assert.AreEqual(LoginDenied, transfer.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_GssapiTokenItCannotAnswer_CancelsAndFailsWith67AuthenticationCancelled()
    {
        var transfer = new Transfer { User = "u@R", Schemes = HttpAuthSchemes.Negotiate, Contexts = new ScriptedContexts("first"u8.ToArray()) };

        await transfer.RunAsync(Mechanisms("GSSAPI"), RootDseDone, Bind(2, 14, "reply"), Bind(3, 0));

        Assert.AreEqual(Cancelled, transfer.Result, transfer.Result.ToString());
        Assert.IsTrue(transfer.Sent.AsSpan().IndexOf(Hex.Bytes(CancelBind)) >= 0);
    }

    [TestMethod]
    public async Task ExecuteAsync_GssapiWithoutACredential_FailsWith94AndSendsNoBind()
    {
        // Measured: --login-options AUTH=* -u u@R:p with no ticket is "curl: (94) An authentication function returned an error".
        var transfer = new Transfer { User = "u@R", LoginOptions = "AUTH=*", Contexts = new ScriptedContexts() };

        await transfer.RunAsync(Mechanisms("GSSAPI", "PLAIN"), RootDseDone);

        transfer.AssertFailed(TransferResult.Failure(CurlExitCode.AuthError, "An authentication function returned an error"), RootDseSearch, Unbind(2));
    }

    [TestMethod]
    public async Task ExecuteAsync_GssapiWithAUserNamingNoRealm_FailsWith67AndSendsNoBind()
    {
        var transfer = new Transfer { LoginOptions = "AUTH=GSSAPI", Contexts = new ScriptedContexts("first"u8.ToArray()) };

        await transfer.RunAsync(Mechanisms("GSSAPI"), RootDseDone);

        transfer.AssertFailed(LoginDenied, RootDseSearch, Unbind(2));
    }

    [TestMethod]
    [DataRow("OAUTHBEARER")]
    [DataRow("oauthbearer")]
    public async Task ExecuteAsync_BearerToken_SendsCurlsOAuthBearerBind(string offered)
    {
        var transfer = new Transfer { Password = string.Empty, BearerToken = "tok", Schemes = HttpAuthSchemes.Bearer };

        await transfer.RunAsync(Mechanisms(offered), RootDseDone, Bind(2, 0), SearchDone(3));

        transfer.AssertBound(RootDseSearch, OAuthBearerBind);
    }

    [TestMethod]
    public async Task ExecuteAsync_WrongBearerToken_AcknowledgesTheErrorAndFailsWith67()
    {
        var transfer = new Transfer { Password = string.Empty, BearerToken = "tok", Schemes = HttpAuthSchemes.Bearer };

        await transfer.RunAsync(Mechanisms("OAUTHBEARER"), RootDseDone, Bind(2, 14, "{\"status\":\"401\"}"), Bind(3, 49));

        transfer.AssertFailed(LoginDenied, RootDseSearch, OAuthBearerBind, OAuthBearerAcknowledgement, Unbind(4));
    }

    [TestMethod]
    public async Task ExecuteAsync_XOAuth2_SendsTheTokenAndSearches()
    {
        var transfer = new Transfer { Password = string.Empty, BearerToken = "tok", Schemes = HttpAuthSchemes.Bearer, ServiceName = "directory" };

        await transfer.RunAsync(Mechanisms("XOAUTH2"), RootDseDone, Bind(2, 0), SearchDone(3));

        Assert.AreEqual(TransferResult.Success(0), transfer.Result, transfer.Result.ToString());
        Assert.IsTrue(transfer.Sent.AsSpan().IndexOf("user=u\u0001auth=Bearer tok\u0001\u0001"u8) >= 0);
    }

    [TestMethod]
    public async Task ExecuteAsync_External_BindsAsTheUser()
    {
        var transfer = new Transfer { Password = string.Empty, LoginOptions = "AUTH=EXTERNAL" };

        await transfer.RunAsync(Mechanisms("EXTERNAL"), RootDseDone, Bind(2, 0), SearchDone(3));

        transfer.AssertBound(RootDseSearch, ExternalAsUserBind);
    }

    [TestMethod]
    public async Task ExecuteAsync_ExternalWithoutAUser_BindsWithEmptyCredentials()
    {
        var transfer = new Transfer { User = null, LoginOptions = "AUTH=EXTERNAL" };

        await transfer.RunAsync(Mechanisms("EXTERNAL"), RootDseDone, Bind(2, 0), SearchDone(3));

        transfer.AssertBound(RootDseSearch, ExternalWithNoUserBind);
    }

    [TestMethod]
    public async Task ExecuteAsync_ExternalRefused_FailsWith67()
    {
        var transfer = new Transfer { User = string.Empty, Password = string.Empty, LoginOptions = "AUTH=EXTERNAL" };

        await transfer.RunAsync(Mechanisms("EXTERNAL"), RootDseDone, Bind(2, 49));

        transfer.AssertFailed(LoginDenied, RootDseSearch, ExternalWithNoUserBind, Unbind(3));
    }

    [TestMethod]
    [DataRow(null, HttpAuthSchemes.Any, null)]
    [DataRow(null, HttpAuthSchemes.Bearer, "tok")]
    [DataRow("AUTH=PLAIN", HttpAuthSchemes.Basic, null)]
    public async Task ExecuteAsync_SaslWithoutAUser_FailsWith67AndSendsNoBind(string? loginOptions, HttpAuthSchemes schemes, string? bearerToken)
    {
        var transfer = new Transfer { User = null, LoginOptions = loginOptions, Schemes = schemes, BearerToken = bearerToken };

        await transfer.RunAsync(Mechanisms("PLAIN", "OAUTHBEARER"), RootDseDone);

        transfer.AssertFailed(LoginDenied, RootDseSearch, Unbind(2));
    }

    [TestMethod]
    public async Task ExecuteAsync_AuthorizationIdentityAndAnExtraChallenge_SendsItAndFailsWith67()
    {
        var transfer = new Transfer { LoginOptions = "AUTH=PLAIN", AuthorizationIdentity = "z" };

        await transfer.RunAsync(Mechanisms("PLAIN"), RootDseDone, Bind(2, 14), Bind(3, 0));

        transfer.AssertFailed(LoginDenied, RootDseSearch, AuthorizationIdentityBind, Unbind(3));
    }

    [TestMethod]
    [DataRow("LOGIN", LoginBind)]
    [DataRow("CRAM-MD5", CramBind)]
    public async Task ExecuteAsync_SuccessBeforeTheLastAnswer_FailsWith67(string mechanism, string bind)
    {
        var transfer = new Transfer { LoginOptions = "AUTH=" + mechanism };

        await transfer.RunAsync(Mechanisms(mechanism), RootDseDone, Bind(2, 0));

        transfer.AssertFailed(LoginDenied, RootDseSearch, bind, Unbind(3));
    }

    [TestMethod]
    public async Task ExecuteAsync_SecondCramChallenge_FailsWith67()
    {
        var transfer = new Transfer { LoginOptions = "AUTH=CRAM-MD5" };

        await transfer.RunAsync(Mechanisms("CRAM-MD5"), RootDseDone, Bind(2, 14, "<1.2@x>"), Bind(3, 14, "again"));

        Assert.AreEqual(CurlExitCode.LoginDenied, transfer.Result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_CramChallengeWithoutCredentials_FailsWith8()
    {
        var transfer = new Transfer { LoginOptions = "AUTH=CRAM-MD5" };

        await transfer.RunAsync(Mechanisms("CRAM-MD5"), RootDseDone, Bind(2, 14));

        transfer.AssertFailed(TransferResult.Failure(CurlExitCode.WeirdServerReply, "Weird server reply"), RootDseSearch, CramBind, Unbind(3));
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerClosesAfterTheFirstBind_FailsWith7()
    {
        var transfer = new Transfer { LoginOptions = "AUTH=CRAM-MD5" };

        await transfer.RunAsync(Mechanisms("CRAM-MD5"), RootDseDone);

        Assert.AreEqual(CurlExitCode.CouldntConnect, transfer.Result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_MaskNamingNoSaslScheme_AllowsTheDefaultMechanisms()
    {
        var transfer = new Transfer { Schemes = HttpAuthSchemes.None, WithoutMailOptions = true };

        await transfer.RunAsync(Mechanisms("EXTERNAL", "PLAIN"), RootDseDone, Bind(2, 0), SearchDone(3));

        transfer.AssertBound(RootDseSearch, PlainBind);
    }

    [TestMethod]
    public async Task ExecuteAsync_AuthOptionWithoutHttpOptions_BindsWithSasl()
    {
        var transfer = new Transfer { LoginOptions = "AUTH=PLAIN", WithoutHttpOptions = true };

        await transfer.RunAsync(Mechanisms("PLAIN"), RootDseDone, Bind(2, 0), SearchDone(3));

        transfer.AssertBound(RootDseSearch, PlainBind);
    }

    [TestMethod]
    public async Task ExecuteAsync_PlainChallengeWithoutAnAuthenticator_FailsWith67()
    {
        var transfer = new Transfer { LoginOptions = "AUTH=PLAIN", WithoutAuthenticator = true };

        await transfer.RunAsync(Mechanisms("PLAIN"), RootDseDone, Bind(2, 14));

        transfer.AssertFailed(LoginDenied, RootDseSearch, PlainBind, Unbind(3));
    }

    private static string Unbind(int id) => $"30 05 02 01 {id:x2} 42 00";

    private static string SearchDone(int id) => $"30 0c 02 01 {id:x2} 65 07 0a 01 00 04 00 04 00";

    private static string Bind(int id, int code, string? serverCredentials = null)
    {
        byte[] credentials = serverCredentials is null ? [] : [0x87, (byte)serverCredentials.Length, .. Encoding.Latin1.GetBytes(serverCredentials)];
        byte[] response = [0x0a, 0x01, (byte)code, 0x04, 0x00, 0x04, 0x00, .. credentials];
        return Convert.ToHexString([0x30, (byte)(response.Length + 5), 0x02, 0x01, (byte)id, 0x61, (byte)response.Length, .. response]);
    }

    private static string Mechanisms(params string[] names)
    {
        byte[] values = [.. names.SelectMany(name => Element(0x04, Encoding.ASCII.GetBytes(name)))];
        byte[] attribute = Element(0x30, [.. Element(0x04, "supportedSASLMechanisms"u8.ToArray()), .. Element(0x31, values)]);
        byte[] entry = Element(0x64, [0x04, 0x00, .. Element(0x30, attribute)]);
        return Convert.ToHexString(Element(0x30, [0x02, 0x01, 0x01, .. entry]));
    }

    private static byte[] Element(byte tag, byte[] content) => [tag, (byte)content.Length, .. content];

    /// <summary>One OpenLDAP transfer through the shared authenticator against a scripted server.</summary>
    private sealed class Transfer
    {
        public string? User { get; init; } = "u";

        public string Password { get; init; } = "p";

        public string? LoginOptions { get; init; }

        public HttpAuthSchemes Schemes { get; init; } = HttpAuthSchemes.Basic;

        public string? BearerToken { get; init; }

        public string? AuthorizationIdentity { get; init; }

        public string? ServiceName { get; init; }

        public ISecurityContextFactory? Contexts { get; init; }

        public bool WithoutMailOptions { get; init; }

        public bool WithoutHttpOptions { get; init; }

        public bool WithoutAuthenticator { get; init; }

        public TransferResult Result { get; private set; } = TransferResult.Success(0);

        public byte[] Sent { get; private set; } = [];

        public async Task RunAsync(params string[] replies)
        {
            var connection = new ScriptedConnection([.. replies.Select(Hex.Bytes)]);
            var connector = new RecordingConnector(ConnectResult.Connected(connection));
            var tokens = new FakeLogonTokenSource(2);
            LdapProtocolHandler handler = WithoutAuthenticator
                ? new(connector, LdapDialect.OpenLdap, tokens)
                : new(connector, LdapDialect.OpenLdap, tokens)
                {
                    SaslAuthenticator = Contexts is null ? new SaslAuthenticator(Encoding.UTF8) : new SaslAuthenticator(Encoding.UTF8, Contexts),
                };
            var context = new TransferContext
            {
                Url = CurlUrl.Parse(Url),
                Output = new MemoryStream(),
                Credentials = User is null ? null : new NetworkCredential(User, Password),
                Http = WithoutHttpOptions ? null : new Curl.Protocol.Abstractions.HttpRequestOptions { AuthSchemes = Schemes },
                Mail = WithoutMailOptions
                    ? null
                    : new MailRequestOptions { LoginOptions = LoginOptions, BearerToken = BearerToken, SaslAuthorizationIdentity = AuthorizationIdentity, ServiceName = ServiceName },
            };

            Result = await handler.ExecuteAsync(context);
            Sent = connection.Sent;
        }

        /// <summary>The bind succeeded, and the transfer's first requests were <paramref name="requests" />.</summary>
        public void AssertBound(params string[] requests)
        {
            Assert.AreEqual(TransferResult.Success(0), Result, Result.ToString());
            byte[] expected = [.. requests.SelectMany(Hex.Bytes)];
            CollectionAssert.AreEqual(expected, Sent.Take(expected.Length).ToArray());
        }

        /// <summary>The transfer ended in <paramref name="failure" /> having sent exactly <paramref name="requests" />.</summary>
        public void AssertFailed(TransferResult failure, params string[] requests)
        {
            Assert.AreEqual(failure, Result, Result.ToString());
            CollectionAssert.AreEqual(requests.SelectMany(Hex.Bytes).ToArray(), Sent);
        }
    }

    /// <summary>Security contexts that answer with the tokens given, in turn, then fail; wrapping returns the message.</summary>
    private sealed class ScriptedContexts(params byte[][] tokens) : ISecurityContextFactory, ISecurityContext
    {
        private int next;

        public bool IsCompleted => next >= tokens.Length && next > 1;

        public ISecurityContext Create(SecurityContextRequest request) => this;

        public ValueTask<SecurityContextStep> NextTokenAsync(ReadOnlyMemory<byte> incomingToken, CancellationToken cancellationToken) =>
            ValueTask.FromResult(next < tokens.Length
                ? new SecurityContextStep(SecurityContextStatus.ContinueNeeded, tokens[next++])
                : new SecurityContextStep(SecurityContextStatus.NoCredentials, []));

        public byte[]? Wrap(ReadOnlySpan<byte> message, bool encrypt) => message.ToArray();

        public byte[]? Unwrap(ReadOnlySpan<byte> wrappedMessage) => wrappedMessage.ToArray();

        public void Dispose()
        {
        }
    }
}
