using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smtp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Pins how an SMTP upload is sent against curl 8.21.0: <c>MAIL FROM</c>, <c>RCPT TO</c>,
/// <c>DATA</c>, the dot-stuffed message and its end-of-data mark, and the exit code, message,
/// <c>%{size_upload}</c> and <c>%{response_code}</c> of every refusal. Every case was recorded
/// from real curl (the Schannel build) on 2026-09-28 with <c>Record-CurlExchange.ps1 -Smtp</c>,
/// curl running
/// <c>-sS -w [%{size_upload}|%{response_code}] --mail-from a@b --mail-rcpt c@d -T body smtp://127.0.0.1:port/dom</c>
/// (BL-542 Notes). The recorder advertises <c>SIZE</c>, so curl added <c>SIZE=n</c> to
/// <c>MAIL</c>; that parameter is BL-544's, and these tests answer <c>EHLO</c> without it.
/// </summary>
[TestClass]
public sealed class SmtpProtocolHandlerUploadTests
{
    private const string Url = "smtp://127.0.0.1:18025/dom";

    /// <summary>Gets or sets the running test's context, which MSTest sets.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private const string Greeting = "220 localhost ESMTP\r\n";

    private const string EhloReply = "250-localhost\r\n250 8BITMIME\r\n";

    private const string Ok = "250 OK\r\n";

    private const string StartData = "354 End data with <CR><LF>.<CR><LF>\r\n";

    private const string Bye = "221 Bye\r\n";

    private const string Envelope = "EHLO dom\r\nMAIL FROM:<a@b>\r\nRCPT TO:<c@d>\r\nDATA\r\n";

    private const string Quit = "QUIT\r\n";

    /// <summary>Every reply of a session that accepts the message.</summary>
    private const string Accepting = Greeting + EhloReply + Ok + Ok + StartData + Ok + Bye;

    [TestMethod]
    [DataRow("one\r\n", "one\r\n.\r\n", DisplayName = "Ends in CRLF")]
    [DataRow("Subject: t\r\n\r\n.hidden\r\nline\r\n..two\r\n.\r\nend\r\n", "Subject: t\r\n\r\n..hidden\r\nline\r\n...two\r\n..\r\nend\r\n.\r\n", DisplayName = "Lines starting with a dot")]
    [DataRow("a\nb\n\n.c\nd\n", "a\nb\n\n.c\nd\n\r\n.\r\n", DisplayName = "Bare LF endings")]
    [DataRow("abc", "abc\r\n.\r\n", DisplayName = "No line ending at the end")]
    [DataRow("x\r\n.", "x\r\n..\r\n.\r\n", DisplayName = "Ends in a lone dot")]
    [DataRow("a\rb\r\n.\rc", "a\rb\r\n..\rc\r\n.\r\n", DisplayName = "Bare CR")]
    [DataRow("", ".\r\n", DisplayName = "Empty")]
    [DataRow(".x\r\n", "..x\r\n.\r\n", DisplayName = "Starts with a dot")]
    [DataRow(".", "..\r\n.\r\n", DisplayName = "Only a dot")]
    [DataRow("a\r", "a\r\r\n.\r\n", DisplayName = "Ends in CR")]
    [DataRow("a\r\n.\r\nb", "a\r\n..\r\nb\r\n.\r\n", DisplayName = "Holds an end-of-data mark")]
    [DataRow("\r\n", "\r\n.\r\n", DisplayName = "Only CRLF")]
    [DataRow("a\r\r\n.b\n\r\n.c", "a\r\r\n..b\n\r\n..c\r\n.\r\n", DisplayName = "CR before CRLF")]
    public async Task ExecuteAsync_Upload_SendsTheMessageAsCurlDoes(string body, string sentMessage)
    {
        var progress = new RecordingProgress();
        Diagnostics.Bytes("message body sent", Encoding.Latin1.GetBytes(body));
        SmtpRun run = await RunAsync(Accepting, new MemoryStream(Encoding.Latin1.GetBytes(body)), progress: progress);

        Diagnostics.Diff("sent", Envelope + sentMessage + Quit, run.Sent);
        Assert.AreEqual(Envelope + sentMessage + Quit, run.Sent);
        AssertResult(run.Result, CurlExitCode.Ok, null, 250, sentMessage.Length);
        Diagnostics.AssertValues("last progress", ((long)sentMessage.Length, (long?)body.Length), progress.Uploaded[^1]);
        Assert.AreEqual(((long)sentMessage.Length, (long?)body.Length), progress.Uploaded[^1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadFromStandardInput_ReportsProgressWithoutATotal()
    {
        // -T - with "hi\n.x\n" on standard input: size_upload 11, the meter's Total the bytes sent.
        var progress = new RecordingProgress();
        Diagnostics.Bytes("message body sent", "hi\n.x\n"u8);
        SmtpRun run = await RunAsync(Accepting, new NonSeekableStream("hi\n.x\n"u8.ToArray()), progress: progress);

        Diagnostics.Diff("sent", Envelope + "hi\n.x\n\r\n.\r\n" + Quit, run.Sent);
        Assert.AreEqual(Envelope + "hi\n.x\n\r\n.\r\n" + Quit, run.Sent);
        AssertResult(run.Result, CurlExitCode.Ok, null, 250, 11);
        Diagnostics.AssertValues("progress count", 2, progress.Uploaded.Count);
        // The body and the end-of-data mark go out as two sends, as curl sends an upload of unknown size (BL-1198).
        CollectionAssert.AreEqual(new (long, long?)[] { (6, null), (11, null) }, progress.Uploaded);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadFromPartReadStream_CountsOnlyWhatIsLeft()
    {
        var upload = new MemoryStream("skip:one\r\n"u8.ToArray()) { Position = 5 };
        var progress = new RecordingProgress();

        Diagnostics.Arrange("upload position", upload.Position);
        Diagnostics.Bytes("message body sent", "one\r\n"u8);
        await RunAsync(Accepting, upload, progress: progress);

        Diagnostics.AssertValues("last progress", (8L, (long?)5), progress.Uploaded[^1]);
        Assert.AreEqual((8L, (long?)5), progress.Uploaded[^1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_TwoRecipients_SendsRcptForEachInOrder()
    {
        SmtpRun run = await RunAsync(
            Greeting + EhloReply + Ok + Ok + Ok + StartData + Ok + Bye,
            Body("one\r\n"),
            new MailRequestOptions { From = "a@b", Recipients = ["c@d", "e@f"] });

        Diagnostics.Diff("sent", "EHLO dom\r\nMAIL FROM:<a@b>\r\nRCPT TO:<c@d>\r\nRCPT TO:<e@f>\r\nDATA\r\none\r\n.\r\n" + Quit, run.Sent);
        Assert.AreEqual("EHLO dom\r\nMAIL FROM:<a@b>\r\nRCPT TO:<c@d>\r\nRCPT TO:<e@f>\r\nDATA\r\none\r\n.\r\n" + Quit, run.Sent);
        AssertResult(run.Result, CurlExitCode.Ok, null, 250, 8);
    }

    [TestMethod]
    [DataRow(null, "c@d", "MAIL FROM:<>\r\nRCPT TO:<c@d>\r\n", DisplayName = "No --mail-from")]
    [DataRow("<a@b>", "<c@d>", "MAIL FROM:<a@b>\r\nRCPT TO:<c@d>\r\n", DisplayName = "Already bracketed")]
    [DataRow("<a@b", "c@d>", "MAIL FROM:<a@b>\r\nRCPT TO:<c@d>\r\n", DisplayName = "Half bracketed")]
    [DataRow("<<a@b>>", "c@d", "MAIL FROM:<<a@b>>\r\nRCPT TO:<c@d>\r\n", DisplayName = "Doubly bracketed")]
    [DataRow("alice", "bob", "MAIL FROM:<alice>\r\nRCPT TO:<bob>\r\n", DisplayName = "No @")]
    [DataRow("<s@example.com> RET=HDRS", "<r@example.com> NOTIFY=SUCCESS", "MAIL FROM:<s@example.com> RET=HDRS\r\nRCPT TO:<r@example.com> NOTIFY=SUCCESS\r\n", DisplayName = "Bracketed with DSN parameters: sent as given (upstream test3215)")]
    [DataRow("<nohost> A=1", "<r@b> x>y", "MAIL FROM:<nohost> A=1\r\nRCPT TO:<r@b> x>y\r\n", DisplayName = "Bracketed with a suffix: no @, and a > in the suffix")]
    [DataRow("a@b", "r@example.com> X", "MAIL FROM:<a@b>\r\nRCPT TO:<r@example.com> X>\r\n", DisplayName = "Not starting with <: no suffix, bracketed whole")]
    public async Task ExecuteAsync_Addresses_AreBracketedAsCurlDoes(string? from, string recipient, string envelope)
    {
        SmtpRun run = await RunAsync(Accepting, Body("one\r\n"), new MailRequestOptions { From = from, Recipients = [recipient] });

        Diagnostics.Diff("sent", "EHLO dom\r\n" + envelope + "DATA\r\none\r\n.\r\n" + Quit, run.Sent);
        Assert.AreEqual("EHLO dom\r\n" + envelope + "DATA\r\none\r\n.\r\n" + Quit, run.Sent);
    }

    [TestMethod]
    [DataRow("251 ok\r\n", Ok, DisplayName = "MAIL answered 251")]
    [DataRow(Ok, "251 forward\r\n", DisplayName = "RCPT answered 251")]
    public async Task ExecuteAsync_EnvelopeAnsweredWithAnother2xx_IsAccepted(string mailReply, string rcptReply)
    {
        SmtpRun run = await RunAsync(Greeting + EhloReply + mailReply + rcptReply + StartData + Ok + Bye, Body("one\r\n"));

        AssertResult(run.Result, CurlExitCode.Ok, null, 250, 8);
    }

    [TestMethod]
    public async Task ExecuteAsync_MailRefused_IsExit55AndQuits()
    {
        SmtpRun run = await RunAsync(Greeting + EhloReply + "550 bad sender\r\n" + Bye, Body("one\r\n"));

        Diagnostics.Diff("sent", "EHLO dom\r\nMAIL FROM:<a@b>\r\n" + Quit, run.Sent);
        Assert.AreEqual("EHLO dom\r\nMAIL FROM:<a@b>\r\n" + Quit, run.Sent);
        AssertResult(run.Result, CurlExitCode.SendError, "MAIL failed: 550", 550, 0);
    }

    [TestMethod]
    [DataRow("550 no such user\r\n", "RCPT failed: 550", 550)]
    [DataRow("450 later\r\n", "RCPT failed: 450", 450)]
    public async Task ExecuteAsync_OnlyRecipientRefused_IsExit55AndQuits(string rcptReply, string message, int code)
    {
        SmtpRun run = await RunAsync(Greeting + EhloReply + Ok + rcptReply + Bye, Body("one\r\n"));

        Diagnostics.Diff("sent", "EHLO dom\r\nMAIL FROM:<a@b>\r\nRCPT TO:<c@d>\r\n" + Quit, run.Sent);
        Assert.AreEqual("EHLO dom\r\nMAIL FROM:<a@b>\r\nRCPT TO:<c@d>\r\n" + Quit, run.Sent);
        AssertResult(run.Result, CurlExitCode.SendError, message, code, 0);
    }

    [TestMethod]
    public async Task ExecuteAsync_SecondRecipientRefused_IsExit55AndQuits()
    {
        SmtpRun run = await RunAsync(
            Greeting + EhloReply + Ok + "250 ok\r\n" + "550 no\r\n" + Bye,
            Body("one\r\n"),
            new MailRequestOptions { From = "a@b", Recipients = ["c@d", "e@f"] });

        Diagnostics.Diff("sent", "EHLO dom\r\nMAIL FROM:<a@b>\r\nRCPT TO:<c@d>\r\nRCPT TO:<e@f>\r\n" + Quit, run.Sent);
        Assert.AreEqual("EHLO dom\r\nMAIL FROM:<a@b>\r\nRCPT TO:<c@d>\r\nRCPT TO:<e@f>\r\n" + Quit, run.Sent);
        AssertResult(run.Result, CurlExitCode.SendError, "RCPT failed: 550", 550, 0);
    }

    [TestMethod]
    [DataRow("554 no data\r\n", "DATA failed: 554", 554)]
    [DataRow("350 hmm\r\n", "DATA failed: 350", 350)]
    public async Task ExecuteAsync_DataAnsweredOtherThan354_IsExit55AndQuits(string dataReply, string message, int code)
    {
        SmtpRun run = await RunAsync(Greeting + EhloReply + Ok + Ok + dataReply + Bye, Body("one\r\n"));

        Diagnostics.Diff("sent", Envelope + Quit, run.Sent);
        Assert.AreEqual(Envelope + Quit, run.Sent);
        AssertResult(run.Result, CurlExitCode.SendError, message, code, 0);
    }

    [TestMethod]
    [DataRow("552 too big\r\n", 552)]
    [DataRow("251 ok\r\n", 251)]
    public async Task ExecuteAsync_MessageAnsweredOtherThan250_IsExit8AndQuits(string doneReply, int code)
    {
        SmtpRun run = await RunAsync(Greeting + EhloReply + Ok + Ok + StartData + doneReply + Bye, Body("one\r\n"));

        Diagnostics.Diff("sent", Envelope + "one\r\n.\r\n" + Quit, run.Sent);
        Assert.AreEqual(Envelope + "one\r\n.\r\n" + Quit, run.Sent);
        AssertResult(run.Result, CurlExitCode.WeirdServerReply, "Weird server reply", code, 8);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerClosesInsteadOfAnsweringMail_IsExit56WithTheEhloCodeAndNoQuit()
    {
        SmtpRun run = await RunAsync(Greeting + EhloReply, Body("one\r\n"));

        Diagnostics.Diff("sent", "EHLO dom\r\nMAIL FROM:<a@b>\r\n", run.Sent);
        Assert.AreEqual("EHLO dom\r\nMAIL FROM:<a@b>\r\n", run.Sent);
        AssertResult(run.Result, CurlExitCode.RecvError, "response reading failed (errno: 0)", 250, 0);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerClosesInsteadOfAnsweringTheMessage_IsExit56WithCode0AndNoQuit()
    {
        SmtpRun run = await RunAsync(Greeting + EhloReply + Ok + Ok + StartData, Body("one\r\n"));

        Diagnostics.Diff("sent", Envelope + "one\r\n.\r\n", run.Sent);
        Assert.AreEqual(Envelope + "one\r\n.\r\n", run.Sent);
        AssertResult(run.Result, CurlExitCode.RecvError, "response reading failed (errno: 0)", 0, 8);
    }

    [TestMethod]
    public async Task ExecuteAsync_OverlongReplyToMail_IsExit100AndNoQuit()
    {
        SmtpRun run = await RunAsync(Greeting + EhloReply + "250 " + new string('x', 70000) + "\r\n", Body("one\r\n"));

        Diagnostics.Diff("sent", "EHLO dom\r\nMAIL FROM:<a@b>\r\n", run.Sent);
        Assert.AreEqual("EHLO dom\r\nMAIL FROM:<a@b>\r\n", run.Sent);
        AssertResult(run.Result, CurlExitCode.TooLarge, "A value or data field grew larger than allowed", 250, 0);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadWithoutRecipient_SendsHelpInsteadOfMail()
    {
        // Measured (BL-543): -T mail.txt smtp://127.0.0.1:18125/ with no --mail-rcpt sends HELP.
        SmtpRun run = await RunAsync(Greeting + EhloReply + SmtpRun.HelpReply + Bye, Body("one\r\n"), new MailRequestOptions { From = "a@b" });

        Diagnostics.Diff("sent", "EHLO dom\r\nHELP\r\n" + Quit, run.Sent);
        Assert.AreEqual("EHLO dom\r\nHELP\r\n" + Quit, run.Sent);
        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadWithoutMailOptions_SendsHelpInsteadOfMail()
    {
        var context = new TransferContext { Url = CurlUrl.Parse(Url), Output = Stream.Null, Upload = Body("one\r\n") };

        SmtpRun run = await SmtpRun.ExecuteAsync(
            Diagnostics, context, new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting + EhloReply + SmtpRun.HelpReply + Bye)));

        Diagnostics.Diff("sent", "EHLO dom\r\nHELP\r\n" + Quit, run.Sent);
        Assert.AreEqual("EHLO dom\r\nHELP\r\n" + Quit, run.Sent);
        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_RecipientWithoutUpload_SendsVrfyInsteadOfMail()
    {
        SmtpRun run = await RunAsync(Greeting + EhloReply + Ok + Bye, null);

        Diagnostics.Diff("sent", "EHLO dom\r\nVRFY c@d\r\n" + Quit, run.Sent);
        Assert.AreEqual("EHLO dom\r\nVRFY c@d\r\n" + Quit, run.Sent);
        Diagnostics.AssertValues("exit code", CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
    }

    private static MemoryStream Body(string text) => new(Encoding.Latin1.GetBytes(text));

    private void AssertResult(TransferResult result, CurlExitCode exitCode, string? message, int responseCode, long uploadSize)
    {
        Diagnostics.AssertValues("exit code", exitCode, result.ExitCode);
        Diagnostics.AssertValues("error message", message, result.ErrorMessage);
        Diagnostics.AssertValues("bytes transferred", uploadSize, result.BytesTransferred);
        Diagnostics.AssertValues("response code", responseCode, result.Report?.ResponseCode);
        Diagnostics.AssertValues("upload size", uploadSize, result.Report?.UploadSize);
        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
        Assert.AreEqual(uploadSize, result.BytesTransferred);
        Assert.AreEqual(responseCode, result.Report!.ResponseCode);
        Assert.AreEqual(uploadSize, result.Report.UploadSize);
    }

    private Task<SmtpRun> RunAsync(
        string replies,
        Stream? upload,
        MailRequestOptions? mail = null,
        RecordingProgress? progress = null)
    {
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = Stream.Null,
            Upload = upload,
            Mail = mail ?? new MailRequestOptions { From = "a@b", Recipients = ["c@d"] },
            Progress = progress ?? new RecordingProgress(),
        };
        return SmtpRun.ExecuteAsync(Diagnostics, context, new ScriptedConnection(Encoding.Latin1.GetBytes(replies)));
    }
}
