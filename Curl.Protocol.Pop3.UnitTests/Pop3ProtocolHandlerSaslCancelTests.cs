using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Pop3.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Pop3;

/// <summary>
/// Pins how a POP3 session cancels a SASL exchange with <c>*</c> when a mechanism that reads
/// its challenge is sent one that is not base64, as curl 8.21.0 (the Schannel build) did,
/// recorded with <c>Record-CurlExchange.ps1 -Pop3</c> and <c>-v -u user:secret
/// pop3://127.0.0.1:&lt;port&gt;/1</c> on 2026-10-02 (BL-1222 Notes): the response to
/// <c>*</c> is read whatever it is, the next offered mechanism is tried, then <c>APOP</c> or
/// <c>USER</c>/<c>PASS</c>, and with none possible exit 67 <c>Authentication cancelled</c>.
/// </summary>
[TestClass]
public sealed class Pop3ProtocolHandlerSaslCancelTests
{
    /// <summary>Gets or sets the running test's context, which MSTest sets.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private const string Url = "pop3://127.0.0.1:18110/1";

    private const string Greeting = "+OK POP3 ready\r\n";

    private const string TimestampGreeting = "+OK POP3 ready <1896.697170952@dbc.mtview.ca.us>\r\n";

    private const string CramMd5Capa = "+OK\r\nSASL CRAM-MD5\r\n.\r\n";

    private const string BadChallenge = "+ !!!notbase64\r\n";

    private const string Cancelled = "-ERR cancelled\r\n";

    private const string Retrieved = "+OK 4 octets\r\nhi\r\n.\r\n+OK Bye\r\n";

    private const string CancelSent = "CAPA\r\nAUTH CRAM-MD5\r\n*\r\n";

    private const string RetrieveSent = "RETR 1\r\nQUIT\r\n";

    private const string AuthenticationCancelled = "Authentication cancelled";

    private static readonly byte[] CramMd5Answer = Encoding.Latin1.GetBytes("user 0123456789abcdef");

    [TestMethod]
    public async Task ExecuteAsync_OnlyMechanismCancelledAndNoOtherWay_FailsWithAuthenticationCancelled()
    {
        (TransferResult result, RecordingTransferEvents events, string sent, _) = await RunAsync(
            Greeting + CramMd5Capa + BadChallenge + Cancelled + "+OK Bye\r\n", CramMd5());

        Diagnostics.AssertValues("sent", CancelSent, sent);
        Assert.AreEqual(CancelSent, sent);
        Diagnostics.AssertValues("result", TransferResult.Failure(CurlExitCode.LoginDenied, AuthenticationCancelled), result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, AuthenticationCancelled), result);
        CollectionAssert.AreEqual(
            (string[])["< " + BadChallenge, "> *\r\n", "< " + Cancelled, "* " + AuthenticationCancelled],
            events.Transcript.SkipWhile(line => line != "< " + BadChallenge).Take(4).ToArray());
        Assert.IsFalse(events.Info.Any(line => line.StartsWith("SASL", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_MechanismCancelledWithUserListed_FallsBackToUserAndPass()
    {
        (TransferResult result, _, string sent, _) = await RunAsync(
            Greeting + "+OK\r\nSASL CRAM-MD5\r\nUSER\r\n.\r\n" + BadChallenge + Cancelled + "+OK User accepted\r\n+OK Logged in\r\n" + Retrieved,
            CramMd5());

        Diagnostics.AssertValues("sent", CancelSent + "USER user\r\nPASS secret\r\n" + RetrieveSent, sent);
        Assert.AreEqual(CancelSent + "USER user\r\nPASS secret\r\n" + RetrieveSent, sent);
        Diagnostics.AssertValues("result.ExitCode", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_MechanismCancelledWithApopTimestamp_FallsBackToApop()
    {
        (TransferResult result, _, string sent, _) = await RunAsync(
            TimestampGreeting + CramMd5Capa + BadChallenge + Cancelled + "+OK Logged in\r\n" + Retrieved, CramMd5());

        Diagnostics.AssertValues("sent", CancelSent + "APOP user 3f18b52881e44c0cc6067f46e0ced7bc\r\n" + RetrieveSent, sent);
        Assert.AreEqual(CancelSent + "APOP user 3f18b52881e44c0cc6067f46e0ced7bc\r\n" + RetrieveSent, sent);
        Diagnostics.AssertValues("result.ExitCode", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_MechanismCancelledWithAnotherOffered_TriesTheNextMechanism()
    {
        var sasl = new RankedSaslAuthenticator(
            ("CRAM-MD5", null, [CramMd5Answer]),
            ("PLAIN", Encoding.Latin1.GetBytes("\0user\0secret"), []));

        (TransferResult result, _, string sent, RecordingDiagnosticLog log) = await RunAsync(
            Greeting + "+OK\r\nSASL CRAM-MD5 PLAIN\r\n.\r\n" + BadChallenge + Cancelled + "+ \r\n+OK Logged in\r\n" + Retrieved, sasl);

        Diagnostics.AssertValues("sent", CancelSent + "AUTH PLAIN\r\nAHVzZXIAc2VjcmV0\r\n" + RetrieveSent, sent);
        Assert.AreEqual(CancelSent + "AUTH PLAIN\r\nAHVzZXIAc2VjcmV0\r\n" + RetrieveSent, sent);
        Diagnostics.AssertValues("result.ExitCode", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual((string[])["CRAM-MD5", "PLAIN"], sasl.Offers[0]);
        CollectionAssert.AreEqual((string[])["PLAIN"], sasl.Offers[1]);
        Diagnostics.AssertValues("sasl.Challenges count", 0, sasl.Challenges.Count());
        Assert.IsEmpty(sasl.Challenges);
        Diagnostics.AssertValues("log.MessagesAt(DiagnosticLogLevel.Warning) holds", "SASL mechanism cancelled, choosing another: CRAM-MD5", string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Warning)));
        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Warning), "SASL mechanism cancelled, choosing another: CRAM-MD5");
    }

    [TestMethod]
    [DataRow("+OK done\r\n", DisplayName = "+OK")]
    [DataRow("+ more\r\n", DisplayName = "another continuation")]
    public async Task ExecuteAsync_AnyResponseToTheCancel_IsReadAndIgnored(string response)
    {
        (TransferResult result, _, string sent, _) = await RunAsync(
            Greeting + CramMd5Capa + BadChallenge + response + "+OK Bye\r\n", CramMd5());

        Diagnostics.AssertValues("sent", CancelSent, sent);
        Assert.AreEqual(CancelSent, sent);
        Diagnostics.AssertValues("result", TransferResult.Failure(CurlExitCode.LoginDenied, AuthenticationCancelled), result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, AuthenticationCancelled), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_EveryMechanismCancelled_TriesEachThenFails()
    {
        var sasl = new RankedSaslAuthenticator(("DIGEST-MD5", null, []), ("CRAM-MD5", null, []));

        (TransferResult result, _, string sent, _) = await RunAsync(
            Greeting + "+OK\r\nSASL CRAM-MD5 DIGEST-MD5 SCRAM-SHA-1\r\n.\r\n" + BadChallenge + Cancelled + BadChallenge + Cancelled, sasl);

        Diagnostics.AssertValues("sent", "CAPA\r\nAUTH DIGEST-MD5\r\n*\r\nAUTH CRAM-MD5\r\n*\r\n", sent);
        Assert.AreEqual("CAPA\r\nAUTH DIGEST-MD5\r\n*\r\nAUTH CRAM-MD5\r\n*\r\n", sent);
        Diagnostics.AssertValues("result", TransferResult.Failure(CurlExitCode.LoginDenied, AuthenticationCancelled), result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, AuthenticationCancelled), result);
        CollectionAssert.AreEqual((string[])["SCRAM-SHA-1"], sasl.Offers[^1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_MechanismCancelledThenNextRefused_FailsWithLoginDenied()
    {
        var sasl = new RankedSaslAuthenticator(("CRAM-MD5", null, []), ("LOGIN", null, [[0x75]]));

        (TransferResult result, _, string sent, _) = await RunAsync(
            Greeting + "+OK\r\nSASL CRAM-MD5 LOGIN\r\nUSER\r\n.\r\n" + BadChallenge + Cancelled + "-ERR no\r\n", sasl);

        Diagnostics.AssertValues("sent", CancelSent + "AUTH LOGIN\r\n", sent);
        Assert.AreEqual(CancelSent + "AUTH LOGIN\r\n", sent);
        Diagnostics.AssertValues("result", TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_AuthOptionNamesTheCancelledMechanism_FailsWithAuthenticationCancelled()
    {
        (TransferResult result, _, string sent, _) = await RunAsync(
            TimestampGreeting + "+OK\r\nSASL CRAM-MD5\r\nUSER\r\n.\r\n" + BadChallenge + Cancelled, CramMd5(), loginOptions: "AUTH=CRAM-MD5");

        Diagnostics.AssertValues("sent", CancelSent, sent);
        Assert.AreEqual(CancelSent, sent);
        Diagnostics.AssertValues("result", TransferResult.Failure(CurlExitCode.LoginDenied, AuthenticationCancelled), result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, AuthenticationCancelled), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelledMechanismNotAmongTheOffered_StopsWithAuthenticationCancelled()
    {
        var sasl = new RankedSaslAuthenticator(("CRAM-MD5", null, [])) { ChoosesUnoffered = true };

        (TransferResult result, _, string sent, _) = await RunAsync(
            Greeting + "+OK\r\nSASL PLAIN\r\n.\r\n" + BadChallenge + Cancelled, sasl);

        Diagnostics.AssertValues("sent", CancelSent, sent);
        Assert.AreEqual(CancelSent, sent);
        Diagnostics.AssertValues("result", TransferResult.Failure(CurlExitCode.LoginDenied, AuthenticationCancelled), result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, AuthenticationCancelled), result);
        Assert.HasCount(1, sasl.Offers);
    }

    [TestMethod]
    [DataRow("+\r\n", DisplayName = "no text")]
    [DataRow("+ \r\n", DisplayName = "empty text")]
    [DataRow("+ =\r\n", DisplayName = "=")]
    [DataRow("+ =!!!\r\n", DisplayName = "= then not base64")]
    public async Task ExecuteAsync_CramMd5ChallengeEmptyOrStartingWithEquals_IsHandedOverEmpty(string challenge)
    {
        RankedSaslAuthenticator sasl = CramMd5();

        (TransferResult result, _, string sent, _) = await RunAsync(
            Greeting + CramMd5Capa + challenge + "+OK Logged in\r\n" + Retrieved, sasl);

        Diagnostics.AssertValues("sent", "CAPA\r\nAUTH CRAM-MD5\r\n" + Convert.ToBase64String(CramMd5Answer) + "\r\n" + RetrieveSent, sent);
        Assert.AreEqual("CAPA\r\nAUTH CRAM-MD5\r\n" + Convert.ToBase64String(CramMd5Answer) + "\r\n" + RetrieveSent, sent);
        Diagnostics.AssertValues("result.ExitCode", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.AssertValues("sasl.Challenges.Single().Challenge count", 0, sasl.Challenges.Single().Challenge.Count());
        Assert.IsEmpty(sasl.Challenges.Single().Challenge);
    }

    [TestMethod]
    public async Task ExecuteAsync_DigestMd5LaterChallengeNotBase64_IsHandedOverEmpty()
    {
        var sasl = new RankedSaslAuthenticator(("DIGEST-MD5", null, [[0x61], []]));

        (TransferResult result, _, string sent, _) = await RunAsync(
            Greeting + "+OK\r\nSASL DIGEST-MD5\r\n.\r\n+ bm9uY2U9MQ==\r\n" + BadChallenge + "+OK Logged in\r\n" + Retrieved, sasl);

        Diagnostics.AssertValues("sent", "CAPA\r\nAUTH DIGEST-MD5\r\nYQ==\r\n=\r\n" + RetrieveSent, sent);
        Assert.AreEqual("CAPA\r\nAUTH DIGEST-MD5\r\nYQ==\r\n=\r\n" + RetrieveSent, sent);
        Diagnostics.AssertValues("result.ExitCode", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.AssertValues("sasl.Challenges[1].Challenge count", 0, sasl.Challenges[1].Challenge.Count());
        Assert.IsEmpty(sasl.Challenges[1].Challenge);
    }

    [TestMethod]
    public async Task ExecuteAsync_NtlmType2NotBase64_IsCancelled()
    {
        var sasl = new RankedSaslAuthenticator(("NTLM", [0x31], [[0x33]]));

        (TransferResult result, _, string sent, _) = await RunAsync(
            Greeting + "+OK\r\nSASL NTLM\r\n.\r\n+ \r\n" + BadChallenge + Cancelled, sasl);

        Diagnostics.AssertValues("sent", "CAPA\r\nAUTH NTLM\r\nMQ==\r\n*\r\n", sent);
        Assert.AreEqual("CAPA\r\nAUTH NTLM\r\nMQ==\r\n*\r\n", sent);
        Diagnostics.AssertValues("result", TransferResult.Failure(CurlExitCode.LoginDenied, AuthenticationCancelled), result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, AuthenticationCancelled), result);
        Diagnostics.AssertValues("sasl.Challenges count", 0, sasl.Challenges.Count());
        Assert.IsEmpty(sasl.Challenges);
    }

    [TestMethod]
    public async Task ExecuteAsync_GssapiLaterChallengeNotBase64_IsCancelled()
    {
        var sasl = new RankedSaslAuthenticator(("GSSAPI", [0x31], [[0x32]]));

        (TransferResult result, _, string sent, _) = await RunAsync(
            Greeting + "+OK\r\nSASL GSSAPI\r\n.\r\n+ \r\n+ YQ==\r\n" + BadChallenge + Cancelled, sasl);

        Diagnostics.AssertValues("sent", "CAPA\r\nAUTH GSSAPI\r\nMQ==\r\nMg==\r\n*\r\n", sent);
        Assert.AreEqual("CAPA\r\nAUTH GSSAPI\r\nMQ==\r\nMg==\r\n*\r\n", sent);
        Diagnostics.AssertValues("result", TransferResult.Failure(CurlExitCode.LoginDenied, AuthenticationCancelled), result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, AuthenticationCancelled), result);
    }

    [TestMethod]
    [DataRow("PLAIN")]
    [DataRow("LOGIN")]
    [DataRow("EXTERNAL")]
    [DataRow("XOAUTH2")]
    [DataRow("OAUTHBEARER")]
    public async Task ExecuteAsync_MechanismThatIgnoresItsChallenge_IsHandedANonBase64ChallengeEmpty(string mechanism)
    {
        var sasl = new RankedSaslAuthenticator((mechanism, null, [[0x61]]));

        (TransferResult result, _, string sent, _) = await RunAsync(
            Greeting + "+OK\r\nSASL " + mechanism + "\r\n.\r\n" + BadChallenge + "+OK Logged in\r\n" + Retrieved, sasl);

        Diagnostics.AssertValues("sent", "CAPA\r\nAUTH " + mechanism + "\r\nYQ==\r\n" + RetrieveSent, sent);
        Assert.AreEqual("CAPA\r\nAUTH " + mechanism + "\r\nYQ==\r\n" + RetrieveSent, sent);
        Diagnostics.AssertValues("result.ExitCode", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.AssertValues("sasl.Challenges.Single().Challenge count", 0, sasl.Challenges.Single().Challenge.Count());
        Assert.IsEmpty(sasl.Challenges.Single().Challenge);
    }

    [TestMethod]
    public async Task ExecuteAsync_ExchangeCancelsWithAReason_ReportsItThenCancelsAndFallsBack()
    {
        const string Reason = "GSSAPI handshake failure (invalid security data)";
        var sasl = new RankedSaslAuthenticator(("GSSAPI", [0x31], []));
        sasl.CancelReasons["GSSAPI"] = Reason;

        (TransferResult result, RecordingTransferEvents events, string sent, _) = await RunAsync(
            Greeting + "+OK\r\nSASL GSSAPI\r\nUSER\r\n.\r\n+ \r\n+ YQ==\r\n" + Cancelled + "+OK User accepted\r\n+OK Logged in\r\n" + Retrieved, sasl);

        Diagnostics.AssertValues("sent", "CAPA\r\nAUTH GSSAPI\r\nMQ==\r\n*\r\nUSER user\r\nPASS secret\r\n" + RetrieveSent, sent);
        Assert.AreEqual("CAPA\r\nAUTH GSSAPI\r\nMQ==\r\n*\r\nUSER user\r\nPASS secret\r\n" + RetrieveSent, sent);
        Diagnostics.AssertValues("result.ExitCode", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(
            (string[])["< + YQ==\r\n", "* " + Reason, "> *\r\n", "< " + Cancelled],
            events.Transcript.SkipWhile(line => line != "< + YQ==\r\n").Take(4).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_ExchangeGivesNoAnswerAndNoReason_FailsWithLoginDeniedWithoutCancelling()
    {
        var sasl = new RankedSaslAuthenticator(("GSSAPI", [0x31], []));

        (TransferResult result, _, string sent, _) = await RunAsync(
            Greeting + "+OK\r\nSASL GSSAPI\r\nUSER\r\n.\r\n+ \r\n+ YQ==\r\n", sasl);

        Diagnostics.AssertValues("sent", "CAPA\r\nAUTH GSSAPI\r\nMQ==\r\n", sent);
        Assert.AreEqual("CAPA\r\nAUTH GSSAPI\r\nMQ==\r\n", sent);
        Diagnostics.AssertValues("result", TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), result);
    }

    /// <summary>
    /// Upstream test 891: CAPA offers only CRAM-MD5 and its continuation is a bare <c>+</c>
    /// ending in LF alone. curl 8.21.0 sends <c>AUTH CRAM-MD5</c>, reads the bare-LF line as
    /// an empty challenge, answers it, and fails with exit 67 when the answer is refused
    /// (recorded with <c>Record-CurlExchange.ps1 -Script</c> on 2026-10-10, BL-1991 Notes).
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_CramMd5ContinuationEndingInBareLf_AnswersEmptyChallengeThenLoginDenied()
    {
        RankedSaslAuthenticator sasl = CramMd5();

        (TransferResult result, _, string sent, _) = await RunAsync(
            "+OK curl POP3 server ready to serve\r\n" + CramMd5Capa + "+\n-ERR Unrecognized command\r\n", sasl);

        string expected = "CAPA\r\nAUTH CRAM-MD5\r\n" + Convert.ToBase64String(CramMd5Answer) + "\r\n";
        Diagnostics.AssertValues("sent", expected, sent);
        Assert.AreEqual(expected, sent);
        Assert.HasCount(1, sasl.Challenges);
        Assert.IsEmpty(sasl.Challenges[0].Challenge);
        Diagnostics.AssertValues("result", TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), result);
    }

    private static RankedSaslAuthenticator CramMd5() => new(("CRAM-MD5", null, [CramMd5Answer]));

    private async Task<(TransferResult Result, RecordingTransferEvents Events, string Sent, RecordingDiagnosticLog Log)> RunAsync(
        string replies, RankedSaslAuthenticator sasl, string? loginOptions = null)
    {
        var events = new RecordingTransferEvents();
        var log = new RecordingDiagnosticLog();
        // The message body ends its own read, as it does on the wire, so QUIT follows it.
        int bodyEnd = replies.IndexOf(Retrieved, StringComparison.Ordinal) + Retrieved.Length - "+OK Bye\r\n".Length;
        string[] reads = bodyEnd < Retrieved.Length ? [replies] : [replies[..bodyEnd], replies[bodyEnd..]];
        var connection = new ScriptedConnection([.. reads.Select(Encoding.Latin1.GetBytes)]);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = Stream.Null,
            Events = events,
            DiagnosticLog = log,
            Credentials = new NetworkCredential("user", "secret"),
            Mail = new MailRequestOptions { LoginOptions = loginOptions },
        };

        Diagnostics.ArrangeRun(Url, connection.Script);
        Diagnostics.Arrange("login options", Pop3Diagnostics.Show(loginOptions));

        TransferResult result = await new Pop3ProtocolHandler(new QueuedConnector(ConnectResult.Connected(connection)), new QueuedTlsProvider(), sasl)
            .ExecuteAsync(context);
        Diagnostics.ActTransfer(result, events, connection.Sent);
        Diagnostics.Act("diagnostic log", string.Join(" | ", log.Lines.Select(line => $"{line.Level}: {Pop3Diagnostics.Show(line.Message)}")));

        return (result, events, Encoding.Latin1.GetString(connection.Sent), log);
    }
}
