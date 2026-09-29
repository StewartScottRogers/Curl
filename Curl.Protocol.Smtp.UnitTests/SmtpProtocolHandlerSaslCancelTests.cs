using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smtp.Fakes;

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

        Assert.AreEqual(Ehlo + "AUTH CRAM-MD5\r\n*\r\nAUTH PLAIN\r\nAHUAcA==\r\n" + HelpAndQuit, run.Sent);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
        CollectionAssert.AreEqual(new[] { "PLAIN" }, sasl.Offers[1]);
        Assert.IsEmpty(sasl.Challenges);
    }

    [TestMethod]
    public async Task ExecuteAsync_BadCramMd5ChallengeAlone_CancelsAndFailsWithAuthenticationCancelled()
    {
        // Case (b): EHLO offers only AUTH CRAM-MD5; curl sends no QUIT.
        var sasl = new RankedSaslAuthenticator(CramMd5, Plain);

        SmtpRun run = await RunAsync(Offering("CRAM-MD5") + BadChallenge + Cancelled + HelpReplyAndBye, sasl);

        Assert.AreEqual(Ehlo + "AUTH CRAM-MD5\r\n*\r\n", run.Sent);
        Assert.AreEqual(AuthenticationCancelled, run.Result);
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

        Assert.AreEqual(Ehlo + "AUTH CRAM-MD5\r\n*\r\nAUTH PLAIN\r\nAHUAcA==\r\n" + HelpAndQuit, run.Sent);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_EveryMechanismCancelled_FailsWithAuthenticationCancelled()
    {
        var sasl = new RankedSaslAuthenticator(DigestMd5, CramMd5);

        SmtpRun run = await RunAsync(Offering("CRAM-MD5 DIGEST-MD5") + BadChallenge + Cancelled + BadChallenge + Cancelled + HelpReplyAndBye, sasl);

        Assert.AreEqual(Ehlo + "AUTH DIGEST-MD5\r\n*\r\nAUTH CRAM-MD5\r\n*\r\n", run.Sent);
        Assert.AreEqual(AuthenticationCancelled, run.Result);
        Assert.HasCount(3, sasl.Offers);
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelThenRefused_FailsWithLoginDenied()
    {
        var sasl = new RankedSaslAuthenticator(CramMd5, Plain);

        SmtpRun run = await RunAsync(Offering("CRAM-MD5 PLAIN") + BadChallenge + Cancelled + "535 no\r\n" + HelpReplyAndBye, sasl);

        Assert.AreEqual(Ehlo + "AUTH CRAM-MD5\r\n*\r\nAUTH PLAIN\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerClosesAfterTheCancel_FailsWithRecvError()
    {
        var sasl = new RankedSaslAuthenticator(CramMd5, Plain);

        SmtpRun run = await RunAsync(Offering("CRAM-MD5 PLAIN") + BadChallenge, sasl);

        Assert.AreEqual(Ehlo + "AUTH CRAM-MD5\r\n*\r\n", run.Sent);
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

        Assert.AreEqual(Ehlo + "AUTH CRAM-MD5\r\ndSBkaWdlc3Q=\r\n" + HelpAndQuit, run.Sent);
        Assert.IsEmpty(sasl.Challenges.Single().Challenge);
    }

    [TestMethod]
    public async Task ExecuteAsync_BadDigestMd5Rspauth_IsHandedOverEmpty()
    {
        var sasl = new RankedSaslAuthenticator(DigestMd5);

        SmtpRun run = await RunAsync(Offering("DIGEST-MD5") + "334 bm9uY2U9MQ==\r\n" + BadChallenge + "235 ok\r\n" + HelpReplyAndBye, sasl);

        Assert.AreEqual(Ehlo + "AUTH DIGEST-MD5\r\nZGlnZXN0\r\n=\r\n" + HelpAndQuit, run.Sent);
        Assert.IsEmpty(sasl.Challenges[1].Challenge);
    }

    [TestMethod]
    public async Task ExecuteAsync_NtlmBadType2_SendsType1ThenCancels()
    {
        // curl never reads the 334 its Type 1 answers; the Type 2 after it is the one decoded.
        var sasl = new RankedSaslAuthenticator(("NTLM", "type1"u8.ToArray(), ["type3"u8.ToArray()]), Plain);

        SmtpRun run = await RunAsync(
            Offering("NTLM PLAIN") + BadChallenge + BadChallenge + Cancelled + "334 \r\n235 ok\r\n" + HelpReplyAndBye, sasl);

        Assert.AreEqual(Ehlo + "AUTH NTLM\r\ndHlwZTE=\r\n*\r\nAUTH PLAIN\r\nAHUAcA==\r\n" + HelpAndQuit, run.Sent);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_GssapiBadLaterChallenge_Cancels()
    {
        var sasl = new RankedSaslAuthenticator(("GSSAPI", "token"u8.ToArray(), [[]]), Plain);

        // The token answers the first 334, the exchange the second, and the third is not base64.
        SmtpRun run = await RunAsync(
            Offering("GSSAPI PLAIN") + "334 \r\n334 \r\n" + BadChallenge + Cancelled + "334 \r\n235 ok\r\n" + HelpReplyAndBye, sasl);

        Assert.AreEqual(Ehlo + "AUTH GSSAPI\r\ndG9rZW4=\r\n=\r\n*\r\nAUTH PLAIN\r\nAHUAcA==\r\n" + HelpAndQuit, run.Sent);
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

        Assert.AreEqual(Ehlo + "AUTH " + mechanism + "\r\nbQ==\r\n" + HelpAndQuit, run.Sent);
        Assert.IsEmpty(sasl.Challenges.Single().Challenge);
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelledMechanismNotAmongTheOffered_StopsWithAuthenticationCancelled()
    {
        // An authenticator that chose a mechanism the server never offered cannot drop it, so it is not asked again.
        var sasl = new FakeSaslAuthenticator("cram-md5", null);

        SmtpRun run = await RunAsync(Offering("PLAIN LOGIN") + BadChallenge + Cancelled + HelpReplyAndBye, sasl);

        Assert.AreEqual(Ehlo + "AUTH cram-md5\r\n*\r\n", run.Sent);
        Assert.AreEqual(AuthenticationCancelled, run.Result);
        Assert.HasCount(1, sasl.Choices);
    }

    private static string Offering(string mechanisms) => Greeting + "250-localhost\r\n250 AUTH " + mechanisms + "\r\n";

    private static Task<SmtpRun> RunAsync(string replies, ISaslAuthenticator sasl) =>
        SmtpRun.ExecuteAsync(
            new TransferContext { Url = CurlUrl.Parse(Url), Output = Stream.Null, Credentials = new NetworkCredential("u", "p") },
            new ScriptedConnection(Encoding.Latin1.GetBytes(replies)),
            sasl);
}
