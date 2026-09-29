using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Pop3.Fakes;

namespace Curl.Protocol.Pop3;

/// <summary>
/// Pins how a POP3 session ends when the SASL exchange fails with an authentication-function
/// error, as curl 8.21.0 (the Schannel build) did for a DIGEST-MD5 challenge with no nonce,
/// recorded with <c>Record-CurlExchange.ps1 -Pop3</c> on 2026-09-29 (BL-781 Notes): exit 94,
/// "An authentication function returned an error", and nothing sent after <c>AUTH</c>, not
/// even <c>QUIT</c>.
/// </summary>
[TestClass]
public sealed class Pop3ProtocolHandlerSaslAuthErrorTests
{
    private const string Url = "pop3://127.0.0.1:18110/1";

    // realm="localhost",qop="auth",algorithm=md5-sess,charset=utf-8 - no nonce.
    private const string NoNonceChallenge = "cmVhbG09ImxvY2FsaG9zdCIscW9wPSJhdXRoIixhbGdvcml0aG09bWQ1LXNlc3MsY2hhcnNldD11dGYtOA==";

    [TestMethod]
    public async Task ExecuteAsync_ExchangeRejectsTheChallenge_FailsWithAuthErrorSendingNothingMore()
    {
        var sasl = new RejectingSaslAuthenticator("DIGEST-MD5");
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(
            "+OK POP3 ready <1896.697170952@localhost>\r\n+OK\r\nUSER\r\nSASL DIGEST-MD5 PLAIN\r\n.\r\n+ " + NoNonceChallenge + "\r\n+OK Bye\r\n"));
        var context = new TransferContext { Url = CurlUrl.Parse(Url), Output = Stream.Null, Credentials = new NetworkCredential("user", "pencil") };

        TransferResult result = await new Pop3ProtocolHandler(new QueuedConnector(ConnectResult.Connected(connection)), new QueuedTlsProvider(), sasl)
            .ExecuteAsync(context);

        Assert.AreEqual("CAPA\r\nAUTH DIGEST-MD5\r\n", Encoding.Latin1.GetString(connection.Sent));
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.AuthError, "An authentication function returned an error"), result);
        Assert.AreEqual(
            "realm=\"localhost\",qop=\"auth\",algorithm=md5-sess,charset=utf-8",
            Encoding.Latin1.GetString(sasl.Challenges.Single()));
    }

    [TestMethod]
    [DataRow(false, "CAPA\r\nAUTH GSSAPI\r\n", DisplayName = "Without --sasl-ir: AUTH GSSAPI, +, then (94)")]
    [DataRow(true, "CAPA\r\n", DisplayName = "--sasl-ir: no AUTH, then (94)")]
    public async Task ExecuteAsync_ExchangeCannotMakeItsInitialResponse_FailsWithAuthErrorSendingNothingMore(bool saslInitialResponse, string expectedSent)
    {
        // Measured 2026-09-29 with -u 'DOMAIN\u:p' and no KDC (BL-856).
        var sasl = new RejectingSaslAuthenticator("GSSAPI", failsInitialResponse: true);
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes("+OK POP3 ready\r\n+OK\r\nUSER\r\nSASL GSSAPI\r\n.\r\n+ \r\n+OK Bye\r\n"));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = Stream.Null,
            Credentials = new NetworkCredential(@"DOMAIN\u", "p"),
            Mail = new MailRequestOptions { SaslInitialResponse = saslInitialResponse },
        };

        TransferResult result = await new Pop3ProtocolHandler(new QueuedConnector(ConnectResult.Connected(connection)), new QueuedTlsProvider(), sasl)
            .ExecuteAsync(context);

        Assert.AreEqual(expectedSent, Encoding.Latin1.GetString(connection.Sent));
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.AuthError, "An authentication function returned an error"), result);
        Assert.AreEqual(1, sasl.InitialResponsesAsked);
        Assert.IsEmpty(sasl.Challenges);
    }
}
