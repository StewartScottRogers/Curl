using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Imap.Fakes;

namespace Curl.Protocol.Imap;

/// <summary>
/// Pins how an IMAP session ends when the SASL exchange fails with an authentication-function
/// error, as curl 8.21.0 (the Schannel build) did for a DIGEST-MD5 challenge with no nonce,
/// recorded with <c>Record-CurlExchange.ps1 -Imap</c> on 2026-09-29 (BL-781 Notes): exit 94,
/// "An authentication function returned an error", and nothing sent after
/// <c>AUTHENTICATE</c>, not even <c>LOGOUT</c>.
/// </summary>
[TestClass]
public sealed class ImapProtocolHandlerSaslAuthErrorTests
{
    private const string Url = "imap://127.0.0.1:18143/";

    // realm="localhost",qop="auth",algorithm=md5-sess,charset=utf-8 - no nonce.
    private const string NoNonceChallenge = "cmVhbG09ImxvY2FsaG9zdCIscW9wPSJhdXRoIixhbGdvcml0aG09bWQ1LXNlc3MsY2hhcnNldD11dGYtOA==";

    [TestMethod]
    public async Task ExecuteAsync_ExchangeRejectsTheChallenge_FailsWithAuthErrorSendingNothingMore()
    {
        var sasl = new RejectingSaslAuthenticator("DIGEST-MD5");

        ImapRun run = await ImapRun.ExecuteAsync(
            new TransferContext { Url = CurlUrl.Parse(Url), Output = Stream.Null, Credentials = new NetworkCredential("user", "pencil") },
            new ScriptedConnection(Encoding.Latin1.GetBytes(
                "* OK [CAPABILITY IMAP4rev1 AUTH=DIGEST-MD5 AUTH=PLAIN] ready\r\n* CAPABILITY IMAP4rev1 AUTH=DIGEST-MD5 AUTH=PLAIN\r\nA001 OK done\r\n+ "
                    + NoNonceChallenge + "\r\nA002 NO failed\r\n")),
            sasl);

        Assert.AreEqual("A001 CAPABILITY\r\nA002 AUTHENTICATE DIGEST-MD5\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.AuthError, "An authentication function returned an error"), run.Result);
        Assert.AreEqual(
            "realm=\"localhost\",qop=\"auth\",algorithm=md5-sess,charset=utf-8",
            Encoding.Latin1.GetString(sasl.Challenges.Single()));
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerOffersSaslIrToAMechanismWithNoInitialResponse_SendsTheBareCommandAndAnswersTheChallenge()
    {
        var sasl = new RejectingSaslAuthenticator("DIGEST-MD5");

        ImapRun run = await ImapRun.ExecuteAsync(
            new TransferContext { Url = CurlUrl.Parse(Url), Output = Stream.Null, Credentials = new NetworkCredential("user", "pencil") },
            new ScriptedConnection(Encoding.Latin1.GetBytes(
                "* OK ready\r\n* CAPABILITY IMAP4rev1 SASL-IR AUTH=DIGEST-MD5\r\nA001 OK done\r\n+ " + NoNonceChallenge + "\r\nA002 NO failed\r\n")),
            sasl);

        Assert.AreEqual("A001 CAPABILITY\r\nA002 AUTHENTICATE DIGEST-MD5\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.AuthError, "An authentication function returned an error"), run.Result);
        Assert.AreEqual(1, sasl.InitialResponsesAsked);
        Assert.HasCount(1, sasl.Challenges);
    }

    [TestMethod]
    [DataRow(false, "", "A001 CAPABILITY\r\nA002 AUTHENTICATE GSSAPI\r\n", DisplayName = "Without --sasl-ir: AUTHENTICATE GSSAPI, +, then (94)")]
    [DataRow(true, "", "A001 CAPABILITY\r\n", DisplayName = "--sasl-ir: no AUTHENTICATE, then (94)")]
    [DataRow(false, " SASL-IR", "A001 CAPABILITY\r\n", DisplayName = "Server offers SASL-IR: no AUTHENTICATE, then (94)")]
    public async Task ExecuteAsync_ExchangeCannotMakeItsInitialResponse_FailsWithAuthErrorSendingNothingMore(bool saslInitialResponse, string saslIr, string expectedSent)
    {
        // Measured 2026-09-29 with -u 'DOMAIN\u:p' and no KDC (BL-856).
        var sasl = new RejectingSaslAuthenticator("GSSAPI", failsInitialResponse: true);

        ImapRun run = await ImapRun.ExecuteAsync(
            new TransferContext
            {
                Url = CurlUrl.Parse(Url),
                Output = Stream.Null,
                Credentials = new NetworkCredential(@"DOMAIN\u", "p"),
                Mail = new MailRequestOptions { SaslInitialResponse = saslInitialResponse },
            },
            new ScriptedConnection(Encoding.Latin1.GetBytes(
                "* OK ready\r\n* CAPABILITY IMAP4rev1 AUTH=GSSAPI" + saslIr + "\r\nA001 OK done\r\n+ \r\nA002 NO failed\r\n")),
            sasl);

        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.AuthError, "An authentication function returned an error"), run.Result);
        Assert.AreEqual(1, sasl.InitialResponsesAsked);
        Assert.IsEmpty(sasl.Challenges);
    }
}
