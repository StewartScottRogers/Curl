using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Smtp.Fakes;

namespace Curl.Original.Protocol.Smtp;

/// <summary>
/// Pins CGR-104 (BL-3633): curl checks SMTP's login options once connected and, for an option
/// other than <c>AUTH=</c> or an <c>AUTH=</c> naming no mechanism it knows, exits 3 with
/// <c>URL using bad/illegal format or missing URL</c> before sending a byte, where Curl sent
/// <c>EHLO</c>. Measured 2026-10-26 with <c>Record-CurlExchange.ps1 -Smtp</c>, curl running
/// <c>-sS -u u:p --login-options &lt;options&gt; smtp://127.0.0.1:18733 --mail-rcpt x</c>:
/// <c>AUTH=BOGUS</c>, <c>FOO=BAR</c> and <c>AUTH=</c> exit 3 having sent nothing;
/// <c>AUTH=plain</c> and <c>AUTH=*</c> log in and exit 0.
/// </summary>
[TestClass]
public sealed class Cgr104Tests
{
    [TestMethod]
    [DataRow("AUTH=BOGUS")]
    [DataRow("FOO=BAR")]
    [DataRow("AUTH=")]
    [DataRow("AUTH=PLAIN;FOO=BAR")]
    public async Task ExecuteAsync_LoginOptionsCurlRefuses_ExitsThreeHavingSentNothing(string loginOptions)
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes("220 localhost ESMTP\r\n"));
        var handler = new SmtpProtocolHandler(
            new QueuedConnector(ConnectResult.Connected(connection, null)), new QueuedTlsProvider(), new FakeSaslAuthenticator("PLAIN", [1]));

        TransferResult result = await handler.ExecuteAsync(Context(loginOptions));

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual("URL using bad/illegal format or missing URL", result.ErrorMessage);
        Assert.IsEmpty(connection.Sent);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("AUTH=*")]
    [DataRow("auth=plain")]
    [DataRow("AUTH=PLAIN;AUTH=CRAM-MD5;")]
    public void AreLoginOptionsValid_OptionsCurlAccepts_IsTrue(string? loginOptions) =>
        Assert.IsTrue(SmtpSaslAuthentication.AreLoginOptionsValid(loginOptions));

    private static TransferContext Context(string loginOptions) => new()
    {
        Url = CurlUrl.Parse("smtp://127.0.0.1:18733"),
        Output = Stream.Null,
        Credentials = new NetworkCredential("u", "p"),
        Mail = new MailRequestOptions { Recipients = ["x"], LoginOptions = loginOptions },
    };
}
