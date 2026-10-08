using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="SaslAuthenticator" /> to the mechanisms curl 8.21.0 (mingw, Schannel) chose
/// and the bytes it sent to <c>Record-CurlExchange.ps1 -Smtp</c> on 2026-09-28 (ADR-0121,
/// ADR-0123, BL-536 Notes). Base64 values are the lines curl wrote.
/// </summary>
[TestClass]
public sealed class SaslAuthenticatorTests
{
    private const string Host = "127.0.0.1";

    private static readonly Encoding Windows1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;

    private static readonly SaslAuthenticator Authenticator = new(Windows1252);

    private static readonly string[] AllTen =
        ["EXTERNAL", "GSSAPI", "DIGEST-MD5", "CRAM-MD5", "NTLM", "OAUTHBEARER", "XOAUTH2", "LOGIN", "PLAIN", "SCRAM-SHA-256"];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(null, "AHUAcA==", DisplayName = "--sasl-ir -u u:p: AUTH PLAIN AHUAcA==")]
    [DataRow("z", "egB1AHA=", DisplayName = "--sasl-ir --sasl-authzid z -u u:p: AUTH PLAIN egB1AHA=")]
    public async Task Begin_Plain_InitialResponseMatchesCurl(string? authorizationIdentity, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "PLAIN");
        diagnostics.Arrange("credentials", "u:p");
        diagnostics.Arrange("authorization identity", authorizationIdentity);
        ISaslExchange exchange = Authenticator.Begin("PLAIN", Request(new NetworkCredential("u", "p"), authorizationIdentity: authorizationIdentity));

        byte[]? initial = await InitialResponseAsync(exchange);
        byte[]? next = await RespondAsync(exchange, []);

        diagnostics.Act("initial response", Base64(initial));
        diagnostics.Bytes("initial response bytes", initial);
        diagnostics.Act("response to empty challenge", next);
        diagnostics.Assert("mechanism", "PLAIN", exchange.Mechanism);
        diagnostics.Diff("initial response", expected, Base64(initial));
        Assert.AreEqual("PLAIN", exchange.Mechanism);
        Assert.AreEqual(expected, Base64(initial));
        Assert.IsNull(next);
    }

    [TestMethod]
    public async Task Begin_PlainWithoutInitialResponse_AnswersTheEmptyChallengeWithTheSameMessage()
    {
        // Measured: AUTH PLAIN, 334 (empty), AHUAcA==, 235.
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "PLAIN");
        diagnostics.Arrange("credentials", "u:p");
        diagnostics.Arrange("server challenge", "(empty)");

        string[] lines = await ConverseAsync(Authenticator.Begin("PLAIN", Request(new NetworkCredential("u", "p"))), sendInitialResponse: false, "");

        diagnostics.Act("lines sent", string.Join("|", lines));
        diagnostics.Assert("lines sent", "AHUAcA==", string.Join("|", lines));
        CollectionAssert.AreEqual(new[] { "AHUAcA==" }, lines);
    }

    [TestMethod]
    public async Task Begin_LoginWithInitialResponse_SendsTheUserThenThePassword()
    {
        // Measured: AUTH LOGIN dQ==, 334 UGFzc3dvcmQ6, cA==, 235.
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "LOGIN");
        diagnostics.Arrange("credentials", "u:p");
        diagnostics.Arrange("server challenge", "UGFzc3dvcmQ6");

        string[] lines = await ConverseAsync(Authenticator.Begin("LOGIN", Request(new NetworkCredential("u", "p"))), sendInitialResponse: true, "UGFzc3dvcmQ6");

        diagnostics.Act("lines sent", string.Join("|", lines));
        diagnostics.Assert("lines sent", "dQ==|cA==", string.Join("|", lines));
        CollectionAssert.AreEqual(new[] { "dQ==", "cA==" }, lines);
    }

    [TestMethod]
    public async Task Begin_LoginWithoutInitialResponse_AnswersUsernameThenPassword()
    {
        // Measured: AUTH LOGIN, 334 VXNlcm5hbWU6, dQ==, 334 UGFzc3dvcmQ6, cA==, 235.
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "LOGIN");
        diagnostics.Arrange("credentials", "u:p");
        diagnostics.Arrange("server challenges", "VXNlcm5hbWU6, UGFzc3dvcmQ6");

        string[] lines = await ConverseAsync(
            Authenticator.Begin("LOGIN", Request(new NetworkCredential("u", "p"))), sendInitialResponse: false, "VXNlcm5hbWU6", "UGFzc3dvcmQ6");

        diagnostics.Act("lines sent", string.Join("|", lines));
        diagnostics.Assert("lines sent", "dQ==|cA==", string.Join("|", lines));
        CollectionAssert.AreEqual(new[] { "dQ==", "cA==" }, lines);
    }

    [TestMethod]
    public async Task Begin_LoginThirdChallenge_CannotAnswer()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "LOGIN");
        diagnostics.Arrange("credentials", "u:p");
        ISaslExchange exchange = Authenticator.Begin("LOGIN", Request(new NetworkCredential("u", "p")));

        byte[]? first = await RespondAsync(exchange, []);
        byte[]? second = await RespondAsync(exchange, []);

        diagnostics.Act("first answer", Base64(first));
        diagnostics.Act("second answer", second);
        diagnostics.Assert("mechanism", "LOGIN", exchange.Mechanism);
        diagnostics.Diff("first answer", "cA==", Base64(first));
        Assert.AreEqual("LOGIN", exchange.Mechanism);
        Assert.AreEqual("cA==", Base64(first));
        Assert.IsNull(second);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "--sasl-ir --login-options AUTH=EXTERNAL -u u:: AUTH EXTERNAL dQ==")]
    [DataRow("z", DisplayName = "The authorization identity is not sent: AUTH EXTERNAL dQ==")]
    public async Task Begin_External_SendsTheUserName(string? authorizationIdentity)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "EXTERNAL");
        diagnostics.Arrange("credentials", "u:(empty)");
        diagnostics.Arrange("authorization identity", authorizationIdentity);
        ISaslExchange exchange = Authenticator.Begin("EXTERNAL", Request(new NetworkCredential("u", ""), authorizationIdentity: authorizationIdentity));

        byte[]? initial = await InitialResponseAsync(exchange);
        byte[]? next = await RespondAsync(exchange, []);

        diagnostics.Act("initial response", Base64(initial));
        diagnostics.Act("response to empty challenge", next);
        diagnostics.Assert("mechanism", "EXTERNAL", exchange.Mechanism);
        diagnostics.Diff("initial response", "dQ==", Base64(initial));
        Assert.AreEqual("EXTERNAL", exchange.Mechanism);
        Assert.AreEqual("dQ==", Base64(initial));
        Assert.IsNull(next);
    }

    [TestMethod]
    public async Task Begin_XOAuth2_InitialResponseMatchesCurl()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "XOAUTH2");
        diagnostics.Arrange("credentials", "u:(empty)");
        diagnostics.Arrange("bearer token", "tok");
        diagnostics.Arrange("server challenge", "{\"status\":\"401\"}");
        ISaslExchange exchange = Authenticator.Begin("XOAUTH2", Request(new NetworkCredential("u", ""), bearerToken: "tok"));

        byte[]? initial = await InitialResponseAsync(exchange);
        byte[]? next = await RespondAsync(exchange, Encoding.ASCII.GetBytes("{\"status\":\"401\"}"));

        diagnostics.Act("initial response", Base64(initial));
        diagnostics.Act("response to error continuation", next);
        diagnostics.Assert("mechanism", "XOAUTH2", exchange.Mechanism);
        diagnostics.Diff("initial response", "dXNlcj11AWF1dGg9QmVhcmVyIHRvawEB", Base64(initial));
        Assert.AreEqual("XOAUTH2", exchange.Mechanism);
        Assert.AreEqual("dXNlcj11AWF1dGg9QmVhcmVyIHRvawEB", Base64(initial));
        Assert.IsNull(next, "curl fails an XOAUTH2 error continuation with exit 67.");
    }

    [TestMethod]
    public async Task Begin_XOAuth2WithoutUser_SendsAnEmptyUser()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "XOAUTH2");
        diagnostics.Arrange("credentials", "(none)");
        diagnostics.Arrange("bearer token", "tok");
        ISaslExchange exchange = Authenticator.Begin("XOAUTH2", Request(null, bearerToken: "tok"));

        byte[]? initial = await InitialResponseAsync(exchange);

        diagnostics.Bytes("initial response bytes", initial);
        diagnostics.Act("initial response", Windows1252.GetString(initial!));
        diagnostics.Diff("initial response", "user=\u0001auth=Bearer tok\u0001\u0001", Windows1252.GetString(initial!));
        Assert.AreEqual("user=\u0001auth=Bearer tok\u0001\u0001", Windows1252.GetString(initial!));
    }

    [TestMethod]
    [DataRow("u", "bixhPXUsAWhvc3Q9MTI3LjAuMC4xAXBvcnQ9MTgxMjUBYXV0aD1CZWFyZXIgdG9rAQE=", DisplayName = "-u u: --oauth2-bearer tok, port 18125")]
    [DataRow("", "bixhPSwBaG9zdD0xMjcuMC4wLjEBcG9ydD0xODEyNQFhdXRoPUJlYXJlciB0b2sBAQ==", DisplayName = "--oauth2-bearer tok alone, port 18125")]
    public void OAuthBearerMessage_WithPort_MatchesCurl(string user, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("user", user);
        diagnostics.Arrange("host", Host);
        diagnostics.Arrange("port", 18125);
        diagnostics.Arrange("bearer token", "tok");

        string message = SaslAuthenticator.OAuthBearerMessage(user, Host, 18125, "tok");

        diagnostics.Act("message", message);
        diagnostics.Diff("message base64", expected, Convert.ToBase64String(Windows1252.GetBytes(message)));
        Assert.AreEqual(expected, Convert.ToBase64String(Windows1252.GetBytes(message)));
    }

    [TestMethod]
    public async Task Begin_OAuthBearerOnPort18125_MatchesCurl()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "OAUTHBEARER");
        diagnostics.Arrange("credentials", "u:(empty)");
        diagnostics.Arrange("bearer token", "tok");
        diagnostics.Arrange("port", 18125);
        ISaslExchange exchange = Authenticator.Begin("OAUTHBEARER", Request(new NetworkCredential("u", ""), bearerToken: "tok") with { Port = 18125 });

        byte[]? initial = await InitialResponseAsync(exchange);

        diagnostics.Act("initial response", Base64(initial));
        diagnostics.Assert("mechanism", "OAUTHBEARER", exchange.Mechanism);
        diagnostics.Diff("initial response", "bixhPXUsAWhvc3Q9MTI3LjAuMC4xAXBvcnQ9MTgxMjUBYXV0aD1CZWFyZXIgdG9rAQE=", Base64(initial));
        Assert.AreEqual("OAUTHBEARER", exchange.Mechanism);
        Assert.AreEqual("bixhPXUsAWhvc3Q9MTI3LjAuMC4xAXBvcnQ9MTgxMjUBYXV0aD1CZWFyZXIgdG9rAQE=", Base64(initial));
    }

    [TestMethod]
    public async Task Begin_OAuthBearerOnPortZero_LeavesThePortOut()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "OAUTHBEARER");
        diagnostics.Arrange("credentials", "u:(empty)");
        diagnostics.Arrange("bearer token", "tok");
        diagnostics.Arrange("port", 0);
        ISaslExchange exchange = Authenticator.Begin("OAUTHBEARER", Request(new NetworkCredential("u", ""), bearerToken: "tok"));

        byte[]? initial = await InitialResponseAsync(exchange);

        diagnostics.Bytes("initial response bytes", initial);
        diagnostics.Act("initial response", Windows1252.GetString(initial!));
        diagnostics.Assert("mechanism", "OAUTHBEARER", exchange.Mechanism);
        diagnostics.Diff("initial response", "n,a=u,\u0001host=127.0.0.1\u0001auth=Bearer tok\u0001\u0001", Windows1252.GetString(initial!));
        Assert.AreEqual("OAUTHBEARER", exchange.Mechanism);
        Assert.AreEqual("n,a=u,\u0001host=127.0.0.1\u0001auth=Bearer tok\u0001\u0001", Windows1252.GetString(initial!));
    }

    [TestMethod]
    public async Task Begin_OAuthBearerErrorContinuation_AcknowledgedOnceWithOneByte()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "OAUTHBEARER");
        diagnostics.Arrange("bearer token", "tok");
        diagnostics.Arrange("server challenge", "{\"status\":\"401\"}");
        ISaslExchange exchange = Authenticator.Begin("OAUTHBEARER", Request(null, bearerToken: "tok"));

        byte[]? acknowledgement = await RespondAsync(exchange, Encoding.ASCII.GetBytes("{\"status\":\"401\"}"));
        byte[]? afterwards = await RespondAsync(exchange, []);

        diagnostics.Bytes("acknowledgement", acknowledgement);
        diagnostics.Act("acknowledgement", acknowledgement);
        diagnostics.Act("answer afterwards", afterwards);
        diagnostics.Diff("acknowledgement", new byte[] { 0x01 }, acknowledgement);
        CollectionAssert.AreEqual(new byte[] { 0x01 }, acknowledgement);
        Assert.IsNull(afterwards);
    }

    [TestMethod]
    public async Task Begin_PlainWithoutCredential_SendsEmptyUserAndPassword()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "PLAIN");
        diagnostics.Arrange("credentials", "(none)");
        ISaslExchange exchange = Authenticator.Begin("PLAIN", Request(null));

        byte[]? initial = await InitialResponseAsync(exchange);

        diagnostics.Bytes("initial response bytes", initial);
        diagnostics.Act("initial response", initial);
        diagnostics.Diff("initial response", new byte[] { 0, 0 }, initial);
        CollectionAssert.AreEqual(new byte[] { 0, 0 }, initial);
    }

    [TestMethod]
    public async Task Begin_XOAuth2WithoutToken_SendsAnEmptyToken()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "XOAUTH2");
        diagnostics.Arrange("credentials", "u:p");
        diagnostics.Arrange("bearer token", "(none)");
        ISaslExchange exchange = Authenticator.Begin("XOAUTH2", Request(new NetworkCredential("u", "p")));

        byte[]? initial = await InitialResponseAsync(exchange);

        diagnostics.Bytes("initial response bytes", initial);
        diagnostics.Act("initial response", Windows1252.GetString(initial!));
        diagnostics.Diff("initial response", "user=u\u0001auth=Bearer \u0001\u0001", Windows1252.GetString(initial!));
        Assert.AreEqual("user=u\u0001auth=Bearer \u0001\u0001", Windows1252.GetString(initial!));
    }

    [TestMethod]
    public async Task Begin_NonAsciiUser_EncodedInTheCredentialEncoding()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "LOGIN");
        diagnostics.Arrange("credentials", "é:p");
        diagnostics.Arrange("credential encoding", Windows1252.WebName);
        ISaslExchange exchange = Authenticator.Begin("LOGIN", Request(new NetworkCredential("é", "p")));

        byte[]? initial = await InitialResponseAsync(exchange);

        diagnostics.Bytes("initial response bytes", initial);
        diagnostics.Act("initial response", initial);
        diagnostics.Diff("initial response", new byte[] { 0xE9 }, initial);
        CollectionAssert.AreEqual(new byte[] { 0xE9 }, initial);
    }

    [TestMethod]
    public void Begin_LowerCaseMechanism_ReportsTheSaslName()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "plain");
        diagnostics.Arrange("credentials", "u:p");

        ISaslExchange exchange = Authenticator.Begin("plain", Request(new NetworkCredential("u", "p")));

        diagnostics.Act("mechanism", exchange.Mechanism);
        diagnostics.Assert("mechanism", "PLAIN", exchange.Mechanism);
        Assert.AreEqual("PLAIN", exchange.Mechanism);
    }

    [TestMethod]
    [DataRow("user", "pencil", "dXNlciBlZTg3NzliY2M1MzFhNzhmNGRiMzc4YzQ3N2E1N2IwZA==", DisplayName = "-u user:pencil: user ee8779bc...")]
    [DataRow("user", "", "dXNlciAzYjFkZDYxZDNmODM4ZDAzZWVlYWJmZGRlOTFlYzVlOQ==", DisplayName = "-u user: : user 3b1dd61d...")]
    public async Task Begin_CramMd5_AnswersTheChallengeAsCurl(string user, string password, string expected)
    {
        // Measured: AUTH CRAM-MD5 (also under --sasl-ir), 334 PDE4OTYuNjk3MTcwOTUyQGxvY2FsaG9zdD4=, the answer, 235.
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "cram-md5");
        diagnostics.Arrange("credentials", user + ":" + password);
        diagnostics.Arrange("server challenge", "<1896.697170952@localhost>");
        ISaslExchange exchange = Authenticator.Begin("cram-md5", Request(new NetworkCredential(user, password)));

        byte[]? initial = await InitialResponseAsync(exchange);
        byte[]? answer = await RespondAsync(exchange, Encoding.ASCII.GetBytes("<1896.697170952@localhost>"));
        byte[]? afterwards = await RespondAsync(exchange, []);

        diagnostics.Act("initial response", initial);
        diagnostics.Act("answer", Base64(answer));
        diagnostics.Act("answer afterwards", afterwards);
        diagnostics.Assert("mechanism", "CRAM-MD5", exchange.Mechanism);
        diagnostics.Diff("answer", expected, Base64(answer));
        Assert.AreEqual("CRAM-MD5", exchange.Mechanism);
        Assert.IsNull(initial);
        Assert.AreEqual(expected, Base64(answer));
        Assert.IsNull(afterwards);
    }

    [TestMethod]
    public async Task Begin_CramMd5WithoutCredential_SendsAnEmptyUserAndPassword()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "CRAM-MD5");
        diagnostics.Arrange("credentials", "(none)");
        diagnostics.Arrange("server challenge", "c");
        ISaslExchange exchange = Authenticator.Begin("CRAM-MD5", Request(null));

        byte[]? answer = await RespondAsync(exchange, "c"u8);

        diagnostics.Bytes("answer bytes", answer);
        diagnostics.Act("answer", Windows1252.GetString(answer!));
        diagnostics.Assert("answer starts with", " ", Windows1252.GetString(answer!));
        StringAssert.StartsWith(Windows1252.GetString(answer!), " ");
    }

    [TestMethod]
    [DataRow(true, "127.0.0.1", "81eed5b913007ab96776b8224946a866",
        "username=\"user\",realm=\"\",nonce=\"OA6MG9tEQGm2hh\",digest-uri=\"smtp/127.0.0.1\",cnonce=\"81eed5b913007ab96776b8224946a866\",nc=00000001,response=1ae34deb057c4c638fc093d4913d8f29,qop=auth,charset=utf-8",
        DisplayName = "Schannel (SSPI)")]
    [DataRow(false, "172.26.96.1", "dab6bbea0a329577a0c691f97f35a087",
        "username=\"user\",realm=\"localhost\",nonce=\"OA6MG9tEQGm2hh\",cnonce=\"dab6bbea0a329577a0c691f97f35a087\",nc=\"00000001\",digest-uri=\"smtp/172.26.96.1\",response=8c416297044dbc635e16a5432c6cc0a1,qop=auth",
        DisplayName = "OpenSSL")]
    public async Task Begin_DigestMd5_AnswersTheChallengeThenRspauthAsCurl(bool answerAsSspi, string host, string clientNonce, string expected)
    {
        // Measured: AUTH DIGEST-MD5, 334 <challenge>, the answer, 334 <rspauth>, an empty line, 235.
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "DIGEST-MD5");
        diagnostics.Arrange("answer as SSPI", answerAsSspi);
        diagnostics.Arrange("host", host);
        diagnostics.Arrange("client nonce", clientNonce);
        diagnostics.Arrange("credentials", "user:pencil");
        var authenticator = new SaslAuthenticator(Windows1252, () => clientNonce, answerAsSspi, securityContexts: null);
        SaslRequest request = Request(new NetworkCredential("user", "pencil"), authorizationIdentity: "z") with { Host = host };
        ISaslExchange exchange = authenticator.Begin("DIGEST-MD5", request);
        byte[] challenge = Encoding.ASCII.GetBytes("realm=\"localhost\",nonce=\"OA6MG9tEQGm2hh\",qop=\"auth\",algorithm=md5-sess,charset=utf-8");
        diagnostics.Arrange("server challenge", Encoding.ASCII.GetString(challenge));

        byte[]? initial = await InitialResponseAsync(exchange);
        byte[]? answer = await RespondAsync(exchange, challenge);
        byte[]? rspauthAnswer = await RespondAsync(exchange, "rspauth=ea40f60335c427b5527b84dbabcdfffd"u8);
        byte[]? afterwards = await RespondAsync(exchange, []);

        diagnostics.Act("initial response", initial);
        diagnostics.Act("answer", Windows1252.GetString(answer!));
        diagnostics.Act("rspauth answer", rspauthAnswer);
        diagnostics.Act("answer afterwards", afterwards);
        diagnostics.Assert("mechanism", "DIGEST-MD5", exchange.Mechanism);
        diagnostics.Diff("answer", expected, Windows1252.GetString(answer!));
        Assert.AreEqual("DIGEST-MD5", exchange.Mechanism);
        Assert.IsNull(initial);
        Assert.AreEqual(expected, Windows1252.GetString(answer!));
        CollectionAssert.AreEqual(Array.Empty<byte>(), rspauthAnswer);
        Assert.IsNull(afterwards);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task Begin_DigestMd5WithoutCredential_SendsAnEmptyUser(bool answerAsSspi)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "DIGEST-MD5");
        diagnostics.Arrange("answer as SSPI", answerAsSspi);
        diagnostics.Arrange("credentials", "(none)");
        diagnostics.Arrange("server challenge", "nonce=\"n\",qop=\"auth\",algorithm=md5-sess");
        ISaslExchange exchange = new SaslAuthenticator(Windows1252, () => "c", answerAsSspi, securityContexts: null).Begin("DIGEST-MD5", Request(null));

        byte[]? answer = await RespondAsync(exchange, "nonce=\"n\",qop=\"auth\",algorithm=md5-sess"u8);

        diagnostics.Bytes("answer bytes", answer);
        diagnostics.Act("answer", Windows1252.GetString(answer!));
        diagnostics.Assert("answer starts with", "username=\"\",", Windows1252.GetString(answer!));
        StringAssert.StartsWith(Windows1252.GetString(answer!), "username=\"\",");
    }

    [TestMethod]
    public async Task Begin_DigestMd5ChallengeCurlCancels_AnswersNull()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "DIGEST-MD5");
        diagnostics.Arrange("credentials", "u:p");
        diagnostics.Arrange("server challenge", "realm=\"r\"");
        ISaslExchange exchange = new SaslAuthenticator(Windows1252, () => "c", answerDigestMd5AsSspi: false, securityContexts: null)
            .Begin("DIGEST-MD5", Request(new NetworkCredential("u", "p")));

        byte[]? answer = await RespondAsync(exchange, "realm=\"r\""u8);

        diagnostics.Act("answer", answer);
        diagnostics.Assert("answer", null, answer);
        Assert.IsNull(answer);
    }

    // BL-781: curl 8.21.0's Schannel build exits 94, "An authentication function returned an
    // error", sending nothing after these challenges; the OpenSSL build cancels with exit 67.
    [TestMethod]
    [DataRow("realm=\"localhost\",nonce=\"OA6MG9tEQGm2hh\",qop=\"auth\"", DisplayName = "No algorithm")]
    [DataRow("realm=\"localhost\",nonce=\"OA6MG9tEQGm2hh\",qop=\"auth-int\",algorithm=md5-sess", DisplayName = "qop=\"auth-int\"")]
    [DataRow("realm=\"localhost\",qop=\"auth\",algorithm=md5-sess", DisplayName = "No nonce")]
    public async Task Begin_DigestMd5ChallengeSspiRejects_FailsWithAuthError(string challenge)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "DIGEST-MD5");
        diagnostics.Arrange("credentials", "user:pencil");
        diagnostics.Arrange("server challenge", challenge);
        ISaslExchange sspi = new SaslAuthenticator(Windows1252, () => "c", answerDigestMd5AsSspi: true, securityContexts: null)
            .Begin("DIGEST-MD5", Request(new NetworkCredential("user", "pencil")));
        ISaslExchange curl = new SaslAuthenticator(Windows1252, () => "c", answerDigestMd5AsSspi: false, securityContexts: null)
            .Begin("DIGEST-MD5", Request(new NetworkCredential("user", "pencil")));

        SaslAuthenticationFailedException failure = await Assert.ThrowsExactlyAsync<SaslAuthenticationFailedException>(
            async () => await RespondAsync(sspi, Encoding.ASCII.GetBytes(challenge)));
        byte[]? curlAnswer = await RespondAsync(curl, Encoding.ASCII.GetBytes(challenge));

        diagnostics.Act("SSPI exception message", failure.Message);
        diagnostics.Act("SSPI exit code", failure.ExitCode);
        diagnostics.Act("curl answer", curlAnswer);
        diagnostics.Assert("exit code", CurlExitCode.AuthError, failure.ExitCode);
        diagnostics.Diff("message", "An authentication function returned an error", failure.Message);
        Assert.AreEqual(CurlExitCode.AuthError, failure.ExitCode);
        Assert.AreEqual("An authentication function returned an error", failure.Message);
        Assert.IsNull(curlAnswer);
    }

    [TestMethod]
    [DataRow("NTLM")]
    [DataRow("SCRAM-SHA-256")]
    public void Begin_MechanismNotBuiltWithoutSecurityContexts_Throws(string mechanism)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", mechanism);
        diagnostics.Arrange("credentials", "u:p");

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => Authenticator.Begin(mechanism, Request(new NetworkCredential("u", "p"))));

        diagnostics.Act("exception message", exception.Message);
        diagnostics.Assert("parameter name", "mechanism", exception.ParamName);
        Assert.AreEqual("mechanism", exception.ParamName);
    }

    [TestMethod]
    [DataRow(new[] { "EXTERNAL", "GSSAPI", "DIGEST-MD5", "CRAM-MD5", "NTLM", "OAUTHBEARER", "XOAUTH2", "LOGIN", "PLAIN", "SCRAM-SHA-256" }, "DIGEST-MD5",
        DisplayName = "All ten: DIGEST-MD5")]
    [DataRow(new[] { "DIGEST-MD5", "CRAM-MD5", "PLAIN" }, "DIGEST-MD5", DisplayName = "DIGEST-MD5 CRAM-MD5 PLAIN: DIGEST-MD5")]
    [DataRow(new[] { "CRAM-MD5", "PLAIN" }, "CRAM-MD5", DisplayName = "CRAM-MD5 PLAIN: CRAM-MD5")]
    [DataRow(new[] { "NTLM", "OAUTHBEARER", "XOAUTH2", "LOGIN", "PLAIN" }, "PLAIN", DisplayName = "Without the MD5 mechanisms and no security contexts: PLAIN")]
    [DataRow(new[] { "EXTERNAL", "GSSAPI", "OAUTHBEARER", "XOAUTH2", "LOGIN", "SCRAM-SHA-256" }, "LOGIN", DisplayName = "Without PLAIN: LOGIN")]
    [DataRow(new[] { "EXTERNAL", "GSSAPI", "OAUTHBEARER", "XOAUTH2", "SCRAM-SHA-256" }, null, DisplayName = "No usable mechanism: none (exit 67)")]
    [DataRow(new[] { "LOGIN", "PLAIN" }, "PLAIN", DisplayName = "LOGIN PLAIN: PLAIN")]
    [DataRow(new[] { "plain" }, "PLAIN", DisplayName = "Offered in lower case: PLAIN")]
    [DataRow(new string[0], null, DisplayName = "Nothing offered")]
    public void ChooseMechanism_UserAndPassword_PicksAsCurl(string[] offered, string? expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("offered", string.Join(" ", offered));
        diagnostics.Arrange("credentials", "u:p");

        string? chosen = Authenticator.ChooseMechanism(Request(new NetworkCredential("u", "p")), offered);

        diagnostics.Act("chosen", chosen);
        diagnostics.Assert("chosen", expected, chosen);
        Assert.AreEqual(expected, chosen);
    }

    [TestMethod]
    [DataRow(new[] { "EXTERNAL", "LOGIN", "PLAIN" }, "PLAIN", DisplayName = "EXTERNAL LOGIN PLAIN: PLAIN")]
    [DataRow(new[] { "EXTERNAL", "LOGIN" }, "LOGIN", DisplayName = "EXTERNAL LOGIN: LOGIN, the identity is not sent")]
    public void ChooseMechanism_AuthorizationIdentity_DoesNotSkipLogin(string[] offered, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("offered", string.Join(" ", offered));
        diagnostics.Arrange("credentials", "u:p");
        diagnostics.Arrange("authorization identity", "z");

        string? chosen = Authenticator.ChooseMechanism(Request(new NetworkCredential("u", "p"), authorizationIdentity: "z"), offered);

        diagnostics.Act("chosen", chosen);
        diagnostics.Assert("chosen", expected, chosen);
        Assert.AreEqual(expected, chosen);
    }

    [TestMethod]
    [DataRow(new[] { "EXTERNAL", "GSSAPI", "DIGEST-MD5", "CRAM-MD5", "NTLM", "OAUTHBEARER", "XOAUTH2", "LOGIN", "PLAIN" }, "OAUTHBEARER", DisplayName = "Everything: OAUTHBEARER")]
    [DataRow(new[] { "XOAUTH2", "LOGIN", "PLAIN" }, "XOAUTH2", DisplayName = "Without OAUTHBEARER: XOAUTH2")]
    [DataRow(new[] { "PLAIN", "LOGIN" }, null, DisplayName = "PLAIN LOGIN with a token: none (exit 67)")]
    [DataRow(new[] { "DIGEST-MD5", "CRAM-MD5", "PLAIN" }, null, DisplayName = "DIGEST-MD5 CRAM-MD5 PLAIN with a token: none (exit 67)")]
    public void ChooseMechanism_BearerToken_PicksAsCurl(string[] offered, string? expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("offered", string.Join(" ", offered));
        diagnostics.Arrange("credentials", "u:p");
        diagnostics.Arrange("bearer token", "tok");

        string? chosen = Authenticator.ChooseMechanism(Request(new NetworkCredential("u", "p"), bearerToken: "tok"), offered);

        diagnostics.Act("chosen", chosen);
        diagnostics.Assert("chosen", expected, chosen);
        Assert.AreEqual(expected, chosen);
    }

    [TestMethod]
    public void ChooseMechanism_BearerTokenWithoutUser_PicksOAuthBearer()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("offered", string.Join(" ", AllTen));
        diagnostics.Arrange("credentials", "(none)");
        diagnostics.Arrange("bearer token", "tok");

        string? chosen = Authenticator.ChooseMechanism(Request(null, bearerToken: "tok"), AllTen);

        diagnostics.Act("chosen", chosen);
        diagnostics.Assert("chosen", "OAUTHBEARER", chosen);
        Assert.AreEqual("OAUTHBEARER", chosen);
    }

    [TestMethod]
    public void ChooseMechanism_NoCredentialsAndNoToken_PicksNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("offered", string.Join(" ", AllTen));
        diagnostics.Arrange("credentials", "(none)");

        string? chosen = Authenticator.ChooseMechanism(Request(null), AllTen);

        diagnostics.Act("chosen", chosen);
        diagnostics.Assert("chosen", null, chosen);
        Assert.IsNull(chosen);
    }

    [TestMethod]
    [DataRow("u", "", "EXTERNAL", "EXTERNAL", DisplayName = "AUTH=EXTERNAL -u u:: EXTERNAL")]
    [DataRow("u", "", "external", "EXTERNAL", DisplayName = "AUTH=external -u u:: EXTERNAL")]
    [DataRow("u", "p", "EXTERNAL", null, DisplayName = "AUTH=EXTERNAL -u u:p: none (exit 67)")]
    [DataRow("u", "", null, "PLAIN", DisplayName = "No AUTH= and -u u:: never EXTERNAL")]
    [DataRow("u", "", "*", "PLAIN", DisplayName = "AUTH=* and -u u:: never EXTERNAL")]
    public void ChooseMechanism_External_OnlyWhenNamedAndPasswordless(string user, string password, string? required, string? expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("offered", "EXTERNAL PLAIN");
        diagnostics.Arrange("credentials", user + ":" + password);
        diagnostics.Arrange("required mechanism", required);
        SaslRequest request = Request(new NetworkCredential(user, password), requiredMechanism: required);

        string? chosen = Authenticator.ChooseMechanism(request, ["EXTERNAL", "PLAIN"]);

        diagnostics.Act("chosen", chosen);
        diagnostics.Assert("chosen", expected, chosen);
        Assert.AreEqual(expected, chosen);
    }

    [TestMethod]
    public void ChooseMechanism_ExternalWithoutCredential_PicksNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("offered", "EXTERNAL");
        diagnostics.Arrange("credentials", "(none)");
        diagnostics.Arrange("required mechanism", "EXTERNAL");

        string? chosen = Authenticator.ChooseMechanism(Request(null, requiredMechanism: "EXTERNAL"), ["EXTERNAL"]);

        diagnostics.Act("chosen", chosen);
        diagnostics.Assert("chosen", null, chosen);
        Assert.IsNull(chosen);
    }

    [TestMethod]
    [DataRow("LOGIN", "LOGIN", DisplayName = "AUTH=LOGIN: LOGIN")]
    [DataRow("login", "LOGIN", DisplayName = "AUTH=login: LOGIN")]
    [DataRow("*", "PLAIN", DisplayName = "AUTH=*: as with no option")]
    [DataRow(null, "PLAIN", DisplayName = "No AUTH=: PLAIN")]
    [DataRow("CRAM-MD5", null, DisplayName = "AUTH=CRAM-MD5 not offered: none (exit 67)")]
    [DataRow("BOGUS", null, DisplayName = "AUTH=BOGUS: none (curl refuses it earlier, exit 3)")]
    public void ChooseMechanism_LoginOptions_RestrictTheChoice(string? required, string? expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("offered", "LOGIN PLAIN");
        diagnostics.Arrange("credentials", "u:p");
        diagnostics.Arrange("required mechanism", required);
        SaslRequest request = Request(new NetworkCredential("u", "p"), requiredMechanism: required);

        string? chosen = Authenticator.ChooseMechanism(request, ["LOGIN", "PLAIN"]);

        diagnostics.Act("chosen", chosen);
        diagnostics.Assert("chosen", expected, chosen);
        Assert.AreEqual(expected, chosen);
    }

    [TestMethod]
    public void ChooseMechanism_LoginOptionsCramMd5_SkipsDigestMd5()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("offered", "DIGEST-MD5 CRAM-MD5 PLAIN");
        diagnostics.Arrange("credentials", "user:pencil");
        diagnostics.Arrange("required mechanism", "CRAM-MD5");
        SaslRequest request = Request(new NetworkCredential("user", "pencil"), requiredMechanism: "CRAM-MD5");

        string? chosen = Authenticator.ChooseMechanism(request, ["DIGEST-MD5", "CRAM-MD5", "PLAIN"]);

        diagnostics.Act("chosen", chosen);
        diagnostics.Assert("chosen", "CRAM-MD5", chosen);
        Assert.AreEqual("CRAM-MD5", chosen);
    }

    [TestMethod]
    public void ChooseMechanism_TokenWithoutUser_SkipsTheMd5Mechanisms()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("offered", "DIGEST-MD5 CRAM-MD5 PLAIN");
        diagnostics.Arrange("credentials", "(none)");
        diagnostics.Arrange("bearer token", "tok");

        string? chosen = Authenticator.ChooseMechanism(Request(null, bearerToken: "tok"), ["DIGEST-MD5", "CRAM-MD5", "PLAIN"]);

        diagnostics.Act("chosen", chosen);
        diagnostics.Assert("chosen", null, chosen);
        Assert.IsNull(chosen);
    }

    // Plays a handler's side of one exchange: the initial response on the command line or in
    // answer to the first challenge, then RespondAsync for the rest. Returns the base64 lines sent.
    private static async Task<string[]> ConverseAsync(ISaslExchange exchange, bool sendInitialResponse, params string[] challenges)
    {
        List<string> lines = [];
        int next = sendInitialResponse ? 0 : 1;
        lines.Add(Base64(await InitialResponseAsync(exchange)));
        for (; next < challenges.Length; next++)
        {
            lines.Add(Base64(await RespondAsync(exchange, Convert.FromBase64String(challenges[next]))));
        }

        return [.. lines];
    }

    private static ValueTask<byte[]?> InitialResponseAsync(ISaslExchange exchange) =>
        exchange.GetInitialResponseAsync(CancellationToken.None);

    private static ValueTask<byte[]?> RespondAsync(ISaslExchange exchange, ReadOnlySpan<byte> challenge) =>
        exchange.RespondAsync(challenge.ToArray(), CancellationToken.None);

    private static string Base64(byte[]? bytes)
    {
        Assert.IsNotNull(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static SaslRequest Request(
        NetworkCredential? credential,
        string? authorizationIdentity = null,
        string? bearerToken = null,
        string? requiredMechanism = null) =>
        new(credential, authorizationIdentity, bearerToken, requiredMechanism, "smtp", Host);
}
