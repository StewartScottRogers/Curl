using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smtp.Fakes;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Pins the domain an SMTP session names in <c>EHLO</c> against curl 8.21.0: the URL's path,
/// percent-decoded to bytes, or the machine's host name for an empty path, and exit 3 for a
/// path that decodes to a control character. Recorded from real curl on 2026-09-28 with
/// <c>Record-CurlExchange.ps1 -Smtp</c> (BL-540 Notes).
/// </summary>
[TestClass]
public sealed class SmtpProtocolHandlerEhloDomainTests
{
    private const string Replies = "220 localhost ESMTP\r\n250 localhost\r\n" + SmtpRun.HelpReply + "221 Bye\r\n";

    [TestMethod]
    [DataRow("a%20b", "a b", DisplayName = "an escaped space")]
    [DataRow("caf%C3%A9", "cafÃ©", DisplayName = "UTF-8 bytes go out as bytes")]
    [DataRow("a%zz", "a%zz", DisplayName = "an escape that is not hex")]
    [DataRow("a%4", "a%4", DisplayName = "an escape cut short")]
    public async Task ExecuteAsync_PathWithEscapes_NamesTheDecodedBytes(string path, string expectedDomain)
    {
        SmtpRun run = await SmtpRun.ExecuteAsync("smtp://127.0.0.1:18025/" + path, Scripted());

        Assert.AreEqual("EHLO " + expectedDomain + "\r\nHELP\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyPath_NamesTheLocalHostName()
    {
        // -T - smtp://127.0.0.1:18025/: EHLO with the machine's host name.
        SmtpRun run = await SmtpRun.ExecuteAsync("smtp://127.0.0.1:18025/", Scripted());

        Assert.AreEqual("EHLO " + SmtpRun.LocalHostName + "\r\nHELP\r\nQUIT\r\n", run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyPathWithThePublicConstructor_NamesTheMachinesHostName()
    {
        ScriptedConnection connection = Scripted();
        var handler = new SmtpProtocolHandler(new QueuedConnector(ConnectResult.Connected(connection)), new QueuedTlsProvider());

        await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("smtp://127.0.0.1:18025/"), Output = Stream.Null });

        Assert.AreEqual("EHLO " + Dns.GetHostName() + "\r\nHELP\r\nQUIT\r\n", Encoding.Latin1.GetString(connection.Sent));
    }

    [TestMethod]
    [DataRow("a%0Db", DisplayName = "CR")]
    [DataRow("a%00b", DisplayName = "NUL")]
    public async Task ExecuteAsync_PathDecodingToAControlCharacter_FailsWithExit3AfterConnecting(string path)
    {
        SmtpRun run = await SmtpRun.ExecuteAsync("smtp://127.0.0.1:18025/" + path, Scripted());

        Assert.AreEqual(string.Empty, run.Sent);
        Assert.HasCount(1, run.Connector.Targets);
        Assert.IsTrue(run.Connection.IsDisposed);
        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.UrlMalformat, "URL using bad/illegal format or missing URL"),
            run.Result);
    }

    private static ScriptedConnection Scripted() => new(Encoding.Latin1.GetBytes(Replies));
}
