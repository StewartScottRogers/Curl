using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smtp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Pins how an SMTP session cancels a SASL exchange with <c>*</c> when a mechanism that reads
/// its challenge is sent one that is not base64, then tries the next mechanism, as curl 8.21.0
/// (the Schannel build) does, recorded with <c>Record-CurlExchange.ps1 -Smtp</c> on 2026-09-29
/// (BL-774 Notes, ADR-0133's amendment).
/// </summary>
[TestClass]
public sealed class SmtpProtocolHandlerSaslCancelTests
{
    private const string Url = "smtp://127.0.0.1:18025/x";

    /// <summary>Gets or sets the running test's context, which MSTest sets.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private const string Greeting = "220 localhost ESMTP\r\n";

    private const string Ehlo = "EHLO x\r\n";

    private const string BadChallenge = "334 !!!notbase64\r\n";

    private const string Cancelled = "501 cancelled\r\n";

    private const string HelpReplyAndBye = SmtpRun.HelpReply + "221 Bye\r\n";

    private const string HelpAndQuit = "HELP\r\nQUIT\r\n";

    private static readonly (string, byte[]?, byte[][]) CramMd5 = ("CRAM-MD5", null, ["u digest"u8.ToArray()]);

    private static readonly (string, byte[]?, byte[][]) DigestMd5 = ("DIGEST-MD5", null, ["digest"u8.ToArray(), []]);

    private static readonly (string, byte[]?, byte[][]) Plain = ("PLAIN", "\0u\0p"u8.ToArray(), []);

    private static TransferResult AuthenticationCancelled => TransferResult.Failure(CurlExitCode.LoginDenied, "Authentication cancelled");

    [TestMethod]
    public async Task ExecuteAsync_BadCramMd5ChallengeWithPlainOffered_CancelsAndAuthenticatesWithPlain()
    {
        // Case (a): EHLO offers AUTH CRAM-MD5 PLAIN.
        var sasl = new RankedSaslAuthenticator(CramMd5, Plain);

        SmtpRun run = await RunAsync(Offering("CRAM-MD5 PLAIN") + BadChallenge + Cancelled + "334 \r\n235 ok\r\n" + HelpReplyAndBye, sasl);

        Diagnostics.Diff("sent", Ehlo + "AUTH CRAM-MD5\r\n*\r\nAUTH PLAIN\r\nAHUAcA==\r\n" + HelpAndQuit, run.Sent);
        Assert.AreEqual(Ehlo + "AUTH CRAM-MD5\r\n*\r\nAUTH PLAIN\r\nAHUAcA==\r\n" + HelpAndQuit, run.Sent);
        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
        Diagnostics.AssertValues("second offer", "PLAIN", string.Join(", ", sasl.Offers[1]));
        CollectionAssert.AreEqual(new[] { "PLAIN" }, sasl.Offers[1]);
        Diagnostics.AssertValues("challenges handed over", 0, sasl.Challenges.Count);
        Assert.IsEmpty(sasl.Challenges);
    }

    [TestMethod]
    public async Task ExecuteAsync_BadCramMd5ChallengeAlone_CancelsAndFailsWithAuthenticationCancelled()
    {
        // Case (b): EHLO offers only AUTH CRAM-MD5; curl sends no QUIT.
        var sasl = new RankedSaslAuthenticator(CramMd5, Plain);

        SmtpRun run = await RunAsync(Offering("CRAM-MD5") + BadChallenge + Cancelled + HelpReplyAndBye, sasl);

        Diagnostics.Diff("sent", Ehlo + "AUTH CRAM-MD5\r\n*\r\n", run.Sent);
        Assert.AreEqual(Ehlo + "AUTH CRAM-MD5\r\n*\r\n", run.Sent);
        Diagnostics.AssertResult(AuthenticationCancelled, run.Result);
        Assert.AreEqual(AuthenticationCancelled, run.Result);
        Diagnostics.AssertValues("second offer", string.Empty, string.Join(", ", sasl.Offers[1]));
        Assert.IsEmpty(sasl.Offers[1]);
    }

    [TestMethod]
    [DataRow("501 cancelled\r\n", DisplayName = "501")]
    [DataRow("334 odd\r\n", DisplayName = "334")]
    [DataRow("235 ok\r\n", DisplayName = "235")]
    public async Task ExecuteAsync_AnyReplyToTheCancel_IsReadAndTheNextMechanismTried(string reply)
    {
        var sasl = new RankedSaslAuthenticator(CramMd5, Plain);

        SmtpRun run = await RunAsync(Offering("CRAM-MD5 PLAIN") + BadChallenge + reply + "334 \r\n235 ok\r\n" + HelpReplyAndBye, sasl);

        Diagnostics.Diff("sent", Ehlo + "AUTH CRAM-MD5\r\n*\r\nAUTH PLAIN\r\nAHUAcA==\r\n" + HelpAndQuit, run.Sent);
        Assert.AreEqual(Ehlo + "AUTH CRAM-MD5\r\n*\r\nAUTH PLAIN\r\nAHUAcA==\r\n" + HelpAndQuit, run.Sent);
        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_EveryMechanismCancelled_FailsWithAuthenticationCancelled()
    {
        var sasl = new RankedSaslAuthenticator(DigestMd5, CramMd5);

        SmtpRun run = await RunAsync(Offering("CRAM-MD5 DIGEST-MD5") + BadChallenge + Cancelled + BadChallenge + Cancelled + HelpReplyAndBye, sasl);

        Diagnostics.Diff("sent", Ehlo + "AUTH DIGEST-MD5\r\n*\r\nAUTH CRAM-MD5\r\n*\r\n", run.Sent);
        Assert.AreEqual(Ehlo + "AUTH DIGEST-MD5\r\n*\r\nAUTH CRAM-MD5\r\n*\r\n", run.Sent);
        Diagnostics.AssertResult(AuthenticationCancelled, run.Result);
        Assert.AreEqual(AuthenticationCancelled, run.Result);
        Diagnostics.AssertValues("offers count", 3, sasl.Offers.Count);
        Assert.HasCount(3, sasl.Offers);
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelThenRefused_FailsWithLoginDenied()
    {
        var sasl = new RankedSaslAuthenticator(CramMd5, Plain);

        SmtpRun run = await RunAsync(Offering("CRAM-MD5 PLAIN") + BadChallenge + Cancelled + "535 no\r\n" + HelpReplyAndBye, sasl);

        Diagnostics.Diff("sent", Ehlo + "AUTH CRAM-MD5\r\n*\r\nAUTH PLAIN\r\n", run.Sent);
        Assert.AreEqual(Ehlo + "AUTH CRAM-MD5\r\n*\r\nAUTH PLAIN\r\n", run.Sent);
        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), run.Result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerClosesAfterTheCancel_FailsWithRecvError()
    {
        var sasl = new RankedSaslAuthenticator(CramMd5, Plain);

        SmtpRun run = await RunAsync(Offering("CRAM-MD5 PLAIN") + BadChallenge, sasl);

        Diagnostics.Diff("sent", Ehlo + "AUTH CRAM-MD5\r\n*\r\n", run.Sent);
        Assert.AreEqual(Ehlo + "AUTH CRAM-MD5\r\n*\r\n", run.Sent);
        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.RecvError, "response reading failed (errno: 0)"), run.Result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RecvError, "response reading failed (errno: 0)"), run.Result);
    }

    [TestMethod]
    [DataRow("334 \r\n", DisplayName = "Empty text")]
    [DataRow("334\r\n", DisplayName = "No text")]
    [DataRow("334 =!!\r\n", DisplayName = "Text starting =")]
    public async Task ExecuteAsync_EmptyCramMd5Challenge_IsHandedOverEmpty(string challenge)
    {
        var sasl = new RankedSaslAuthenticator(CramMd5, Plain);

        SmtpRun run = await RunAsync(Offering("CRAM-MD5 PLAIN") + challenge + "235 ok\r\n" + HelpReplyAndBye, sasl);

        Diagnostics.Diff("sent", Ehlo + "AUTH CRAM-MD5\r\ndSBkaWdlc3Q=\r\n" + HelpAndQuit, run.Sent);
        Assert.AreEqual(Ehlo + "AUTH CRAM-MD5\r\ndSBkaWdlc3Q=\r\n" + HelpAndQuit, run.Sent);
        Diagnostics.AssertValues("challenge length handed over", 0, sasl.Challenges.Single().Challenge.Length);
        Assert.IsEmpty(sasl.Challenges.Single().Challenge);
    }

    [TestMethod]
    public async Task ExecuteAsync_BadDigestMd5Rspauth_IsHandedOverEmpty()
    {
        var sasl = new RankedSaslAuthenticator(DigestMd5);

        SmtpRun run = await RunAsync(Offering("DIGEST-MD5") + "334 bm9uY2U9MQ==\r\n" + BadChallenge + "235 ok\r\n" + HelpReplyAndBye, sasl);

        Diagnostics.Diff("sent", Ehlo + "AUTH DIGEST-MD5\r\nZGlnZXN0\r\n=\r\n" + HelpAndQuit, run.Sent);
        Assert.AreEqual(Ehlo + "AUTH DIGEST-MD5\r\nZGlnZXN0\r\n=\r\n" + HelpAndQuit, run.Sent);
        Diagnostics.AssertValues("second challenge length handed over", 0, sasl.Challenges[1].Challenge.Length);
        Assert.IsEmpty(sasl.Challenges[1].Challenge);
    }

    [TestMethod]
    public async Task ExecuteAsync_NtlmBadType2_SendsType1ThenCancels()
    {
        // curl never reads the 334 its Type 1 answers; the Type 2 after it is the one decoded.
        var sasl = new RankedSaslAuthenticator(("NTLM", "type1"u8.ToArray(), ["type3"u8.ToArray()]), Plain);

        SmtpRun run = await RunAsync(
            Offering("NTLM PLAIN") + BadChallenge + BadChallenge + Cancelled + "334 \r\n235 ok\r\n" + HelpReplyAndBye, sasl);

        Diagnostics.Diff("sent", Ehlo + "AUTH NTLM\r\ndHlwZTE=\r\n*\r\nAUTH PLAIN\r\nAHUAcA==\r\n" + HelpAndQuit, run.Sent);
        Assert.AreEqual(Ehlo + "AUTH NTLM\r\ndHlwZTE=\r\n*\r\nAUTH PLAIN\r\nAHUAcA==\r\n" + HelpAndQuit, run.Sent);
        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_GssapiBadLaterChallenge_Cancels()
    {
        var sasl = new RankedSaslAuthenticator(("GSSAPI", "token"u8.ToArray(), [[]]), Plain);

        // The token answers the first 334, the exchange the second, and the third is not base64.
        SmtpRun run = await RunAsync(
            Offering("GSSAPI PLAIN") + "334 \r\n334 \r\n" + BadChallenge + Cancelled + "334 \r\n235 ok\r\n" + HelpReplyAndBye, sasl);

        Diagnostics.Diff("sent", Ehlo + "AUTH GSSAPI\r\ndG9rZW4=\r\n=\r\n*\r\nAUTH PLAIN\r\nAHUAcA==\r\n" + HelpAndQuit, run.Sent);
        Assert.AreEqual(Ehlo + "AUTH GSSAPI\r\ndG9rZW4=\r\n=\r\n*\r\nAUTH PLAIN\r\nAHUAcA==\r\n" + HelpAndQuit, run.Sent);
        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
    }

    [TestMethod]
    [DataRow("PLAIN", DisplayName = "PLAIN")]
    [DataRow("LOGIN", DisplayName = "LOGIN")]
    [DataRow("EXTERNAL", DisplayName = "EXTERNAL")]
    [DataRow("XOAUTH2", DisplayName = "XOAUTH2")]
    [DataRow("OAUTHBEARER", DisplayName = "OAUTHBEARER")]
    public async Task ExecuteAsync_MechanismIgnoringItsChallenge_IsHandedABadChallengeEmpty(string mechanism)
    {
        // ADR-0133 point 7 still holds for these.
        var sasl = new RankedSaslAuthenticator((mechanism, null, ["m"u8.ToArray()]));

        SmtpRun run = await RunAsync(Offering(mechanism) + BadChallenge + "235 ok\r\n" + HelpReplyAndBye, sasl);

        Diagnostics.Diff("sent", Ehlo + "AUTH " + mechanism + "\r\nbQ==\r\n" + HelpAndQuit, run.Sent);
        Assert.AreEqual(Ehlo + "AUTH " + mechanism + "\r\nbQ==\r\n" + HelpAndQuit, run.Sent);
        Diagnostics.AssertValues("challenge length handed over", 0, sasl.Challenges.Single().Challenge.Length);
        Assert.IsEmpty(sasl.Challenges.Single().Challenge);
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelledMechanismNotAmongTheOffered_StopsWithAuthenticationCancelled()
    {
        // An authenticator that chose a mechanism the server never offered cannot drop it, so it is not asked again.
        var sasl = new FakeSaslAuthenticator("cram-md5", null);

        SmtpRun run = await RunAsync(Offering("PLAIN LOGIN") + BadChallenge + Cancelled + HelpReplyAndBye, sasl);

        Diagnostics.Diff("sent", Ehlo + "AUTH cram-md5\r\n*\r\n", run.Sent);
        Assert.AreEqual(Ehlo + "AUTH cram-md5\r\n*\r\n", run.Sent);
        Diagnostics.AssertResult(AuthenticationCancelled, run.Result);
        Assert.AreEqual(AuthenticationCancelled, run.Result);
        Diagnostics.AssertValues("choices count", 1, sasl.Choices.Count);
        Assert.HasCount(1, sasl.Choices);
    }

    [TestMethod]
    public async Task ExecuteAsync_ExchangeGivesACancelReason_WritesItThenCancelsAndTriesTheNextMechanism()
    {
        // curl 8.21.0 lib/curl_sasl.c 789-793: CURLE_BAD_CONTENT_ENCODING from a step means infof, cancelauth, next mechanism.
        const string Reason = "GSSAPI handshake failure (empty security message)";
        var sasl = new RankedSaslAuthenticator(("GSSAPI", "token"u8.ToArray(), []), Plain) { CancelReason = Reason };
        var events = new RecordingTransferEvents();

        SmtpRun run = await RunAsync(Offering("GSSAPI PLAIN") + "334 \r\n334 \r\n" + Cancelled + "334 \r\n235 ok\r\n" + HelpReplyAndBye, sasl, events);

        Diagnostics.Diff("sent", Ehlo + "AUTH GSSAPI\r\ndG9rZW4=\r\n*\r\nAUTH PLAIN\r\nAHUAcA==\r\n" + HelpAndQuit, run.Sent);
        Assert.AreEqual(Ehlo + "AUTH GSSAPI\r\ndG9rZW4=\r\n*\r\nAUTH PLAIN\r\nAHUAcA==\r\n" + HelpAndQuit, run.Sent);
        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
        Diagnostics.AssertValues("second offer", "PLAIN", string.Join(", ", sasl.Offers[1]));
        CollectionAssert.AreEqual(new[] { "PLAIN" }, sasl.Offers[1]);
        int reasonAt = events.Transcript.IndexOf("* " + Reason);
        Diagnostics.Act("cancel reason position in transcript", reasonAt);
        Diagnostics.Assert("cancel reason position in transcript is at least", 0, reasonAt);
        Assert.IsGreaterThanOrEqualTo(0, reasonAt);
        Diagnostics.Assert("line after the reason starts with", "> *", SmtpDiagnostics.Show(events.Transcript[reasonAt + 1]));
        Assert.StartsWith("> *", events.Transcript[reasonAt + 1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_ExchangeCannotAnswerWithoutACancelReason_FailsWithLoginDeniedAndSendsNoCancel()
    {
        var sasl = new RankedSaslAuthenticator(("GSSAPI", "token"u8.ToArray(), []), Plain);
        var events = new RecordingTransferEvents();

        SmtpRun run = await RunAsync(Offering("GSSAPI PLAIN") + "334 \r\n334 \r\n" + Cancelled + HelpReplyAndBye, sasl, events);

        Diagnostics.Diff("sent", Ehlo + "AUTH GSSAPI\r\ndG9rZW4=\r\n", run.Sent);
        Assert.AreEqual(Ehlo + "AUTH GSSAPI\r\ndG9rZW4=\r\n", run.Sent);
        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), run.Result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), run.Result);
        Diagnostics.AssertValues("an info line starts with GSSAPI", false, events.Info.Any(line => line.StartsWith("GSSAPI", StringComparison.Ordinal)));
        Assert.IsFalse(events.Info.Any(line => line.StartsWith("GSSAPI", StringComparison.Ordinal)));
    }

    private static string Offering(string mechanisms) => Greeting + "250-localhost\r\n250 AUTH " + mechanisms + "\r\n";

    private Task<SmtpRun> RunAsync(string replies, ISaslAuthenticator sasl, ITransferEvents? events = null)
    {
        Diagnostics.Arrange("sasl authenticator", sasl.GetType().Name);
        Diagnostics.Arrange("credentials", "u:p");
        return SmtpRun.ExecuteAsync(
            Diagnostics,
            new TransferContext { Url = CurlUrl.Parse(Url), Output = Stream.Null, Credentials = new NetworkCredential("u", "p"), Events = events ?? NoTransferEvents.Instance },
            new ScriptedConnection(Encoding.Latin1.GetBytes(replies)),
            sasl);
    }
}
