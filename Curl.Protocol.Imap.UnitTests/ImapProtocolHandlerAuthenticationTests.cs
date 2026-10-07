using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Imap.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Imap;

/// <summary>
/// Pins how an IMAP session logs in against curl 8.21.0: <c>AUTHENTICATE</c> through the SASL
/// authenticator, <c>SASL-IR</c> and <c>--sasl-ir</c>, <c>LOGIN</c> and its quoting,
/// <c>LOGINDISABLED</c>, the login options, and the exit code and message of every refusal.
/// Every case was recorded from real curl (the Schannel build) on 2026-09-28 with
/// <c>Record-CurlExchange.ps1 -Imap</c>, curl running
/// <c>-sS -u u:p imap://127.0.0.1:port/INBOX;UID=1</c> (BL-554 Notes). The URL here names no
/// mailbox, so <c>LIST "" *</c> follows the login where curl sent <c>SELECT</c> (BL-556).
/// </summary>
[TestClass]
public sealed class ImapProtocolHandlerAuthenticationTests
{
    private const string Url = "imap://127.0.0.1:18143/";

    /// <summary>Gets or sets the running test's context, which carries its diagnostics.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private const string Greeting = "* OK [CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN] ready\r\n";

    private const string Capability = "A001 CAPABILITY\r\n";

    private const string AccessDeniedNo = "Access denied. \u0002";

    private const string LoginDenied = "Login denied";

    private static readonly byte[] PlainMessage = Encoding.Latin1.GetBytes("\0u\0p");

    [TestMethod]
    public async Task ExecuteAsync_PlainOffered_AuthenticatesAfterTheFirstContinuation()
    {
        // plain-offered: AUTHENTICATE PLAIN, "+ ", AHUAcA==, OK.
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);

        ImapRun run = await RunAsync(
            Greeting + Caps("IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN") + "+ \r\nA002 OK Authenticated\r\n" + ListReply("A003") + LogoutReply("A004"), sasl);

        DiffSent(run, Capability + "A002 AUTHENTICATE PLAIN\r\nAHUAcA==\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n");
        Assert.AreEqual(Capability + "A002 AUTHENTICATE PLAIN\r\nAHUAcA==\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n", run.Sent);
        AssertResult(run, TransferResult.Success(0));
        Assert.AreEqual(TransferResult.Success(0), run.Result);
        (SaslRequest request, string[] offered) = sasl.Choices.Single();
        CollectionAssert.AreEqual(new[] { "PLAIN", "LOGIN" }, offered);
        Assert.AreEqual("imap", request.ServiceName);
        Assert.AreEqual("127.0.0.1", request.Host);
        Assert.AreEqual("u", request.Credential!.UserName);
        Assert.IsNull(request.RequiredMechanism);
        Assert.IsEmpty(sasl.Challenges);
    }

    [TestMethod]
    public async Task ExecuteAsync_MailOptions_ReachTheSaslRequest()
    {
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);
        var mail = new MailRequestOptions { SaslAuthorizationIdentity = "boss", BearerToken = "tok", ServiceName = "mail" };

        await RunAsync(Greeting + Caps("AUTH=PLAIN") + "+ \r\nA002 OK done\r\n" + ListReply("A003") + LogoutReply("A004"), sasl, mail: mail);

        SaslRequest request = sasl.Begun.Single();
        Diagnostics.Act("sasl request", $"identity {request.AuthorizationIdentity}, service {request.ServiceName}, bearer token set {request.BearerToken is not null}");
        Diagnostics.Assert("authorization identity", "boss", request.AuthorizationIdentity);
        Diagnostics.Assert("bearer token", "tok", request.BearerToken);
        Diagnostics.Assert("service name", "mail", request.ServiceName);
        Assert.AreEqual("boss", request.AuthorizationIdentity);
        Assert.AreEqual("tok", request.BearerToken);
        Assert.AreEqual("mail", request.ServiceName);
    }

    [TestMethod]
    [DataRow(Url, 18143, DisplayName = "The URL's port")]
    [DataRow("imap://127.0.0.1/", 143, DisplayName = "The scheme's default")]
    public async Task ExecuteAsync_SaslRequest_CarriesTheConnectionsPort(string url, int expected)
    {
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);

        await RunAsync(Greeting + Caps("AUTH=PLAIN") + "+ \r\nA002 OK done\r\n" + ListReply("A003") + LogoutReply("A004"), sasl, url: url);

        Diagnostics.Act("sasl request port", sasl.Begun.Single().Port);
        Diagnostics.Assert("port", expected, sasl.Begun.Single().Port);
        Assert.AreEqual(expected, sasl.Begun.Single().Port);
    }

    [TestMethod]
    public async Task ExecuteAsync_LoginMechanism_AnswersEachChallenge()
    {
        // opt-auth-login: AUTHENTICATE LOGIN, "+ VXNlcm5hbWU6", dQ==, "+ UGFzc3dvcmQ6", cA==, OK.
        var sasl = new FakeSaslAuthenticator("LOGIN", Latin1("u"), Latin1("p"));

        ImapRun run = await RunAsync(
            Greeting + Caps("AUTH=LOGIN") + "+ VXNlcm5hbWU6\r\n+ UGFzc3dvcmQ6\r\nA002 OK Authenticated\r\n" + ListReply("A003") + LogoutReply("A004"), sasl);

        DiffSent(run, Capability + "A002 AUTHENTICATE LOGIN\r\ndQ==\r\ncA==\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n");
        Assert.AreEqual(Capability + "A002 AUTHENTICATE LOGIN\r\ndQ==\r\ncA==\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n", run.Sent);
        AssertResult(run, TransferResult.Success(0));
        Assert.AreEqual(TransferResult.Success(0), run.Result);
        Assert.AreEqual("Password:", Encoding.Latin1.GetString(sasl.Challenges.Single()));
    }

    [TestMethod]
    [DataRow("+ !!notbase64\r\n", DisplayName = "not base64")]
    [DataRow("+\r\n", DisplayName = "+ and its CR")]
    [DataRow("+ \t\r\n", DisplayName = "blanks only")]
    public async Task ExecuteAsync_ChallengeNotBase64_IsHandedOverEmpty(string challenge)
    {
        var sasl = new FakeSaslAuthenticator("LOGIN", Latin1("u"), Latin1("p"));

        ImapRun run = await RunAsync(
            Greeting + Caps("AUTH=LOGIN") + "+ \r\n" + challenge + "A002 OK done\r\n" + ListReply("A003") + LogoutReply("A004"), sasl);

        AssertResult(run, TransferResult.Success(0));
        Assert.AreEqual(TransferResult.Success(0), run.Result);
        Assert.IsEmpty(sasl.Challenges.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_UntaggedLinesDuringAuthenticate_AreSkipped()
    {
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);

        ImapRun run = await RunAsync(
            Greeting + Caps("AUTH=PLAIN") + "* 1 EXISTS\r\n+ \r\n* OK still\r\nA002 OK done\r\n" + ListReply("A003") + LogoutReply("A004"), sasl);

        DiffSent(run, Capability + "A002 AUTHENTICATE PLAIN\r\nAHUAcA==\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n");
        Assert.AreEqual(Capability + "A002 AUTHENTICATE PLAIN\r\nAHUAcA==\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n", run.Sent);
        AssertResult(run, TransferResult.Success(0));
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    [DataRow("IMAP4rev1 SASL-IR AUTH=PLAIN", false, DisplayName = "saslir-cap-only")]
    [DataRow("IMAP4rev1 sasl-ir auth=PLAIN", false, DisplayName = "lower-caps-ir")]
    [DataRow("IMAP4rev1 SASL-IR AUTH=PLAIN", true, DisplayName = "saslir-flag")]
    [DataRow("IMAP4rev1 AUTH=PLAIN", true, DisplayName = "saslir-flag-nocap")]
    public async Task ExecuteAsync_SaslIrAdvertisedOrAsked_SendsTheInitialResponseOnTheCommand(string capabilities, bool saslIr)
    {
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);
        var mail = new MailRequestOptions { SaslInitialResponse = saslIr };

        ImapRun run = await RunAsync(Greeting + Caps(capabilities) + "A002 OK Authenticated\r\n" + ListReply("A003") + LogoutReply("A004"), sasl, mail: mail);

        Assert.AreEqual(Capability + "A002 AUTHENTICATE PLAIN AHUAcA==\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n", run.Sent);
        AssertResult(run, TransferResult.Success(0));
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_LoginMechanismUnderSaslIr_SendsTheUserOnTheCommand()
    {
        // login-mech-ir: AUTHENTICATE LOGIN dQ==, "+ UGFzc3dvcmQ6", cA==, OK.
        var sasl = new FakeSaslAuthenticator("LOGIN", Latin1("u"), Latin1("p"));
        var mail = new MailRequestOptions { SaslInitialResponse = true, LoginOptions = "AUTH=LOGIN" };

        ImapRun run = await RunAsync(
            Greeting + Caps("AUTH=PLAIN AUTH=LOGIN") + "+ UGFzc3dvcmQ6\r\nA002 OK Authenticated\r\n" + ListReply("A003") + LogoutReply("A004"), sasl, mail: mail);

        Assert.AreEqual(Capability + "A002 AUTHENTICATE LOGIN dQ==\r\ncA==\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n", run.Sent);
        AssertResult(run, TransferResult.Success(0));
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyMessages_AreSentAsEquals()
    {
        var sasl = new FakeSaslAuthenticator("EXTERNAL", [], Array.Empty<byte>());
        var mail = new MailRequestOptions { SaslInitialResponse = true, LoginOptions = "AUTH=EXTERNAL" };

        ImapRun run = await RunAsync(Greeting + Caps("AUTH=EXTERNAL") + "+ \r\nA002 OK done\r\n" + ListReply("A003") + LogoutReply("A004"), sasl, mail: mail);

        Assert.AreEqual(Capability + "A002 AUTHENTICATE EXTERNAL =\r\n=\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n", run.Sent);
        AssertResult(run, TransferResult.Success(0));
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ExternalNamedWithoutUser_AuthenticatesAndIsAskedForFirst()
    {
        var sasl = new FakeSaslAuthenticator("EXTERNAL", []);
        var mail = new MailRequestOptions { LoginOptions = "AUTH=EXTERNAL" };

        ImapRun run = await RunAsync(
            Greeting + Caps("AUTH=PLAIN AUTH=EXTERNAL") + "+ \r\nA002 OK done\r\n" + ListReply("A003") + LogoutReply("A004"), sasl, user: null, mail: mail);

        DiffSent(run, Capability + "A002 AUTHENTICATE EXTERNAL\r\n=\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n");
        Diagnostics.Assert("required mechanism", "EXTERNAL", sasl.Choices.Single().Request.RequiredMechanism);
        AssertOffered(sasl, "EXTERNAL");
        Assert.AreEqual(Capability + "A002 AUTHENTICATE EXTERNAL\r\n=\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n", run.Sent);
        Assert.AreEqual("EXTERNAL", sasl.Choices.Single().Request.RequiredMechanism);
        CollectionAssert.AreEqual(new[] { "EXTERNAL" }, sasl.Choices.Single().Offered);
        Assert.AreEqual("EXTERNAL", sasl.Begun.Single().RequiredMechanism);
    }

    [TestMethod]
    public async Task ExecuteAsync_ExternalNamedButNotOfferedWithoutUser_DoesNotLogIn()
    {
        var sasl = new FakeSaslAuthenticator("EXTERNAL", []);
        var mail = new MailRequestOptions { LoginOptions = "AUTH=EXTERNAL" };

        ImapRun run = await RunAsync(Greeting + Caps("AUTH=PLAIN") + ListReply("A002") + LogoutReply("A003"), sasl, user: null, mail: mail);

        DiffSent(run, Capability + "A002 LIST \"\" *\r\nA003 LOGOUT\r\n");
        Assert.AreEqual(Capability + "A002 LIST \"\" *\r\nA003 LOGOUT\r\n", run.Sent);
        Assert.IsEmpty(sasl.Choices);
    }

    [TestMethod]
    public async Task ExecuteAsync_ExternalAndPlainNamed_FallsBackToTheOtherWhenExternalIsNotChosen()
    {
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);
        var mail = new MailRequestOptions { LoginOptions = "AUTH=EXTERNAL;AUTH=PLAIN" };

        ImapRun run = await RunAsync(Greeting + Caps("AUTH=PLAIN AUTH=LOGIN") + "+ \r\nA002 OK done\r\n" + ListReply("A003") + LogoutReply("A004"), sasl, mail: mail);

        DiffSent(run, Capability + "A002 AUTHENTICATE PLAIN\r\nAHUAcA==\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n");
        Assert.AreEqual(Capability + "A002 AUTHENTICATE PLAIN\r\nAHUAcA==\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n", run.Sent);
        Assert.HasCount(2, sasl.Choices);
        Assert.IsNull(sasl.Begun.Single().RequiredMechanism);
    }

    [TestMethod]
    public async Task ExecuteAsync_TwoMechanismsNamed_ChoosesAmongBoth()
    {
        // opt-two: AUTH=LOGIN;AUTH=PLAIN with only PLAIN offered authenticates with PLAIN.
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);
        var mail = new MailRequestOptions { LoginOptions = "AUTH=LOGIN;AUTH=PLAIN" };

        ImapRun run = await RunAsync(Greeting + Caps("IMAP4rev1 AUTH=PLAIN") + "+ \r\nA002 OK Authenticated\r\n" + ListReply("A003") + LogoutReply("A004"), sasl, mail: mail);

        DiffSent(run, Capability + "A002 AUTHENTICATE PLAIN\r\nAHUAcA==\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n");
        Assert.AreEqual(Capability + "A002 AUTHENTICATE PLAIN\r\nAHUAcA==\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n", run.Sent);
    }

    [TestMethod]
    [DataRow("auth=LOGIN", DisplayName = "opt-lower-key")]
    [DataRow("AUTH=login", DisplayName = "opt-lower-mech")]
    [DataRow("AUTH=LOGIN;", DisplayName = "url-opt-trailing")]
    public async Task ExecuteAsync_LoginOptionNamesAMechanism_OnlyItIsOffered(string loginOptions)
    {
        var sasl = new FakeSaslAuthenticator("LOGIN", Latin1("u"), Latin1("p"));
        var mail = new MailRequestOptions { LoginOptions = loginOptions };

        ImapRun run = await RunAsync(
            Greeting + Caps("IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN") + "+ VXNlcm5hbWU6\r\n+ UGFzc3dvcmQ6\r\nA002 OK Authenticated\r\n" + ListReply("A003") + LogoutReply("A004"),
            sasl,
            mail: mail);

        DiffSent(run, Capability + "A002 AUTHENTICATE LOGIN\r\ndQ==\r\ncA==\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n");
        Assert.AreEqual(Capability + "A002 AUTHENTICATE LOGIN\r\ndQ==\r\ncA==\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n", run.Sent);
        CollectionAssert.AreEqual(new[] { "LOGIN" }, sasl.Choices.Single().Offered);
    }

    [TestMethod]
    public async Task ExecuteAsync_UrlLoginOptions_AreReadWhenLoginOptionsIsNotGiven()
    {
        // url-options: imap://u:p;AUTH=LOGIN@host/ authenticates with LOGIN.
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);

        ImapRun run = await RunAsync(
            Greeting + Caps("AUTH=PLAIN AUTH=LOGIN") + ListReply("A002") + LogoutReply("A003"), sasl, url: "imap://u:p;AUTH=LOGIN@127.0.0.1:18143/");

        CollectionAssert.AreEqual(new[] { "LOGIN" }, sasl.Choices.Single().Offered);
        AssertResult(run, TransferResult.Failure(CurlExitCode.LoginDenied, LoginDenied));
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, LoginDenied), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_LoginOptionsGiven_WinOverTheUrls()
    {
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);
        var mail = new MailRequestOptions { LoginOptions = "AUTH=PLAIN" };

        await RunAsync(
            Greeting + Caps("AUTH=PLAIN AUTH=LOGIN") + "+ \r\nA002 OK done\r\n" + ListReply("A003") + LogoutReply("A004"),
            sasl,
            mail: mail,
            url: "imap://u:p;AUTH=LOGIN@127.0.0.1:18143/");

        AssertOffered(sasl, "PLAIN");
        CollectionAssert.AreEqual(new[] { "PLAIN" }, sasl.Choices.Single().Offered);
    }

    [TestMethod]
    public async Task ExecuteAsync_NamedMechanismNotOffered_FailsWithLoginDeniedAndSendsNothingMore()
    {
        // opt-login-notoffered: exit 67, "Login denied", nothing after CAPABILITY.
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);
        var mail = new MailRequestOptions { LoginOptions = "AUTH=LOGIN" };

        ImapRun run = await RunAsync(Greeting + Caps("IMAP4rev1 AUTH=PLAIN"), sasl, mail: mail);

        DiffSent(run, Capability);
        Assert.AreEqual(Capability, run.Sent);
        AssertResult(run, TransferResult.Failure(CurlExitCode.LoginDenied, LoginDenied));
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, LoginDenied), run.Result);
    }

    [TestMethod]
    [DataRow("AUTH=+LOGIN", DisplayName = "opt-plus-login")]
    [DataRow("AUTH=+login", DisplayName = "opt-plus-lower")]
    public async Task ExecuteAsync_PlusLoginOption_SendsLoginEvenWithMechanismsOffered(string loginOptions)
    {
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);
        var mail = new MailRequestOptions { LoginOptions = loginOptions };

        ImapRun run = await RunAsync(
            Greeting + Caps("IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN") + "A002 OK LOGIN completed\r\n" + ListReply("A003") + LogoutReply("A004"), sasl, mail: mail);

        DiffSent(run, Capability + "A002 LOGIN u p\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n");
        Assert.AreEqual(Capability + "A002 LOGIN u p\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n", run.Sent);
        AssertResult(run, TransferResult.Success(0));
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    [DataRow("IMAP4rev1", null, DisplayName = "none-offered")]
    [DataRow("IMAP4rev1 AUTH=FOO", null, DisplayName = "unknown-mech-only")]
    [DataRow("IMAP4rev1 AUTH=", null, DisplayName = "bare AUTH=")]
    [DataRow("IMAP4rev1", "AUTH=*", DisplayName = "opt-star")]
    public async Task ExecuteAsync_NoMechanismChosen_SendsLogin(string capabilities, string? loginOptions)
    {
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);
        var mail = new MailRequestOptions { LoginOptions = loginOptions };

        ImapRun run = await RunAsync(Greeting + Caps(capabilities) + "A002 OK LOGIN completed\r\n" + ListReply("A003") + LogoutReply("A004"), sasl, mail: mail);

        DiffSent(run, Capability + "A002 LOGIN u p\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n");
        Assert.AreEqual(Capability + "A002 LOGIN u p\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n", run.Sent);
        AssertResult(run, TransferResult.Success(0));
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_HandlerWithoutAuthenticator_SendsLoginWhateverIsOffered()
    {
        ImapRun run = await RunAsync(Greeting + Caps("AUTH=PLAIN AUTH=LOGIN") + "A002 OK LOGIN completed\r\n" + ListReply("A003") + LogoutReply("A004"), sasl: null);

        DiffSent(run, Capability + "A002 LOGIN u p\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n");
        Assert.AreEqual(Capability + "A002 LOGIN u p\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n", run.Sent);
        AssertResult(run, TransferResult.Success(0));
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    [DataRow("us er", "a\"b\\c d", "\"us er\" \"a\\\"b\\\\c d\"", DisplayName = "quoted")]
    [DataRow("u", "", "u ", DisplayName = "empty-pass")]
    [DataRow("u(1)", "%*]{", "\"u(1)\" \"%*]{\"", DisplayName = "every special")]
    public async Task ExecuteAsync_Login_QuotesUserAndPasswordAsCurlDoes(string user, string password, string arguments)
    {
        ImapRun run = await RunAsync(Greeting + Caps("IMAP4rev1") + "A002 OK LOGIN completed\r\n" + ListReply("A003") + LogoutReply("A004"), null, user, password);

        DiffSent(run, Capability + "A002 LOGIN " + arguments + "\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n");
        Assert.AreEqual(Capability + "A002 LOGIN " + arguments + "\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n", run.Sent);
    }

    [TestMethod]
    [DataRow("A002 NO denied\r\n", "\u0002", DisplayName = "login-no")]
    [DataRow("A002 BAD what\r\n", "\u0002", DisplayName = "login-bad")]
    [DataRow("A002 NO\r\n", "\u0002", DisplayName = "login-no-text")]
    [DataRow("A002 PREAUTH x\r\n", "\u0003", DisplayName = "login-preauth")]
    public async Task ExecuteAsync_LoginRefused_FailsWithAccessDeniedAndNoLogout(string loginReply, string code)
    {
        ImapRun run = await RunAsync(Greeting + Caps("IMAP4rev1") + loginReply, null);

        DiffSent(run, Capability + "A002 LOGIN u p\r\n");
        Assert.AreEqual(Capability + "A002 LOGIN u p\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Access denied. " + code), run.Result);
    }

    [TestMethod]
    public void AccessDenied_NotOk_EndsInByte2()
    {
        Diagnostics.Arrange("status", ImapResponseStatus.NotOk);
        string message = ImapSessionMessages.AccessDenied(ImapResponseStatus.NotOk);
        Diagnostics.Act("message", DiagnosticText.Escape(message));

        Diagnostics.Diff("message", AccessDeniedNo, message);
        Assert.AreEqual(AccessDeniedNo, ImapSessionMessages.AccessDenied(ImapResponseStatus.NotOk));
    }

    [TestMethod]
    [DataRow("A002 NO denied\r\n", DisplayName = "auth-no")]
    [DataRow("A002 OK too soon\r\n", DisplayName = "OK before any message")]
    [DataRow("+ \r\n+ more\r\n", DisplayName = "a challenge the exchange cannot answer")]
    public async Task ExecuteAsync_AuthenticateRefused_FailsWithLoginDeniedAndNoLogout(string replies)
    {
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);

        ImapRun run = await RunAsync(Greeting + Caps("AUTH=PLAIN") + replies, sasl);

        Assert.StartsWith(Capability + "A002 AUTHENTICATE PLAIN\r\n", run.Sent);
        Assert.DoesNotContain("LOGOUT", run.Sent);
        AssertResult(run, TransferResult.Failure(CurlExitCode.LoginDenied, LoginDenied));
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, LoginDenied), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_BearerOnlyAndOkAtOnce_FailsWithLoginDenied()
    {
        // bearer-only: AUTHENTICATE XOAUTH2 answered OK before any message is exit 67.
        var sasl = new FakeSaslAuthenticator("XOAUTH2", Latin1("token"));
        var mail = new MailRequestOptions { BearerToken = "tok" };

        ImapRun run = await RunAsync(Greeting + Caps("IMAP4rev1 AUTH=XOAUTH2") + "A002 OK Authenticated\r\n", sasl, user: null, mail: mail);

        Assert.AreEqual(Capability + "A002 AUTHENTICATE XOAUTH2\r\n", run.Sent);
        AssertResult(run, TransferResult.Failure(CurlExitCode.LoginDenied, LoginDenied));
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, LoginDenied), run.Result);
    }

    [TestMethod]
    [DataRow("IMAP4rev1", null, "tok", DisplayName = "bearer-none-offered")]
    [DataRow("IMAP4rev1 LOGINDISABLED", null, null, DisplayName = "logindisabled")]
    [DataRow("IMAP4rev1 logindisabled", null, null, DisplayName = "lower-logindisabled")]
    [DataRow("IMAP4rev1 LOGINDISABLED AUTH=PLAIN", "AUTH=+LOGIN", null, DisplayName = "opt-plus-disabled")]
    public async Task ExecuteAsync_NoWayToLogIn_FailsWithLoginDeniedAndSendsNothingMore(string capabilities, string? loginOptions, string? bearer)
    {
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);
        var mail = new MailRequestOptions { LoginOptions = loginOptions, BearerToken = bearer };

        ImapRun run = await RunAsync(Greeting + Caps(capabilities), sasl, user: bearer is null ? "u" : null, mail: mail);

        DiffSent(run, Capability);
        Assert.AreEqual(Capability, run.Sent);
        AssertResult(run, TransferResult.Failure(CurlExitCode.LoginDenied, LoginDenied));
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, LoginDenied), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_LoginDisabledButMechanismOffered_Authenticates()
    {
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);

        ImapRun run = await RunAsync(Greeting + Caps("LOGINDISABLED AUTH=PLAIN") + "+ \r\nA002 OK done\r\n" + ListReply("A003") + LogoutReply("A004"), sasl);

        DiffSent(run, Capability + "A002 AUTHENTICATE PLAIN\r\nAHUAcA==\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n");
        Assert.AreEqual(Capability + "A002 AUTHENTICATE PLAIN\r\nAHUAcA==\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n", run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_CapabilityNotOk_StillAuthenticates()
    {
        // cap-no: CAPABILITY answered NO still leads to AUTHENTICATE PLAIN.
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);

        ImapRun run = await RunAsync(
            Greeting + "* CAPABILITY IMAP4rev1 AUTH=PLAIN\r\nA001 NO nope\r\n+ \r\nA002 OK Authenticated\r\n" + ListReply("A003") + LogoutReply("A004"), sasl);

        DiffSent(run, Capability + "A002 AUTHENTICATE PLAIN\r\nAHUAcA==\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n");
        Assert.AreEqual(Capability + "A002 AUTHENTICATE PLAIN\r\nAHUAcA==\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n", run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoUser_DoesNotLogIn()
    {
        // no-user: CAPABILITY, then straight on.
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);

        ImapRun run = await RunAsync(Greeting + Caps("AUTH=PLAIN") + ListReply("A002") + LogoutReply("A003"), sasl, user: null);

        DiffSent(run, Capability + "A002 LIST \"\" *\r\nA003 LOGOUT\r\n");
        Assert.AreEqual(Capability + "A002 LIST \"\" *\r\nA003 LOGOUT\r\n", run.Sent);
        Assert.IsEmpty(sasl.Choices);
    }

    [TestMethod]
    public async Task ExecuteAsync_PreauthGreeting_DoesNotLogIn()
    {
        // preauth: CAPABILITY, then straight on, with -u u:p.
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);

        ImapRun run = await RunAsync("* PREAUTH hi\r\n" + Caps("AUTH=PLAIN") + ListReply("A002") + LogoutReply("A003"), sasl);

        DiffSent(run, Capability + "A002 LIST \"\" *\r\nA003 LOGOUT\r\n");
        Assert.AreEqual(Capability + "A002 LIST \"\" *\r\nA003 LOGOUT\r\n", run.Sent);
        Assert.IsEmpty(sasl.Choices);
    }

    [TestMethod]
    [DataRow("AUTH=BOGUS", DisplayName = "opt-bogus")]
    [DataRow("FOO=1", DisplayName = "opt-unknown-key")]
    [DataRow("AUTH=", DisplayName = "opt-empty-auth")]
    [DataRow(";AUTH=LOGIN", DisplayName = "leading ;")]
    public async Task ExecuteAsync_LoginOptionsCurlRejects_FailWithExit3BeforeAnything(string loginOptions)
    {
        var mail = new MailRequestOptions { LoginOptions = loginOptions };

        ImapRun run = await RunAsync(Greeting, null, mail: mail);

        DiffSent(run, string.Empty);
        AssertResult(run, TransferResult.Failure(CurlExitCode.UrlMalformat, "URL using bad/illegal format or missing URL"));
        Assert.AreEqual(string.Empty, run.Sent);
        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.UrlMalformat, "URL using bad/illegal format or missing URL"), run.Result);
    }

    [TestMethod]
    [DataRow("+ \r\n", DisplayName = "during AUTHENTICATE")]
    [DataRow("", DisplayName = "before any continuation")]
    public async Task ExecuteAsync_ServerClosesDuringAuthenticate_FailsWithExit56(string replies)
    {
        var sasl = new FakeSaslAuthenticator("LOGIN", Latin1("u"), Latin1("p"));

        ImapRun run = await RunAsync(Greeting + Caps("AUTH=LOGIN") + replies, sasl);

        AssertResult(run, TransferResult.Failure(CurlExitCode.RecvError, "response reading failed (errno: 0)"));
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RecvError, "response reading failed (errno: 0)"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerClosesDuringLogin_FailsWithExit56()
    {
        ImapRun run = await RunAsync(Greeting + Caps("IMAP4rev1"), null);

        DiffSent(run, Capability + "A002 LOGIN u p\r\n");
        Assert.AreEqual(Capability + "A002 LOGIN u p\r\n", run.Sent);
        AssertResult(run, TransferResult.Failure(CurlExitCode.RecvError, "response reading failed (errno: 0)"));
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RecvError, "response reading failed (errno: 0)"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_AfterStartTls_OnlyTheNewCapabilitiesMechanismsAreOffered()
    {
        var sasl = new FakeSaslAuthenticator("LOGIN", Latin1("u"), Latin1("p"));
        var secured = new ScriptedConnection(Latin1(
            "* CAPABILITY IMAP4rev1 AUTH=LOGIN\r\nA003 OK done\r\n+ \r\n+ \r\nA004 OK done\r\n" + ListReply("A005") + LogoutReply("A006")));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = Stream.Null,
            Credentials = new NetworkCredential("u", "p"),
            SslLevel = TransportSecurityLevel.Required,
        };

        Diagnostics.Arrange("url", Url);
        Diagnostics.Arrange("server", DiagnosticText.Escape(Greeting + Caps("STARTTLS AUTH=PLAIN") + "A002 OK go\r\n"));
        Diagnostics.Arrange("server after STARTTLS", "CAPABILITY IMAP4rev1 AUTH=LOGIN, then LOGIN exchange");

        ImapRun run = await ImapRun.ExecuteAsync(
            context,
            new ScriptedConnection(Latin1(Greeting + Caps("STARTTLS AUTH=PLAIN") + "A002 OK go\r\n")),
            sasl,
            ConnectResult.Connected(secured));
        string securedSent = Encoding.Latin1.GetString(secured.Sent);
        Diagnostics.Act("result", DiagnosticText.Result(run.Result));
        Diagnostics.Act("sent after STARTTLS", DiagnosticText.Escape(securedSent));

        AssertOffered(sasl, "LOGIN");
        Diagnostics.Diff("sent after STARTTLS", "A003 CAPABILITY\r\nA004 AUTHENTICATE LOGIN\r\ndQ==\r\ncA==\r\nA005 LIST \"\" *\r\nA006 LOGOUT\r\n", securedSent);
        AssertResult(run, TransferResult.Success(0));
        CollectionAssert.AreEqual(new[] { "LOGIN" }, sasl.Choices.Single().Offered);
        Assert.AreEqual("A003 CAPABILITY\r\nA004 AUTHENTICATE LOGIN\r\ndQ==\r\ncA==\r\nA005 LIST \"\" *\r\nA006 LOGOUT\r\n", Encoding.Latin1.GetString(secured.Sent));
        AssertResult(run, TransferResult.Success(0));
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public void Constructor_NullSaslAuthenticator_Throws()
    {
        Diagnostics.Arrange("sasl authenticator", "null");
        ArgumentNullException thrown = Assert.ThrowsExactly<ArgumentNullException>(
            () => new ImapProtocolHandler(new QueuedConnector(), new QueuedTlsProvider(), null!));
        Diagnostics.Act("thrown", thrown.GetType().Name);
        Diagnostics.Assert("thrown type", nameof(ArgumentNullException), thrown.GetType().Name);
    }

    private static string Caps(string words) => "* CAPABILITY " + words + "\r\nA001 OK done\r\n";

    private static string ListReply(string tag) => tag + " OK LIST completed\r\n";

    private static string LogoutReply(string tag) => "* BYE Logging out\r\n" + tag + " OK LOGOUT completed\r\n";

    private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);

    private static TransferContext Context(string url, string? user, string password, MailRequestOptions? mail) =>
        new()
        {
            Url = CurlUrl.Parse(url),
            Output = Stream.Null,
            Credentials = user is null ? null : new NetworkCredential(user, password),
            Mail = mail,
        };

    private async Task<ImapRun> RunAsync(
        string replies,
        FakeSaslAuthenticator? sasl,
        string? user = "u",
        string password = "p",
        MailRequestOptions? mail = null,
        string url = Url)
    {
        Diagnostics.Arrange("url", url);
        Diagnostics.Arrange("user", DiagnosticText.Escape(user));
        Diagnostics.Arrange("password", DiagnosticText.Escape(password));
        Diagnostics.Arrange("sasl authenticator", sasl is null ? "none" : "fake");
        Diagnostics.Arrange("login options", DiagnosticText.Escape(mail?.LoginOptions));
        Diagnostics.Arrange("sasl initial response", mail?.SaslInitialResponse);
        Diagnostics.Arrange("bearer token set", mail?.BearerToken is not null);
        Diagnostics.Arrange("server", DiagnosticText.Escape(replies));
        ImapRun run = await ImapRun.ExecuteAsync(Context(url, user, password, mail), new ScriptedConnection(Latin1(replies)), sasl);
        Diagnostics.Act("result", DiagnosticText.Result(run.Result));
        Diagnostics.Act("sent", DiagnosticText.Escape(run.Sent));
        return run;
    }

    private void DiffSent(ImapRun run, string expected) =>
        Diagnostics.Diff("sent", expected, run.Sent);

    private void AssertResult(ImapRun run, TransferResult expected) =>
        Diagnostics.Assert("result", DiagnosticText.Result(expected), DiagnosticText.Result(run.Result));

    private void AssertOffered(FakeSaslAuthenticator sasl, string expected) =>
        Diagnostics.Assert("offered mechanisms", expected, string.Join(",", sasl.Choices.Single().Offered));
}
