using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Imap.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Imap;

/// <summary>
/// Pins how an IMAP transfer meets <c>-I</c> (<see cref="ITransferContext.NoBody" />) and
/// <c>--max-filesize</c> (<see cref="ITransferContext.MaxFileSize" />) against curl 8.21.0
/// (mingw, Schannel), whose <c>cw_download_write</c> sees the <c>FETCH</c> literal and the
/// listed responses as body bytes (BL-1311). Measured on 2026-10-03 with
/// <c>Record-CurlExchange.ps1 -Imap</c>, curl running <c>-sv</c> as <c>u:p</c> against the
/// recorder's default replies; these runs log in as nobody, so <c>CAPABILITY</c> is followed
/// by the command curl sent after logging in.
/// </summary>
[TestClass]
public sealed class ImapProtocolHandlerBodyLimitTests
{
    private const string Host = "imap://127.0.0.1:18143/";

    private const string Opening = "* OK [CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN] ready\r\n"
        + "* CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN\r\nA001 OK CAPABILITY completed\r\n";

    private const string SelectReply = "* FLAGS (\\Answered \\Flagged \\Deleted \\Seen \\Draft)\r\n* 2 EXISTS\r\n* 0 RECENT\r\n"
        + "* OK [UIDVALIDITY 1] UIDs valid\r\n* OK [UIDNEXT 3] Predicted next UID\r\nA002 OK [READ-WRITE] SELECT completed\r\n";

    /// <summary>The recorder's default message: 100 bytes.</summary>
    private const string Message = "From: sender@example.com\r\nTo: recipient@example.com\r\nSubject: Recorded\r\n\r\nHello from the recorder.\r\n";

    private const string FetchReply = "* 1 FETCH (UID 1 BODY[] {100}\r\n" + Message + ")\r\nA003 OK FETCH completed\r\n";

    private const string ListLine = "* LIST (\\HasNoChildren) \"/\" INBOX\r\n";

    private const string ListReply = ListLine + "* LIST (\\HasNoChildren) \"/\" Sent\r\nA002 OK LIST completed\r\n";

    private const string LogoutReply = "* BYE Logging out\r\nA004 OK LOGOUT completed\r\n";

    private const string SentThroughSelect = "A001 CAPABILITY\r\nA002 SELECT INBOX\r\n";

    private const string ShuttingDown = "* shutting down connection #0";

    private const string WeirdServerReply = "Weird server reply";

    /// <summary>Gets or sets the running test's context, which carries its diagnostics.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_NoBodyOnAUidFetch_FailsWithExit8AtTheLiteralWithoutLogout()
    {
        // Measured: -sv -I .../INBOX;UID=1: exit 8, stdout empty, stderr ending Found 100
        // bytes, { [100 bytes data], shutting down connection #0; no LOGOUT sent.
        BodyRun run = await RunAsync(Context(Host + "INBOX;UID=1", noBody: true), Opening + SelectReply + FetchReply + LogoutReply);

        Diagnostics.Diff("sent", SentThroughSelect + "A003 UID FETCH 1 BODY[]\r\n", run.Sent);
        Assert.AreEqual(SentThroughSelect + "A003 UID FETCH 1 BODY[]\r\n", run.Sent);
        Diagnostics.Assert("result", TransferResult.Failure(CurlExitCode.WeirdServerReply, WeirdServerReply), run.Result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.WeirdServerReply, WeirdServerReply), run.Result);
        Diagnostics.Diff("output", string.Empty, run.Output);
        Assert.AreEqual(string.Empty, run.Output);
        CollectionAssert.AreEqual(
            (string[])["< * 1 FETCH (UID 1 BODY[] {100}\r\n", "* Found 100 bytes to download", "{ 100", ShuttingDown],
            run.Events.Transcript.TakeLast(4).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_NoBodyOnAList_FailsWithExit8AtTheFirstLineAfterLogout()
    {
        // Measured: -sv -I .../: exit 8, stdout empty, stderr ending with the first LIST
        // line, { [35 bytes data], shutting down connection #0; LOGOUT sent, not shown.
        BodyRun run = await RunAsync(Context(Host, noBody: true), Opening + ListReply + "* BYE\r\nA003 OK LOGOUT completed\r\n");

        Diagnostics.Diff("sent", "A001 CAPABILITY\r\nA002 LIST \"\" *\r\nA003 LOGOUT\r\n", run.Sent);
        Assert.AreEqual("A001 CAPABILITY\r\nA002 LIST \"\" *\r\nA003 LOGOUT\r\n", run.Sent);
        Diagnostics.Assert("result", TransferResult.Failure(CurlExitCode.WeirdServerReply, WeirdServerReply), run.Result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.WeirdServerReply, WeirdServerReply), run.Result);
        Diagnostics.Diff("output", string.Empty, run.Output);
        Assert.AreEqual(string.Empty, run.Output);
        CollectionAssert.AreEqual(
            (string[])["< " + ListLine, "{ 35", ShuttingDown],
            run.Events.Transcript.TakeLast(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_NoBodyOnASearch_FailsWithExit8AtTheSearchLineAfterLogout()
    {
        // Measured: -sv -I .../INBOX?ALL: exit 8, stderr ending * SEARCH 1 2,
        // { [14 bytes data], shutting down connection #0; LOGOUT sent.
        BodyRun run = await RunAsync(
            Context(Host + "INBOX?ALL", noBody: true),
            Opening + SelectReply + "* SEARCH 1 2\r\nA003 OK SEARCH completed\r\n" + LogoutReply);

        Diagnostics.Diff("sent", SentThroughSelect + "A003 SEARCH ALL\r\nA004 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(SentThroughSelect + "A003 SEARCH ALL\r\nA004 LOGOUT\r\n", run.Sent);
        Diagnostics.Assert("result", TransferResult.Failure(CurlExitCode.WeirdServerReply, WeirdServerReply), run.Result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.WeirdServerReply, WeirdServerReply), run.Result);
        Diagnostics.Diff("output", string.Empty, run.Output);
        Assert.AreEqual(string.Empty, run.Output);
        CollectionAssert.AreEqual(
            (string[])["< * SEARCH 1 2\r\n", "{ 14", ShuttingDown],
            run.Events.Transcript.TakeLast(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_UidFetchPastMaxFileSize_WritesTheAllowedBytesAndFailsWithExit63WithoutLogout()
    {
        // Measured: -sv --max-filesize 3 .../INBOX;UID=1: exit 63, stdout Fro, stderr ending
        // { [100 bytes data], the message, shutting down connection #0; no LOGOUT sent.
        const string Exceeded = "Exceeded the maximum allowed file size (3) with 3 bytes";
        BodyRun run = await RunAsync(Context(Host + "INBOX;UID=1", maxFileSize: 3), Opening + SelectReply + FetchReply + LogoutReply);

        Diagnostics.Diff("sent", SentThroughSelect + "A003 UID FETCH 1 BODY[]\r\n", run.Sent);
        Assert.AreEqual(SentThroughSelect + "A003 UID FETCH 1 BODY[]\r\n", run.Sent);
        Diagnostics.Assert("result", TransferResult.Failure(CurlExitCode.FilesizeExceeded, Exceeded, 3), run.Result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FilesizeExceeded, Exceeded, 3), run.Result);
        Diagnostics.Diff("output", "Fro", run.Output);
        Assert.AreEqual("Fro", run.Output);
        CollectionAssert.AreEqual(
            (string[])["* Found 100 bytes to download", "{ 100", "* " + Exceeded, ShuttingDown],
            run.Events.Transcript.TakeLast(4).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_ListPastMaxFileSize_WritesTheFirstLineWholeCutsTheSecondAndLogsOut()
    {
        // Measured: -sv --max-filesize 40 .../: exit 63, stdout the first LIST line and
        // "* LIS", { [34 bytes data], the message, shutting down connection #0; LOGOUT sent.
        const string Exceeded = "Exceeded the maximum allowed file size (40) with 40 bytes";
        BodyRun run = await RunAsync(Context(Host, maxFileSize: 40), Opening + ListReply + "* BYE\r\nA003 OK LOGOUT completed\r\n");

        Diagnostics.Diff("sent", "A001 CAPABILITY\r\nA002 LIST \"\" *\r\nA003 LOGOUT\r\n", run.Sent);
        Assert.AreEqual("A001 CAPABILITY\r\nA002 LIST \"\" *\r\nA003 LOGOUT\r\n", run.Sent);
        Diagnostics.Assert("result", TransferResult.Failure(CurlExitCode.FilesizeExceeded, Exceeded, 40), run.Result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FilesizeExceeded, Exceeded, 40), run.Result);
        Diagnostics.Diff("output", "* LIST (\\HasNoChildren) \"/\" INBOX\r\n* LIS", run.Output);
        Assert.AreEqual("* LIST (\\HasNoChildren) \"/\" INBOX\r\n* LIS", run.Output);
        CollectionAssert.AreEqual(
            (string[])["{ 34", "* " + Exceeded, ShuttingDown],
            run.Events.Transcript.TakeLast(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_ListedLiteralPastMaxFileSize_CutsTheLiteralAndLogsOut()
    {
        BodyRun run = await RunAsync(
            Context(Host + "INBOX", maxFileSize: 32, customCommand: "FETCH 1 BODY[]"),
            Opening + SelectReply + FetchReply + LogoutReply);

        Diagnostics.Diff("sent", SentThroughSelect + "A003 FETCH 1 BODY[]\r\nA004 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(SentThroughSelect + "A003 FETCH 1 BODY[]\r\nA004 LOGOUT\r\n", run.Sent);
        Diagnostics.Assert("exit code", CurlExitCode.FilesizeExceeded, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.FilesizeExceeded, run.Result.ExitCode);
        Diagnostics.Diff("output", "* 1 FETCH (UID 1 BODY[] {100}\r\nF", run.Output);
        Assert.AreEqual("* 1 FETCH (UID 1 BODY[] {100}\r\nF", run.Output);
    }

    [TestMethod]
    [DataRow(0L, DisplayName = "0 is no limit")]
    [DataRow(null, DisplayName = "no --max-filesize")]
    [DataRow(100L, DisplayName = "exactly the 100-byte message")]
    public async Task ExecuteAsync_UidFetchWithinMaxFileSize_WritesTheWholeMessage(long? maxFileSize)
    {
        BodyRun run = await RunAsync(Context(Host + "INBOX;UID=1", maxFileSize: maxFileSize), Opening + SelectReply + FetchReply + LogoutReply);

        Diagnostics.Diff("sent", SentThroughSelect + "A003 UID FETCH 1 BODY[]\r\nA004 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(SentThroughSelect + "A003 UID FETCH 1 BODY[]\r\nA004 LOGOUT\r\n", run.Sent);
        Diagnostics.Assert("result", TransferResult.Success(100), run.Result);
        Assert.AreEqual(TransferResult.Success(100), run.Result);
        Diagnostics.Diff("output", Message, run.Output);
        Assert.AreEqual(Message, run.Output);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadUnderNoBodyAndMaxFileSize_AppendsAsWithout()
    {
        TransferContext context = Context(Host + "INBOX", noBody: true, maxFileSize: 1, upload: "hi\r\n");

        BodyRun run = await RunAsync(context, Opening + "+ Ready\r\nA002 OK APPEND completed\r\n* BYE\r\nA003 OK LOGOUT completed\r\n");

        Diagnostics.Diff("sent", "A001 CAPABILITY\r\nA002 APPEND INBOX {4}\r\nhi\r\n\r\nA003 LOGOUT\r\n", run.Sent);
        Assert.AreEqual("A001 CAPABILITY\r\nA002 APPEND INBOX {4}\r\nhi\r\n\r\nA003 LOGOUT\r\n", run.Sent);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        Diagnostics.Diff("output", string.Empty, run.Output);
        Assert.AreEqual(string.Empty, run.Output);
    }

    private static TransferContext Context(string url, bool noBody = false, long? maxFileSize = null, string? customCommand = null, string? upload = null) =>
        new()
        {
            Upload = upload is null ? null : new MemoryStream(Encoding.Latin1.GetBytes(upload)),
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
            Events = new RecordingTransferEvents(),
            NoBody = noBody,
            MaxFileSize = maxFileSize,
            Mail = customCommand is null ? null : new MailRequestOptions { CustomCommand = customCommand },
        };

    private async Task<BodyRun> RunAsync(TransferContext context, string replies)
    {
        Diagnostics.Arrange("url", context.Url);
        Diagnostics.Arrange("server", DiagnosticText.Escape(replies));
        Diagnostics.Arrange("no body", context.NoBody);
        Diagnostics.Arrange("max file size", context.MaxFileSize);
        ImapRun run = await ImapRun.ExecuteAsync(context, new ScriptedConnection(Encoding.Latin1.GetBytes(replies)));
        string output = Encoding.Latin1.GetString(((MemoryStream)context.Output).ToArray());
        Diagnostics.Act("result", DiagnosticText.Result(run.Result));
        Diagnostics.Act("sent", DiagnosticText.Escape(run.Sent));
        Diagnostics.Act("output", DiagnosticText.Escape(output));
        Diagnostics.Act("transcript", DiagnosticText.Lines(((RecordingTransferEvents)context.Events).Transcript));
        return new BodyRun(run.Result, run.Sent, output, (RecordingTransferEvents)context.Events);
    }

    private sealed record BodyRun(TransferResult Result, string Sent, string Output, RecordingTransferEvents Events);
}
