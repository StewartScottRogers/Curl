using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smtp.Fakes;

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

    // realm="localhost",qop="auth",algorithm=md5-sess,charset=utf-8 - no nonce.
    private const string NoNonceChallenge = "cmVhbG09ImxvY2FsaG9zdCIscW9wPSJhdXRoIixhbGdvcml0aG09bWQ1LXNlc3MsY2hhcnNldD11dGYtOA==";

    [TestMethod]
    public async Task ExecuteAsync_ExchangeRejectsTheChallenge_FailsWithAuthErrorSendingNothingMore()
    {
        var sasl = new RejectingSaslAuthenticator("DIGEST-MD5");

        SmtpRun run = await SmtpRun.ExecuteAsync(
            new TransferContext { Url = CurlUrl.Parse(Url), Output = Stream.Null, Credentials = new NetworkCredential("user", "pencil") },
            new ScriptedConnection(Encoding.Latin1.GetBytes(
                "220 localhost ESMTP\r\n250-localhost\r\n250 AUTH DIGEST-MD5 PLAIN\r\n334 " + NoNonceChallenge + "\r\n" + SmtpRun.HelpReply + "221 Bye\r\n")),
            sasl);

        Assert.AreEqual("EHLO x\r\nAUTH DIGEST-MD5\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.AuthError, "An authentication function returned an error"), run.Result);
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

        SmtpRun run = await SmtpRun.ExecuteAsync(
            new TransferContext
            {
                Url = CurlUrl.Parse(Url),
                Output = Stream.Null,
                Credentials = new NetworkCredential(@"DOMAIN\u", "p"),
                Mail = new MailRequestOptions { SaslInitialResponse = saslInitialResponse },
            },
            new ScriptedConnection(Encoding.Latin1.GetBytes("220 localhost ESMTP\r\n250-localhost\r\n250 AUTH GSSAPI\r\n334 \r\n" + SmtpRun.HelpReply + "221 Bye\r\n")),
            sasl);

        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.AuthError, "An authentication function returned an error"), run.Result);
        Assert.AreEqual(1, sasl.InitialResponsesAsked);
        Assert.IsEmpty(sasl.Challenges);
    }
}
