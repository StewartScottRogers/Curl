using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smtp.Fakes;
using Curl.Testing;

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

    /// <summary>Gets or sets the running test's context, which MSTest sets.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("a%20b", "a b", DisplayName = "an escaped space")]
    [DataRow("caf%C3%A9", "cafÃ©", DisplayName = "UTF-8 bytes go out as bytes")]
    [DataRow("a%zz", "a%zz", DisplayName = "an escape that is not hex")]
    [DataRow("a%4", "a%4", DisplayName = "an escape cut short")]
    public async Task ExecuteAsync_PathWithEscapes_NamesTheDecodedBytes(string path, string expectedDomain)
    {
        SmtpRun run = await SmtpRun.ExecuteAsync(Diagnostics, "smtp://127.0.0.1:18025/" + path, Scripted());

        Diagnostics.Diff("sent", "EHLO " + expectedDomain + "\r\nHELP\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual("EHLO " + expectedDomain + "\r\nHELP\r\nQUIT\r\n", run.Sent);
        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyPath_NamesTheLocalHostName()
    {
        // -T - smtp://127.0.0.1:18025/: EHLO with the machine's host name.
        SmtpRun run = await SmtpRun.ExecuteAsync(Diagnostics, "smtp://127.0.0.1:18025/", Scripted());

        Diagnostics.Diff("sent", "EHLO " + SmtpRun.LocalHostName + "\r\nHELP\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual("EHLO " + SmtpRun.LocalHostName + "\r\nHELP\r\nQUIT\r\n", run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyPathWithThePublicConstructor_NamesTheMachinesHostName()
    {
        ScriptedConnection connection = Scripted();
        Diagnostics.ArrangeRun("smtp://127.0.0.1:18025/", Replies);
        var handler = new SmtpProtocolHandler(new QueuedConnector(ConnectResult.Connected(connection)), new QueuedTlsProvider());

        TransferResult result = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("smtp://127.0.0.1:18025/"), Output = Stream.Null });
        Diagnostics.ActResult(result);
        Diagnostics.Act("sent", SmtpDiagnostics.Show(Encoding.Latin1.GetString(connection.Sent)));

        Diagnostics.Diff("sent", "EHLO " + Dns.GetHostName() + "\r\nHELP\r\nQUIT\r\n", Encoding.Latin1.GetString(connection.Sent));
        Assert.AreEqual("EHLO " + Dns.GetHostName() + "\r\nHELP\r\nQUIT\r\n", Encoding.Latin1.GetString(connection.Sent));
    }

    [TestMethod]
    [DataRow("a%0Db", DisplayName = "CR")]
    [DataRow("a%00b", DisplayName = "NUL")]
    public async Task ExecuteAsync_PathDecodingToAControlCharacter_FailsWithExit3AfterConnecting(string path)
    {
        SmtpRun run = await SmtpRun.ExecuteAsync(Diagnostics, "smtp://127.0.0.1:18025/" + path, Scripted());

        Diagnostics.Diff("sent", string.Empty, run.Sent);
        Assert.AreEqual(string.Empty, run.Sent);
        Diagnostics.AssertValues("connect target count", 1, run.Connector.Targets.Count);
        Assert.HasCount(1, run.Connector.Targets);
        Diagnostics.AssertValues("connection disposed", true, run.Connection.IsDisposed);
        Assert.IsTrue(run.Connection.IsDisposed);
        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.UrlMalformat, "URL using bad/illegal format or missing URL"), run.Result);
        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.UrlMalformat, "URL using bad/illegal format or missing URL"),
            run.Result);
    }

    private static ScriptedConnection Scripted() => new(Encoding.Latin1.GetBytes(Replies));
}
