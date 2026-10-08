using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Pop3.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Pop3;

/// <summary>
/// Pins <c>LIST</c> and <c>RETR</c> against curl 8.21.0: the command each URL sends, the bytes
/// written for each answer, and the exit code and message of each failure. Every case was
/// recorded from real curl (the Schannel build) on 2026-09-28 with
/// <c>Record-CurlExchange.ps1 -Pop3</c>, curl running <c>-sS -w '[%{size_download}]'</c>
/// (BL-549 Notes); <c>%{size_download}</c> is the byte count each success carries.
/// </summary>
[TestClass]
public sealed class Pop3ProtocolHandlerTransferTests
{
    /// <summary>Gets or sets the running test's context, which MSTest sets.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private const string Opening =
        "+OK POP3 ready <1896.697170952@localhost>\r\n"
        + "+OK Capability list follows\r\nUSER\r\nSASL PLAIN LOGIN\r\nSTLS\r\nTOP\r\nUIDL\r\n.\r\n";

    /// <summary>The <c>-Pop3Message</c> measured, with a line starting <c>..</c> and one starting <c>.</c>.</summary>
    private const string Message = "Subject: a\r\n\r\nline one\r\n..two dots\r\n.one dot\r\nlast\r\n";

    /// <summary>The recorder's <c>RETR 1</c> answer for <see cref="Message" />: dot-stuffed, then the terminator.</summary>
    private const string RetrReply =
        "+OK 52 octets\r\nSubject: a\r\n\r\nline one\r\n...two dots\r\n..one dot\r\nlast\r\n.\r\n";

    private const string Bye = "+OK Bye\r\n";

    private const string Capa = "CAPA\r\n";

    private const string Quit = "QUIT\r\n";

    private const string Url = "pop3://127.0.0.1:18110/";

    [TestMethod]
    public async Task ExecuteAsync_UrlWithoutMessage_SendsListAndWritesTheListing()
    {
        Pop3Run run = await RunAsync(Url, Opening, "+OK 2 messages (104 octets)\r\n1 52\r\n2 52\r\n.\r\n", Bye);

        Diagnostics.Diff("sent", Capa + "LIST\r\n" + Quit, run.Sent);
        Assert.AreEqual(Capa + "LIST\r\n" + Quit, run.Sent);
        Diagnostics.Diff("output", "1 52\r\n2 52\r\n", Encoding.Latin1.GetString(run.Output));
        Assert.AreEqual("1 52\r\n2 52\r\n", Encoding.Latin1.GetString(run.Output));
        Diagnostics.AssertResult(TransferResult.Success(12), run.Result);
        Assert.AreEqual(TransferResult.Success(12), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_UrlNamingAMessage_SendsRetrAndWritesItUnstuffedWithoutTheTerminator()
    {
        Pop3Run run = await RunAsync(Url + "1", Opening, RetrReply, Bye);

        Diagnostics.Diff("sent", Capa + "RETR 1\r\n" + Quit, run.Sent);
        Assert.AreEqual(Capa + "RETR 1\r\n" + Quit, run.Sent);
        Diagnostics.Diff("output", Message, Encoding.Latin1.GetString(run.Output));
        Assert.AreEqual(Message, Encoding.Latin1.GetString(run.Output));
        Diagnostics.AssertResult(TransferResult.Success(52), run.Result);
        Assert.AreEqual(TransferResult.Success(52), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_Retr_ReportsStartAndEachChunkDownloaded()
    {
        Pop3Run run = await RunAsync(Url + "1", Opening, RetrReply, Bye);

        Diagnostics.Act("progress", $"started {run.Progress.Started}, downloaded {string.Join(", ", run.Progress.Downloaded)}");
        Diagnostics.Assert("last download report", (52L, (long?)null), run.Progress.Downloaded[^1]);
        Assert.IsTrue(run.Progress.Started);
        Assert.AreEqual((52L, (long?)null), run.Progress.Downloaded[^1]);
    }

    [TestMethod]
    [DataRow("a%20b", "RETR a b", DisplayName = "percent-decoded")]
    [DataRow("1/2", "RETR 1/2", DisplayName = "everything after the first slash")]
    [DataRow("%zz%4", "RETR %zz%4", DisplayName = "an invalid escape stays as written")]
    [DataRow("%C3%A9", "RETR Ã©", DisplayName = "decoded bytes are sent as bytes")]
    [DataRow("%7f", "RETR \u007F", DisplayName = "0x7F is kept")]
    public async Task ExecuteAsync_MessageIdInTheUrl_IsSentAsCurlDecodesIt(string path, string command)
    {
        Pop3Run run = await RunAsync(Url + path, Opening, RetrReply, Bye);

        Diagnostics.Diff("sent", Capa + command + "\r\n" + Quit, run.Sent);
        Assert.AreEqual(Capa + command + "\r\n" + Quit, run.Sent);
        Diagnostics.AssertResult(TransferResult.Success(52), run.Result);
        Assert.AreEqual(TransferResult.Success(52), run.Result);
    }

    [TestMethod]
    [DataRow("%0d", DisplayName = "CR")]
    [DataRow("%1f", DisplayName = "0x1F")]
    [DataRow("1%00", DisplayName = "NUL")]
    public async Task ExecuteAsync_MessageIdWithAControlCharacter_FailsWithExit3AfterCapaAndStillQuits(string path)
    {
        // Measured for %0d and %1f: CAPA, QUIT, exit 3.
        Pop3Run run = await RunAsync(Url + path, Opening, Bye);

        Diagnostics.Diff("sent", Capa + Quit, run.Sent);
        Assert.AreEqual(Capa + Quit, run.Sent);
        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.UrlMalformat, "URL using bad/illegal format or missing URL"),
            run.Result);
    }

    [TestMethod]
    [DataRow(Url + "9", "RETR 9", "-ERR no such message", DisplayName = "RETR=-ERR no such message")]
    [DataRow(Url, "LIST", "-ERR nope", DisplayName = "LIST=-ERR nope")]
    [DataRow(Url + "1", "RETR 1", "+ here\r\nx\r\n.", DisplayName = "RETR=+ here")]
    public async Task ExecuteAsync_CommandNotAnsweredOk_FailsWithExit8AndStillQuits(string url, string command, string reply)
    {
        Pop3Run run = await RunAsync(url, Opening, reply + "\r\n", Bye);

        Diagnostics.Diff("sent", Capa + command + "\r\n" + Quit, run.Sent);
        Assert.AreEqual(Capa + command + "\r\n" + Quit, run.Sent);
        Diagnostics.AssertValues("run.Output count", 0, run.Output.Count());
        Assert.IsEmpty(run.Output);
        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.WeirdServerReply, "Weird server reply"), run.Result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.WeirdServerReply, "Weird server reply"), run.Result);
    }

    [TestMethod]
    [DataRow(Url + "1", "RETR 1", DisplayName = "RETR=CLOSE")]
    [DataRow(Url, "LIST", DisplayName = "LIST=CLOSE")]
    public async Task ExecuteAsync_ServerClosesBeforeTheStatusLine_FailsWithExit56WithoutQuit(string url, string command)
    {
        Pop3Run run = await RunAsync(url, Opening);

        Diagnostics.Diff("sent", Capa + command + "\r\n", run.Sent);
        Assert.AreEqual(Capa + command + "\r\n", run.Sent);
        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.RecvError, "response reading failed (errno: 0)"), run.Result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RecvError, "response reading failed (errno: 0)"), run.Result);
    }

    [TestMethod]
    [DataRow("junk\r\n+OK\r\nabc\r\n.\r\n", "abc\r\n", DisplayName = "a line before the status line is skipped")]
    [DataRow("+OK 0 octets\r\n.\r\n", "\r\n", DisplayName = "an empty message writes CRLF")]
    [DataRow("+OK\r\na\r\r\nb\r\n\r\n..\r\n.\r\n", "a\r\r\nb\r\n\r\n.\r\n", DisplayName = "a stray CR and a lone stuffed dot")]
    [DataRow("+OK\r\n..first\r\nb\r\n.\r\n", ".first\r\nb\r\n", DisplayName = "a stuffed first line")]
    [DataRow("+OK\r\nabc\r\n.x\r\n.\r\n", "abc\r\n.x\r\n", DisplayName = "an unstuffed dot line is written as it came")]
    public async Task ExecuteAsync_RetrAnswer_IsWrittenAsCurlWritesIt(string reply, string written)
    {
        Pop3Run run = await RunAsync(Url + "1", Opening, reply, Bye);

        Diagnostics.Diff("sent", Capa + "RETR 1\r\n" + Quit, run.Sent);
        Assert.AreEqual(Capa + "RETR 1\r\n" + Quit, run.Sent);
        Diagnostics.Diff("output", written, Encoding.Latin1.GetString(run.Output));
        Assert.AreEqual(written, Encoding.Latin1.GetString(run.Output));
        Diagnostics.AssertResult(TransferResult.Success(written.Length), run.Result);
        Assert.AreEqual(TransferResult.Success(written.Length), run.Result);
    }

    [TestMethod]
    [DataRow("+OK\r\nabc\r\n", "abc", DisplayName = "no terminator: the held-back CRLF is lost")]
    [DataRow("+OK\r\nabc\r\n.\r\nextra\r\n", "abc\r\n.\r\nextra", DisplayName = "bytes after the terminator in the same read")]
    [DataRow("+OK\nabc\n.\n\r\n", "abc\n.\n", DisplayName = "LF-only line ends")]
    public async Task ExecuteAsync_ServerClosesBeforeATerminatorEndsARead_SucceedsWithWhatItWroteAndNoQuit(string reply, string written)
    {
        // Measured: exit 0 once the recorder hung up, and no QUIT.
        Pop3Run run = await RunAsync(Url + "1", Opening, reply);

        Diagnostics.Diff("sent", Capa + "RETR 1\r\n", run.Sent);
        Assert.AreEqual(Capa + "RETR 1\r\n", run.Sent);
        Diagnostics.Diff("output", written, Encoding.Latin1.GetString(run.Output));
        Assert.AreEqual(written, Encoding.Latin1.GetString(run.Output));
        Diagnostics.AssertResult(TransferResult.Success(written.Length), run.Result);
        Assert.AreEqual(TransferResult.Success(written.Length), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReadFailsMidBody_SucceedsWithWhatItWroteAndNoQuit()
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(Opening + "+OK\r\nabc")) { FailReadsWhenExhausted = true };

        Pop3Run run = await Pop3Run.ExecuteAsync(Diagnostics, Url + "1", connection);

        Diagnostics.Diff("sent", Capa + "RETR 1\r\n", run.Sent);
        Assert.AreEqual(Capa + "RETR 1\r\n", run.Sent);
        Diagnostics.Diff("output", "abc", Encoding.Latin1.GetString(run.Output));
        Assert.AreEqual("abc", Encoding.Latin1.GetString(run.Output));
        Diagnostics.AssertResult(TransferResult.Success(3), run.Result);
        Assert.AreEqual(TransferResult.Success(3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_RetrAnswerSplitAtEveryByte_IsWrittenWhole()
    {
        byte[] replies = Encoding.Latin1.GetBytes(Opening + RetrReply + Bye);

        Pop3Run run = await Pop3Run.ExecuteAsync(Diagnostics, Url + "1", new ScriptedConnection([.. replies.Chunk(1)]));

        Diagnostics.Diff("sent", Capa + "RETR 1\r\n" + Quit, run.Sent);
        Assert.AreEqual(Capa + "RETR 1\r\n" + Quit, run.Sent);
        Diagnostics.Diff("output", Message, Encoding.Latin1.GetString(run.Output));
        Assert.AreEqual(Message, Encoding.Latin1.GetString(run.Output));
        Diagnostics.AssertResult(TransferResult.Success(52), run.Result);
        Assert.AreEqual(TransferResult.Success(52), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_RetrAnswerSplitIntoTwoReadsAnywhere_IsWrittenWhole()
    {
        byte[] retr = Encoding.Latin1.GetBytes(RetrReply);
        Diagnostics.ArrangeRun(Url + "1", Opening + RetrReply + Bye);
        Diagnostics.Arrange("splits", $"the RETR reply split into two reads at every byte from 1 to {retr.Length - 1}");
        int splitsRun = 0;
        for (int split = 1; split < retr.Length; split++)
        {
            var connection = new ScriptedConnection(
                Encoding.Latin1.GetBytes(Opening), retr[..split], retr[split..], Encoding.Latin1.GetBytes(Bye));

            Pop3Run run = await Pop3Run.ExecuteAsync(Url + "1", connection);
            if (Encoding.Latin1.GetString(run.Output) != Message || run.Sent != Capa + "RETR 1\r\n" + Quit)
            {
                Diagnostics.Act("first failing split", split);
                Diagnostics.ActRun(run);
                Diagnostics.Diff("output", Message, Encoding.Latin1.GetString(run.Output));
                Diagnostics.Diff("sent", Capa + "RETR 1\r\n" + Quit, run.Sent);
            }

            splitsRun++;
            Assert.AreEqual(Message, Encoding.Latin1.GetString(run.Output), $"split at {split}");
            Assert.AreEqual(Capa + "RETR 1\r\n" + Quit, run.Sent, $"split at {split}");
        }

        Diagnostics.Act("splits run", splitsRun);
        Diagnostics.Assert("splits with the whole message and the right commands", retr.Length - 1, splitsRun);
    }

    [TestMethod]
    public async Task ExecuteAsync_TerminatorFollowedByTheQuitReplyInOneRead_IsNotTheEndOfTheBody()
    {
        // The terminator ends the body only when it ends a read, as curl's pop3_write checks
        // it only there; the "after" case measured the same with the recorder's own bytes.
        byte[] retr = Encoding.Latin1.GetBytes("+OK\r\nabc\r\n.\r\n" + Bye);
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(Opening), retr);

        Pop3Run run = await Pop3Run.ExecuteAsync(Diagnostics, Url + "1", connection);

        Diagnostics.Diff("output", "abc\r\n.\r\n+OK Bye", Encoding.Latin1.GetString(run.Output));
        Assert.AreEqual("abc\r\n.\r\n+OK Bye", Encoding.Latin1.GetString(run.Output));
        Diagnostics.Diff("sent", Capa + "RETR 1\r\n", run.Sent);
        Assert.AreEqual(Capa + "RETR 1\r\n", run.Sent);
    }

    /// <summary>
    /// Runs <paramref name="url" /> against a server that sends each of <paramref name="reads" />
    /// as one read, as the recorder sends each reply in one write once curl's command arrives.
    /// </summary>
    private Task<Pop3Run> RunAsync(string url, params string[] reads) =>
        Pop3Run.ExecuteAsync(Diagnostics, url, new ScriptedConnection([.. reads.Select(Encoding.Latin1.GetBytes)]));
}
