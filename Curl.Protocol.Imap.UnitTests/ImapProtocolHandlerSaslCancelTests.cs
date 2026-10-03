using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Imap.Fakes;

namespace Curl.Protocol.Imap;

/// <summary>
/// Pins how an IMAP session cancels a SASL exchange with <c>*</c> when a mechanism that reads
/// its challenge is handed one that is not base64, as curl 8.21.0 (the Schannel build) did,
/// recorded with <c>Record-CurlExchange.ps1 -Imap</c> on 2026-10-02 (BL-1220 Notes): the reply
/// to <c>*</c> is read whatever it is, the next mechanism is tried, and with none left
/// <c>LOGIN</c> is sent when it may be, else exit 67 <c>Authentication cancelled</c>.
/// </summary>
[TestClass]
public sealed class ImapProtocolHandlerSaslCancelTests
{
    private const string Url = "imap://127.0.0.1:18143/";

    private const string Greeting = "* OK ready\r\n";

    private const string NotBase64 = "+ !!!notbase64\r\n";

    private const string Capability = "A001 CAPABILITY\r\n";

    private const string AuthenticationCancelled = "Authentication cancelled";

    private const string SecurityLayerFailure = "GSSAPI handshake failure (invalid security layer)";

    private static readonly byte[] PlainMessage = Latin1("\0user\0secret");

    [TestMethod]
    public async Task ExecuteAsync_CramMd5ChallengeNotBase64_CancelsAndAuthenticatesWithPlain()
    {
        // Measured case (a): AUTHENTICATE CRAM-MD5, + !!!notbase64, A002 BAD cancelled, *,
        // AUTHENTICATE PLAIN, +, AHVzZXIAc2VjcmV0, then LIST and LOGOUT, exit 0.
        var sasl = new RankedSaslAuthenticator(("CRAM-MD5", null, []), ("PLAIN", PlainMessage, []));
        var log = new RecordingDiagnosticLog();

        ImapRun run = await RunAsync(
            Caps("IMAP4rev1 AUTH=CRAM-MD5 AUTH=PLAIN") + NotBase64 + "A002 BAD cancelled\r\n+ \r\nA003 OK done\r\n" + ListAndLogout("A004", "A005"),
            sasl,
            log: log);

        Assert.AreEqual(
            Capability + "A002 AUTHENTICATE CRAM-MD5\r\n*\r\nA003 AUTHENTICATE PLAIN\r\nAHVzZXIAc2VjcmV0\r\nA004 LIST \"\" *\r\nA005 LOGOUT\r\n",
            run.Sent);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
        Assert.IsEmpty(sasl.Challenges);
        CollectionAssert.AreEqual(new[] { "PLAIN" }, sasl.Offers[1]);
        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Warning), "SASL mechanism cancelled, choosing another: CRAM-MD5");
    }

    [TestMethod]
    [DataRow("A002 BAD cancelled\r\n", DisplayName = "tagged BAD")]
    [DataRow("* BAD Command not recognized\r\nA002 NO cancelled\r\n", DisplayName = "untagged line, then tagged NO")]
    [DataRow("+ more\r\n", DisplayName = "another continuation")]
    [DataRow("A002 OK done\r\n", DisplayName = "tagged OK")]
    public async Task ExecuteAsync_ReplyToTheCancel_IsReadWhateverItIs(string reply)
    {
        var sasl = new RankedSaslAuthenticator(("CRAM-MD5", null, []), ("PLAIN", PlainMessage, []));

        ImapRun run = await RunAsync(
            Caps("AUTH=CRAM-MD5 AUTH=PLAIN") + NotBase64 + reply + "+ \r\nA003 OK done\r\n" + ListAndLogout("A004", "A005"), sasl);

        StringAssert.StartsWith(run.Sent, Capability + "A002 AUTHENTICATE CRAM-MD5\r\n*\r\nA003 AUTHENTICATE PLAIN\r\nAHVzZXIAc2VjcmV0\r\n");
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_EveryMechanismCancelledAndLoginAllowed_FallsBackToLogin()
    {
        // Measured case (c): with no mechanism left, curl sends LOGIN.
        var sasl = new RankedSaslAuthenticator(("CRAM-MD5", null, []));

        ImapRun run = await RunAsync(
            Caps("IMAP4rev1 AUTH=CRAM-MD5") + NotBase64 + "A002 BAD cancelled\r\n* BAD Command not recognized\r\nA003 OK LOGIN completed\r\n"
                + ListAndLogout("A004", "A005"),
            sasl);

        Assert.AreEqual(Capability + "A002 AUTHENTICATE CRAM-MD5\r\n*\r\nA003 LOGIN user secret\r\nA004 LIST \"\" *\r\nA005 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_EveryMechanismCancelledAndLoginDisabled_FailsWithAuthenticationCancelledSendingNothingMore()
    {
        // Measured case (b): AUTH=CRAM-MD5 alone with LOGINDISABLED; nothing after the reply to *,
        // -v writes "* Authentication cancelled", no SASL: lines, exit 67.
        var sasl = new RankedSaslAuthenticator(("CRAM-MD5", null, []));
        var events = new RecordingTransferEvents();

        ImapRun run = await RunAsync(
            Caps("IMAP4rev1 AUTH=CRAM-MD5 LOGINDISABLED") + NotBase64 + "A002 BAD cancelled\r\n* BAD Command not recognized\r\n",
            sasl,
            events);

        Assert.AreEqual(Capability + "A002 AUTHENTICATE CRAM-MD5\r\n*\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, AuthenticationCancelled), run.Result);
        CollectionAssert.Contains(events.Info, AuthenticationCancelled);
        Assert.IsFalse(events.Info.Any(line => line.StartsWith("SASL", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelledThenTheNextMechanismRefused_FailsWithLoginDenied()
    {
        var sasl = new RankedSaslAuthenticator(("CRAM-MD5", null, []), ("PLAIN", PlainMessage, []));

        ImapRun run = await RunAsync(
            Caps("AUTH=CRAM-MD5 AUTH=PLAIN") + NotBase64 + "A002 BAD cancelled\r\n+ \r\nA003 NO denied\r\n", sasl);

        Assert.AreEqual(Capability + "A002 AUTHENTICATE CRAM-MD5\r\n*\r\nA003 AUTHENTICATE PLAIN\r\nAHVzZXIAc2VjcmV0\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelledMechanismNotAmongTheOffered_StopsWithAuthenticationCancelled()
    {
        var sasl = new StubbornSaslAuthenticator("CRAM-MD5");

        ImapRun run = await RunAsync(Caps("AUTH=PLAIN LOGINDISABLED") + NotBase64 + "A002 BAD cancelled\r\n", sasl);

        Assert.AreEqual(Capability + "A002 AUTHENTICATE CRAM-MD5\r\n*\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, AuthenticationCancelled), run.Result);
        Assert.AreEqual(1, sasl.ChoicesMade);
    }

    [TestMethod]
    public async Task ExecuteAsync_NtlmTypeTwoNotBase64_IsCancelled()
    {
        // NTLM's Type 1 answers the first +; the Type 2 after it is the challenge NTLM reads.
        var sasl = new RankedSaslAuthenticator(("NTLM", Latin1("T1"), []), ("PLAIN", PlainMessage, []));

        ImapRun run = await RunAsync(
            Caps("AUTH=NTLM AUTH=PLAIN") + "+ \r\n" + NotBase64 + "A002 BAD cancelled\r\n+ \r\nA003 OK done\r\n" + ListAndLogout("A004", "A005"), sasl);

        StringAssert.StartsWith(run.Sent, Capability + "A002 AUTHENTICATE NTLM\r\nVDE=\r\n*\r\nA003 AUTHENTICATE PLAIN\r\n");
        Assert.AreEqual(TransferResult.Success(0), run.Result);
        Assert.IsEmpty(sasl.Challenges);
    }

    [TestMethod]
    public async Task ExecuteAsync_GssapiLaterChallengeNotBase64_IsCancelled()
    {
        var sasl = new RankedSaslAuthenticator(("GSSAPI", Latin1("G"), [Latin1("r")]), ("PLAIN", PlainMessage, []));

        ImapRun run = await RunAsync(
            Caps("AUTH=GSSAPI AUTH=PLAIN") + "+ \r\n+ AAAA\r\n" + NotBase64 + "A002 BAD cancelled\r\n+ \r\nA003 OK done\r\n" + ListAndLogout("A004", "A005"),
            sasl);

        StringAssert.StartsWith(run.Sent, Capability + "A002 AUTHENTICATE GSSAPI\r\nRw==\r\ncg==\r\n*\r\nA003 AUTHENTICATE PLAIN\r\n");
        Assert.AreEqual(TransferResult.Success(0), run.Result);
        Assert.HasCount(1, sasl.Challenges);
    }

    [TestMethod]
    public async Task ExecuteAsync_DigestMd5RspauthNotBase64_IsHandedOverEmpty()
    {
        // DIGEST-MD5 reads only its first challenge; the rspauth one after it is not decoded.
        var sasl = new RankedSaslAuthenticator(("DIGEST-MD5", null, [Latin1("d"), Latin1("")]));

        ImapRun run = await RunAsync(
            Caps("AUTH=DIGEST-MD5") + "+ AAAA\r\n" + NotBase64 + "A002 OK done\r\n" + ListAndLogout("A003", "A004"), sasl);

        Assert.AreEqual(Capability + "A002 AUTHENTICATE DIGEST-MD5\r\nZA==\r\n=\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
        Assert.IsEmpty(sasl.Challenges[1].Challenge);
    }

    [TestMethod]
    [DataRow("+ =\r\n", DisplayName = "text starting =")]
    [DataRow("+ =x\r\n", DisplayName = "text starting = and more")]
    [DataRow("+ \r\n", DisplayName = "empty text")]
    public async Task ExecuteAsync_CramMd5ChallengeEmptyOrStartingEquals_IsHandedOverEmpty(string challenge)
    {
        var sasl = new RankedSaslAuthenticator(("CRAM-MD5", null, [Latin1("r")]));

        ImapRun run = await RunAsync(Caps("AUTH=CRAM-MD5") + challenge + "A002 OK done\r\n" + ListAndLogout("A003", "A004"), sasl);

        Assert.AreEqual(Capability + "A002 AUTHENTICATE CRAM-MD5\r\ncg==\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
        Assert.IsEmpty(sasl.Challenges.Single().Challenge);
    }

    [TestMethod]
    [DataRow("PLAIN")]
    [DataRow("LOGIN")]
    [DataRow("EXTERNAL")]
    [DataRow("XOAUTH2")]
    [DataRow("OAUTHBEARER")]
    public async Task ExecuteAsync_MechanismThatIgnoresItsChallenge_IsHandedANonBase64ChallengeEmpty(string mechanism)
    {
        // ADR-0133's rule stands for every mechanism curl does not decode a challenge for.
        var sasl = new FakeSaslAuthenticator(mechanism, null, Latin1("x"));

        ImapRun run = await RunAsync(Caps("AUTH=" + mechanism) + NotBase64 + "A002 OK done\r\n" + ListAndLogout("A003", "A004"), sasl);

        Assert.AreEqual(Capability + "A002 AUTHENTICATE " + mechanism + "\r\neA==\r\nA003 LIST \"\" *\r\nA004 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
        Assert.IsEmpty(sasl.Challenges.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_ExchangeGivesCancelReasonAndLoginDisabled_WritesTheReasonThenCancelsWithAuthenticationCancelled()
    {
        // curl 8.21.0, lib/curl_sasl.c lines 789-793: the GSSAPI step writes its infof line,
        // returns CURLE_BAD_CONTENT_ENCODING, and curl sends * and moves to SASL_CANCEL (BL-1348).
        var sasl = new RankedSaslAuthenticator(("GSSAPI", Latin1("G"), []));
        sasl.CancelReasons["GSSAPI"] = SecurityLayerFailure;
        var events = new RecordingTransferEvents();

        ImapRun run = await RunAsync(
            Caps("IMAP4rev1 AUTH=GSSAPI LOGINDISABLED") + "+ \r\n+ AAAA\r\nA002 BAD cancelled\r\n* BAD Command not recognized\r\n",
            sasl,
            events);

        Assert.AreEqual(Capability + "A002 AUTHENTICATE GSSAPI\r\nRw==\r\n*\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, AuthenticationCancelled), run.Result);
        Assert.HasCount(1, sasl.Challenges);
        int reasonAt = events.Transcript.IndexOf("* " + SecurityLayerFailure);
        int cancelAt = events.Transcript.FindIndex(line => line.StartsWith("> *", StringComparison.Ordinal));
        Assert.IsGreaterThanOrEqualTo(0, reasonAt);
        Assert.IsGreaterThan(reasonAt, cancelAt, "The reason is written before * is sent.");
    }

    [TestMethod]
    public async Task ExecuteAsync_ExchangeGivesCancelReasonAndAnotherMechanismOffered_StartsItAfterTheCancel()
    {
        var sasl = new RankedSaslAuthenticator(("GSSAPI", Latin1("G"), []), ("PLAIN", PlainMessage, []));
        sasl.CancelReasons["GSSAPI"] = SecurityLayerFailure;
        var events = new RecordingTransferEvents();

        ImapRun run = await RunAsync(
            Caps("AUTH=GSSAPI AUTH=PLAIN") + "+ \r\n+ AAAA\r\nA002 BAD cancelled\r\n+ \r\nA003 OK done\r\n" + ListAndLogout("A004", "A005"),
            sasl,
            events);

        Assert.AreEqual(
            Capability + "A002 AUTHENTICATE GSSAPI\r\nRw==\r\n*\r\nA003 AUTHENTICATE PLAIN\r\nAHVzZXIAc2VjcmV0\r\nA004 LIST \"\" *\r\nA005 LOGOUT\r\n",
            run.Sent);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
        CollectionAssert.Contains(events.Info, SecurityLayerFailure);
        CollectionAssert.AreEqual(new[] { "PLAIN" }, sasl.Offers[1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_ExchangeCannotAnswerWithNoCancelReason_FailsWithLoginDeniedSendingNoCancel()
    {
        var sasl = new RankedSaslAuthenticator(("GSSAPI", Latin1("G"), []), ("PLAIN", PlainMessage, []));
        var events = new RecordingTransferEvents();

        ImapRun run = await RunAsync(Caps("AUTH=GSSAPI AUTH=PLAIN") + "+ \r\n+ AAAA\r\n", sasl, events);

        Assert.AreEqual(Capability + "A002 AUTHENTICATE GSSAPI\r\nRw==\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), run.Result);
        CollectionAssert.DoesNotContain(events.Info, SecurityLayerFailure);
    }

    private static Task<ImapRun> RunAsync(string script, ISaslAuthenticator sasl, RecordingTransferEvents? events = null, RecordingDiagnosticLog? log = null)
    {
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = Stream.Null,
            Credentials = new NetworkCredential("user", "secret"),
            Events = (ITransferEvents?)events ?? NoTransferEvents.Instance,
            DiagnosticLog = (IDiagnosticLog?)log ?? NoDiagnosticLog.Instance,
        };
        return ImapRun.ExecuteAsync(context, new ScriptedConnection(Latin1(Greeting + script)), sasl);
    }

    private static string Caps(string words) => "* CAPABILITY " + words + "\r\nA001 OK done\r\n";

    private static string ListAndLogout(string listTag, string logoutTag) =>
        listTag + " OK LIST completed\r\n* BYE Logging out\r\n" + logoutTag + " OK LOGOUT completed\r\n";

    private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);
}
