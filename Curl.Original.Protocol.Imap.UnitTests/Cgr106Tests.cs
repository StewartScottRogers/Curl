using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Imap.Fakes;

namespace Curl.Original.Protocol.Imap;

/// <summary>
/// Pins CGR-106: an upload with <c>--crlf</c> is refused before <c>APPEND</c>, because the
/// conversion makes the literal's size unknown. Measured with curl 8.22.0 (Schannel) and
/// <c>Record-CurlExchange.ps1 -Imap</c> on 2026-10-26: <c>-s -S -u u:p --crlf -T msg.txt</c>
/// to <c>imap://127.0.0.1/INBOX</c>, to <c>imap://127.0.0.1/</c> and with <c>-T -</c> each
/// logged in, sent <c>LOGOUT</c> and exited 25 with
/// <c>curl: (25) Cannot APPEND with CRLF conversion making size unknown</c>, so the check comes
/// before the missing-mailbox and unknown-size ones.
/// </summary>
[TestClass]
public sealed class Cgr106Tests
{
    private const string Replies = "* OK ready\r\n* CAPABILITY IMAP4rev1\r\nA001 OK done\r\n"
        + "* BYE Logging out\r\nA002 OK LOGOUT completed\r\n";

    private const string CrlfRefused = "Cannot APPEND with CRLF conversion making size unknown";

    [TestMethod]
    [DataRow("INBOX", true, DisplayName = "imap://h/INBOX -T msg.txt (measured)")]
    [DataRow("", true, DisplayName = "imap://h/ -T msg.txt (measured)")]
    [DataRow("INBOX", false, DisplayName = "imap://h/INBOX -T - (measured)")]
    public async Task ExecuteAsync_UploadWithCrlf_LogsOutAndFailsWithExit25(string mailbox, bool seekable)
    {
        byte[] message = Encoding.Latin1.GetBytes("Subject: hi\n\nline\n");
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("imap://127.0.0.1:18143/" + mailbox),
            Output = new MemoryStream(),
            Upload = seekable ? new MemoryStream(message) : new UnseekableStream(message),
            Mail = new MailRequestOptions { UploadFlags = ["seen"] },
            ConvertLineEndings = true,
        };

        ImapRun run = await ImapRun.ExecuteAsync(context, new ScriptedConnection(Encoding.Latin1.GetBytes(Replies)));

        Assert.AreEqual("A001 CAPABILITY\r\nA002 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UploadFailed, CrlfRefused), run.Result);
    }
}
