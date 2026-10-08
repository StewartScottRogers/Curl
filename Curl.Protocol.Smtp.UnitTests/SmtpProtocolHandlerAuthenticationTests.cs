using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smtp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Pins how an SMTP session authenticates with <c>AUTH</c> against curl 8.21.0: which
/// <c>EHLO</c> lines offer mechanisms, when the session authenticates at all, where the
/// initial response goes, how each <c>334</c> is answered, and the exit code of every
/// refusal. Every case was recorded from real curl (the Schannel build) on 2026-09-28 with
/// <c>Record-CurlExchange.ps1 -Smtp</c> (BL-541 Notes, ADR-0133). The fake authenticator
/// stands in for the mechanism, so these tests pin the handler's framing: the client bytes
/// are curl's for the credentials <c>u</c>/<c>p</c>.
/// </summary>
[TestClass]
public sealed class SmtpProtocolHandlerAuthenticationTests
{
    private const string Url = "smtp://127.0.0.1:18025/x";

    private const string Greeting = "220 localhost ESMTP\r\n";

    private const string EhloReply = "250-localhost\r\n250-AUTH PLAIN LOGIN\r\n250 OK\r\n";

    private const string Ehlo = "EHLO x\r\n";

    private const string Accepted = "235 Authentication successful\r\n";

    private const string HelpReplyAndBye = SmtpRun.HelpReply + "221 Bye\r\n";

    private const string HelpAndQuit = "HELP\r\nQUIT\r\n";

    /// <summary>PLAIN's message for <c>u</c>/<c>p</c>: NUL, user, NUL, password.</summary>
    private static readonly byte[] PlainMessage = "\0u\0p"u8.ToArray();

    /// <summary>Gets or sets the running test's context, which MSTest sets.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_Plain_SendsTheResponseAfterTheFirst334()
    {
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);

        SmtpRun run = await RunAsync(Greeting + EhloReply + "334 \r\n" + Accepted + HelpReplyAndBye, sasl);

        Diagnostics.Diff("sent", Ehlo + "AUTH PLAIN\r\nAHUAcA==\r\n" + HelpAndQuit, run.Sent);
        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Diagnostics.AssertValues("challenges handed to the authenticator", 0, sasl.Challenges.Count);
        Assert.AreEqual(Ehlo + "AUTH PLAIN\r\nAHUAcA==\r\n" + HelpAndQuit, run.Sent);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
        Assert.IsEmpty(sasl.Challenges);
    }

    [TestMethod]
    public async Task ExecuteAsync_Plain_PassesTheOfferedMechanismsAndTheRequest()
    {
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);
        var context = Context(new MailRequestOptions { SaslAuthorizationIdentity = "z", ServiceName = "svc" });

        await RunAsync(context, Greeting + EhloReply + "334 \r\n" + Accepted + HelpReplyAndBye, sasl);

        (SaslRequest request, string[] offered) = sasl.Choices.Single();
        Diagnostics.AssertValues("offered mechanisms", "PLAIN, LOGIN", string.Join(", ", offered));
        Diagnostics.AssertValues("user name", "u", request.Credential!.UserName);
        Diagnostics.AssertValues("password", "p", request.Credential.Password);
        Diagnostics.AssertValues("authorization identity", "z", request.AuthorizationIdentity);
        Diagnostics.AssertValues("bearer token", null, request.BearerToken);
        Diagnostics.AssertValues("required mechanism", null, request.RequiredMechanism);
        Diagnostics.AssertValues("service name", "svc", request.ServiceName);
        Diagnostics.AssertValues("host", "127.0.0.1", request.Host);
        CollectionAssert.AreEqual(new[] { "PLAIN", "LOGIN" }, offered);
        Assert.AreEqual("u", request.Credential!.UserName);
        Assert.AreEqual("p", request.Credential.Password);
        Assert.AreEqual("z", request.AuthorizationIdentity);
        Assert.IsNull(request.BearerToken);
        Assert.IsNull(request.RequiredMechanism);
        Assert.AreEqual("svc", request.ServiceName);
        Assert.AreEqual("127.0.0.1", request.Host);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoMailOptions_UsesTheSmtpServiceNameAndNoInitialResponse()
    {
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);
        var context = new TransferContext { Url = CurlUrl.Parse(Url), Output = Stream.Null, Credentials = new NetworkCredential("u", "p") };

        SmtpRun run = await RunAsync(context, Greeting + EhloReply + "334 \r\n" + Accepted + HelpReplyAndBye, sasl);

        Diagnostics.Diff("sent", Ehlo + "AUTH PLAIN\r\nAHUAcA==\r\n" + HelpAndQuit, run.Sent);
        Diagnostics.AssertValues("service name", "smtp", sasl.Choices.Single().Request.ServiceName);
        Assert.AreEqual(Ehlo + "AUTH PLAIN\r\nAHUAcA==\r\n" + HelpAndQuit, run.Sent);
        Assert.AreEqual("smtp", sasl.Choices.Single().Request.ServiceName);
    }

    [TestMethod]
    public async Task ExecuteAsync_SaslIr_SendsTheInitialResponseOnTheAuthLine()
    {
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);

        SmtpRun run = await RunAsync(Context(SaslIr), Greeting + EhloReply + Accepted + HelpReplyAndBye, sasl);

        Diagnostics.Diff("sent", Ehlo + "AUTH PLAIN AHUAcA==\r\n" + HelpAndQuit, run.Sent);
        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Assert.AreEqual(Ehlo + "AUTH PLAIN AHUAcA==\r\n" + HelpAndQuit, run.Sent);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_Login_AnswersEach334WithTheNextMessage()
    {
        var sasl = new FakeSaslAuthenticator("LOGIN", "u"u8.ToArray(), "p"u8.ToArray());

        SmtpRun run = await RunAsync(
            Greeting + EhloReply + "334 VXNlcm5hbWU6\r\n334 UGFzc3dvcmQ6\r\n" + Accepted + HelpReplyAndBye, sasl);

        Diagnostics.Diff("sent", Ehlo + "AUTH LOGIN\r\ndQ==\r\ncA==\r\n" + HelpAndQuit, run.Sent);
        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Diagnostics.AssertValues("second challenge", "Password:", Encoding.ASCII.GetString(sasl.Challenges.Single()));
        Assert.AreEqual(Ehlo + "AUTH LOGIN\r\ndQ==\r\ncA==\r\n" + HelpAndQuit, run.Sent);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
        Assert.AreEqual("Password:", Encoding.ASCII.GetString(sasl.Challenges.Single()));
    }

    [TestMethod]
    public async Task ExecuteAsync_LoginWithSaslIr_SendsTheUserOnTheAuthLineAndThePasswordAfter334()
    {
        var sasl = new FakeSaslAuthenticator("LOGIN", "u"u8.ToArray(), "p"u8.ToArray());

        SmtpRun run = await RunAsync(Context(SaslIr), Greeting + EhloReply + "334 UGFzc3dvcmQ6\r\n" + Accepted + HelpReplyAndBye, sasl);

        Diagnostics.Diff("sent", Ehlo + "AUTH LOGIN dQ==\r\ncA==\r\n" + HelpAndQuit, run.Sent);
        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Assert.AreEqual(Ehlo + "AUTH LOGIN dQ==\r\ncA==\r\n" + HelpAndQuit, run.Sent);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_MechanismWithoutInitialResponse_AnswersTheFirstChallengeEvenUnderSaslIr()
    {
        // curl -u u:p with CRAM-MD5 offered: the recorder's challenge and curl's digest.
        var sasl = new FakeSaslAuthenticator("CRAM-MD5", null, "u 05eea7f7bd83786044680b700b4965a4"u8.ToArray());

        SmtpRun run = await RunAsync(
            Context(SaslIr), Greeting + EhloReply + "334 PDE4OTYuNjk3MTcwOTUyQGxvY2FsaG9zdD4=\r\n" + Accepted + HelpReplyAndBye, sasl);

        Diagnostics.Diff("sent", Ehlo + "AUTH CRAM-MD5\r\ndSAwNWVlYTdmN2JkODM3ODYwNDQ2ODBiNzAwYjQ5NjVhNA==\r\n" + HelpAndQuit, run.Sent);
        Diagnostics.AssertValues("challenge", "<1896.697170952@localhost>", Encoding.ASCII.GetString(sasl.Challenges.Single()));
        Assert.AreEqual(Ehlo + "AUTH CRAM-MD5\r\ndSAwNWVlYTdmN2JkODM3ODYwNDQ2ODBiNzAwYjQ5NjVhNA==\r\n" + HelpAndQuit, run.Sent);
        Assert.AreEqual("<1896.697170952@localhost>", Encoding.ASCII.GetString(sasl.Challenges.Single()));
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyMessages_AreSentAsEquals()
    {
        // curl -u : --sasl-ir --login-options AUTH=LOGIN
        var sasl = new FakeSaslAuthenticator("LOGIN", [], Array.Empty<byte>());

        SmtpRun run = await RunAsync(Context(SaslIr), Greeting + EhloReply + "334 UGFzc3dvcmQ6\r\n" + Accepted + HelpReplyAndBye, sasl);

        Diagnostics.Diff("sent", Ehlo + "AUTH LOGIN =\r\n=\r\n" + HelpAndQuit, run.Sent);
        Assert.AreEqual(Ehlo + "AUTH LOGIN =\r\n=\r\n" + HelpAndQuit, run.Sent);
    }

    [TestMethod]
    [DataRow("535 5.7.8 bad", false, "AUTH PLAIN\r\n", DisplayName = "535 to AUTH")]
    [DataRow("535 5.7.8 bad", true, "AUTH PLAIN AHUAcA==\r\n", DisplayName = "535 to the initial response")]
    [DataRow("504 nope", false, "AUTH PLAIN\r\n", DisplayName = "504")]
    [DataRow("250 ok", true, "AUTH PLAIN AHUAcA==\r\n", DisplayName = "A 2xx other than 235")]
    [DataRow("334 ", true, "AUTH PLAIN AHUAcA==\r\n", DisplayName = "A challenge the exchange cannot answer")]
    [DataRow("235 ok", false, "AUTH PLAIN\r\n", DisplayName = "235 before any message")]
    public async Task ExecuteAsync_Refused_FailsWithLoginDeniedAndSendsNothingMore(string reply, bool saslIr, string auth)
    {
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);

        SmtpRun run = await RunAsync(
            Context(new MailRequestOptions { SaslInitialResponse = saslIr }), Greeting + EhloReply + reply + "\r\n" + HelpReplyAndBye, sasl);

        Diagnostics.Arrange("refusing reply", SmtpDiagnostics.Show(reply));
        Diagnostics.Arrange("--sasl-ir", saslIr);
        Diagnostics.Diff("sent", Ehlo + auth, run.Sent);
        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), run.Result);
        Assert.AreEqual(Ehlo + auth, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_OAuthBearerErrorContinuation_AcknowledgesItThenFailsOnTheFinalRefusal()
    {
        var sasl = new FakeSaslAuthenticator("OAUTHBEARER", "n,a=u,"u8.ToArray(), [0x01]);
        var context = Context(new MailRequestOptions { SaslInitialResponse = true, BearerToken = "tok" });

        SmtpRun run = await RunAsync(context, Greeting + EhloReply + "334 eyJzdGF0dXMiOiJpbnZhbGlkIn0=\r\n535 no\r\n" + HelpReplyAndBye, sasl);

        Diagnostics.Diff("sent", Ehlo + "AUTH OAUTHBEARER bixhPXUs\r\nAQ==\r\n", run.Sent);
        Diagnostics.AssertValues("exit code", CurlExitCode.LoginDenied, run.Result.ExitCode);
        Diagnostics.AssertValues("challenge", "{\"status\":\"invalid\"}", Encoding.ASCII.GetString(sasl.Challenges.Single()));
        Assert.AreEqual(Ehlo + "AUTH OAUTHBEARER bixhPXUs\r\nAQ==\r\n", run.Sent);
        Assert.AreEqual(CurlExitCode.LoginDenied, run.Result.ExitCode);
        Assert.AreEqual("{\"status\":\"invalid\"}", Encoding.ASCII.GetString(sasl.Challenges.Single()));
    }

    [TestMethod]
    public async Task ExecuteAsync_BearerTokenWithoutUser_Authenticates()
    {
        // curl --oauth2-bearer tok --sasl-ir, no -u
        var sasl = new FakeSaslAuthenticator("OAUTHBEARER", "n,a=,"u8.ToArray());
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = Stream.Null,
            Mail = new MailRequestOptions { SaslInitialResponse = true, BearerToken = "tok" },
        };

        SmtpRun run = await RunAsync(context, Greeting + EhloReply + Accepted + HelpReplyAndBye, sasl);

        Diagnostics.Diff("sent", Ehlo + "AUTH OAUTHBEARER bixhPSw=\r\n" + HelpAndQuit, run.Sent);
        Diagnostics.AssertValues("bearer token", "tok", sasl.Choices.Single().Request.BearerToken);
        Diagnostics.AssertValues("credential", null, sasl.Choices.Single().Request.Credential);
        Assert.AreEqual(Ehlo + "AUTH OAUTHBEARER bixhPSw=\r\n" + HelpAndQuit, run.Sent);
        Assert.AreEqual("tok", sasl.Choices.Single().Request.BearerToken);
        Assert.IsNull(sasl.Choices.Single().Request.Credential);
    }

    [TestMethod]
    public async Task ExecuteAsync_ExternalRequiredAndOfferedWithoutUser_AuthenticatesWithAnEmptyResponse()
    {
        // curl --login-options AUTH=EXTERNAL --sasl-ir, no -u
        var sasl = new FakeSaslAuthenticator("EXTERNAL", []);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = Stream.Null,
            Mail = new MailRequestOptions { SaslInitialResponse = true, LoginOptions = "AUTH=external" },
        };

        SmtpRun run = await RunAsync(context, Greeting + "250-localhost\r\n250-AUTH EXTERNAL\r\n250 OK\r\n" + Accepted + HelpReplyAndBye, sasl);

        Diagnostics.Diff("sent", Ehlo + "AUTH EXTERNAL =\r\n" + HelpAndQuit, run.Sent);
        Diagnostics.AssertValues("required mechanism", "external", sasl.Choices.Single().Request.RequiredMechanism);
        Assert.AreEqual(Ehlo + "AUTH EXTERNAL =\r\n" + HelpAndQuit, run.Sent);
        Assert.AreEqual("external", sasl.Choices.Single().Request.RequiredMechanism);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "No login options")]
    [DataRow("AUTH=EXTERNAL", DisplayName = "EXTERNAL required but not offered")]
    public async Task ExecuteAsync_NothingToAuthenticateWith_SendsNoAuth(string? loginOptions)
    {
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = Stream.Null,
            Mail = new MailRequestOptions { LoginOptions = loginOptions },
        };

        SmtpRun run = await RunAsync(context, Greeting + EhloReply + HelpReplyAndBye, sasl);

        Diagnostics.Arrange("login options", SmtpDiagnostics.Show(loginOptions));
        Diagnostics.Diff("sent", Ehlo + HelpAndQuit, run.Sent);
        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Diagnostics.AssertValues("authenticator choices", 0, sasl.Choices.Count);
        Assert.AreEqual(Ehlo + HelpAndQuit, run.Sent);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
        Assert.IsEmpty(sasl.Choices);
    }

    [TestMethod]
    [DataRow("250-localhost\r\n250 SIZE 1000\r\n", DisplayName = "No AUTH line")]
    [DataRow("250-localhost\r\n250-AUTH=PLAIN\r\n250 SIZE 1\r\n", DisplayName = "AUTH= is not the keyword")]
    [DataRow("250-localhost\r\n250-AUTH\tPLAIN\r\n250 SIZE 1\r\n", DisplayName = "A tab after AUTH is not the keyword")]
    public async Task ExecuteAsync_NoAuthOffered_SendsTheMailWithoutAuth(string ehloReply)
    {
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);

        SmtpRun run = await RunAsync(Greeting + ehloReply + HelpReplyAndBye, sasl);

        Diagnostics.Diff("sent", Ehlo + HelpAndQuit, run.Sent);
        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Diagnostics.AssertValues("authenticator choices", 0, sasl.Choices.Count);
        Assert.AreEqual(Ehlo + HelpAndQuit, run.Sent);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
        Assert.IsEmpty(sasl.Choices);
    }

    [TestMethod]
    public async Task ExecuteAsync_AfterStartTls_AuthenticatesOverTlsWithTheSecondEhlosMechanisms()
    {
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = Stream.Null,
            Credentials = new NetworkCredential("u", "p"),
            SslLevel = TransportSecurityLevel.Required,
        };
        var plaintext = new ScriptedConnection(Encoding.Latin1.GetBytes(
            Greeting + "250-localhost\r\n250-AUTH LOGIN\r\n250 STARTTLS\r\n220 Ready to start TLS\r\n"));
        var secured = new ScriptedConnection(Encoding.Latin1.GetBytes(EhloReply + "334 \r\n" + Accepted + HelpReplyAndBye));

        SmtpRun run = await SmtpRun.ExecuteAsync(Diagnostics, context, plaintext, sasl, ConnectResult.Connected(secured));

        Diagnostics.Diff("sent", Ehlo + "STARTTLS\r\n", run.Sent);
        Diagnostics.Diff("sent over TLS", Ehlo + "AUTH PLAIN\r\nAHUAcA==\r\n" + HelpAndQuit, Encoding.Latin1.GetString(secured.Sent));
        Diagnostics.AssertValues("offered mechanisms", "PLAIN, LOGIN", string.Join(", ", sasl.Choices.Single().Offered));
        Assert.AreEqual(Ehlo + "STARTTLS\r\n", run.Sent);
        Assert.AreEqual(Ehlo + "AUTH PLAIN\r\nAHUAcA==\r\n" + HelpAndQuit, Encoding.Latin1.GetString(secured.Sent));
        CollectionAssert.AreEqual(new[] { "PLAIN", "LOGIN" }, sasl.Choices.Single().Offered);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeloSession_NeverAuthenticates()
    {
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);

        SmtpRun run = await RunAsync(Greeting + "502 no\r\n250 localhost\r\n" + HelpReplyAndBye, sasl);

        Diagnostics.Diff("sent", Ehlo + "HELO x\r\n" + HelpAndQuit, run.Sent);
        Diagnostics.AssertValues("authenticator choices", 0, sasl.Choices.Count);
        Assert.AreEqual(Ehlo + "HELO x\r\n" + HelpAndQuit, run.Sent);
        Assert.IsEmpty(sasl.Choices);
    }

    [TestMethod]
    [DataRow("250-localhost\r\n250 AUTH PLAIN\r\n", new[] { "PLAIN" }, DisplayName = "AUTH on the final line")]
    [DataRow("250-localhost\r\n250-auth PLAIN\r\n250 OK\r\n", new[] { "PLAIN" }, DisplayName = "Lower-case keyword")]
    [DataRow("250-localhost\r\n250-AUTH LOGIN\r\n250-AUTH PLAIN\r\n250 OK\r\n", new[] { "LOGIN", "PLAIN" }, DisplayName = "Two AUTH lines")]
    [DataRow("250-localhost\r\n250-AUTH  PLAINX\tLOGIN \r\n250 OK\r\n", new[] { "PLAINX", "LOGIN" }, DisplayName = "Spaces and tabs between words")]
    public async Task ExecuteAsync_AuthLines_OfferEveryWordAfterTheKeyword(string ehloReply, string[] expected)
    {
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);

        await RunAsync(Greeting + ehloReply + "334 \r\n" + Accepted + HelpReplyAndBye, sasl);

        Diagnostics.AssertValues("offered mechanisms", string.Join(", ", expected), string.Join(", ", sasl.Choices.Single().Offered));
        CollectionAssert.AreEqual(expected, sasl.Choices.Single().Offered);
    }

    [TestMethod]
    [DataRow("250-localhost\r\n250-AUTH \r\n250 SIZE 1\r\n", DisplayName = "A bare AUTH line")]
    [DataRow(EhloReply, DisplayName = "No usable mechanism")]
    public async Task ExecuteAsync_NoMechanismChosen_FailsWithLoginDeniedBeforeSendingAuth(string ehloReply)
    {
        var sasl = new FakeSaslAuthenticator(null, null);

        SmtpRun run = await RunAsync(Greeting + ehloReply + HelpReplyAndBye, sasl);

        Diagnostics.Diff("sent", Ehlo, run.Sent);
        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), run.Result);
        Assert.AreEqual(Ehlo, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), run.Result);
    }

    [TestMethod]
    [DataRow(371, "AUTH PLAIN ", "", DisplayName = "501 characters fit")]
    [DataRow(374, "AUTH PLAIN\r\n", "334 \r\n", DisplayName = "505 characters do not")]
    public async Task ExecuteAsync_SaslIr_SendsTheInitialResponseInlineOnlyWithin504Characters(int messageLength, string expectedStart, string challenge)
    {
        // curl -u <368 or 371 u's>:p --sasl-ir: base64 of 496 or 500 characters after "PLAIN".
        byte[] message = Enumerable.Repeat((byte)'u', messageLength).ToArray();
        var sasl = new FakeSaslAuthenticator("PLAIN", message);

        SmtpRun run = await RunAsync(Context(SaslIr), Greeting + EhloReply + challenge + Accepted + HelpReplyAndBye, sasl);

        Diagnostics.Arrange("message length", messageLength);
        Diagnostics.AssertValues("sent starts with", Ehlo + expectedStart + Convert.ToBase64String(message)[..4], run.Sent[..Math.Min(run.Sent.Length, (Ehlo + expectedStart).Length + 4)]);
        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        StringAssert.StartsWith(run.Sent, Ehlo + expectedStart + Convert.ToBase64String(message)[..4], StringComparison.Ordinal);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
    }

    [TestMethod]
    [DataRow("334 not*base64\r\n", DisplayName = "Not base64")]
    [DataRow("334\r\n", DisplayName = "No text")]
    public async Task ExecuteAsync_ChallengeWithoutBase64_IsHandedOverEmpty(string challenge)
    {
        var sasl = new FakeSaslAuthenticator("LOGIN", "u"u8.ToArray(), "p"u8.ToArray());

        SmtpRun run = await RunAsync(Context(SaslIr), Greeting + EhloReply + challenge + Accepted + HelpReplyAndBye, sasl);

        Diagnostics.Arrange("challenge", SmtpDiagnostics.Show(challenge));
        Diagnostics.Diff("sent", Ehlo + "AUTH LOGIN dQ==\r\ncA==\r\n" + HelpAndQuit, run.Sent);
        Diagnostics.AssertValues("challenge length handed over", 0, sasl.Challenges.Single().Length);
        Assert.AreEqual(Ehlo + "AUTH LOGIN dQ==\r\ncA==\r\n" + HelpAndQuit, run.Sent);
        Assert.IsEmpty(sasl.Challenges.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerClosesDuringAuth_FailsWithRecvError()
    {
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);

        SmtpRun run = await RunAsync(Greeting + EhloReply, sasl);

        Diagnostics.Diff("sent", Ehlo + "AUTH PLAIN\r\n", run.Sent);
        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.RecvError, "response reading failed (errno: 0)"), run.Result);
        Assert.AreEqual(Ehlo + "AUTH PLAIN\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RecvError, "response reading failed (errno: 0)"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_AuthenticatesBeforeMailFrom()
    {
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);
        var context = Context(
            new MailRequestOptions { SaslInitialResponse = true, From = "a@b", Recipients = ["c@d"] },
            upload: new MemoryStream("hi\r\n"u8.ToArray()));

        SmtpRun run = await RunAsync(
            context, Greeting + EhloReply + Accepted + "250 OK\r\n250 OK\r\n354 go\r\n250 OK\r\n" + HelpReplyAndBye, sasl);

        string expectedStart = Ehlo + "AUTH PLAIN AHUAcA==\r\nMAIL FROM:<a@b>\r\n";
        Diagnostics.AssertValues("sent starts with", expectedStart, run.Sent[..Math.Min(run.Sent.Length, expectedStart.Length)]);
        Diagnostics.AssertValues("is success", true, run.Result.IsSuccess);
        StringAssert.StartsWith(run.Sent, Ehlo + "AUTH PLAIN AHUAcA==\r\nMAIL FROM:<a@b>\r\n", StringComparison.Ordinal);
        Assert.IsTrue(run.Result.IsSuccess);
    }

    [TestMethod]
    [DataRow(null, "AUTH=LOGIN", "LOGIN", DisplayName = "From the URL")]
    [DataRow("AUTH=PLAIN", "AUTH=LOGIN", "PLAIN", DisplayName = "--login-options wins over the URL")]
    [DataRow("x=1;auth=*", null, "*", DisplayName = "Among other options")]
    [DataRow("x=1", null, null, DisplayName = "No AUTH option")]
    public async Task ExecuteAsync_LoginOptions_NameTheRequiredMechanism(string? loginOptions, string? urlOptions, string? expected)
    {
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);
        string url = urlOptions is null ? Url : "smtp://u:p;" + urlOptions + "@127.0.0.1:18025/x";
        var context = Context(new MailRequestOptions { LoginOptions = loginOptions }, url);

        await RunAsync(context, Greeting + EhloReply + "334 \r\n" + Accepted + HelpReplyAndBye, sasl);

        Diagnostics.Arrange("--login-options", SmtpDiagnostics.Show(loginOptions));
        Diagnostics.Arrange("url options", SmtpDiagnostics.Show(urlOptions));
        Diagnostics.AssertValues("required mechanism", expected, sasl.Choices.Single().Request.RequiredMechanism);
        Assert.AreEqual(expected, sasl.Choices.Single().Request.RequiredMechanism);
    }

    [TestMethod]
    [DataRow(Url, 18025, DisplayName = "The URL's port")]
    [DataRow("smtp://127.0.0.1/x", 25, DisplayName = "The scheme's default")]
    public async Task ExecuteAsync_Request_CarriesTheConnectionsPort(string url, int expected)
    {
        var sasl = new FakeSaslAuthenticator("PLAIN", PlainMessage);

        await RunAsync(Context(url: url), Greeting + EhloReply + "334 \r\n" + Accepted + HelpReplyAndBye, sasl);

        Diagnostics.AssertValues("request port", expected, sasl.Choices.Single().Request.Port);
        Assert.AreEqual(expected, sasl.Choices.Single().Request.Port);
    }

    [TestMethod]
    public void Constructor_WithAuthenticator_ServesSmtpAndSmtps()
    {
        var handler = new SmtpProtocolHandler(new QueuedConnector(), new QueuedTlsProvider(), new FakeSaslAuthenticator(null, null));

        Diagnostics.Arrange("handler", "with a SASL authenticator");
        string schemes = string.Join(", ", handler.SupportedSchemes);
        Diagnostics.Act("supported schemes", schemes);
        Diagnostics.AssertValues("supported schemes", "smtp, smtps", schemes);
        CollectionAssert.AreEqual(new[] { "smtp", "smtps" }, handler.SupportedSchemes.ToArray());
    }

    private static MailRequestOptions SaslIr => new() { SaslInitialResponse = true };

    private static TransferContext Context(MailRequestOptions? mail = null, string url = Url, Stream? upload = null) =>
        new() { Url = CurlUrl.Parse(url), Output = Stream.Null, Credentials = new NetworkCredential("u", "p"), Mail = mail, Upload = upload };

    private Task<SmtpRun> RunAsync(string replies, FakeSaslAuthenticator sasl) =>
        RunAsync(Context(), replies, sasl);

    private Task<SmtpRun> RunAsync(TransferContext context, string replies, FakeSaslAuthenticator sasl) =>
        SmtpRun.ExecuteAsync(Diagnostics, context, new ScriptedConnection(Encoding.Latin1.GetBytes(replies)), sasl);
}
