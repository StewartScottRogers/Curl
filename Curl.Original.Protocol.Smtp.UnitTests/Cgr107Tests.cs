using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Smtp.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Smtp;

/// <summary>
/// Pins CGR-107's SMTP part (BL-3958): curl 8.22.0's <c>%{size_request}</c> for an SMTP upload
/// is the message bytes sent after <c>DATA</c>, dot-stuffed and with the end-of-data mark, the
/// same as <c>%{size_upload}</c>; no command line counts, so a session without an upload reports
/// 0. Recorded from real curl (the Schannel build) on 2026-10-28 with
/// <c>Record-CurlExchange.ps1 -Smtp</c>, curl running
/// <c>-sS -w "%{size_request} %{size_upload}" --mail-from a@b --mail-rcpt c@d -T body smtp://127.0.0.1:port/dom</c>:
/// a 21-byte body ending in CRLF printed <c>24 24</c>, <c>a\r\n.b</c> printed <c>11 11</c>, and
/// <c>--mail-rcpt c@d</c> without <c>-T</c> (a <c>VRFY</c>) printed <c>0 0</c>.
/// </summary>
[TestClass]
public sealed class Cgr107Tests
{
    private const string Url = "smtp://127.0.0.1:18025/dom";

    private const string Greeting = "220 localhost ESMTP\r\n";

    private const string EhloReply = "250-localhost\r\n250 8BITMIME\r\n";

    private const string Ok = "250 OK\r\n";

    private const string StartData = "354 End data with <CR><LF>.<CR><LF>\r\n";

    private const string Bye = "221 Bye\r\n";

    /// <summary>Gets or sets the running test's context, which MSTest sets.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("Subject: x\r\n\r\nhello\r\n", 24L, DisplayName = "Ends in CRLF")]
    [DataRow("a\r\n.b", 11L, DisplayName = "Dot-stuffed, no line ending at the end")]
    public async Task ExecuteAsync_Upload_ReportsTheMessageBytesSentAsTheRequestSize(string body, long requestSize)
    {
        SmtpRun run = await RunAsync(
            Greeting + EhloReply + Ok + Ok + StartData + Ok + Bye,
            new MemoryStream(Encoding.Latin1.GetBytes(body)),
            new MailRequestOptions { From = "a@b", Recipients = ["c@d"] });

        Diagnostics.AssertValues("size_request", requestSize, run.Result.Report?.RequestSize);
        Assert.AreEqual(requestSize, run.Result.Report?.RequestSize);
        Assert.AreEqual(run.Result.Report!.UploadSize, run.Result.Report.RequestSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_RecipientWithoutUpload_ReportsNoRequestSize()
    {
        SmtpRun run = await RunAsync(
            Greeting + EhloReply + Ok + Bye, null, new MailRequestOptions { Recipients = ["c@d"] });

        Diagnostics.AssertValues("size_request", 0L, run.Result.Report?.RequestSize ?? 0);
        Assert.AreEqual(0L, run.Result.Report?.RequestSize ?? 0);
    }

    private Task<SmtpRun> RunAsync(string replies, Stream? upload, MailRequestOptions mail)
    {
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = Stream.Null,
            Upload = upload,
            Mail = mail,
            Progress = new RecordingProgress(),
        };
        return SmtpRun.ExecuteAsync(Diagnostics, context, new ScriptedConnection(Encoding.Latin1.GetBytes(replies)));
    }
}
