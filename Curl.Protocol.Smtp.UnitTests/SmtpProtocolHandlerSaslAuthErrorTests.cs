using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smtp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Pins how an SMTP session ends when the SASL exchange fails with an authentication-function
/// error, as curl 8.21.0 (the Schannel build) did for a DIGEST-MD5 challenge with no nonce,
/// recorded with <c>Record-CurlExchange.ps1 -Smtp</c> on 2026-09-29 (BL-781 Notes): exit 94,
/// "An authentication function returned an error", and nothing sent after <c>AUTH</c>, not
/// even <c>QUIT</c>.
/// </summary>
[TestClass]
public sealed class SmtpProtocolHandlerSaslAuthErrorTests
{
    private const string Url = "smtp://127.0.0.1:18025/x";

    /// <summary>Gets or sets the running test's context, which MSTest sets.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    // realm="localhost",qop="auth",algorithm=md5-sess,charset=utf-8 - no nonce.
    private const string NoNonceChallenge = "cmVhbG09ImxvY2FsaG9zdCIscW9wPSJhdXRoIixhbGdvcml0aG09bWQ1LXNlc3MsY2hhcnNldD11dGYtOA==";

    [TestMethod]
    public async Task ExecuteAsync_ExchangeRejectsTheChallenge_FailsWithAuthErrorSendingNothingMore()
    {
        var sasl = new RejectingSaslAuthenticator("DIGEST-MD5");

        Diagnostics.Arrange("sasl mechanism", "DIGEST-MD5 (rejects the challenge)");
        SmtpRun run = await SmtpRun.ExecuteAsync(
            Diagnostics,
            new TransferContext { Url = CurlUrl.Parse(Url), Output = Stream.Null, Credentials = new NetworkCredential("user", "pencil") },
            new ScriptedConnection(Encoding.Latin1.GetBytes(
                "220 localhost ESMTP\r\n250-localhost\r\n250 AUTH DIGEST-MD5 PLAIN\r\n334 " + NoNonceChallenge + "\r\n" + SmtpRun.HelpReply + "221 Bye\r\n")),
            sasl);

        Diagnostics.Diff("sent", "EHLO x\r\nAUTH DIGEST-MD5\r\n", run.Sent);
        Assert.AreEqual("EHLO x\r\nAUTH DIGEST-MD5\r\n", run.Sent);
        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.AuthError, "An authentication function returned an error"), run.Result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.AuthError, "An authentication function returned an error"), run.Result);
        Diagnostics.Diff(
            "challenge given to the exchange",
            "realm=\"localhost\",qop=\"auth\",algorithm=md5-sess,charset=utf-8",
            Encoding.Latin1.GetString(sasl.Challenges.Single()));
        Assert.AreEqual(
            "realm=\"localhost\",qop=\"auth\",algorithm=md5-sess,charset=utf-8",
            Encoding.Latin1.GetString(sasl.Challenges.Single()));
    }

    [TestMethod]
    [DataRow(false, "EHLO x\r\nAUTH GSSAPI\r\n", DisplayName = "Without --sasl-ir: AUTH GSSAPI, 334, then (94)")]
    [DataRow(true, "EHLO x\r\n", DisplayName = "--sasl-ir: no AUTH, then (94)")]
    public async Task ExecuteAsync_ExchangeCannotMakeItsInitialResponse_FailsWithAuthErrorSendingNothingMore(bool saslInitialResponse, string expectedSent)
    {
        // Measured 2026-09-29 with -u 'DOMAIN\u:p' and no KDC (BL-856).
        var sasl = new RejectingSaslAuthenticator("GSSAPI", failsInitialResponse: true);

        Diagnostics.Arrange("sasl mechanism", "GSSAPI (fails the initial response)");
        Diagnostics.Arrange("--sasl-ir", saslInitialResponse);
        SmtpRun run = await SmtpRun.ExecuteAsync(
            Diagnostics,
            new TransferContext
            {
                Url = CurlUrl.Parse(Url),
                Output = Stream.Null,
                Credentials = new NetworkCredential(@"DOMAIN\u", "p"),
                Mail = new MailRequestOptions { SaslInitialResponse = saslInitialResponse },
            },
            new ScriptedConnection(Encoding.Latin1.GetBytes("220 localhost ESMTP\r\n250-localhost\r\n250 AUTH GSSAPI\r\n334 \r\n" + SmtpRun.HelpReply + "221 Bye\r\n")),
            sasl);

        Diagnostics.Diff("sent", expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.AuthError, "An authentication function returned an error"), run.Result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.AuthError, "An authentication function returned an error"), run.Result);
        Diagnostics.AssertValues("initial responses asked", 1, sasl.InitialResponsesAsked);
        Assert.AreEqual(1, sasl.InitialResponsesAsked);
        Diagnostics.AssertValues("challenges", 0, sasl.Challenges.Count);
        Assert.IsEmpty(sasl.Challenges);
    }
}
