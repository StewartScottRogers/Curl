using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Imap.Fakes;

namespace Curl.Protocol.Imap;

/// <summary>
/// Pins what an IMAP transfer reports for <c>-v</c> and <c>--trace</c> against curl 8.21.0
/// (mingw, Schannel), recorded on 2026-09-28 with <c>Record-CurlExchange.ps1 -Imap</c>
/// (BL-559 Notes): each command as a request header with its CRLF, each response line as a
/// response header with its line end, a literal's pieces as data, the <c>Found</c>,
/// <c>Written</c> and upload lines, <c>LOGOUT</c> not at all, and the closing lines.
/// </summary>
[TestClass]
public sealed class ImapProtocolHandlerEventTests
{
    private const string Host = "imap://127.0.0.1:18143/";

    private const string Greeting = "* OK ready\r\n";

    private const string CapabilityReply = "* CAPABILITY IMAP4rev1\r\nA001 OK done\r\n";

    private const string LogoutReply = "* BYE Logging out\r\nA005 OK LOGOUT completed\r\n";

    private const string LeftIntact = "* Connection #0 to host 127.0.0.1:18143 left intact";

    private static readonly string[] LoggedIn =
    [
        "< * OK ready\r\n", "> A001 CAPABILITY\r\n", "< * CAPABILITY IMAP4rev1\r\n", "< A001 OK done\r\n",
        "> A002 LOGIN user secret\r\n", "< A002 OK LOGIN completed\r\n",
    ];

    private static readonly string[] Selected =
    [
        .. LoggedIn, "> A003 SELECT INBOX\r\n", "< A003 OK [READ-WRITE] SELECT completed\r\n", "> A004 UID FETCH 1 BODY[]\r\n",
    ];

    [TestMethod]
    public async Task ExecuteAsync_UidFetchWithLogin_ReportsTheSessionThePasswordUnmaskedAndTheLiteral()
    {
        RecordingTransferEvents events = await RunFetchAsync("* 1 FETCH (UID 1 BODY[] {5}\r\nhello)\r\nA004 OK FETCH completed\r\n", LogoutReply);

        CollectionAssert.AreEqual(
            (string[])[
                .. Selected, "< * 1 FETCH (UID 1 BODY[] {5}\r\n",
                "* Found 5 bytes to download", "{ 5", "* Written 5 bytes, 0 bytes are left for transfer",
                "< )\r\n", "< A004 OK FETCH completed\r\n", LeftIntact,
            ],
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_LiteralSplitAcrossReads_WritesTheWrittenLineOnlyForThePieceThatCameWithTheResponse()
    {
        RecordingTransferEvents events = await RunFetchAsync("* 1 FETCH (UID 1 BODY[] {5}\r\nhel", "lo", ")\r\nA004 OK FETCH completed\r\n", LogoutReply);

        CollectionAssert.AreEqual(
            (string[])[
                "* Found 5 bytes to download", "{ 3", "* Written 3 bytes, 2 bytes are left for transfer", "{ 2",
                "< )\r\n", "< A004 OK FETCH completed\r\n", LeftIntact,
            ],
            events.Transcript.Skip(Selected.Length + 1).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_LiteralCutShort_ReportsTheEmptyPieceAndClosesTheConnection()
    {
        // Measured with UID FETCH=* 1 FETCH (UID 1 BODY[] {100}\r\nhello and the server
        // hanging up: { [12 bytes data], Written 12 bytes, 88 left, { [0 bytes data].
        RecordingTransferEvents events = await RunFetchAsync("* 1 FETCH (UID 1 BODY[] {5}\r\nhel");

        CollectionAssert.AreEqual(
            (string[])[
                "* Found 5 bytes to download", "{ 3", "* Written 3 bytes, 2 bytes are left for transfer", "{ 0",
                "* end of response with 2 bytes missing", "* closing connection #0",
            ],
            events.Transcript.Skip(Selected.Length + 1).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_FetchCompletionNotOk_LeavesTheConnectionIntactWithNoMessage()
    {
        RecordingTransferEvents events = await RunFetchAsync("* 1 FETCH (UID 1 BODY[] {5}\r\nhello)\r\nA004 NO odd\r\n", LogoutReply);

        CollectionAssert.AreEqual((string[])["< A004 NO odd\r\n", LeftIntact], events.Transcript.TakeLast(2).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_FetchNo_ShutsDownTheConnectionWithNoMessage()
    {
        RecordingTransferEvents events = await RunFetchAsync("A004 NO gone\r\n", LogoutReply);

        CollectionAssert.AreEqual((string[])[.. Selected, "< A004 NO gone\r\n", "* shutting down connection #0"], events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerHangsUpAfterTheFetch_WritesTheReadFailureAndShutsDown()
    {
        RecordingTransferEvents events = await RunFetchAsync();

        CollectionAssert.AreEqual(
            (string[])[.. Selected, "* response reading failed (errno: 0)", "* shutting down connection #0"],
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_SelectNo_WritesSelectFailedAndShutsDown()
    {
        var events = new RecordingTransferEvents();
        await ImapRun.ExecuteAsync(
            Context(Host + "INBOX;UID=1", events, withUser: true),
            Connection(Greeting, CapabilityReply, "A002 OK LOGIN completed\r\n", "A003 NO no such mailbox\r\n", LogoutReply));

        CollectionAssert.AreEqual(
            (string[])[
                .. LoggedIn, "> A003 SELECT INBOX\r\n", "< A003 NO no such mailbox\r\n",
                "* Select failed", "* shutting down connection #0",
            ],
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_LoginNo_WritesAccessDeniedAndClosesTheConnection()
    {
        var events = new RecordingTransferEvents();
        await ImapRun.ExecuteAsync(
            Context(Host + "INBOX;UID=1", events, withUser: true),
            Connection(Greeting, CapabilityReply, "A002 NO denied\r\n", LogoutReply));

        CollectionAssert.AreEqual(
            (string[])["< A002 NO denied\r\n", "* Access denied. \u0002", "* closing connection #0"],
            events.Transcript.TakeLast(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_ByeGreeting_WritesTheUnexpectedResponseAndClosesTheConnection()
    {
        var events = new RecordingTransferEvents();
        await ImapRun.ExecuteAsync(Context(Host, events), Connection("* BYE go away\r\n"));

        CollectionAssert.AreEqual(
            (string[])["< * BYE go away\r\n", "* Got unexpected imap-server response", "* closing connection #0"],
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_PreauthGreeting_SaysTheConnectionIsAlreadyAuthenticated()
    {
        var events = new RecordingTransferEvents();
        await ImapRun.ExecuteAsync(
            Context(Host, events, withUser: true),
            Connection("* PREAUTH welcome\r\n", CapabilityReply, "A002 OK LIST completed\r\n", LogoutReply));

        CollectionAssert.AreEqual(
            (string[])["< * PREAUTH welcome\r\n", "* PREAUTH connection, already authenticated", "> A001 CAPABILITY\r\n"],
            events.Transcript.Take(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_CustomFetch_ReportsTheLineAndTheLiteralAsDataWithNoWrittenLine()
    {
        // Measured with -X "FETCH 1 BODY[]": Found, the untagged line as data, the literal as
        // data, then left intact; ")" and the completion are never read.
        var events = new RecordingTransferEvents();
        var progress = new RecordingTransferProgress();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Host),
            Output = new MemoryStream(),
            Events = events,
            Mail = new MailRequestOptions { CustomCommand = "FETCH 1 BODY[]" },
            Progress = progress,
        };

        ImapRun run = await ImapRun.ExecuteAsync(context, Connection(Greeting, CapabilityReply, "* 1 FETCH (BODY[] {5}\r\nhello)\r\nA002 OK done\r\n", LogoutReply));

        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        CollectionAssert.AreEqual(
            (string[])["> A002 FETCH 1 BODY[]\r\n", "< * 1 FETCH (BODY[] {5}\r\n", "* Found 5 bytes to download", "{ 23", "{ 5", LeftIntact],
            events.Transcript.Skip(4).ToArray());
        Assert.IsFalse(progress.Reports.Any(report => report.StartsWith("down", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow("Subject: hi\r\n\r\nbody\r\n", "* upload completely sent off: 21 bytes", DisplayName = "a 21-byte message (measured with 37)")]
    [DataRow("", "* Request completely sent off", DisplayName = "an empty message (measured)")]
    public async Task ExecuteAsync_Append_ReportsTheLiteralSentAndTheLineEndAfterIt(string message, string sentLine)
    {
        var events = new RecordingTransferEvents();
        TransferContext context = AppendContext(events, message);

        await ImapRun.ExecuteAsync(context, Connection(Greeting, CapabilityReply, "+ Ready for literal data\r\n", "A002 OK APPEND completed\r\n", LogoutReply));

        string[] literal = message.Length == 0 ? [] : [$"}} {message.Length}"];
        CollectionAssert.AreEqual(
            (string[])[
                $"> A002 APPEND Sent (\\Seen) {{{message.Length}}}\r\n", "< + Ready for literal data\r\n",
                .. literal, sentLine, "> \r\n", "< A002 OK APPEND completed\r\n", LeftIntact,
            ],
            events.Transcript.Skip(4).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_AppendNoOnceTheMessageIsSent_LeavesTheConnectionIntact()
    {
        var events = new RecordingTransferEvents();
        TransferContext context = AppendContext(events, "hi\r\n");

        ImapRun run = await ImapRun.ExecuteAsync(context, Connection(Greeting, CapabilityReply, "+ Ready for literal data\r\n", "A002 NO full\r\n", LogoutReply));

        Assert.AreEqual(CurlExitCode.UploadFailed, run.Result.ExitCode);
        CollectionAssert.AreEqual((string[])["< A002 NO full\r\n", LeftIntact], events.Transcript.TakeLast(2).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_AppendNoBeforeTheContinuation_ShutsDownTheConnection()
    {
        var events = new RecordingTransferEvents();
        TransferContext context = AppendContext(events, "hi\r\n");

        await ImapRun.ExecuteAsync(context, Connection(Greeting, CapabilityReply, "A002 NO full\r\n", LogoutReply));

        CollectionAssert.AreEqual((string[])["< A002 NO full\r\n", "* shutting down connection #0"], events.Transcript.TakeLast(2).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_StartTlsAccepted_ReportsTheHandshakeAndTheConnectionOpenedAgainBeforeTheSecondCapability()
    {
        // curl 8.21.0 -v -k --ssl-reqd: between "< A002 OK Begin TLS negotiation now" and the
        // second CAPABILITY it writes the TLS lines and the connect's "Established connection"
        // line again (measured, BL-1084).
        var events = new RecordingTransferEvents();
        var plaintext = Connection(Greeting, "* CAPABILITY IMAP4rev1 STARTTLS\r\nA001 OK done\r\n", "A002 OK Begin TLS negotiation now\r\n");
        var secured = Connection("* CAPABILITY IMAP4rev1\r\nA003 OK done\r\n", "* LIST () \"/\" INBOX\r\nA004 OK LIST completed\r\n", LogoutReply);
        var tls = new QueuedTlsProvider(ConnectResult.Connected(secured));
        TransferContext context = TlsRequiredContext(events);

        await new ImapProtocolHandler(new OpenedReportingConnector(ConnectResult.Connected(plaintext)), tls).ExecuteAsync(context);

        Assert.AreSame(events, tls.HandshakeEvents.Single());
        CollectionAssert.AreEqual(
            (string[])[
                "+ opened #3 to 127.0.0.1",
                "< * OK ready\r\n", "> A001 CAPABILITY\r\n", "< * CAPABILITY IMAP4rev1 STARTTLS\r\n", "< A001 OK done\r\n",
                "> A002 STARTTLS\r\n", "< A002 OK Begin TLS negotiation now\r\n",
                "+ opened #3 to 127.0.0.1",
                "> A003 CAPABILITY\r\n",
            ],
            events.Transcript.Take(9).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_StartTlsAcceptedAfterAConnectThatReportedNothing_ReportsNoConnectionOpened()
    {
        var events = new RecordingTransferEvents();
        var plaintext = Connection(Greeting, "* CAPABILITY IMAP4rev1 STARTTLS\r\nA001 OK done\r\n", "A002 OK Begin TLS negotiation now\r\n");
        var secured = Connection("* CAPABILITY IMAP4rev1\r\nA003 OK done\r\n", "* LIST () \"/\" INBOX\r\nA004 OK LIST completed\r\n", LogoutReply);
        TransferContext context = TlsRequiredContext(events);

        await new ImapProtocolHandler(new QueuedConnector(ConnectResult.Connected(plaintext)), new QueuedTlsProvider(ConnectResult.Connected(secured))).ExecuteAsync(context);

        Assert.Contains("> A003 CAPABILITY\r\n", events.Transcript);
        Assert.IsFalse(events.Transcript.Any(line => line.StartsWith('+')));
    }

    private static async Task<RecordingTransferEvents> RunFetchAsync(params string[] afterSelect)
    {
        var events = new RecordingTransferEvents();
        await ImapRun.ExecuteAsync(
            Context(Host + "INBOX;UID=1", events, withUser: true),
            Connection([Greeting, CapabilityReply, "A002 OK LOGIN completed\r\n", "A003 OK [READ-WRITE] SELECT completed\r\n", .. afterSelect]));
        return events;
    }

    private static TransferContext Context(string url, ITransferEvents events, bool withUser = false) =>
        new()
        {
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
            Events = events,
            Credentials = withUser ? new NetworkCredential("user", "secret") : null,
        };

    private static TransferContext TlsRequiredContext(ITransferEvents events) =>
        new()
        {
            Url = CurlUrl.Parse(Host),
            Output = new MemoryStream(),
            Events = events,
            SslLevel = TransportSecurityLevel.Required,
        };

    private static TransferContext AppendContext(ITransferEvents events, string message) =>
        new()
        {
            Url = CurlUrl.Parse(Host + "Sent"),
            Output = new MemoryStream(),
            Events = events,
            Upload = new MemoryStream(Encoding.Latin1.GetBytes(message)),
            Mail = new MailRequestOptions { UploadFlags = ["seen"] },
        };

    private static ScriptedConnection Connection(params string[] reads) =>
        new([.. reads.Select(Encoding.Latin1.GetBytes)]);
}
