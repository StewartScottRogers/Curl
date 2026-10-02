using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smtp.Fakes;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Pins what an SMTP transfer writes to the <c>-D</c> stream against curl 8.21.0 (Schannel),
/// recorded on 2026-10-01 with <c>Record-CurlExchange.ps1</c> (BL-1134 Notes): every reply
/// line read, continuation and skipped lines included, byte for byte with its line end and
/// in arrival order, and nothing of <c>QUIT</c>'s reply, which curl reads once the transfer
/// is over. Under <c>-i</c> alone, where there is no <c>-D</c> stream, nothing extra is
/// written to the output.
/// </summary>
[TestClass]
public sealed class SmtpProtocolHandlerDumpHeaderTests
{
    private const string Url = "smtp://127.0.0.1:18725/dom";

    private const string Greeting = "220 localhost ESMTP\r\n";

    private const string EhloReply =
        "250-localhost\r\n250-AUTH PLAIN LOGIN CRAM-MD5\r\n250-STARTTLS\r\n250-SIZE 1000000\r\n250-8BITMIME\r\n250 SMTPUTF8\r\n";

    private const string Bye = "221 Bye\r\n";

    [TestMethod]
    public async Task ExecuteAsync_GreetingThenARefusalThenClose_DumpsBothLinesAndFailsWithExit56()
    {
        // -Response '220 hi\r\n554 no\r\n': exit 56, the -D file "220 hi\r\n554 no\r\n", stdout empty.
        DumpRun run = await RunAsync("220 hi\r\n554 no\r\n", mail: null, upload: null);

        Assert.AreEqual(CurlExitCode.RecvError, run.Result.ExitCode);
        Assert.AreEqual("220 hi\r\n554 no\r\n", run.Dumped);
        Assert.AreEqual(string.Empty, run.Output);
    }

    [TestMethod]
    public async Task ExecuteAsync_SendsAMessage_DumpsEveryReplyButQuits()
    {
        // --mail-from a@b --mail-rcpt c@d -T mail.txt: the -D file holds every reply line, the
        // multi-line EHLO reply included, and not QUIT's 221.
        const string Replies =
            Greeting + EhloReply + "250 OK\r\n250 OK\r\n354 End data with <CR><LF>.<CR><LF>\r\n250 OK message accepted\r\n";

        DumpRun run = await RunAsync(
            Replies + Bye,
            new MailRequestOptions { From = "a@b", Recipients = ["c@d"] },
            new MemoryStream(Encoding.Latin1.GetBytes("Subject: x\r\n\r\nhi\r\n")));

        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual(Replies, run.Dumped);
        Assert.AreEqual(string.Empty, run.Output);
    }

    [TestMethod]
    public async Task ExecuteAsync_CustomCommandWithAMultiLineReply_DumpsEveryLineAndWritesTheReplyAsBefore()
    {
        // -X 'VRFY c@d' answered "250-first\r\n250 second": both in the -D file and on stdout.
        const string Reply = "250-first\r\n250 second\r\n";

        DumpRun run = await RunAsync(
            Greeting + EhloReply + Reply + Bye, new MailRequestOptions { CustomCommand = "VRFY c@d" }, upload: null);

        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual(Greeting + EhloReply + Reply, run.Dumped);
        Assert.AreEqual(Reply, run.Output);
    }

    [TestMethod]
    public async Task ExecuteAsync_ALineWithNoCodeIsSkipped_DumpsTheSkippedLineToo()
    {
        // GREETING='junk line\r\n220 hi', EHLO='554 no': the skipped line is in the -D file too.
        const string Replies = "junk line\r\n220 hi\r\n554 no\r\n250 localhost\r\n" + SmtpRun.HelpReply;

        DumpRun run = await RunAsync(Replies + Bye, mail: null, upload: null);

        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual(Replies, run.Dumped);
        Assert.AreEqual(SmtpRun.HelpReply, run.Output);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeaderOutputIsTheOutputWithNoDumpStream_WritesOnlyTheReplyAsBefore()
    {
        // -i alone: curl's stdout holds the HELP reply and nothing else.
        var output = new MemoryStream();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = output,
            HeaderOutput = output,
            Progress = new RecordingProgress(),
        };

        SmtpRun run = await SmtpRun.ExecuteAsync(
            context, new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting + EhloReply + SmtpRun.HelpReply + Bye)));

        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual(SmtpRun.HelpReply, Encoding.Latin1.GetString(output.ToArray()));
    }

    private static async Task<DumpRun> RunAsync(string replies, MailRequestOptions? mail, Stream? upload)
    {
        var output = new MemoryStream();
        var dump = new MemoryStream();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = output,
            DumpHeaderOutput = dump,
            Mail = mail,
            Upload = upload,
            Progress = new RecordingProgress(),
        };

        SmtpRun run = await SmtpRun.ExecuteAsync(context, new ScriptedConnection(Encoding.Latin1.GetBytes(replies)));

        return new DumpRun(run.Result, Encoding.Latin1.GetString(dump.ToArray()), Encoding.Latin1.GetString(output.ToArray()));
    }

    private sealed record DumpRun(TransferResult Result, string Dumped, string Output);
}
