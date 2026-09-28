using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Imap.Fakes;

namespace Curl.Protocol.Imap;

/// <summary>
/// Pins how an IMAP URL naming a mailbox and a <c>UID</c> or <c>MAILINDEX</c> is fetched
/// against curl 8.21.0: the <c>SELECT</c> and <c>FETCH</c> lines sent, the literal's bytes
/// written, and the exit code and message of every failure. Every case marked measured was
/// recorded from real curl (the Schannel build) on 2026-09-28 with
/// <c>Record-CurlExchange.ps1 -Imap</c>, curl running <c>-sS</c> against the recorder's
/// default replies unless an <c>-ImapReply</c> is named (BL-555 Notes).
/// </summary>
[TestClass]
public sealed class ImapProtocolHandlerFetchTests
{
    private const string Host = "imap://127.0.0.1:18155/";

    private const string Opening = "* OK [CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN] ready\r\n"
        + "* CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN\r\nA001 OK CAPABILITY completed\r\n";

    private const string SelectReply = "* FLAGS (\\Answered \\Flagged \\Deleted \\Seen \\Draft)\r\n* 2 EXISTS\r\n* 0 RECENT\r\n"
        + "* OK [UIDVALIDITY 1] UIDs valid\r\n* OK [UIDNEXT 3] Predicted next UID\r\nA002 OK [READ-WRITE] SELECT completed\r\n";

    /// <summary>The recorder's default message: 100 bytes.</summary>
    private const string Message = "From: sender@example.com\r\nTo: recipient@example.com\r\nSubject: Recorded\r\n\r\nHello from the recorder.\r\n";

    private const string FetchReply = "* 1 FETCH (UID 1 BODY[] {100}\r\n" + Message + ")\r\nA003 OK FETCH completed\r\n";

    private const string LogoutReply = "* BYE Logging out\r\nA004 OK LOGOUT completed\r\n";

    private const string SentThroughSelect = "A001 CAPABILITY\r\nA002 SELECT INBOX\r\n";

    private const string SentThroughFetch = SentThroughSelect + "A003 UID FETCH 1 BODY[]\r\n";

    [TestMethod]
    public async Task ExecuteAsync_MailboxAndUid_SelectsFetchesWritesTheLiteralAndLogsOut()
    {
        // Measured: INBOX;UID=1, stdout the 100 message bytes, exit 0.
        FetchRun run = await RunAsync(Host + "INBOX;UID=1", Opening + SelectReply + FetchReply + LogoutReply);

        Assert.AreEqual(SentThroughFetch + "A004 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(Message, run.Output);
        Assert.AreEqual(TransferResult.Success(100), run.Result);
        CollectionAssert.AreEqual(new[] { "started", "down 100/100" }, run.Progress.Reports);
    }

    [TestMethod]
    [DataRow("INBOX;MAILINDEX=1", "INBOX", "FETCH 1 BODY[]", DisplayName = "INBOX;MAILINDEX=1 (measured)")]
    [DataRow("INBOX;UID=1;SECTION=TEXT", "INBOX", "UID FETCH 1 BODY[TEXT]", DisplayName = "INBOX;UID=1;SECTION=TEXT (measured)")]
    [DataRow("INBOX;UID=1;PARTIAL=0.10", "INBOX", "UID FETCH 1 BODY[]<0.10>", DisplayName = "INBOX;UID=1;PARTIAL=0.10 (measured)")]
    [DataRow("INBOX;MAILINDEX=2;SECTION=HEADER;PARTIAL=0.5", "INBOX", "FETCH 2 BODY[HEADER]<0.5>", DisplayName = "MAILINDEX with SECTION and PARTIAL")]
    [DataRow("INBOX;MAILINDEX=2;UID=7", "INBOX", "UID FETCH 7 BODY[]", DisplayName = "UID wins over MAILINDEX")]
    [DataRow("INBOX;UIDVALIDITY=1;UID=1", "INBOX", "UID FETCH 1 BODY[]", DisplayName = "UIDVALIDITY=1 matching (measured)")]
    [DataRow("INBOX;UIDVALIDITY=01;UID=1", "INBOX", "UID FETCH 1 BODY[]", DisplayName = "UIDVALIDITY=01 compared as a number (measured)")]
    [DataRow("INBOX;UIDVALIDITY=abc;UID=1", "INBOX", "UID FETCH 1 BODY[]", DisplayName = "UIDVALIDITY=abc not a number, not compared (measured)")]
    [DataRow("INBOX;UIDVALIDITY=1x;UID=1", "INBOX", "UID FETCH 1 BODY[]", DisplayName = "UIDVALIDITY=1x read as 1")]
    [DataRow("INBOX;UIDVALIDITY=4294967297;UID=1", "INBOX", "UID FETCH 1 BODY[]", DisplayName = "UIDVALIDITY past 32 bits, not compared")]
    [DataRow("My%20Box;UID=1", "\"My Box\"", "UID FETCH 1 BODY[]", DisplayName = "My%20Box quoted (measured)")]
    [DataRow("a%22b;UID=1", "\"a\\\"b\"", "UID FETCH 1 BODY[]", DisplayName = "a quote escaped (measured)")]
    [DataRow("a%5Cb;UID=1", "\"a\\\\b\"", "UID FETCH 1 BODY[]", DisplayName = "a backslash escaped (measured)")]
    [DataRow("a(b;UID=1", "\"a(b\"", "UID FETCH 1 BODY[]", DisplayName = "a( quoted (measured)")]
    [DataRow("a)b;UID=1", "\"a)b\"", "UID FETCH 1 BODY[]", DisplayName = "a) quoted")]
    [DataRow("a%25b;UID=1", "\"a%b\"", "UID FETCH 1 BODY[]", DisplayName = "a percent quoted (measured)")]
    [DataRow("a%5Db;UID=1", "\"a]b\"", "UID FETCH 1 BODY[]", DisplayName = "a] quoted (measured)")]
    [DataRow("a*b;UID=1", "\"a*b\"", "UID FETCH 1 BODY[]", DisplayName = "a* quoted (measured)")]
    [DataRow("a%7Bb;UID=1", "\"a{b\"", "UID FETCH 1 BODY[]", DisplayName = "a{ quoted (measured)")]
    [DataRow("a%zzb%4;UID=1", "\"a%zzb%4\"", "UID FETCH 1 BODY[]", DisplayName = "a percent without two hex digits kept")]
    [DataRow("Sent/2026;UID=1", "Sent/2026", "UID FETCH 1 BODY[]", DisplayName = "a hierarchy separator kept")]
    [DataRow("INBOX/;UID=1/;SECTION=TEXT/", "INBOX", "UID FETCH 1 BODY[TEXT]", DisplayName = "trailing slashes dropped (measured)")]
    [DataRow("INBOX;uid=1;section=text", "INBOX", "UID FETCH 1 BODY[text]", DisplayName = "names in any case (measured)")]
    [DataRow("INBOX;SECTION=1.MIME;UID=5=6", "INBOX", "UID FETCH 5=6 BODY[1.MIME]", DisplayName = "values as given")]
    [DataRow("%49NBOX;UID=%31", "INBOX", "UID FETCH 1 BODY[]", DisplayName = "mailbox and values decoded")]
    public async Task ExecuteAsync_FetchUrl_SendsSelectAndFetchAsCurlDoes(string path, string mailbox, string fetch)
    {
        FetchRun run = await RunAsync(Host + path, Opening + SelectReply + FetchReply + LogoutReply);

        Assert.AreEqual($"A001 CAPABILITY\r\nA002 SELECT {mailbox}\r\nA003 {fetch}\r\nA004 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(Message, run.Output);
        Assert.AreEqual(TransferResult.Success(100), run.Result);
    }

    [TestMethod]
    [DataRow("* OK [UIDVALIDITY 1] UIDs valid\r\n", DisplayName = "UIDVALIDITY 1 (measured)")]
    [DataRow("* ok [uidvalidity 1]\r\n", DisplayName = "in lower case")]
    [DataRow("* OK [UIDVALIDITY 3]\r\n* OK [UIDVALIDITY 1]\r\n", DisplayName = "the last one counts")]
    [DataRow("* OK [UIDVALIDITY 1]\r\n* OK [UIDVALIDITY x]\r\n", DisplayName = "one with no number is ignored")]
    [DataRow("* 2 EXISTS\r\n* OK [UIDVALIDITY 1 {3}\r\nabc]\r\n", DisplayName = "a literal in an untagged line")]
    public async Task ExecuteAsync_UidValidityOtherThanTheUrls_FailsWithExit78AfterLogout(string untagged)
    {
        // Measured: INBOX;UIDVALIDITY=2;UID=1 against UIDVALIDITY 1: SELECT, LOGOUT, exit 78.
        FetchRun run = await RunAsync(
            Host + "INBOX;UIDVALIDITY=2;UID=1",
            Opening + untagged + "A002 OK [READ-WRITE] done\r\n* BYE\r\nA003 OK bye\r\n");

        Assert.AreEqual(SentThroughSelect + "A003 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(string.Empty, run.Output);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RemoteFileNotFound, "Mailbox UIDVALIDITY has changed"), run.Result);
    }

    [TestMethod]
    [DataRow("A002 OK [READ-WRITE] no validity reported\r\n", DisplayName = "SELECT reports none")]
    [DataRow("* OK [UIDVALIDITY 4294967298]\r\nA002 OK done\r\n", DisplayName = "SELECT reports one past 32 bits")]
    public async Task ExecuteAsync_SelectReportsNoUidValidity_FetchesWhateverTheUrlSays(string selectReply)
    {
        FetchRun run = await RunAsync(Host + "INBOX;UIDVALIDITY=2;UID=1", Opening + selectReply + FetchReply + LogoutReply);

        Assert.AreEqual(SentThroughFetch + "A004 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(100), run.Result);
    }

    [TestMethod]
    [DataRow("A002 NO no mailbox\r\n", DisplayName = "SELECT=NO no mailbox (measured)")]
    [DataRow("A002 BAD what\r\n", DisplayName = "SELECT=BAD what")]
    [DataRow("A002 PREAUTH x\r\n", DisplayName = "SELECT=PREAUTH x")]
    public async Task ExecuteAsync_SelectNotOk_FailsWithExit67AfterLogout(string selectReply)
    {
        FetchRun run = await RunAsync(Host + "INBOX;UID=1", Opening + selectReply + "A003 OK bye\r\n");

        Assert.AreEqual(SentThroughSelect + "A003 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Select failed"), run.Result);
    }

    [TestMethod]
    [DataRow("A003 NO no such message\r\n", DisplayName = "UID FETCH=NO no such message (measured)")]
    [DataRow("A003 OK nothing\r\n", DisplayName = "UID FETCH=OK nothing (measured)")]
    [DataRow("* 1 EXISTS\r\n)\r\n* 1 FETCHED x\r\n* x FETCH (BODY[] {1}\r\n* 1FETCH (BODY[] {1}\r\nA003 OK done\r\n", DisplayName = "no untagged FETCH among other lines")]
    public async Task ExecuteAsync_FetchCompletesWithNoFetchResponse_FailsWithExit78AfterLogout(string fetchReply)
    {
        FetchRun run = await RunAsync(Host + "INBOX;UID=1", Opening + SelectReply + fetchReply + LogoutReply);

        Assert.AreEqual(SentThroughFetch + "A004 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(string.Empty, run.Output);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RemoteFileNotFound, "Remote file not found"), run.Result);
    }

    [TestMethod]
    [DataRow("* 1 FETCH (UID 1 BODY[] \"hi\")\r\n", DisplayName = "a quoted string (measured)")]
    [DataRow("* 1 FETCH (BODY[] {abc}\r\n", DisplayName = "letters in the braces")]
    [DataRow("* 1 FETCH (BODY[] {}\r\n", DisplayName = "empty braces")]
    [DataRow("* 1 FETCH (BODY[] {12\r\n", DisplayName = "no closing brace")]
    [DataRow("* 1 FETCH (BODY[] {99999999999999999999}\r\n", DisplayName = "a size past 63 bits")]
    [DataRow("* 1 FETCH (X {a} BODY[] {3}\r\n", DisplayName = "only the first brace is read")]
    public async Task ExecuteAsync_FetchResponseWithoutALiteral_FailsWithExit8AfterLogout(string fetchLine)
    {
        FetchRun run = await RunAsync(Host + "INBOX;UID=1", Opening + SelectReply + fetchLine + "A003 OK done\r\n" + LogoutReply);

        Assert.AreEqual(SentThroughFetch + "A004 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.WeirdServerReply, "Failed to parse FETCH response."), run.Result);
    }

    [TestMethod]
    [DataRow("* 1 FETCH (BODY[] {3}\r\nabc)\r\nA003 OK done\r\n", DisplayName = "the usual form")]
    [DataRow("* 1 fetch (BODY[] {3}xyz\r\nabc)\r\nA003 OK done\r\n", DisplayName = "a size not at the end, in lower case")]
    [DataRow("* 1 EXISTS\r\n* FETCH {3}\r\nabcA003 OK done\r\n", DisplayName = "no number, and the completion right after the literal")]
    public async Task ExecuteAsync_FetchResponseForms_WriteTheLiteral(string fetchReply)
    {
        FetchRun run = await RunAsync(Host + "INBOX;UID=1", Opening + SelectReply + fetchReply + LogoutReply);

        Assert.AreEqual("abc", run.Output);
        Assert.AreEqual(TransferResult.Success(3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyLiteral_WritesNothingAndSucceeds()
    {
        // Measured: UID FETCH=* 1 FETCH (BODY[] {0}\r\n)\r\nOK done: empty stdout, exit 0.
        FetchRun run = await RunAsync(Host + "INBOX;UID=1", Opening + SelectReply + "* 1 FETCH (BODY[] {0}\r\n)\r\nA003 OK done\r\n" + LogoutReply);

        Assert.AreEqual(SentThroughFetch + "A004 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(string.Empty, run.Output);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_LargeLiteralSplitAcrossReads_IsWrittenWhole()
    {
        string body = new('x', 10000);
        byte[] replies = Latin1(Opening + SelectReply + "* 1 FETCH (BODY[] {10000}\r\n" + body + ")\r\nA003 OK done\r\n" + LogoutReply);

        FetchRun run = await RunAsync(Host + "INBOX;UID=1", new ScriptedConnection([.. replies.Chunk(3000)]));

        Assert.AreEqual(body, run.Output);
        Assert.AreEqual(TransferResult.Success(10000), run.Result);
        Assert.AreEqual("down 10000/10000", run.Progress.Reports[^1]);
        Assert.IsGreaterThan(2, run.Progress.Reports.Count);
    }

    [TestMethod]
    [DataRow("A003 NO after\r\n", DisplayName = "UID FETCH=...NO after (measured)")]
    [DataRow("A003 BAD after\r\n", DisplayName = "BAD")]
    public async Task ExecuteAsync_FetchCompletionNotOkAfterTheLiteral_FailsWithExit8AfterLogout(string completion)
    {
        // Measured: stdout "abc", LOGOUT, exit 8 "Weird server reply".
        FetchRun run = await RunAsync(Host + "INBOX;UID=1", Opening + SelectReply + "* 1 FETCH (BODY[] {3}\r\nabc)\r\n" + completion + LogoutReply);

        Assert.AreEqual(SentThroughFetch + "A004 LOGOUT\r\n", run.Sent);
        Assert.AreEqual("abc", run.Output);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.WeirdServerReply, "Weird server reply", 3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerClosesInsideTheLiteral_FailsWithExit18WithoutLogout()
    {
        // Measured: UID FETCH=* 1 FETCH (BODY[] {100}\r\nabc, the recorder tagging the last
        // line: stdout "A003 abc\r\n", exit 18, no LOGOUT seen.
        FetchRun run = await RunAsync(Host + "INBOX;UID=1", Opening + SelectReply + "* 1 FETCH (BODY[] {100}\r\nA003 abc\r\n");

        Assert.AreEqual(SentThroughFetch, run.Sent);
        Assert.AreEqual("A003 abc\r\n", run.Output);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.PartialFile, "end of response with 90 bytes missing", 10), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerClosesAfterTheLiteral_FailsWithExit56()
    {
        FetchRun run = await RunAsync(Host + "INBOX;UID=1", Opening + SelectReply + "* 1 FETCH (BODY[] {3}\r\nabc)\r\n");

        Assert.AreEqual(SentThroughFetch, run.Sent);
        Assert.AreEqual("abc", run.Output);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RecvError, "response reading failed (errno: 0)"), run.Result);
    }

    [TestMethod]
    [DataRow("", DisplayName = "before the SELECT completes")]
    [DataRow(SelectReply, DisplayName = "before any FETCH response")]
    [DataRow(SelectReply + "* 1 EXISTS\r\n", DisplayName = "after an untagged line")]
    public async Task ExecuteAsync_ServerClosesBeforeTheLiteral_FailsWithExit56WithoutLogout(string replies)
    {
        FetchRun run = await RunAsync(Host + "INBOX;UID=1", Opening + replies);

        Assert.DoesNotContain("LOGOUT", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RecvError, "response reading failed (errno: 0)"), run.Result);
    }

    [TestMethod]
    [DataRow("+ more\r\n", "Unexpected continuation response", DisplayName = "a continuation")]
    [DataRow("* 1 EXI\0STS\r\n", "Nul byte in server response line", DisplayName = "a NUL byte")]
    public async Task ExecuteAsync_WeirdLineWhileWaitingForFetch_FailsWithExit8(string line, string message)
    {
        FetchRun run = await RunAsync(Host + "INBOX;UID=1", Opening + SelectReply + line + FetchReply + LogoutReply);

        Assert.AreEqual(SentThroughFetch, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.WeirdServerReply, message), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_OutputFailsPartway_FailsWithExit23WithoutLogout()
    {
        var context = Context(Host + "INBOX;UID=1", output: new FailingOutputStream(new OutputWriteFailedException(40, "disk full")));

        FetchRun run = await RunAsync(context, new ScriptedConnection(Latin1(Opening + SelectReply + FetchReply + LogoutReply)));

        Assert.AreEqual(SentThroughFetch, run.Sent);
        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.WriteError, "Failure writing output to destination, passed 100 returned 40"),
            run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_OutputFailsWithAPlainIOException_ReportsNoneAccepted()
    {
        var context = Context(Host + "INBOX;UID=1", output: new FailingOutputStream(new IOException("gone")));

        FetchRun run = await RunAsync(context, new ScriptedConnection(Latin1(Opening + SelectReply + FetchReply)));

        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.WriteError, "Failure writing output to destination, passed 100 returned 0"),
            run.Result);
    }

    [TestMethod]
    [DataRow("INBOX;FOO=1", DisplayName = "INBOX;FOO=1 an unknown name (measured)")]
    [DataRow("INBOX;UID", DisplayName = "INBOX;UID with no = (measured)")]
    [DataRow("INBOX;UID=1;UID=2", DisplayName = "INBOX;UID=1;UID=2 a repeated name (measured)")]
    [DataRow("INBOX;UIDVALIDITY=1;UIDVALIDITY=1;UID=1", DisplayName = "a repeated UIDVALIDITY")]
    [DataRow("INBOX;MAILINDEX=1;MAILINDEX=1", DisplayName = "a repeated MAILINDEX")]
    [DataRow("INBOX;UID=1;SECTION=A;SECTION=B", DisplayName = "a repeated SECTION")]
    [DataRow("INBOX;UID=1;PARTIAL=1;PARTIAL=2", DisplayName = "a repeated PARTIAL")]
    [DataRow("a%09b;UID=1", DisplayName = "a%09b a control byte in the mailbox (measured)")]
    [DataRow("INBOX;UID=%01", DisplayName = "a control byte in a value")]
    [DataRow("INBOX;U%0AID=1", DisplayName = "a control byte in a name")]
    [DataRow("INBOX;UID=1\"x", DisplayName = "a character past the parameters")]
    [DataRow("INBOX\"x", DisplayName = "a character past the mailbox")]
    public async Task ExecuteAsync_MalformedPath_FailsWithExit3AfterLogout(string path)
    {
        FetchRun run = await RunAsync(Host + path, Opening + "* BYE\r\nA002 OK bye\r\n");

        Assert.AreEqual("A001 CAPABILITY\r\nA002 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UrlMalformat, "URL using bad/illegal format or missing URL"), run.Result);
    }

    [TestMethod]
    [DataRow("", DisplayName = "no mailbox")]
    [DataRow("INBOX", DisplayName = "a mailbox and no UID")]
    [DataRow(";UID=1", DisplayName = "a UID and no mailbox")]
    [DataRow("INBOX;SECTION=TEXT", DisplayName = "a SECTION and no UID")]
    public async Task ExecuteAsync_UrlNamingNoMessage_LogsOutAndSucceeds(string path)
    {
        FetchRun run = await RunAsync(Host + path, Opening + "* BYE\r\nA002 OK bye\r\n");

        Assert.AreEqual("A001 CAPABILITY\r\nA002 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_CustomCommand_IsNotAFetch()
    {
        var context = Context(Host + "INBOX;UID=1", mail: new MailRequestOptions { CustomCommand = "EXAMINE INBOX" });

        FetchRun run = await RunAsync(context, new ScriptedConnection(Latin1(Opening + "A002 OK bye\r\n")));

        Assert.AreEqual("A001 CAPABILITY\r\nA002 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_IsNotAFetch()
    {
        var context = Context(Host + "INBOX;UID=1", upload: new MemoryStream([1]));

        FetchRun run = await RunAsync(context, new ScriptedConnection(Latin1(Opening + "A002 OK bye\r\n")));

        Assert.AreEqual("A001 CAPABILITY\r\nA002 LOGOUT\r\n", run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_MailOptionsWithoutCustomCommand_Fetches()
    {
        var context = Context(Host + "INBOX;UID=1", mail: new MailRequestOptions());

        FetchRun run = await RunAsync(context, new ScriptedConnection(Latin1(Opening + SelectReply + FetchReply + LogoutReply)));

        Assert.AreEqual(TransferResult.Success(100), run.Result);
    }

    private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);

    private static TransferContext Context(
        string url, Stream? output = null, MailRequestOptions? mail = null, Stream? upload = null) =>
        new()
        {
            Url = CurlUrl.Parse(url),
            Output = output ?? new MemoryStream(),
            Mail = mail,
            Upload = upload,
            Progress = new RecordingTransferProgress(),
        };

    private static Task<FetchRun> RunAsync(string url, string replies) =>
        RunAsync(url, new ScriptedConnection(Latin1(replies)));

    private static Task<FetchRun> RunAsync(string url, ScriptedConnection connection) =>
        RunAsync(Context(url), connection);

    private static async Task<FetchRun> RunAsync(TransferContext context, ScriptedConnection connection)
    {
        ImapRun run = await ImapRun.ExecuteAsync(context, connection);
        string output = context.Output is MemoryStream memory ? Encoding.Latin1.GetString(memory.ToArray()) : string.Empty;
        return new FetchRun(run.Result, run.Sent, output, (RecordingTransferProgress)context.Progress);
    }

    /// <summary>What a fetch did: its result, the bytes sent, the output written and the progress reported.</summary>
    private sealed record FetchRun(TransferResult Result, string Sent, string Output, RecordingTransferProgress Progress);
}
