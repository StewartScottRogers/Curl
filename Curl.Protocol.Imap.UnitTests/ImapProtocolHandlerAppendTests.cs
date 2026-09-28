using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Imap.Fakes;

namespace Curl.Protocol.Imap;

/// <summary>
/// Pins how a <c>-T</c> upload is appended to an IMAP mailbox against curl 8.21.0: the
/// <c>APPEND</c> line with its <c>--upload-flags</c>, the message bytes and the CRLF after
/// them, the progress and <c>%{size_upload}</c> reported, and the exit code and message of
/// every failure. Every case marked measured was recorded from real curl (the Schannel build)
/// on 2026-09-28 with <c>Record-CurlExchange.ps1 -Imap</c>, curl running
/// <c>-sS -u u:p -T mail.txt</c> with a 21-byte message against the recorder's default
/// replies unless an <c>-ImapReply</c> is named (BL-557 Notes). The login is left out here:
/// with no user, the command after <c>CAPABILITY</c> is the one curl sent after logging in.
/// </summary>
[TestClass]
public sealed class ImapProtocolHandlerAppendTests
{
    private const string Host = "imap://127.0.0.1:18143/";

    private const string Opening = "* OK [CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN] ready\r\n"
        + "* CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN\r\nA001 OK CAPABILITY completed\r\n";

    private const string Message = "Subject: hi\r\n\r\nbody\r\n";

    private const string Continuation = "+ Ready for literal data\r\n";

    private const string Appended = "A002 OK APPEND completed\r\n";

    private const string LogoutReply = "* BYE Logging out\r\nA003 OK LOGOUT completed\r\n";

    private const string UploadFailed = "Upload failed (at start/before it took off)";

    [TestMethod]
    [DataRow(new[] { "seen" }, " (\\Seen)", DisplayName = "no --upload-flags, curl's default seen (measured)")]
    [DataRow(new[] { "flagged", "seen" }, " (\\Flagged \\Seen)", DisplayName = "--upload-flags seen,flagged (measured)")]
    [DataRow(new[] { "draft" }, " (\\Draft)", DisplayName = "--upload-flags draft,-seen (measured)")]
    [DataRow(new[] { "answered", "deleted", "draft", "flagged", "seen" }, " (\\Answered \\Deleted \\Draft \\Flagged \\Seen)", DisplayName = "every flag (measured)")]
    [DataRow(new string[0], "", DisplayName = "--upload-flags -seen, no flag and no parentheses (measured)")]
    public async Task ExecuteAsync_Upload_AppendsWithTheFlagsAndLogsOut(string[] flags, string flagList)
    {
        AppendRun run = await RunAsync(Context(Host + "INBOX", flags), Opening + Continuation + Appended + LogoutReply);

        Assert.AreEqual($"A001 CAPABILITY\r\nA002 APPEND INBOX{flagList} {{21}}\r\n{Message}\r\nA003 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual(21, run.Result.BytesTransferred);
        Assert.AreEqual(21, run.Result.Report!.UploadSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadWithoutMailOptions_AppendsWithoutFlags()
    {
        TransferContext context = Context(Host + "INBOX", withMail: false);

        AppendRun run = await RunAsync(context, Opening + Continuation + Appended + LogoutReply);

        StringAssert.StartsWith(run.Sent, "A001 CAPABILITY\r\nA002 APPEND INBOX {21}\r\n", StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("My%20Box", "\"My Box\"", DisplayName = "My%20Box quoted (measured)")]
    [DataRow("INBOX;UID=1", "INBOX", DisplayName = "INBOX;UID=1, the UID ignored (measured)")]
    [DataRow("Sent/", "Sent", DisplayName = "a trailing slash dropped")]
    public async Task ExecuteAsync_Upload_AppendsToTheMailboxAsWritten(string path, string mailbox)
    {
        AppendRun run = await RunAsync(Context(Host + path), Opening + Continuation + Appended + LogoutReply);

        StringAssert.StartsWith(run.Sent, $"A001 CAPABILITY\r\nA002 APPEND {mailbox} (\\Seen) {{21}}\r\n", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadWithACustomCommand_StillAppends()
    {
        TransferContext context = Context(Host + "INBOX", customCommand: "NOOP");

        AppendRun run = await RunAsync(context, Opening + Continuation + Appended + LogoutReply);

        StringAssert.StartsWith(run.Sent, "A001 CAPABILITY\r\nA002 APPEND INBOX (\\Seen) {21}\r\n", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task ExecuteAsync_UntaggedLinesDuringTheAppend_AreSkipped()
    {
        string replies = Opening + "* OK noise\r\n" + Continuation + "* 3 EXISTS\r\n" + Appended + LogoutReply;

        AppendRun run = await RunAsync(Context(Host + "INBOX"), replies);

        Assert.AreEqual($"A001 CAPABILITY\r\nA002 APPEND INBOX (\\Seen) {{21}}\r\n{Message}\r\nA003 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_ReportsTheUploadToTheProgressMeter()
    {
        AppendRun run = await RunAsync(Context(Host + "INBOX"), Opening + Continuation + Appended + LogoutReply);

        CollectionAssert.AreEqual(new[] { "started", "up 21/21" }, run.Progress.Reports);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadFromPartWayThrough_AppendsWhatIsLeft()
    {
        var upload = new MemoryStream(Encoding.Latin1.GetBytes("XX" + Message)) { Position = 2 };

        AppendRun run = await RunAsync(Context(Host + "INBOX", upload: upload), Opening + Continuation + Appended + LogoutReply);

        Assert.AreEqual($"A001 CAPABILITY\r\nA002 APPEND INBOX (\\Seen) {{21}}\r\n{Message}\r\nA003 LOGOUT\r\n", run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyUpload_AppendsAZeroLengthLiteral()
    {
        // Measured: an empty file sends {0}, and after the continuation only the CRLF.
        AppendRun run = await RunAsync(Context(Host + "INBOX", upload: new MemoryStream()), Opening + Continuation + Appended + LogoutReply);

        Assert.AreEqual("A001 CAPABILITY\r\nA002 APPEND INBOX (\\Seen) {0}\r\n\r\nA003 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual(0, run.Result.Report!.UploadSize);
        CollectionAssert.AreEqual(new[] { "started" }, run.Progress.Reports);
    }

    [TestMethod]
    [DataRow("", DisplayName = "imap://h/ (the command line appends the file name first)")]
    [DataRow(";UID=1", DisplayName = "imap://h/;UID=1 (measured)")]
    public async Task ExecuteAsync_UploadWithoutMailbox_FailsWithExit3AfterLogout(string path)
    {
        AppendRun run = await RunAsync(Context(Host + path), Opening + "* BYE Logging out\r\nA002 OK LOGOUT completed\r\n");

        Assert.AreEqual("A001 CAPABILITY\r\nA002 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UrlMalformat, "Cannot APPEND without a mailbox."), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadOfUnknownSize_FailsWithExit25AfterLogout()
    {
        // Measured: -T - sends no APPEND at all.
        var context = Context(Host + "INBOX", upload: new UnseekableStream(Encoding.Latin1.GetBytes(Message)));

        AppendRun run = await RunAsync(context, Opening + "* BYE Logging out\r\nA002 OK LOGOUT completed\r\n");

        Assert.AreEqual("A001 CAPABILITY\r\nA002 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UploadFailed, "Cannot APPEND with unknown input file size"), run.Result);
    }

    [TestMethod]
    [DataRow("A002 NO [TRYCREATE] no such mailbox\r\n", DisplayName = "APPEND=NO [TRYCREATE] no such mailbox (measured)")]
    [DataRow("A002 BAD what\r\n", DisplayName = "BAD")]
    public async Task ExecuteAsync_AppendRefusedAfterTheMessage_FailsWithExit25AfterLogout(string completion)
    {
        AppendRun run = await RunAsync(Context(Host + "INBOX"), Opening + Continuation + completion + LogoutReply);

        Assert.AreEqual($"A001 CAPABILITY\r\nA002 APPEND INBOX (\\Seen) {{21}}\r\n{Message}\r\nA003 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(CurlExitCode.UploadFailed, run.Result.ExitCode);
        Assert.AreEqual(UploadFailed, run.Result.ErrorMessage);
        Assert.AreEqual(21, run.Result.BytesTransferred);
        Assert.AreEqual(21, run.Result.Report!.UploadSize);
    }

    [TestMethod]
    [DataRow("A002 NO [TRYCREATE] no such mailbox\r\n", DisplayName = "NO")]
    [DataRow("A002 OK already\r\n", DisplayName = "even OK")]
    public async Task ExecuteAsync_AppendAnsweredWithoutAContinuation_FailsWithExit25WithoutSendingTheMessage(string answer)
    {
        AppendRun run = await RunAsync(Context(Host + "INBOX"), Opening + answer + LogoutReply);

        Assert.AreEqual("A001 CAPABILITY\r\nA002 APPEND INBOX (\\Seen) {21}\r\nA003 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UploadFailed, UploadFailed), run.Result);
        Assert.IsEmpty(run.Progress.Reports);
    }

    [TestMethod]
    [DataRow("", DisplayName = "before the continuation")]
    [DataRow(Continuation, DisplayName = "before the completion")]
    public async Task ExecuteAsync_ServerClosesDuringTheAppend_FailsWithExit56WithoutLogout(string replies)
    {
        AppendRun run = await RunAsync(Context(Host + "INBOX"), Opening + replies);

        Assert.IsFalse(run.Sent.Contains("LOGOUT", StringComparison.Ordinal));
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RecvError, "response reading failed (errno: 0)"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ContinuationAfterTheMessage_FailsWithExit8()
    {
        AppendRun run = await RunAsync(Context(Host + "INBOX"), Opening + Continuation + Continuation);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.WeirdServerReply, "Unexpected continuation response"), run.Result);
    }

    private static TransferContext Context(
        string url, string[]? flags = null, Stream? upload = null, bool withMail = true, string? customCommand = null) =>
        new()
        {
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
            Upload = upload ?? new MemoryStream(Encoding.Latin1.GetBytes(Message)),
            Mail = withMail ? new MailRequestOptions { UploadFlags = flags ?? ["seen"], CustomCommand = customCommand } : null,
            Progress = new RecordingTransferProgress(),
        };

    private static async Task<AppendRun> RunAsync(TransferContext context, string replies)
    {
        ImapRun run = await ImapRun.ExecuteAsync(context, new ScriptedConnection(Encoding.Latin1.GetBytes(replies)));
        return new AppendRun(run.Result, run.Sent, (RecordingTransferProgress)context.Progress);
    }

    /// <summary>What an upload did: its result, the bytes sent and the progress reported.</summary>
    private sealed record AppendRun(TransferResult Result, string Sent, RecordingTransferProgress Progress);
}
