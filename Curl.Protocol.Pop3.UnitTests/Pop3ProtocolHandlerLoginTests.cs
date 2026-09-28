using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Pop3.Fakes;

namespace Curl.Protocol.Pop3;

/// <summary>
/// Pins how a POP3 session logs in against curl 8.21.0: SASL <c>AUTH</c>, <c>APOP</c>,
/// <c>USER</c>/<c>PASS</c>, the login options that steer the choice, and the exit code and
/// message of every refusal. Every case named after a recording was recorded from real curl
/// (the Schannel build) on 2026-09-28 with <c>Record-CurlExchange.ps1 -Pop3</c>, curl running
/// <c>-sS -u u:p pop3://127.0.0.1:&lt;port&gt;/</c> unless stated (BL-548 Notes).
/// </summary>
[TestClass]
public sealed class Pop3ProtocolHandlerLoginTests
{
    private const string Url = "pop3://127.0.0.1:18110/";

    private const string Greeting = "+OK POP3 ready <1896.697170952@localhost>\r\n";

    private const string PlainGreeting = "+OK hi\r\n";

    /// <summary>The recorder's default <c>CAPA</c> answer.</summary>
    private const string CapaReply =
        "+OK Capability list follows\r\nUSER\r\nSASL PLAIN LOGIN\r\nSTLS\r\nTOP\r\nUIDL\r\n.\r\n";

    private const string UserOnlyCapaReply = "+OK\r\nUSER\r\n.\r\n";

    private const string ListReply = "+OK 2 messages (104 octets)\r\n1 52\r\n2 52\r\n.\r\n";

    private const long ListedBytes = 12;

    private const string Bye = "+OK Bye\r\n";

    private const string Capa = "CAPA\r\n";

    private const string List = "LIST\r\n";

    private const string Quit = "QUIT\r\n";

    /// <summary>What curl sent for <c>-u u:p</c> and the recorder's timestamp.</summary>
    private const string Apop = "APOP u d727ab40e6dcedbb6cf2f6735fe51cc8\r\n";

    private const string UserAndPass = "USER u\r\nPASS p\r\n";

    private const string LoginDenied = "Login denied";

    private static readonly NetworkCredential UserP = new("u", "p");

    [TestMethod]
    public async Task ExecuteAsync_SaslOffered_AuthenticatesWithTheChosenMechanism()
    {
        // Recording "sasl": AUTH PLAIN, "+ ", AHUAcA==, +OK, then LIST and QUIT.
        var sasl = PlainAndLogin();
        Pop3Run run = await RunAsync(Greeting + CapaReply + "+ \r\n+OK Authenticated\r\n" + ListReply + Bye, sasl: sasl);

        Assert.AreEqual(Capa + "AUTH PLAIN\r\nAHUAcA==\r\n" + List + Quit, run.Sent);
        Assert.AreEqual(TransferResult.Success(ListedBytes), run.Result);
        CollectionAssert.AreEqual(new[] { "PLAIN", "LOGIN" }, sasl.Offers.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_SaslRequest_CarriesTheTransfersCredentialsAndOptions()
    {
        var sasl = PlainAndLogin();
        var mail = new MailRequestOptions { SaslAuthorizationIdentity = "z", BearerToken = "tok" };
        await RunAsync(Greeting + CapaReply + "+ \r\n+OK\r\n" + ListReply + Bye, mail: mail, sasl: sasl);

        Assert.AreEqual(new SaslRequest(UserP, "z", "tok", null, "pop", "127.0.0.1"), sasl.Requests.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_ServiceName_ReplacesPop()
    {
        var sasl = PlainAndLogin();
        await RunAsync(Greeting + CapaReply + "+ \r\n+OK\r\n" + ListReply + Bye, mail: new MailRequestOptions { ServiceName = "pop3" }, sasl: sasl);

        Assert.AreEqual("pop3", sasl.Requests.Single().ServiceName);
    }

    [TestMethod]
    public async Task ExecuteAsync_SaslIr_SendsTheInitialResponseOnTheAuthLine()
    {
        // Recording "saslir": --sasl-ir.
        Pop3Run run = await RunAsync(
            Greeting + CapaReply + "+OK Authenticated\r\n" + ListReply + Bye, mail: new MailRequestOptions { SaslInitialResponse = true }, sasl: PlainAndLogin());

        Assert.AreEqual(Capa + "AUTH PLAIN AHUAcA==\r\n" + List + Quit, run.Sent);
        Assert.AreEqual(TransferResult.Success(ListedBytes), run.Result);
    }

    [TestMethod]
    [DataRow(177, true, DisplayName = "--sasl-ir -u u:<177 p>: 5 + 240 characters, inline")]
    [DataRow(180, false, DisplayName = "--sasl-ir -u u:<180 p>: 5 + 244 characters, after the continuation")]
    public async Task ExecuteAsync_SaslIrLongerThan247_WaitsForTheContinuation(int passwordLength, bool inline)
    {
        string password = new('p', passwordLength);
        string encoded = Convert.ToBase64String(Encoding.Latin1.GetBytes("\0u\0" + password));
        var sasl = new ScriptedSaslAuthenticator(("PLAIN", ["\0u\0" + password]));
        string replies = inline ? "+OK\r\n" : "+ \r\n+OK\r\n";

        Pop3Run run = await RunAsync(
            Greeting + CapaReply + replies + ListReply + Bye, new NetworkCredential("u", password), new MailRequestOptions { SaslInitialResponse = true }, sasl);

        string expected = inline ? $"AUTH PLAIN {encoded}\r\n" : $"AUTH PLAIN\r\n{encoded}\r\n";
        Assert.AreEqual(Capa + expected + List + Quit, run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_LoginOptionsNameLogin_AnswersBothContinuations()
    {
        // Recording "login": --login-options AUTH=LOGIN.
        var sasl = PlainAndLogin();
        Pop3Run run = await RunAsync(
            Greeting + CapaReply + "+ VXNlcm5hbWU6\r\n+ UGFzc3dvcmQ6\r\n+OK Authenticated\r\n" + ListReply + Bye,
            mail: new MailRequestOptions { LoginOptions = "AUTH=LOGIN" },
            sasl: sasl);

        Assert.AreEqual(Capa + "AUTH LOGIN\r\ndQ==\r\ncA==\r\n" + List + Quit, run.Sent);
        Assert.AreEqual(TransferResult.Success(ListedBytes), run.Result);
        Assert.AreEqual("LOGIN", sasl.Requests.Single().RequiredMechanism);
        CollectionAssert.AreEqual(new[] { "Password:" }, sasl.Challenges);
    }

    [TestMethod]
    public async Task ExecuteAsync_ChallengeThatIsNotBase64_IsAnEmptyChallenge()
    {
        // Recording "authbad64": AUTH=LOGIN, "+ !!!" answered dQ==, then -ERR: exit 67.
        var sasl = PlainAndLogin();
        Pop3Run run = await RunAsync(
            Greeting + CapaReply + "+ !!!\r\n-ERR Command not recognized\r\n",
            mail: new MailRequestOptions { LoginOptions = "AUTH=LOGIN" },
            sasl: sasl);

        Assert.AreEqual(Capa + "AUTH LOGIN\r\ndQ==\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, LoginDenied), run.Result);
    }

    [TestMethod]
    [DataRow("-ERR bad\r\n", "AUTH PLAIN\r\n", false, DisplayName = "AUTH=-ERR bad")]
    [DataRow("+OK\r\n", "AUTH PLAIN\r\n", false, DisplayName = "+OK before the initial response")]
    [DataRow("+ YWJj\r\n+OK\r\n", "AUTH PLAIN AHUAcA==\r\n", true, DisplayName = "--sasl-ir, AUTH=+ YWJj")]
    [DataRow("+junk\r\n+OK yes\r\n", "AUTH PLAIN AHUAcA==\r\n", true, DisplayName = "--sasl-ir, AUTH=+junk then +OK yes")]
    public async Task ExecuteAsync_SaslExchangeFails_Exit67WithNoFallbackAndNoQuit(string authReplies, string sent, bool initialResponse)
    {
        // Recordings "autherr", "authokbare" (measured with LOGIN), "plainextra", "plainjunk".
        Pop3Run run = await RunAsync(
            Greeting + CapaReply + authReplies, mail: new MailRequestOptions { SaslInitialResponse = initialResponse }, sasl: PlainAndLogin());

        Assert.AreEqual(Capa + sent, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, LoginDenied), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_BearerTokenWithoutUser_AuthenticatesWithXOAuth2()
    {
        // Recording "bearernouser": --oauth2-bearer tok --sasl-ir, no -u.
        var sasl = new ScriptedSaslAuthenticator(("XOAUTH2", ["user=\u0001auth=Bearer tok\u0001\u0001"]));
        Pop3Run run = await RunAsync(
            PlainGreeting + "+OK\r\nUSER\r\nSASL XOAUTH2\r\n.\r\n+OK Authenticated\r\n" + ListReply + Bye,
            noCredential: true,
            mail: new MailRequestOptions { BearerToken = "tok", SaslInitialResponse = true },
            sasl: sasl);

        Assert.AreEqual(Capa + "AUTH XOAUTH2 dXNlcj0BYXV0aD1CZWFyZXIgdG9rAQE=\r\n" + List + Quit, run.Sent);
        Assert.AreEqual(TransferResult.Success(ListedBytes), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_BearerTokenWithoutUserAndNoSaslChosen_Exit67WithoutUser()
    {
        // Recording "bearerfallback": --oauth2-bearer tok, CAPA offering USER only.
        Pop3Run run = await RunAsync(
            PlainGreeting + UserOnlyCapaReply, noCredential: true, mail: new MailRequestOptions { BearerToken = "tok" }, sasl: PlainAndLogin());

        Assert.AreEqual(Capa, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, LoginDenied), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_LaterChallengeThatIsNotBase64_ReachesTheExchangeEmpty()
    {
        var sasl = PlainAndLogin();
        Pop3Run run = await RunAsync(
            Greeting + CapaReply + "+ VXNlcm5hbWU6\r\n+ !!!\r\n+OK\r\n" + ListReply + Bye,
            mail: new MailRequestOptions { LoginOptions = "AUTH=LOGIN" },
            sasl: sasl);

        Assert.AreEqual(Capa + "AUTH LOGIN\r\ndQ==\r\ncA==\r\n" + List + Quit, run.Sent);
        CollectionAssert.AreEqual(new[] { string.Empty }, sasl.Challenges);
    }

    [TestMethod]
    [DataRow(true, "AUTH EXTERNAL =\r\n", DisplayName = "--sasl-ir: = on the line")]
    [DataRow(false, "AUTH EXTERNAL\r\n=\r\n", DisplayName = "no --sasl-ir: = after the continuation")]
    public async Task ExecuteAsync_EmptyInitialResponse_IsSentAsEquals(bool initialResponse, string sent)
    {
        // Not measured on POP3: RFC 5034 section 4 and curl's sasl.c send "=" for an empty message.
        var sasl = new ScriptedSaslAuthenticator(("EXTERNAL", [string.Empty]));
        string replies = initialResponse ? "+OK\r\n" : "+ \r\n+OK\r\n";
        Pop3Run run = await RunAsync(
            Greeting + "+OK\r\nSASL EXTERNAL\r\n.\r\n" + replies + ListReply + Bye,
            new NetworkCredential("", ""),
            new MailRequestOptions { SaslInitialResponse = initialResponse },
            sasl);

        Assert.AreEqual(Capa + sent + List + Quit, run.Sent);
    }

    [TestMethod]
    [DataRow(PlainGreeting, "+OK\r\nUSER\r\n.\r\n", DisplayName = "CAPA offering USER only")]
    [DataRow(PlainGreeting, "+OK\r\nuser\r\n.\r\n", DisplayName = "CAPA offering user")]
    [DataRow(PlainGreeting, "-ERR no\r\n", DisplayName = "CAPA=-ERR no")]
    [DataRow(PlainGreeting, "+OK\r\nSASL\r\nUSER\r\n.\r\n", DisplayName = "SASL listing no mechanism")]
    public async Task ExecuteAsync_NoTimestampAndNoSasl_SendsUserThenPass(string greeting, string capaReply)
    {
        // Recordings "useronly", "userlowonly", "capaerr", "capasaslnomech".
        Pop3Run run = await RunAsync(greeting + capaReply + "+OK User accepted\r\n+OK Logged in\r\n" + ListReply + Bye, sasl: PlainAndLogin());

        Assert.AreEqual(Capa + UserAndPass + List + Quit, run.Sent);
        Assert.AreEqual(TransferResult.Success(ListedBytes), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyUser_SendsUserWithNothingAfterIt()
    {
        // Recording "emptyuser": -u :p.
        Pop3Run run = await RunAsync(
            PlainGreeting + UserOnlyCapaReply + "+OK\r\n+OK\r\n" + ListReply + Bye, new NetworkCredential(string.Empty, "p"));

        Assert.AreEqual(Capa + "USER \r\nPASS p\r\n" + List + Quit, run.Sent);
    }

    [TestMethod]
    [DataRow("+OK\r\nUSER\r\nAPOP\r\n.\r\n", DisplayName = "CAPA offering USER and APOP")]
    [DataRow(UserOnlyCapaReply, DisplayName = "CAPA offering USER only")]
    [DataRow("-ERR no\r\n", DisplayName = "CAPA=-ERR no")]
    [DataRow("+OK\r\nUSER\r\nSASL SCRAM-SHA-256\r\n.\r\n", DisplayName = "SASL offering no usable mechanism")]
    public async Task ExecuteAsync_TimestampAndNoSaslChosen_SendsApop(string capaReply)
    {
        // Recordings "apop", "tsuseronly", "capaerr-ts", "saslunusable".
        Pop3Run run = await RunAsync(Greeting + capaReply + "+OK Logged in\r\n" + ListReply + Bye, sasl: PlainAndLogin());

        Assert.AreEqual(Capa + Apop + List + Quit, run.Sent);
        Assert.AreEqual(TransferResult.Success(ListedBytes), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoAuthenticator_LogsInWithoutSasl()
    {
        Pop3Run run = await RunAsync(Greeting + CapaReply + "+OK Logged in\r\n" + ListReply + Bye);

        Assert.AreEqual(Capa + Apop + List + Quit, run.Sent);
    }

    [TestMethod]
    [DataRow("AUTH=+APOP", null, DisplayName = "--login-options AUTH=+APOP")]
    [DataRow("auth=+apop", null, DisplayName = "--login-options auth=+apop")]
    [DataRow(null, "pop3://u:p;AUTH=+APOP@127.0.0.1:18110/", DisplayName = "pop3://u:p;AUTH=+APOP@...")]
    [DataRow("AUTH=+APOP", "pop3://u:p;AUTH=PLAIN@127.0.0.1:18110/", DisplayName = "the command line wins")]
    public async Task ExecuteAsync_LoginOptionsAskForApop_SendsApopThoughSaslIsOffered(string? loginOptions, string? url)
    {
        // Recordings "forceapop", "apoplower", "urlapop".
        var sasl = PlainAndLogin();
        Pop3Run run = await RunAsync(
            Greeting + CapaReply + "+OK Logged in\r\n" + ListReply + Bye, mail: new MailRequestOptions { LoginOptions = loginOptions }, sasl: sasl, url: url ?? Url);

        Assert.AreEqual(Capa + Apop + List + Quit, run.Sent);
        Assert.IsEmpty(sasl.Requests);
    }

    [TestMethod]
    [DataRow(PlainGreeting, CapaReply, "AUTH=+APOP", DisplayName = "AUTH=+APOP, no timestamp")]
    [DataRow(Greeting, CapaReply, "AUTH=CRAM-MD5", DisplayName = "AUTH=CRAM-MD5, not offered")]
    [DataRow(PlainGreeting, "+OK\r\nTOP\r\n.\r\n", null, DisplayName = "no SASL, no timestamp, no USER")]
    public async Task ExecuteAsync_NoWayToLogIn_Exit67LoginDenied(string greeting, string capaReply, string? loginOptions)
    {
        // Recordings "forceapop-nots", "crammissing", "nothing": nothing sent after CAPA.
        Pop3Run run = await RunAsync(greeting + capaReply, mail: new MailRequestOptions { LoginOptions = loginOptions }, sasl: PlainAndLogin());

        Assert.AreEqual(Capa, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, LoginDenied), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SaslMechanismRequiredButNoAuthenticator_Exit67()
    {
        Pop3Run run = await RunAsync(Greeting + CapaReply, mail: new MailRequestOptions { LoginOptions = "AUTH=PLAIN" });

        Assert.AreEqual(Capa, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, LoginDenied), run.Result);
    }

    [TestMethod]
    [DataRow("-ERR who\r\n", "USER u\r\n", "Access denied. -", DisplayName = "USER=-ERR who")]
    [DataRow("+junk\r\n", "USER u\r\n", "Access denied. *", DisplayName = "USER=+junk")]
    [DataRow("+OK User accepted\r\n-ERR denied\r\n", UserAndPass, "Access denied. -", DisplayName = "PASS=-ERR denied")]
    [DataRow("+OK User accepted\r\n+junk\r\n", UserAndPass, "Access denied. *", DisplayName = "PASS=+junk")]
    public async Task ExecuteAsync_UserOrPassRefused_Exit67AccessDenied(string replies, string sent, string message)
    {
        // Recordings "usererr", "userplus", "passerr", "passplus": no QUIT.
        Pop3Run run = await RunAsync(PlainGreeting + UserOnlyCapaReply + replies);

        Assert.AreEqual(Capa + sent, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, message), run.Result);
    }

    [TestMethod]
    [DataRow("-ERR bad\r\n", "Authentication failed: 45", DisplayName = "APOP=-ERR bad")]
    [DataRow("+junk\r\n", "Authentication failed: 42", DisplayName = "APOP=+junk")]
    public async Task ExecuteAsync_ApopRefused_Exit67AuthenticationFailed(string reply, string message)
    {
        // Recordings "apoperr", "apopplus": no QUIT.
        Pop3Run run = await RunAsync(Greeting + UserOnlyCapaReply + reply);

        Assert.AreEqual(Capa + Apop, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, message), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoCredentials_SendsNoLogin()
    {
        // Recording "nocreds": no -u.
        var sasl = PlainAndLogin();
        Pop3Run run = await RunAsync(Greeting + CapaReply + ListReply + Bye, noCredential: true, sasl: sasl);

        Assert.AreEqual(Capa + List + Quit, run.Sent);
        Assert.IsEmpty(sasl.Requests);
    }

    [TestMethod]
    [DataRow("AUTH=USER", DisplayName = "--login-options AUTH=USER")]
    [DataRow("FOO=bar", DisplayName = "--login-options FOO=bar")]
    public async Task ExecuteAsync_LoginOptionsCurlRefuses_Exit3BeforeReadingOrSending(string loginOptions)
    {
        // Recordings "forceuser", "othopt": exit 3, nothing sent.
        Pop3Run run = await RunAsync(Greeting + CapaReply, mail: new MailRequestOptions { LoginOptions = loginOptions });

        Assert.AreEqual(string.Empty, run.Sent);
        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.UrlMalformat, "URL using bad/illegal format or missing URL"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerClosesDuringLogin_Exit56()
    {
        Pop3Run run = await RunAsync(PlainGreeting + UserOnlyCapaReply);

        Assert.AreEqual(Capa + "USER u\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RecvError, "response reading failed (errno: 0)"), run.Result);
    }

    private static ScriptedSaslAuthenticator PlainAndLogin() =>
        new(("PLAIN", ["\0u\0p"]), ("LOGIN", ["u", "p"]));

    private static async Task<Pop3Run> RunAsync(
        string replies,
        NetworkCredential? credential = null,
        MailRequestOptions? mail = null,
        ScriptedSaslAuthenticator? sasl = null,
        string url = Url,
        bool noCredential = false)
    {
        int listEnd = replies.IndexOf(ListReply, StringComparison.Ordinal) + ListReply.Length;
        string[] reads = listEnd < ListReply.Length ? [replies] : [replies[..listEnd], replies[listEnd..]];
        var connection = new ScriptedConnection([.. reads.Where(read => read.Length > 0).Select(Encoding.Latin1.GetBytes)]);
        using var output = new MemoryStream();
        var progress = new RecordingProgress();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(url),
            Output = output,
            Progress = progress,
            Credentials = noCredential ? null : credential ?? UserP,
            Mail = mail,
        };
        var connector = new QueuedConnector(ConnectResult.Connected(connection));
        var tls = new QueuedTlsProvider();

        TransferResult result = await new Pop3ProtocolHandler(connector, tls, sasl).ExecuteAsync(context);

        return new Pop3Run(result, connection, connector, tls, output.ToArray(), progress);
    }
}
