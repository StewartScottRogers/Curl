using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Imap.Fakes;

namespace Curl.Protocol.Imap;

/// <summary>
/// Pins how an IMAP URL with no message to fetch, a search query or a <c>-X</c> command is
/// served against curl 8.21.0: the <c>LIST</c>, <c>SELECT</c>, <c>SEARCH</c> and custom
/// command lines sent, the untagged responses written, and the exit code and message of every
/// failure. Every case marked measured was recorded from real curl (the Schannel build) on
/// 2026-09-28 with <c>Record-CurlExchange.ps1 -Imap</c>, curl running <c>-sS -u u:p</c>
/// against the recorder's default replies unless an <c>-ImapReply</c> is named (BL-556
/// Notes). The login is left out here: with no user, the command after <c>CAPABILITY</c> is
/// the one curl sent after logging in.
/// </summary>
[TestClass]
public sealed class ImapProtocolHandlerListTests
{
    private const string Host = "imap://127.0.0.1:18143/";

    private const string Opening = "* OK [CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN] ready\r\n"
        + "* CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN\r\nA001 OK CAPABILITY completed\r\n";

    private const string ListLines = "* LIST (\\HasNoChildren) \"/\" INBOX\r\n* LIST (\\HasNoChildren) \"/\" Sent\r\n";

    private const string SelectLines = "* FLAGS (\\Answered \\Flagged \\Deleted \\Seen \\Draft)\r\n* 2 EXISTS\r\n* 0 RECENT\r\n"
        + "* OK [UIDVALIDITY 1] UIDs valid\r\n* OK [UIDNEXT 3] Predicted next UID\r\n";

    private const string SelectReply = SelectLines + "A002 OK [READ-WRITE] SELECT completed\r\n";

    private const string SentThroughSelect = "A001 CAPABILITY\r\nA002 SELECT INBOX\r\n";

    private const string QuoteError = "Quote command returned error";

    private const string MalformedUrl = "URL using bad/illegal format or missing URL";

    [TestMethod]
    [DataRow("", "LIST \"\" *", DisplayName = "imap://h/ (measured)")]
    [DataRow("INBOX", "LIST \"INBOX\" *", DisplayName = "imap://h/INBOX (measured)")]
    [DataRow("A%22B%5CC", "LIST \"A\\\"B\\\\C\" *", DisplayName = "A%22B%5CC escaped, not quoted again (measured)")]
    [DataRow("My%20Box", "LIST \"My Box\" *", DisplayName = "My%20Box (measured)")]
    [DataRow("INBOX?", "LIST \"INBOX\" *", DisplayName = "INBOX? an empty query (measured)")]
    [DataRow("?NEW", "LIST \"\" *", DisplayName = "?NEW a query without a mailbox (measured)")]
    [DataRow("INBOX?A%0AB", "LIST \"INBOX\" *", DisplayName = "INBOX?A%0AB a control byte drops the query (measured)")]
    public async Task ExecuteAsync_NoMessageNoQuery_ListsAndWritesEveryListResponse(string path, string list)
    {
        ListRun run = await RunAsync(Host + path, Opening + ListLines + "A002 OK LIST completed\r\n");

        Assert.AreEqual($"A001 CAPABILITY\r\nA002 {list}\r\nA003 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(ListLines, run.Output);
        Assert.AreEqual(TransferResult.Success(ListLines.Length), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ListWithOtherUntaggedLines_WritesOnlyTheListResponses()
    {
        // Measured: LIST=* LIST () "/" A\r\n* OK noise\r\n* 3 EXISTS\r\n* 4 LIST x\r\nOK done.
        ListRun run = await RunAsync(Host, Opening + "* LIST () \"/\" A\r\n* OK noise\r\n* 3 EXISTS\r\n* 4 LIST x\r\n* LISTX y\r\n* list z\nA002 OK done\r\n");

        Assert.AreEqual("* LIST () \"/\" A\r\n* 4 LIST x\r\n* list z\n", run.Output);
    }

    [TestMethod]
    [DataRow("A002 NO nope\r\n", DisplayName = "LIST=NO nope (measured)")]
    [DataRow("A002 BAD what\r\n", DisplayName = "BAD")]
    public async Task ExecuteAsync_ListNotOk_FailsWithExit21AfterLogout(string completion)
    {
        ListRun run = await RunAsync(Host, Opening + "* LIST () \"/\" A\r\n" + completion);

        Assert.AreEqual("A001 CAPABILITY\r\nA002 LIST \"\" *\r\nA003 LOGOUT\r\n", run.Sent);
        Assert.AreEqual("* LIST () \"/\" A\r\n", run.Output);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.QuoteError, QuoteError, 17), run.Result);
    }

    [TestMethod]
    [DataRow("INBOX?NEW", "SEARCH NEW", DisplayName = "INBOX?NEW (measured)")]
    [DataRow("INBOX?SUBJECT%20hi", "SEARCH SUBJECT hi", DisplayName = "INBOX?SUBJECT%20hi decoded (measured)")]
    [DataRow("INBOX?SUBJECT%C3%A9", "SEARCH SUBJECT\u00C3\u00A9", DisplayName = "INBOX?SUBJECT%C3%A9 sent as the bytes (measured)")]
    [DataRow("INBOX;SECTION=TEXT?ALL", "SEARCH ALL", DisplayName = "a SECTION does not stop the search")]
    public async Task ExecuteAsync_MailboxAndQuery_SelectsSearchesAndWritesTheSearchResponse(string path, string search)
    {
        ListRun run = await RunAsync(Host + path, Opening + SelectReply + "* SEARCH 1 2\r\nA003 OK SEARCH completed\r\n");

        Assert.AreEqual(SentThroughSelect + $"A003 {search}\r\nA004 LOGOUT\r\n", run.Sent);
        Assert.AreEqual("* SEARCH 1 2\r\n", run.Output);
        Assert.AreEqual(TransferResult.Success(14), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SearchWithOtherUntaggedLines_WritesOnlyTheSearchResponses()
    {
        // Measured: SEARCH=* 1 EXISTS\r\n* SEARCH 5\r\n* search\r\nOK done.
        ListRun run = await RunAsync(Host + "INBOX?NEW", Opening + SelectReply + "* 1 EXISTS\r\n* SEARCH 5\r\n* search\r\nA003 OK done\r\n");

        Assert.AreEqual("* SEARCH 5\r\n* search\r\n", run.Output);
    }

    [TestMethod]
    public async Task ExecuteAsync_SearchNotOk_FailsWithExit21AfterLogout()
    {
        // Measured: SEARCH=BAD Nope: empty stdout, LOGOUT, exit 21.
        ListRun run = await RunAsync(Host + "INBOX?NEW", Opening + SelectReply + "A003 BAD Nope\r\n");

        Assert.AreEqual(SentThroughSelect + "A003 SEARCH NEW\r\nA004 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(string.Empty, run.Output);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.QuoteError, QuoteError), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_QueryWithAUid_FetchesInstead()
    {
        ListRun run = await RunAsync(Host + "INBOX;UID=1?NEW", Opening + SelectReply + "* 1 FETCH (BODY[] {2}\r\nhi)\r\nA003 OK done\r\n");

        Assert.AreEqual(SentThroughSelect + "A003 UID FETCH 1 BODY[]\r\nA004 LOGOUT\r\n", run.Sent);
        Assert.AreEqual("hi", run.Output);
    }

    [TestMethod]
    public async Task ExecuteAsync_SelectNotOkBeforeACustomCommand_FailsWithExit67AfterLogout()
    {
        // Measured: -X NOOP imap://h/INBOX, SELECT=NO nope: LOGOUT, exit 67.
        ListRun run = await RunAsync(Custom(Host + "INBOX", "NOOP"), Opening + "A002 NO nope\r\n");

        Assert.AreEqual(SentThroughSelect + "A003 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Select failed"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_CustomCommandWithoutMailbox_IsSentAndEveryUntaggedLineOfAnExamineWritten()
    {
        // Measured: -X "EXAMINE INBOX" imap://h/: every EXAMINE line written, exit 0.
        ListRun run = await RunAsync(Custom(Host, "EXAMINE INBOX"), Opening + SelectLines + "A002 OK [READ-ONLY] EXAMINE completed\r\n");

        Assert.AreEqual("A001 CAPABILITY\r\nA002 EXAMINE INBOX\r\nA003 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(SelectLines, run.Output);
        Assert.AreEqual(TransferResult.Success(SelectLines.Length), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_CustomCommandWithMailbox_SelectsThenSendsIt()
    {
        // Measured: -X "STORE 1 +FLAGS \Seen" imap://h/INBOX, STORE=* 1 FETCH (FLAGS (\Seen))\r\n* 2 EXISTS\r\n* STORE x\r\nOK STORE completed.
        ListRun run = await RunAsync(
            Custom(Host + "INBOX", "STORE 1 +FLAGS \\Seen"),
            Opening + SelectReply + "* 1 FETCH (FLAGS (\\Seen))\r\n* 2 EXISTS\r\n* STORE x\r\nA003 OK STORE completed\r\n");

        Assert.AreEqual(SentThroughSelect + "A003 STORE 1 +FLAGS \\Seen\r\nA004 LOGOUT\r\n", run.Sent);
        Assert.AreEqual("* 1 FETCH (FLAGS (\\Seen))\r\n* STORE x\r\n", run.Output);
    }

    [TestMethod]
    public async Task ExecuteAsync_CustomCommandWithMailboxAndQuery_SendsTheCommandNotSearch()
    {
        ListRun run = await RunAsync(Custom(Host + "INBOX?NEW", "NOOP"), Opening + SelectReply + "A003 OK done\r\n");

        Assert.AreEqual(SentThroughSelect + "A003 NOOP\r\nA004 LOGOUT\r\n", run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_CustomCommandNotOk_FailsWithExit21AfterLogout()
    {
        // Measured: -X "STORE 1 +FLAGS \Seen" imap://h/INBOX answered BAD: LOGOUT, exit 21.
        ListRun run = await RunAsync(Custom(Host + "INBOX", "STORE 1 +FLAGS \\Seen"), Opening + SelectReply + "A003 BAD Command not recognized\r\n");

        Assert.AreEqual(SentThroughSelect + "A003 STORE 1 +FLAGS \\Seen\r\nA004 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.QuoteError, QuoteError), run.Result);
    }

    [TestMethod]
    [DataRow("NAMESPACE", "* NAMESPACE ((\"\" \"/\")) NIL NIL\r\n* OTHER x\r\n* namespace\r\n* NAMESPACEX y\r\n", "* NAMESPACE ((\"\" \"/\")) NIL NIL\r\n* namespace\r\n", DisplayName = "NAMESPACE its own responses in any case (measured)")]
    [DataRow("FOO%20BAR", "* FOO a\r\n* BAR b\r\n", "* FOO a\r\n", DisplayName = "FOO%20BAR decoded, FOO its name (measured)")]
    [DataRow("NOOP", "* 3 EXISTS\r\n* FOO\r\n", "* 3 EXISTS\r\n* FOO\r\n", DisplayName = "NOOP every response (measured)")]
    [DataRow("store 1 +FLAGS x", "* 1 FETCH (FLAGS ())\r\n* 3 EXISTS\r\n", "* 1 FETCH (FLAGS ())\r\n", DisplayName = "store in lower case with FETCH (measured)")]
    [DataRow("UID FETCH 1 FLAGS", "* 1 FETCH (UID 1)\r\n* 3 EXISTS\r\n", "* 1 FETCH (UID 1)\r\n* 3 EXISTS\r\n", DisplayName = "UID every response (measured)")]
    [DataRow("LSUB \"\" *", "* LSUB () \"/\" A\r\n* 3 EXISTS\r\n", "* LSUB () \"/\" A\r\n* 3 EXISTS\r\n", DisplayName = "LSUB every response (measured)")]
    [DataRow("select INBOX", "* 3 EXISTS\r\n", "* 3 EXISTS\r\n", DisplayName = "select every response")]
    [DataRow("SEARCH ALL", "* 3 EXISTS\r\n", "* 3 EXISTS\r\n", DisplayName = "SEARCH every response")]
    [DataRow("EXPUNGE", "* 3 EXPUNGE\r\n* 2 EXISTS\r\n", "* 3 EXPUNGE\r\n* 2 EXISTS\r\n", DisplayName = "EXPUNGE every response")]
    [DataRow("GETQUOTAROOT INBOX", "* QUOTA x\r\n", "* QUOTA x\r\n", DisplayName = "GETQUOTAROOT every response")]
    [DataRow("FETCH 1 FLAGS", "* 1 FETCH (FLAGS ())\r\n* 3 EXISTS\r\n", "* 1 FETCH (FLAGS ())\r\n", DisplayName = "FETCH its own responses")]
    public async Task ExecuteAsync_CustomCommand_WritesTheUntaggedResponsesCurlWrites(string command, string untagged, string written)
    {
        ListRun run = await RunAsync(Custom(Host, command), Opening + untagged + "A002 OK done\r\n");

        Assert.AreEqual(written, run.Output);
        Assert.AreEqual(TransferResult.Success(written.Length), run.Result);
    }

    [TestMethod]
    [DataRow("FOO%0ABAR", "", DisplayName = "-X FOO%0ABAR (measured)")]
    [DataRow("NOOP", "INBOX%09", DisplayName = "a malformed path with a command")]
    public async Task ExecuteAsync_CustomCommandOrPathDecodingToAControlByte_FailsWithExit3AfterLogout(string command, string path)
    {
        ListRun run = await RunAsync(Custom(Host + path, command), Opening + "* BYE\r\nA002 OK bye\r\n");

        Assert.AreEqual("A001 CAPABILITY\r\nA002 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UrlMalformat, MalformedUrl), run.Result);
    }

    [TestMethod]
    [DataRow("* LIST () \"/\" {5}\r\nINBOX\r\n* LIST () \"/\" B\r\n", "* LIST () \"/\" {5}\r\nINBOX", DisplayName = "{5} at the end (measured)")]
    [DataRow("* LIST () \"/\" {3}x\r\nabc\r\n* LIST () \"/\" B\r\n", "* LIST () \"/\" {3}x\r\nabc", DisplayName = "{3} before more text (measured)")]
    [DataRow("* LIST () x{3}\r\nabc\r\n* LIST () \"/\" B\r\n", "* LIST () x{3}\r\nabc", DisplayName = "{3} after a letter (measured)")]
    [DataRow("* LIST () {3} \"{4}\"\r\nabc\r\n* LIST () \"/\" B\r\n", "* LIST () {3} \"{4}\"\r\nabc", DisplayName = "the first brace outside quotes (measured)")]
    [DataRow("* LIST () \"a\" {3}\r\nabc\r\n* LIST () \"/\" B\r\n", "* LIST () \"a\" {3}\r\nabc", DisplayName = "after a quoted string (measured)")]
    [DataRow("* LIST () {0}\r\n* LIST () \"/\" B\r\n", "* LIST () {0}\r\n", DisplayName = "{0} ends the listing (measured)")]
    [DataRow("* OTHER {3}\r\nabc\r\n* LIST () \"/\" B\r\n", "* LIST () \"/\" B\r\n", DisplayName = "a literal in a line not written is no literal (measured)")]
    public async Task ExecuteAsync_ListResponseAnnouncingALiteral_WritesItAndLogsOutWithTheRestUnread(string replies, string written)
    {
        ListRun run = await RunAsync(Host, Opening + replies + "A002 OK done\r\n");

        Assert.AreEqual("A001 CAPABILITY\r\nA002 LIST \"\" *\r\nA003 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(written, run.Output);
        Assert.AreEqual(TransferResult.Success(written.Length), run.Result);
    }

    [TestMethod]
    [DataRow("* LIST () \"{3}\" A\r\n", DisplayName = "{3} in quotes (measured)")]
    [DataRow("* LIST () \"{3}\"x\r\n", DisplayName = "{3} in quotes before a letter (measured)")]
    [DataRow("* LIST () \"a {3}\"\r\n", DisplayName = "{3} after a space in quotes (measured)")]
    [DataRow("* LIST () \"a\\\"{3}\" x\r\n", DisplayName = "an escaped quote keeps the string open (measured)")]
    [DataRow("* LIST () {x} {3}\r\n", DisplayName = "only the first brace is read (measured)")]
    [DataRow("* LIST () {3x\r\n", DisplayName = "no closing brace after the digits")]
    [DataRow("* LIST () {12", DisplayName = "digits to the end of the line")]
    [DataRow("* LIST () {99999999999999999999}\r\n", DisplayName = "a size past 63 bits")]
    public async Task ExecuteAsync_ListResponseWithoutALiteral_IsWrittenAndTheListingGoesOn(string line)
    {
        string replies = line + (line.EndsWith('\n') ? string.Empty : "\n") + "* LIST () \"/\" B\r\n";

        ListRun run = await RunAsync(Host, Opening + replies + "A002 OK done\r\n");

        Assert.AreEqual(replies.Length, run.Output.Length);
        Assert.AreEqual(TransferResult.Success(replies.Length), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SearchResponseAnnouncingALiteral_WritesIt()
    {
        // Measured: INBOX?ALL, SEARCH=* SEARCH {3}\r\n1 2\r\n* SEARCH 4\r\nOK done.
        ListRun run = await RunAsync(Host + "INBOX?ALL", Opening + SelectReply + "* SEARCH {3}\r\n1 2\r\n* SEARCH 4\r\nA003 OK done\r\n");

        Assert.AreEqual("* SEARCH {3}\r\n1 2", run.Output);
        Assert.AreEqual(SentThroughSelect + "A003 SEARCH ALL\r\nA004 LOGOUT\r\n", run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerClosesInsideAListedLiteral_FailsWithExit18WithoutLogout()
    {
        ListRun run = await RunAsync(Host, Opening + "* LIST () {10}\r\nabc");

        Assert.AreEqual("A001 CAPABILITY\r\nA002 LIST \"\" *\r\n", run.Sent);
        Assert.AreEqual("* LIST () {10}\r\nabc", run.Output);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.PartialFile, "end of response with 7 bytes missing", 19), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerClosesBeforeTheListCompletes_FailsWithExit56WithoutLogout()
    {
        ListRun run = await RunAsync(Host, Opening + "* LIST () \"/\" A\r\n");

        Assert.AreEqual("A001 CAPABILITY\r\nA002 LIST \"\" *\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RecvError, "response reading failed (errno: 0)"), run.Result);
    }

    [TestMethod]
    [DataRow("* LIST () \"/\" A\r\nA002 OK done\r\n", "passed 17 returned 4", DisplayName = "writing a listed line")]
    [DataRow("* LIST () {3}\r\nabcA002 OK done\r\n", "passed 15 returned 4", DisplayName = "writing the line before a literal")]
    public async Task ExecuteAsync_OutputFails_FailsWithExit23WithoutLogout(string replies, string counts)
    {
        var context = Context(Host, output: new FailingOutputStream(new OutputWriteFailedException(4, "disk full")));

        ListRun run = await RunAsync(context, Opening + replies);

        Assert.AreEqual("A001 CAPABILITY\r\nA002 LIST \"\" *\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.WriteError, "Failure writing output to destination, " + counts), run.Result);
    }

    private static TransferContext Custom(string url, string command) =>
        Context(url, mail: new MailRequestOptions { CustomCommand = command });

    private static TransferContext Context(string url, Stream? output = null, MailRequestOptions? mail = null) =>
        new()
        {
            Url = CurlUrl.Parse(url),
            Output = output ?? new MemoryStream(),
            Mail = mail,
            Progress = new RecordingTransferProgress(),
        };

    private static Task<ListRun> RunAsync(string url, string replies) => RunAsync(Context(url), replies);

    private static async Task<ListRun> RunAsync(TransferContext context, string replies)
    {
        ImapRun run = await ImapRun.ExecuteAsync(context, new ScriptedConnection(Encoding.Latin1.GetBytes(replies)));
        string output = context.Output is MemoryStream memory ? Encoding.Latin1.GetString(memory.ToArray()) : string.Empty;
        return new ListRun(run.Result, run.Sent, output);
    }

    /// <summary>What a listing did: its result, the bytes sent and the output written.</summary>
    private sealed record ListRun(TransferResult Result, string Sent, string Output);
}
