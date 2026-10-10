using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smtp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Pins what an SMTP session with no message to send does against curl 8.21.0: <c>VRFY</c>
/// for each <c>--mail-rcpt</c>, the <c>-X</c> command for each <c>--mail-rcpt</c> or alone,
/// <c>HELP</c> otherwise, the reply bytes written to the output, and the exit code, message,
/// <c>%{size_download}</c> and <c>%{response_code}</c> of every refusal. Every case was
/// recorded from real curl (the Schannel build) on 2026-09-28 with
/// <c>Record-CurlExchange.ps1 -Smtp</c>, curl running
/// <c>-sS -w [%{response_code} %{size_download} %{size_upload}] ... smtp://127.0.0.1:18125/</c>
/// (BL-543 Notes). curl named its machine in <c>EHLO</c>; these tests use the path <c>dom</c>.
/// </summary>
[TestClass]
public sealed class SmtpProtocolHandlerCommandTests
{
    private const string Url = "smtp://127.0.0.1:18125/dom";

    private const string Greeting = "220 localhost ESMTP\r\n";

    /// <summary>The recorder's <c>EHLO</c> reply, which advertises <c>SMTPUTF8</c>.</summary>
    private const string EhloReply =
        "250-localhost\r\n250-AUTH PLAIN LOGIN CRAM-MD5\r\n250-STARTTLS\r\n250-SIZE 1000000\r\n250-8BITMIME\r\n250 SMTPUTF8\r\n";

    private const string Verified = "250 Recorder <recorder@localhost>\r\n";

    /// <summary>The 35-byte <c>VRFY</c> reply the <c>--max-filesize</c> cases were recorded with (BL-1386).</summary>
    private const string LongReply = "250 a-reply-longer-than-ten-bytes\r\n";

    private const string Bye = "221 Bye\r\n";

    private const string Ehlo = "EHLO dom\r\n";

    private const string Quit = "QUIT\r\n";

    private const string CommandFailed550 = "Command failed: 550";

    /// <summary>Gets or sets the running test's context, which MSTest sets.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_OneRecipient_SendsVrfyAndWritesTheReply()
    {
        // --mail-rcpt a@b: 250 Recorder <recorder@localhost>, exit 0.
        CommandRun run = await RunAsync(EhloReply + Verified + Bye, new MailRequestOptions { Recipients = ["a@b"] });

        Diagnostics.Diff("sent", Ehlo + "VRFY a@b\r\n" + Quit, run.Sent);
        Assert.AreEqual(Ehlo + "VRFY a@b\r\n" + Quit, run.Sent);
        Diagnostics.Diff("output", Verified, run.Output);
        Assert.AreEqual(Verified, run.Output);
        AssertResult(run.Result, CurlExitCode.Ok, null, 250, 35);
        Diagnostics.AssertValues("downloaded progress", "35,(null)", string.Join(";", run.Progress.Downloaded.Select(item => $"{item.Item1},{item.Item2?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(null)"}")));
        CollectionAssert.AreEqual(new[] { (35L, (long?)null) }, run.Progress.Downloaded);
    }

    [TestMethod]
    public async Task ExecuteAsync_TwoRecipients_SendsVrfyForEach()
    {
        CommandRun run = await RunAsync(EhloReply + Verified + Verified + Bye, new MailRequestOptions { Recipients = ["a@b", "c@d"] });

        Diagnostics.Diff("sent", Ehlo + "VRFY a@b\r\nVRFY c@d\r\n" + Quit, run.Sent);
        Assert.AreEqual(Ehlo + "VRFY a@b\r\nVRFY c@d\r\n" + Quit, run.Sent);
        Diagnostics.Diff("output", Verified + Verified, run.Output);
        Assert.AreEqual(Verified + Verified, run.Output);
        AssertResult(run.Result, CurlExitCode.Ok, null, 250, 70);
    }

    [TestMethod]
    public async Task ExecuteAsync_RecipientsInBracketsOrWithoutHost_AreVerifiedBare()
    {
        // --mail-rcpt <a@b> --mail-rcpt local: VRFY a@b, then VRFY local.
        CommandRun run = await RunAsync(EhloReply + Verified + Verified + Bye, new MailRequestOptions { Recipients = ["<a@b>", "local"] });

        Diagnostics.Diff("sent", Ehlo + "VRFY a@b\r\nVRFY local\r\n" + Quit, run.Sent);
        Assert.AreEqual(Ehlo + "VRFY a@b\r\nVRFY local\r\n" + Quit, run.Sent);
    }

    [TestMethod]
    [DataRow("<v@example.com> X", "VRFY v@example.com", DisplayName = "the suffix after > dropped")]
    [DataRow("<r@b> x>y", "VRFY r@b> x", DisplayName = "cut at the last >")]
    public async Task ExecuteAsync_BracketedRecipientWithSuffix_IsVerifiedWithoutTheSuffix(string recipient, string expected)
    {
        // Measured on curl 8.21.0 (BL-1993).
        CommandRun run = await RunAsync(EhloReply + Verified + Bye, new MailRequestOptions { Recipients = [recipient] });

        Diagnostics.Diff("sent", Ehlo + expected + "\r\n" + Quit, run.Sent);
        Assert.AreEqual(Ehlo + expected + "\r\n" + Quit, run.Sent);
    }

    [TestMethod]
    [DataRow("jörg@example.com", "VRFY jörg@example.com SMTPUTF8", DisplayName = "non-ASCII local part")]
    [DataRow("a@bücher.example", "VRFY a@xn--bcher-kva.example SMTPUTF8", DisplayName = "non-ASCII host")]
    [DataRow("a@bü..x", "VRFY a@bü..x SMTPUTF8", DisplayName = "host no IDNA A-label exists for")]
    public async Task ExecuteAsync_NonAsciiRecipientWithSmtpUtf8Advertised_SendsTheALabelAndSmtpUtf8(string recipient, string expected)
    {
        // Measured: jörg@example.com and a@bücher.example (curl sent ö as the one byte F6).
        CommandRun run = await RunAsync(EhloReply + Verified + Bye, new MailRequestOptions { Recipients = [recipient] });

        Diagnostics.Diff("sent", Ehlo + expected + "\r\n" + Quit, run.Sent);
        Assert.AreEqual(Ehlo + expected + "\r\n" + Quit, run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_NonAsciiRecipientWithoutSmtpUtf8Advertised_SendsNoSmtpUtf8()
    {
        CommandRun run = await RunAsync("250-localhost\r\n250 8BITMIME\r\n" + Verified + Bye, new MailRequestOptions { Recipients = ["jörg@x"] });

        Diagnostics.Diff("sent", Ehlo + "VRFY jörg@x\r\n" + Quit, run.Sent);
        Assert.AreEqual(Ehlo + "VRFY jörg@x\r\n" + Quit, run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_SmtpUtf8AdvertisedInLowerCase_CountsAsAdvertised()
    {
        // Measured (BL-544): EHLO=250-localhost\r\n250-smtputf8\r\n250 OK, --mail-rcpt jörg@x: VRFY jörg@x SMTPUTF8.
        CommandRun run = await RunAsync("250-localhost\r\n250-smtputf8\r\n250 OK\r\n" + Verified + Bye, new MailRequestOptions { Recipients = ["jörg@x"] });

        Diagnostics.Diff("sent", Ehlo + "VRFY jörg@x SMTPUTF8\r\n" + Quit, run.Sent);
        Assert.AreEqual(Ehlo + "VRFY jörg@x SMTPUTF8\r\n" + Quit, run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ExpnWithSmtpUtf8Advertised_AppendsSmtpUtf8AndWritesEveryLine()
    {
        // -X EXPN --mail-rcpt list, EXPN=250-Alice <a@b>\r\n250 Bob <c@d>.
        const string expansion = "250-Alice <a@b>\r\n250 Bob <c@d>\r\n";

        CommandRun run = await RunAsync(EhloReply + expansion + Bye, new MailRequestOptions { Recipients = ["list"], CustomCommand = "EXPN" });

        Diagnostics.Diff("sent", Ehlo + "EXPN list SMTPUTF8\r\n" + Quit, run.Sent);
        Assert.AreEqual(Ehlo + "EXPN list SMTPUTF8\r\n" + Quit, run.Sent);
        Diagnostics.Diff("output", expansion, run.Output);
        Assert.AreEqual(expansion, run.Output);
        AssertResult(run.Result, CurlExitCode.Ok, null, 250, 32);
    }

    [TestMethod]
    public async Task ExecuteAsync_ExpnOnAHeloSession_SendsNoSmtpUtf8()
    {
        // -X EXPN --mail-rcpt list, EHLO=502 no: HELO, then EXPN list.
        CommandRun run = await RunAsync("502 no\r\n250 localhost\r\n" + Verified + Bye, new MailRequestOptions { Recipients = ["list"], CustomCommand = "EXPN" });

        Diagnostics.Diff("sent", Ehlo + "HELO dom\r\nEXPN list\r\n" + Quit, run.Sent);
        Assert.AreEqual(Ehlo + "HELO dom\r\nEXPN list\r\n" + Quit, run.Sent);
        AssertResult(run.Result, CurlExitCode.Ok, null, 250, 35);
    }

    [TestMethod]
    [DataRow("expn", "list", "expn list", DisplayName = "-X expn: not EXPN in capitals, so no SMTPUTF8")]
    [DataRow("VRFY", "<a@b>", "VRFY <a@b>", DisplayName = "-X VRFY: the recipient as given")]
    [DataRow("NOOP", "a@b", "NOOP a@b", DisplayName = "-X NOOP")]
    public async Task ExecuteAsync_CustomCommandWithRecipient_SendsTheCommandAndTheRecipientAsGiven(string command, string recipient, string expected)
    {
        CommandRun run = await RunAsync(EhloReply + Verified + Bye, new MailRequestOptions { Recipients = [recipient], CustomCommand = command });

        Diagnostics.Diff("sent", Ehlo + expected + "\r\n" + Quit, run.Sent);
        Assert.AreEqual(Ehlo + expected + "\r\n" + Quit, run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_CustomCommandWithTwoRecipients_SendsItForEach()
    {
        // -X NOOP --mail-rcpt a@b --mail-rcpt c@d: [250 16 0].
        CommandRun run = await RunAsync(EhloReply + "250 OK\r\n250 OK\r\n" + Bye, new MailRequestOptions { Recipients = ["a@b", "c@d"], CustomCommand = "NOOP" });

        Diagnostics.Diff("sent", Ehlo + "NOOP a@b\r\nNOOP c@d\r\n" + Quit, run.Sent);
        Assert.AreEqual(Ehlo + "NOOP a@b\r\nNOOP c@d\r\n" + Quit, run.Sent);
        Diagnostics.Diff("output", "250 OK\r\n250 OK\r\n", run.Output);
        Assert.AreEqual("250 OK\r\n250 OK\r\n", run.Output);
        AssertResult(run.Result, CurlExitCode.Ok, null, 250, 16);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoRecipient_SendsHelpAndWritesEveryLine()
    {
        // HELP=214-Commands:\r\n214 HELO EHLO MAIL RCPT DATA.
        const string help = "214-Commands:\r\n214 HELO EHLO MAIL RCPT DATA\r\n";

        CommandRun run = await RunAsync(EhloReply + help + Bye, new MailRequestOptions());

        Diagnostics.Diff("sent", Ehlo + "HELP\r\n" + Quit, run.Sent);
        Assert.AreEqual(Ehlo + "HELP\r\n" + Quit, run.Sent);
        Diagnostics.Diff("output", help, run.Output);
        Assert.AreEqual(help, run.Output);
        AssertResult(run.Result, CurlExitCode.Ok, null, 214, 45);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoMailOptions_SendsHelp()
    {
        CommandRun run = await RunAsync(EhloReply + SmtpRun.HelpReply + Bye, mail: null);

        Diagnostics.Diff("sent", Ehlo + "HELP\r\n" + Quit, run.Sent);
        Assert.AreEqual(Ehlo + "HELP\r\n" + Quit, run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyCustomCommand_SendsHelpOrVrfy()
    {
        CommandRun help = await RunAsync(EhloReply + SmtpRun.HelpReply + Bye, new MailRequestOptions { CustomCommand = string.Empty });
        CommandRun verify = await RunAsync(EhloReply + Verified + Bye, new MailRequestOptions { Recipients = ["a@b"], CustomCommand = string.Empty });

        Diagnostics.Diff("help sent", Ehlo + "HELP\r\n" + Quit, help.Sent);
        Assert.AreEqual(Ehlo + "HELP\r\n" + Quit, help.Sent);
        Diagnostics.Diff("verify sent", Ehlo + "VRFY a@b\r\n" + Quit, verify.Sent);
        Assert.AreEqual(Ehlo + "VRFY a@b\r\n" + Quit, verify.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_CustomCommandWithoutRecipient_SendsItAlone()
    {
        // -X NOOP: 250 OK, exit 0.
        CommandRun run = await RunAsync(EhloReply + "250 OK\r\n" + Bye, new MailRequestOptions { CustomCommand = "NOOP" });

        Diagnostics.Diff("sent", Ehlo + "NOOP\r\n" + Quit, run.Sent);
        Assert.AreEqual(Ehlo + "NOOP\r\n" + Quit, run.Sent);
        Diagnostics.Diff("output", "250 OK\r\n", run.Output);
        Assert.AreEqual("250 OK\r\n", run.Output);
        AssertResult(run.Result, CurlExitCode.Ok, null, 250, 8);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplyLinesEndingInLf_AreWrittenAsTheyCame()
    {
        // -X NOOP, NOOP=250-a\n250 b: stdout 250-a\n250 b\r\n, [250 13 0].
        CommandRun run = await RunAsync(EhloReply + "250-a\n250 b\r\n" + Bye, new MailRequestOptions { CustomCommand = "NOOP" });

        Diagnostics.Diff("output", "250-a\n250 b\r\n", run.Output);
        Assert.AreEqual("250-a\n250 b\r\n", run.Output);
        AssertResult(run.Result, CurlExitCode.Ok, null, 250, 13);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoBody_WritesNothing()
    {
        // -I -X NOOP: nothing on stdout, [250 0 0].
        CommandRun run = await RunAsync(EhloReply + "250 OK\r\n" + Bye, new MailRequestOptions { CustomCommand = "NOOP" }, noBody: true);

        Diagnostics.Diff("output", string.Empty, run.Output);
        Assert.AreEqual(string.Empty, run.Output);
        AssertResult(run.Result, CurlExitCode.Ok, null, 250, 0);
    }

    [TestMethod]
    public async Task ExecuteAsync_VrfyRefused_FailsWithExit8AndStillQuits()
    {
        // VRFY=550 no such user: curl: (8) Command failed: 550, nothing on stdout.
        CommandRun run = await RunAsync(EhloReply + "550 no such user\r\n" + Bye, new MailRequestOptions { Recipients = ["x@y"] });

        Diagnostics.Diff("sent", Ehlo + "VRFY x@y\r\n" + Quit, run.Sent);
        Assert.AreEqual(Ehlo + "VRFY x@y\r\n" + Quit, run.Sent);
        Diagnostics.Diff("output", string.Empty, run.Output);
        Assert.AreEqual(string.Empty, run.Output);
        AssertResult(run.Result, CurlExitCode.WeirdServerReply, CommandFailed550, 550, 0);
    }

    [TestMethod]
    public async Task ExecuteAsync_FirstRecipientRefused_TriesNoOther()
    {
        // --mail-rcpt a@b --mail-rcpt c@d, VRFY=550 no: [550 0 0], exit 8.
        CommandRun run = await RunAsync(EhloReply + "550 no\r\n" + Bye, new MailRequestOptions { Recipients = ["a@b", "c@d"] });

        Diagnostics.Diff("sent", Ehlo + "VRFY a@b\r\n" + Quit, run.Sent);
        Assert.AreEqual(Ehlo + "VRFY a@b\r\n" + Quit, run.Sent);
        AssertResult(run.Result, CurlExitCode.WeirdServerReply, CommandFailed550, 550, 0);
    }

    [TestMethod]
    public async Task ExecuteAsync_VrfyAnswered553_IsAcceptedAndWritten()
    {
        // VRFY=553 ambiguous: stdout 553 ambiguous, [553 15 0], exit 0.
        CommandRun run = await RunAsync(EhloReply + "553 ambiguous\r\n" + Bye, new MailRequestOptions { Recipients = ["a@b"] });

        Diagnostics.Diff("output", "553 ambiguous\r\n", run.Output);
        Assert.AreEqual("553 ambiguous\r\n", run.Output);
        AssertResult(run.Result, CurlExitCode.Ok, null, 553, 15);
    }

    [TestMethod]
    public async Task ExecuteAsync_HelpAnswered553_Fails()
    {
        CommandRun run = await RunAsync(EhloReply + "553 ambiguous\r\n" + Bye, new MailRequestOptions());

        Diagnostics.Diff("output", string.Empty, run.Output);
        Assert.AreEqual(string.Empty, run.Output);
        AssertResult(run.Result, CurlExitCode.WeirdServerReply, "Command failed: 553", 553, 0);
    }

    [TestMethod]
    public async Task ExecuteAsync_MultilineRefusal_WritesTheContinuationLinesBeforeFailing()
    {
        // HELP=550-first\r\n550 second: stdout 550-first\r\n, [550 11 0], exit 8.
        CommandRun run = await RunAsync(EhloReply + "550-first\r\n550 second\r\n" + Bye, new MailRequestOptions());

        Diagnostics.Diff("sent", Ehlo + "HELP\r\n" + Quit, run.Sent);
        Assert.AreEqual(Ehlo + "HELP\r\n" + Quit, run.Sent);
        Diagnostics.Diff("output", "550-first\r\n", run.Output);
        Assert.AreEqual("550-first\r\n", run.Output);
        AssertResult(run.Result, CurlExitCode.WeirdServerReply, CommandFailed550, 550, 11);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnknownCustomCommand_FailsWithTheServersCode()
    {
        // -X "FOO bar": 502 Command not implemented, curl: (8) Command failed: 502.
        CommandRun run = await RunAsync(EhloReply + "502 Command not implemented\r\n" + Bye, new MailRequestOptions { CustomCommand = "FOO bar" });

        Diagnostics.Diff("sent", Ehlo + "FOO bar\r\n" + Quit, run.Sent);
        Assert.AreEqual(Ehlo + "FOO bar\r\n" + Quit, run.Sent);
        AssertResult(run.Result, CurlExitCode.WeirdServerReply, "Command failed: 502", 502, 0);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerClosesMidReply_FailsWithExit56WithoutQuit()
    {
        CommandRun run = await RunAsync(EhloReply + "250-a\r\n", new MailRequestOptions { CustomCommand = "NOOP" });

        Diagnostics.Diff("sent", Ehlo + "NOOP\r\n", run.Sent);
        Assert.AreEqual(Ehlo + "NOOP\r\n", run.Sent);
        Diagnostics.Diff("output", "250-a\r\n", run.Output);
        Assert.AreEqual("250-a\r\n", run.Output);
        AssertResult(run.Result, CurlExitCode.RecvError, "response reading failed (errno: 0)", 250, 7);
    }

    [TestMethod]
    public async Task ExecuteAsync_OverlongReplyLine_FailsWithExit100WithoutQuit()
    {
        CommandRun run = await RunAsync(EhloReply + "250 " + new string('x', 70000) + "\r\n", new MailRequestOptions { CustomCommand = "NOOP" });

        Diagnostics.Diff("sent", Ehlo + "NOOP\r\n", run.Sent);
        Assert.AreEqual(Ehlo + "NOOP\r\n", run.Sent);
        AssertResult(run.Result, CurlExitCode.TooLarge, "A value or data field grew larger than allowed", 250, 0);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplyLongerThanMaxFileSize_CutsItAtTheLimitAndFailsWithExit63AfterQuit()
    {
        // Measured (BL-1386): --max-filesize 10 --mail-rcpt a@b, VRFY=250 a-reply-longer-than-ten-bytes:
        // stdout "250 a-repl", exit 63, QUIT still sent.
        CommandRun run = await RunAsync(EhloReply + LongReply + Bye, new MailRequestOptions { Recipients = ["a@b"] }, maxFileSize: 10);

        Diagnostics.Diff("sent", Ehlo + "VRFY a@b\r\n" + Quit, run.Sent);
        Assert.AreEqual(Ehlo + "VRFY a@b\r\n" + Quit, run.Sent);
        Diagnostics.Diff("output", "250 a-repl", run.Output);
        Assert.AreEqual("250 a-repl", run.Output);
        AssertResult(run.Result, CurlExitCode.FilesizeExceeded, "Exceeded the maximum allowed file size (10) with 10 bytes", 250, 10);
    }

    [TestMethod]
    public async Task ExecuteAsync_SecondReplyPassesMaxFileSize_TriesNoFurtherRecipient()
    {
        // Measured (BL-1386): --max-filesize 40 and three recipients: VRFY a@b, VRFY c@d, QUIT;
        // stdout the first reply and "250 a", exit 63.
        CommandRun run = await RunAsync(
            EhloReply + LongReply + LongReply + Bye, new MailRequestOptions { Recipients = ["a@b", "c@d", "e@f"] }, maxFileSize: 40);

        Diagnostics.Diff("sent", Ehlo + "VRFY a@b\r\nVRFY c@d\r\n" + Quit, run.Sent);
        Assert.AreEqual(Ehlo + "VRFY a@b\r\nVRFY c@d\r\n" + Quit, run.Sent);
        Diagnostics.Diff("output", LongReply + "250 a", run.Output);
        Assert.AreEqual(LongReply + "250 a", run.Output);
        AssertResult(run.Result, CurlExitCode.FilesizeExceeded, "Exceeded the maximum allowed file size (40) with 40 bytes", 250, 40);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplyExactlyMaxFileSize_Succeeds()
    {
        // Measured (BL-1386): --max-filesize 35 and a 35-byte reply: exit 0, the whole reply written.
        CommandRun run = await RunAsync(EhloReply + LongReply + Bye, new MailRequestOptions { Recipients = ["a@b"] }, maxFileSize: 35);

        Diagnostics.Diff("output", LongReply, run.Output);
        Assert.AreEqual(LongReply, run.Output);
        AssertResult(run.Result, CurlExitCode.Ok, null, 250, 35);
    }

    [TestMethod]
    public async Task ExecuteAsync_ContinuationLinePassesMaxFileSize_CutsItAndKeepsTheEarlierCode()
    {
        CommandRun run = await RunAsync(EhloReply + "214-first line\r\n214 end\r\n" + Bye, new MailRequestOptions(), maxFileSize: 5);

        Diagnostics.Diff("output", "214-f", run.Output);
        Assert.AreEqual("214-f", run.Output);
        AssertResult(run.Result, CurlExitCode.FilesizeExceeded, "Exceeded the maximum allowed file size (5) with 5 bytes", 250, 5);
    }

    private void AssertResult(TransferResult result, CurlExitCode exitCode, string? message, int responseCode, long downloadSize)
    {
        Diagnostics.AssertValues("exit code", exitCode, result.ExitCode);
        Assert.AreEqual(exitCode, result.ExitCode);
        Diagnostics.AssertValues("error message", message, result.ErrorMessage);
        Assert.AreEqual(message, result.ErrorMessage);
        Diagnostics.AssertValues("bytes transferred", downloadSize, result.BytesTransferred);
        Assert.AreEqual(downloadSize, result.BytesTransferred);
        Diagnostics.AssertValues("response code", responseCode, result.Report!.ResponseCode);
        Assert.AreEqual(responseCode, result.Report!.ResponseCode);
    }

    private async Task<CommandRun> RunAsync(string replies, MailRequestOptions? mail, bool noBody = false, long? maxFileSize = null)
    {
        var output = new MemoryStream();
        var progress = new RecordingProgress();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = output,
            Mail = mail,
            NoBody = noBody,
            MaxFileSize = maxFileSize,
            Progress = progress,
        };

        Diagnostics.Arrange("no body", noBody);
        Diagnostics.Arrange("max file size", maxFileSize?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(none)");
        SmtpRun run = await SmtpRun.ExecuteAsync(Diagnostics, context, new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting + replies)));
        string written = Encoding.Latin1.GetString(output.ToArray());
        Diagnostics.Act("output", SmtpDiagnostics.Show(written));

        return new CommandRun(run.Result, run.Sent, written, progress);
    }

    private sealed record CommandRun(TransferResult Result, string Sent, string Output, RecordingProgress Progress);
}
