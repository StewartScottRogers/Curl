using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

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

    [TestMethod]
    [DataRow(null, "AHUAcA==", DisplayName = "--sasl-ir -u u:p: AUTH PLAIN AHUAcA==")]
    [DataRow("z", "egB1AHA=", DisplayName = "--sasl-ir --sasl-authzid z -u u:p: AUTH PLAIN egB1AHA=")]
    public void Begin_Plain_InitialResponseMatchesCurl(string? authorizationIdentity, string expected)
    {
        ISaslExchange exchange = Authenticator.Begin("PLAIN", Request(new NetworkCredential("u", "p"), authorizationIdentity: authorizationIdentity));

        Assert.AreEqual("PLAIN", exchange.Mechanism);
        Assert.AreEqual(expected, Base64(exchange.InitialResponse));
        Assert.IsNull(exchange.Respond([]));
    }

    [TestMethod]
    public void Begin_PlainWithoutInitialResponse_AnswersTheEmptyChallengeWithTheSameMessage()
    {
        // Measured: AUTH PLAIN, 334 (empty), AHUAcA==, 235.
        string[] lines = Converse(Authenticator.Begin("PLAIN", Request(new NetworkCredential("u", "p"))), sendInitialResponse: false, "");

        CollectionAssert.AreEqual(new[] { "AHUAcA==" }, lines);
    }

    [TestMethod]
    public void Begin_LoginWithInitialResponse_SendsTheUserThenThePassword()
    {
        // Measured: AUTH LOGIN dQ==, 334 UGFzc3dvcmQ6, cA==, 235.
        string[] lines = Converse(Authenticator.Begin("LOGIN", Request(new NetworkCredential("u", "p"))), sendInitialResponse: true, "UGFzc3dvcmQ6");

        CollectionAssert.AreEqual(new[] { "dQ==", "cA==" }, lines);
    }

    [TestMethod]
    public void Begin_LoginWithoutInitialResponse_AnswersUsernameThenPassword()
    {
        // Measured: AUTH LOGIN, 334 VXNlcm5hbWU6, dQ==, 334 UGFzc3dvcmQ6, cA==, 235.
        string[] lines = Converse(
            Authenticator.Begin("LOGIN", Request(new NetworkCredential("u", "p"))), sendInitialResponse: false, "VXNlcm5hbWU6", "UGFzc3dvcmQ6");

        CollectionAssert.AreEqual(new[] { "dQ==", "cA==" }, lines);
    }

    [TestMethod]
    public void Begin_LoginThirdChallenge_CannotAnswer()
    {
        ISaslExchange exchange = Authenticator.Begin("LOGIN", Request(new NetworkCredential("u", "p")));

        Assert.AreEqual("LOGIN", exchange.Mechanism);
        Assert.AreEqual("cA==", Base64(exchange.Respond([])));
        Assert.IsNull(exchange.Respond([]));
    }

    [TestMethod]
    [DataRow(null, DisplayName = "--sasl-ir --login-options AUTH=EXTERNAL -u u:: AUTH EXTERNAL dQ==")]
    [DataRow("z", DisplayName = "The authorization identity is not sent: AUTH EXTERNAL dQ==")]
    public void Begin_External_SendsTheUserName(string? authorizationIdentity)
    {
        ISaslExchange exchange = Authenticator.Begin("EXTERNAL", Request(new NetworkCredential("u", ""), authorizationIdentity: authorizationIdentity));

        Assert.AreEqual("EXTERNAL", exchange.Mechanism);
        Assert.AreEqual("dQ==", Base64(exchange.InitialResponse));
        Assert.IsNull(exchange.Respond([]));
    }

    [TestMethod]
    public void Begin_XOAuth2_InitialResponseMatchesCurl()
    {
        ISaslExchange exchange = Authenticator.Begin("XOAUTH2", Request(new NetworkCredential("u", ""), bearerToken: "tok"));

        Assert.AreEqual("XOAUTH2", exchange.Mechanism);
        Assert.AreEqual("dXNlcj11AWF1dGg9QmVhcmVyIHRvawEB", Base64(exchange.InitialResponse));
        Assert.IsNull(exchange.Respond(Encoding.ASCII.GetBytes("{\"status\":\"401\"}")), "curl fails an XOAUTH2 error continuation with exit 67.");
    }

    [TestMethod]
    public void Begin_XOAuth2WithoutUser_SendsAnEmptyUser()
    {
        ISaslExchange exchange = Authenticator.Begin("XOAUTH2", Request(null, bearerToken: "tok"));

        Assert.AreEqual("user=\u0001auth=Bearer tok\u0001\u0001", Windows1252.GetString(exchange.InitialResponse!));
    }

    [TestMethod]
    [DataRow("u", "bixhPXUsAWhvc3Q9MTI3LjAuMC4xAXBvcnQ9MTgxMjUBYXV0aD1CZWFyZXIgdG9rAQE=", DisplayName = "-u u: --oauth2-bearer tok, port 18125")]
    [DataRow("", "bixhPSwBaG9zdD0xMjcuMC4wLjEBcG9ydD0xODEyNQFhdXRoPUJlYXJlciB0b2sBAQ==", DisplayName = "--oauth2-bearer tok alone, port 18125")]
    public void OAuthBearerMessage_WithPort_MatchesCurl(string user, string expected)
    {
        string message = SaslAuthenticator.OAuthBearerMessage(user, Host, 18125, "tok");

        Assert.AreEqual(expected, Convert.ToBase64String(Windows1252.GetBytes(message)));
    }

    [TestMethod]
    public void Begin_OAuthBearer_LeavesThePortOutUntilTheRequestCarriesIt()
    {
        ISaslExchange exchange = Authenticator.Begin("OAUTHBEARER", Request(new NetworkCredential("u", ""), bearerToken: "tok"));

        Assert.AreEqual("OAUTHBEARER", exchange.Mechanism);
        Assert.AreEqual("n,a=u,\u0001host=127.0.0.1\u0001auth=Bearer tok\u0001\u0001", Windows1252.GetString(exchange.InitialResponse!));
    }

    [TestMethod]
    public void Begin_OAuthBearerErrorContinuation_AcknowledgedOnceWithOneByte()
    {
        ISaslExchange exchange = Authenticator.Begin("OAUTHBEARER", Request(null, bearerToken: "tok"));

        CollectionAssert.AreEqual(new byte[] { 0x01 }, exchange.Respond(Encoding.ASCII.GetBytes("{\"status\":\"401\"}")));
        Assert.IsNull(exchange.Respond([]));
    }

    [TestMethod]
    public void Begin_PlainWithoutCredential_SendsEmptyUserAndPassword()
    {
        ISaslExchange exchange = Authenticator.Begin("PLAIN", Request(null));

        CollectionAssert.AreEqual(new byte[] { 0, 0 }, exchange.InitialResponse);
    }

    [TestMethod]
    public void Begin_XOAuth2WithoutToken_SendsAnEmptyToken()
    {
        ISaslExchange exchange = Authenticator.Begin("XOAUTH2", Request(new NetworkCredential("u", "p")));

        Assert.AreEqual("user=u\u0001auth=Bearer \u0001\u0001", Windows1252.GetString(exchange.InitialResponse!));
    }

    [TestMethod]
    public void Begin_NonAsciiUser_EncodedInTheCredentialEncoding()
    {
        ISaslExchange exchange = Authenticator.Begin("LOGIN", Request(new NetworkCredential("é", "p")));

        CollectionAssert.AreEqual(new byte[] { 0xE9 }, exchange.InitialResponse);
    }

    [TestMethod]
    public void Begin_LowerCaseMechanism_ReportsTheSaslName()
    {
        ISaslExchange exchange = Authenticator.Begin("plain", Request(new NetworkCredential("u", "p")));

        Assert.AreEqual("PLAIN", exchange.Mechanism);
    }

    [TestMethod]
    [DataRow("user", "pencil", "dXNlciBlZTg3NzliY2M1MzFhNzhmNGRiMzc4YzQ3N2E1N2IwZA==", DisplayName = "-u user:pencil: user ee8779bc...")]
    [DataRow("user", "", "dXNlciAzYjFkZDYxZDNmODM4ZDAzZWVlYWJmZGRlOTFlYzVlOQ==", DisplayName = "-u user: : user 3b1dd61d...")]
    public void Begin_CramMd5_AnswersTheChallengeAsCurl(string user, string password, string expected)
    {
        // Measured: AUTH CRAM-MD5 (also under --sasl-ir), 334 PDE4OTYuNjk3MTcwOTUyQGxvY2FsaG9zdD4=, the answer, 235.
        ISaslExchange exchange = Authenticator.Begin("cram-md5", Request(new NetworkCredential(user, password)));

        Assert.AreEqual("CRAM-MD5", exchange.Mechanism);
        Assert.IsNull(exchange.InitialResponse);
        Assert.AreEqual(expected, Base64(exchange.Respond(Encoding.ASCII.GetBytes("<1896.697170952@localhost>"))));
        Assert.IsNull(exchange.Respond([]));
    }

    [TestMethod]
    public void Begin_CramMd5WithoutCredential_SendsAnEmptyUserAndPassword()
    {
        ISaslExchange exchange = Authenticator.Begin("CRAM-MD5", Request(null));

        StringAssert.StartsWith(Windows1252.GetString(exchange.Respond("c"u8)!), " ");
    }

    [TestMethod]
    [DataRow(true, "127.0.0.1", "81eed5b913007ab96776b8224946a866",
        "username=\"user\",realm=\"\",nonce=\"OA6MG9tEQGm2hh\",digest-uri=\"smtp/127.0.0.1\",cnonce=\"81eed5b913007ab96776b8224946a866\",nc=00000001,response=1ae34deb057c4c638fc093d4913d8f29,qop=auth,charset=utf-8",
        DisplayName = "Schannel (SSPI)")]
    [DataRow(false, "172.26.96.1", "dab6bbea0a329577a0c691f97f35a087",
        "username=\"user\",realm=\"localhost\",nonce=\"OA6MG9tEQGm2hh\",cnonce=\"dab6bbea0a329577a0c691f97f35a087\",nc=\"00000001\",digest-uri=\"smtp/172.26.96.1\",response=8c416297044dbc635e16a5432c6cc0a1,qop=auth",
        DisplayName = "OpenSSL")]
    public void Begin_DigestMd5_AnswersTheChallengeThenRspauthAsCurl(bool answerAsSspi, string host, string clientNonce, string expected)
    {
        // Measured: AUTH DIGEST-MD5, 334 <challenge>, the answer, 334 <rspauth>, an empty line, 235.
        var authenticator = new SaslAuthenticator(Windows1252, () => clientNonce, answerAsSspi);
        SaslRequest request = Request(new NetworkCredential("user", "pencil"), authorizationIdentity: "z") with { Host = host };
        ISaslExchange exchange = authenticator.Begin("DIGEST-MD5", request);

        Assert.AreEqual("DIGEST-MD5", exchange.Mechanism);
        Assert.IsNull(exchange.InitialResponse);
        byte[] challenge = Encoding.ASCII.GetBytes("realm=\"localhost\",nonce=\"OA6MG9tEQGm2hh\",qop=\"auth\",algorithm=md5-sess,charset=utf-8");
        Assert.AreEqual(expected, Windows1252.GetString(exchange.Respond(challenge)!));
        CollectionAssert.AreEqual(Array.Empty<byte>(), exchange.Respond("rspauth=ea40f60335c427b5527b84dbabcdfffd"u8));
        Assert.IsNull(exchange.Respond([]));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Begin_DigestMd5WithoutCredential_SendsAnEmptyUser(bool answerAsSspi)
    {
        ISaslExchange exchange = new SaslAuthenticator(Windows1252, () => "c", answerAsSspi).Begin("DIGEST-MD5", Request(null));

        StringAssert.StartsWith(
            Windows1252.GetString(exchange.Respond("nonce=\"n\",qop=\"auth\",algorithm=md5-sess"u8)!), "username=\"\",");
    }

    [TestMethod]
    public void Begin_DigestMd5ChallengeCurlCancels_AnswersNull()
    {
        ISaslExchange exchange = Authenticator.Begin("DIGEST-MD5", Request(new NetworkCredential("u", "p")));

        Assert.IsNull(exchange.Respond("realm=\"r\""u8));
    }

    [TestMethod]
    [DataRow("NTLM")]
    [DataRow("SCRAM-SHA-256")]
    public void Begin_MechanismNotBuilt_Throws(string mechanism)
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => Authenticator.Begin(mechanism, Request(new NetworkCredential("u", "p"))));

        Assert.AreEqual("mechanism", exception.ParamName);
    }

    [TestMethod]
    [DataRow(new[] { "EXTERNAL", "GSSAPI", "DIGEST-MD5", "CRAM-MD5", "NTLM", "OAUTHBEARER", "XOAUTH2", "LOGIN", "PLAIN", "SCRAM-SHA-256" }, "DIGEST-MD5",
        DisplayName = "All ten: DIGEST-MD5")]
    [DataRow(new[] { "DIGEST-MD5", "CRAM-MD5", "PLAIN" }, "DIGEST-MD5", DisplayName = "DIGEST-MD5 CRAM-MD5 PLAIN: DIGEST-MD5")]
    [DataRow(new[] { "CRAM-MD5", "PLAIN" }, "CRAM-MD5", DisplayName = "CRAM-MD5 PLAIN: CRAM-MD5")]
    [DataRow(new[] { "NTLM", "OAUTHBEARER", "XOAUTH2", "LOGIN", "PLAIN" }, "PLAIN", DisplayName = "Without the MD5 mechanisms: PLAIN until NTLM is built (BL-538)")]
    [DataRow(new[] { "EXTERNAL", "GSSAPI", "OAUTHBEARER", "XOAUTH2", "LOGIN", "SCRAM-SHA-256" }, "LOGIN", DisplayName = "Without PLAIN: LOGIN")]
    [DataRow(new[] { "EXTERNAL", "GSSAPI", "OAUTHBEARER", "XOAUTH2", "SCRAM-SHA-256" }, null, DisplayName = "No usable mechanism: none (exit 67)")]
    [DataRow(new[] { "LOGIN", "PLAIN" }, "PLAIN", DisplayName = "LOGIN PLAIN: PLAIN")]
    [DataRow(new[] { "plain" }, "PLAIN", DisplayName = "Offered in lower case: PLAIN")]
    [DataRow(new string[0], null, DisplayName = "Nothing offered")]
    public void ChooseMechanism_UserAndPassword_PicksAsCurl(string[] offered, string? expected)
    {
        Assert.AreEqual(expected, Authenticator.ChooseMechanism(Request(new NetworkCredential("u", "p")), offered));
    }

    [TestMethod]
    [DataRow(new[] { "EXTERNAL", "LOGIN", "PLAIN" }, "PLAIN", DisplayName = "EXTERNAL LOGIN PLAIN: PLAIN")]
    [DataRow(new[] { "EXTERNAL", "LOGIN" }, "LOGIN", DisplayName = "EXTERNAL LOGIN: LOGIN, the identity is not sent")]
    public void ChooseMechanism_AuthorizationIdentity_DoesNotSkipLogin(string[] offered, string expected)
    {
        Assert.AreEqual(expected, Authenticator.ChooseMechanism(Request(new NetworkCredential("u", "p"), authorizationIdentity: "z"), offered));
    }

    [TestMethod]
    [DataRow(new[] { "EXTERNAL", "GSSAPI", "DIGEST-MD5", "CRAM-MD5", "NTLM", "OAUTHBEARER", "XOAUTH2", "LOGIN", "PLAIN" }, "OAUTHBEARER", DisplayName = "Everything: OAUTHBEARER")]
    [DataRow(new[] { "XOAUTH2", "LOGIN", "PLAIN" }, "XOAUTH2", DisplayName = "Without OAUTHBEARER: XOAUTH2")]
    [DataRow(new[] { "PLAIN", "LOGIN" }, null, DisplayName = "PLAIN LOGIN with a token: none (exit 67)")]
    [DataRow(new[] { "DIGEST-MD5", "CRAM-MD5", "PLAIN" }, null, DisplayName = "DIGEST-MD5 CRAM-MD5 PLAIN with a token: none (exit 67)")]
    public void ChooseMechanism_BearerToken_PicksAsCurl(string[] offered, string? expected)
    {
        Assert.AreEqual(expected, Authenticator.ChooseMechanism(Request(new NetworkCredential("u", "p"), bearerToken: "tok"), offered));
    }

    [TestMethod]
    public void ChooseMechanism_BearerTokenWithoutUser_PicksOAuthBearer()
    {
        Assert.AreEqual("OAUTHBEARER", Authenticator.ChooseMechanism(Request(null, bearerToken: "tok"), AllTen));
    }

    [TestMethod]
    public void ChooseMechanism_NoCredentialsAndNoToken_PicksNothing()
    {
        Assert.IsNull(Authenticator.ChooseMechanism(Request(null), AllTen));
    }

    [TestMethod]
    [DataRow("u", "", "EXTERNAL", "EXTERNAL", DisplayName = "AUTH=EXTERNAL -u u:: EXTERNAL")]
    [DataRow("u", "", "external", "EXTERNAL", DisplayName = "AUTH=external -u u:: EXTERNAL")]
    [DataRow("u", "p", "EXTERNAL", null, DisplayName = "AUTH=EXTERNAL -u u:p: none (exit 67)")]
    [DataRow("u", "", null, "PLAIN", DisplayName = "No AUTH= and -u u:: never EXTERNAL")]
    [DataRow("u", "", "*", "PLAIN", DisplayName = "AUTH=* and -u u:: never EXTERNAL")]
    public void ChooseMechanism_External_OnlyWhenNamedAndPasswordless(string user, string password, string? required, string? expected)
    {
        SaslRequest request = Request(new NetworkCredential(user, password), requiredMechanism: required);

        Assert.AreEqual(expected, Authenticator.ChooseMechanism(request, ["EXTERNAL", "PLAIN"]));
    }

    [TestMethod]
    public void ChooseMechanism_ExternalWithoutCredential_PicksNothing()
    {
        Assert.IsNull(Authenticator.ChooseMechanism(Request(null, requiredMechanism: "EXTERNAL"), ["EXTERNAL"]));
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
        SaslRequest request = Request(new NetworkCredential("u", "p"), requiredMechanism: required);

        Assert.AreEqual(expected, Authenticator.ChooseMechanism(request, ["LOGIN", "PLAIN"]));
    }

    [TestMethod]
    public void ChooseMechanism_LoginOptionsCramMd5_SkipsDigestMd5()
    {
        SaslRequest request = Request(new NetworkCredential("user", "pencil"), requiredMechanism: "CRAM-MD5");

        Assert.AreEqual("CRAM-MD5", Authenticator.ChooseMechanism(request, ["DIGEST-MD5", "CRAM-MD5", "PLAIN"]));
    }

    [TestMethod]
    public void ChooseMechanism_TokenWithoutUser_SkipsTheMd5Mechanisms()
    {
        Assert.IsNull(Authenticator.ChooseMechanism(Request(null, bearerToken: "tok"), ["DIGEST-MD5", "CRAM-MD5", "PLAIN"]));
    }

    // Plays a handler's side of one exchange: the initial response on the command line or in
    // answer to the first challenge, then Respond for the rest. Returns the base64 lines sent.
    private static string[] Converse(ISaslExchange exchange, bool sendInitialResponse, params string[] challenges)
    {
        List<string> lines = [];
        int next = sendInitialResponse ? 0 : 1;
        lines.Add(Base64(exchange.InitialResponse));
        for (; next < challenges.Length; next++)
        {
            lines.Add(Base64(exchange.Respond(Convert.FromBase64String(challenges[next]))));
        }

        return [.. lines];
    }

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
