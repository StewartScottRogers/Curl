using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Imap.Fakes;

namespace Curl.Protocol.Imap;

/// <summary>
/// Pins how an IMAP connection outlives its transfer, as curl 8.21.0 does in upstream tests
/// 804, 815, 836 and 1982 (BL-1987): a successful transfer leaves it logged in to the run's
/// connection cache, the next URL for the same login carries on with the next tag and skips a
/// <c>SELECT</c> of the mailbox already selected, <c>LOGOUT</c> is sent only when the cache
/// closes it, and a connection's tags start with <c>A</c> plus its number modulo 26.
/// </summary>
[TestClass]
public sealed class ImapKeptConnectionTests
{
    private const string Greeting = "* OK ready\r\n";

    private const string Capability = "* CAPABILITY IMAP4rev1\r\nA001 OK CAPABILITY completed\r\n";

    [TestMethod]
    public async Task ExecuteAsync_SecondUrlOnTheSameMailbox_ReusesTheConnectionWithoutSelectingAgain()
    {
        var connection = new PoolingScriptedConnection(
            Read(Greeting), Read(Capability), Read("A002 OK LOGIN completed\r\n"), Read("A003 OK [READ-WRITE] SELECT completed\r\n"),
            Read("* 123 FETCH (BODY[1] {5}\r\nhello)\r\nA004 OK FETCH completed\r\n"),
            Read("* 456 FETCH (BODY[2.3] {3}\r\nbye)\r\nA005 OK FETCH completed\r\n"),
            Read("* BYE\r\nA006 OK LOGOUT completed\r\n"));
        var handler = new ImapProtocolHandler(Reusing(connection), new QueuedTlsProvider());
        var output = new MemoryStream();

        TransferResult first = await handler.ExecuteAsync(Context("imap://127.0.0.1/804/;MAILINDEX=123/;SECTION=1", output));
        TransferResult second = await handler.ExecuteAsync(Context("imap://127.0.0.1/804/;MAILINDEX=456/;SECTION=2.3", output));
        await connection.CloseAsync();

        Assert.AreEqual(TransferResult.Success(5), first);
        Assert.AreEqual(TransferResult.Success(3), second);
        Assert.AreEqual("hellobye", Encoding.Latin1.GetString(output.ToArray()));
        Assert.AreEqual(
            "A001 CAPABILITY\r\nA002 LOGIN user secret\r\nA003 SELECT 804\r\nA004 FETCH 123 BODY[1]\r\nA005 FETCH 456 BODY[2.3]\r\nA006 LOGOUT\r\n",
            Encoding.Latin1.GetString(connection.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_CustomCommandsOnTheSameMailbox_SendTheSecondWithoutSelectingAgain()
    {
        var connection = new PoolingScriptedConnection(
            Read(Greeting), Read(Capability), Read("A002 OK LOGIN completed\r\n"), Read("A003 OK SELECT completed\r\n"),
            Read("A004 OK STORE completed\r\n"), Read("A005 OK CLOSE completed\r\n"));
        var handler = new ImapProtocolHandler(Reusing(connection), new QueuedTlsProvider());

        await handler.ExecuteAsync(Context("imap://127.0.0.1/815", Stream.Null, "STORE 123 +Flags \\Deleted"));
        TransferResult second = await handler.ExecuteAsync(Context("imap://127.0.0.1/815", Stream.Null, "CLOSE"));

        Assert.AreEqual(TransferResult.Success(0), second);
        Assert.AreEqual(
            "A001 CAPABILITY\r\nA002 LOGIN user secret\r\nA003 SELECT 815\r\nA004 STORE 123 +Flags \\Deleted\r\nA005 CLOSE\r\n",
            Encoding.Latin1.GetString(connection.Sent));
        Assert.IsFalse(connection.IsClosed);
    }

    [TestMethod]
    [DataRow("imap://127.0.0.1/OTHER;UID=2", "A003 SELECT OTHER\r\nA004 UID FETCH 2 BODY[]\r\n", DisplayName = "another mailbox")]
    [DataRow("imap://127.0.0.1/INBOX;UIDVALIDITY=8;UID=2", "A003 SELECT INBOX\r\nA004 UID FETCH 2 BODY[]\r\n", DisplayName = "another UIDVALIDITY")]
    [DataRow("imap://127.0.0.1/inbox;UIDVALIDITY=7;UID=2", "A003 UID FETCH 2 BODY[]\r\n", DisplayName = "same UIDVALIDITY, name in another case")]
    [DataRow("imap://127.0.0.1/INBOX;UIDVALIDITY=x;UID=2", "A003 UID FETCH 2 BODY[]\r\n", DisplayName = "UIDVALIDITY not a number")]
    public async Task ExecuteAsync_KeptConnectionWithInboxSelected_SelectsOnlyWhenTheUrlAsksForAnotherSelection(string url, string expectedSent)
    {
        var kept = new ImapKeptConnection(new ScriptedConnection(), "\0\0\0None", 'A', TimeProvider.System);
        kept.Remember(2, "INBOX", 7);
        var connection = new PoolingScriptedConnection(
            Read("* 1 EXISTS\r\nA003 OK SELECT completed\r\n"), Read("* 2 FETCH (BODY[] {2}\r\nhi)\r\nA003 OK FETCH completed\r\n"), Read("A004 OK FETCH completed\r\n"));
        connection.TryHoldSession(kept);
        var handler = new ImapProtocolHandler(new QueuedConnector(ConnectResult.Connected(connection, null, isReused: true)), new QueuedTlsProvider());

        await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse(url), Output = Stream.Null });

        StringAssert.StartsWith(Encoding.Latin1.GetString(connection.Sent), expectedSent);
    }

    [TestMethod]
    public async Task ExecuteAsync_KeptConnectionWithoutAReportedUidValidity_IsNotSelectedAgain()
    {
        var kept = new ImapKeptConnection(new ScriptedConnection(), "\0\0\0None", 'A', TimeProvider.System);
        kept.Remember(2, "INBOX", null);
        var connection = new PoolingScriptedConnection(Read("* 2 FETCH (BODY[] {2}\r\nhi)\r\nA003 OK FETCH completed\r\n"));
        connection.TryHoldSession(kept);
        var handler = new ImapProtocolHandler(new QueuedConnector(ConnectResult.Connected(connection, null, isReused: true)), new QueuedTlsProvider());

        await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("imap://127.0.0.1/INBOX;UIDVALIDITY=8;UID=2"), Output = Stream.Null });

        Assert.AreEqual("A003 UID FETCH 2 BODY[]\r\n", Encoding.Latin1.GetString(connection.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_SecondUserOnTheSameHost_LogsTheFirstOutAndTagsTheSecondConnectionB()
    {
        var first = new PoolingScriptedConnection(
            Read(Greeting), Read(Capability), Read("A002 OK LOGIN completed\r\n"), Read("A003 OK SELECT completed\r\n"),
            Read("* 1 FETCH (BODY[] {1}\r\na)\r\nA004 OK FETCH completed\r\n"), Read("* BYE\r\nA005 OK LOGOUT completed\r\n"));
        var second = new PoolingScriptedConnection(
            Read(Greeting), Read(Capability.Replace("A001", "B001", StringComparison.Ordinal)), Read("B002 OK LOGIN completed\r\n"),
            Read("B003 OK SELECT completed\r\n"), Read("* 2 FETCH (BODY[] {1}\r\nb)\r\nB004 OK FETCH completed\r\n"));
        var connector = new QueuedConnector(
            ConnectResult.Connected(first, null), ConnectResult.Connected(first, null, isReused: true), ConnectResult.Connected(second, null, connectionNumber: 1));
        var handler = new ImapProtocolHandler(connector, new QueuedTlsProvider());

        await handler.ExecuteAsync(Context("imap://127.0.0.1/836/;MAILINDEX=1", Stream.Null, user: "user.one"));
        TransferResult result = await handler.ExecuteAsync(Context("imap://127.0.0.1/836/;MAILINDEX=2", Stream.Null, user: "user.two"));

        Assert.AreEqual(TransferResult.Success(1), result);
        Assert.IsTrue(first.IsClosed);
        StringAssert.EndsWith(Encoding.Latin1.GetString(first.Sent), "A004 FETCH 1 BODY[]\r\nA005 LOGOUT\r\n");
        Assert.AreEqual(
            "B001 CAPABILITY\r\nB002 LOGIN user.two secret\r\nB003 SELECT 836\r\nB004 FETCH 2 BODY[]\r\n",
            Encoding.Latin1.GetString(second.Sent));
    }

    [TestMethod]
    [DataRow(0L, 'A')]
    [DataRow(1L, 'B')]
    [DataRow(25L, 'Z')]
    [DataRow(27L, 'B')]
    public void TagLetterOf_ConnectionNumber_IsATheNumberModulo26Letters(long connectionNumber, char expected)
    {
        char letter = ImapProtocolHandler.TagLetterOf(connectionNumber);

        Assert.AreEqual(expected, letter);
    }

    [TestMethod]
    public async Task ShutDownAsync_NotLeftIntact_SendsNothing()
    {
        var connection = new ScriptedConnection();
        var kept = new ImapKeptConnection(connection, string.Empty, 'C', TimeProvider.System);

        await kept.ShutDownAsync(CancellationToken.None);

        Assert.IsEmpty(connection.Sent);
    }

    [TestMethod]
    [DataRow("* BYE\r\n|C010 OK| LOGOUT completed\r\n", DisplayName = "tagged reply split across reads")]
    [DataRow("* BYE going\r\n", DisplayName = "server hangs up")]
    public async Task ShutDownAsync_LeftIntact_SendsLogoutWithTheNextTagAndReadsUntilItsReply(string reads)
    {
        var connection = new ScriptedConnection([.. reads.Split('|').Select(Read)]);
        var kept = new ImapKeptConnection(connection, string.Empty, 'C', TimeProvider.System);
        kept.Remember(9, null, null);

        await kept.ShutDownAsync(CancellationToken.None);

        Assert.AreEqual("C010 LOGOUT\r\n", Encoding.Latin1.GetString(connection.Sent));
    }

    [TestMethod]
    public async Task ShutDownAsync_ReplyNeverTagged_StopsAfter64KiB()
    {
        var connection = new ScriptedConnection(new byte[64 * 1024], Read("C001 OK\r\n"));
        var kept = new ImapKeptConnection(connection, string.Empty, 'C', TimeProvider.System);
        kept.Remember(0, null, null);

        await kept.ShutDownAsync(CancellationToken.None);

        Assert.AreEqual("C001 LOGOUT\r\n", Encoding.Latin1.GetString(connection.Sent));
    }

    [TestMethod]
    public async Task ShutDownAsync_ConnectionReset_IsIgnored()
    {
        var connection = new ScriptedConnection { WritesBeforeFailure = 0 };
        var kept = new ImapKeptConnection(connection, string.Empty, 'A', TimeProvider.System);
        kept.Remember(1, null, null);

        await kept.ShutDownAsync(CancellationToken.None);

        Assert.IsEmpty(connection.Sent);
    }

    [TestMethod]
    public async Task ShutDownAsync_Cancelled_IsIgnored()
    {
        var connection = new ScriptedConnection { WritesBeforeFailure = 0, WriteFailure = new OperationCanceledException() };
        var kept = new ImapKeptConnection(connection, string.Empty, 'A', TimeProvider.System);
        kept.Remember(1, null, null);

        await kept.ShutDownAsync(CancellationToken.None);

        Assert.IsEmpty(connection.Sent);
    }

    private static byte[] Read(string text) => Encoding.Latin1.GetBytes(text);

    private static QueuedConnector Reusing(IConnection connection) =>
        new(ConnectResult.Connected(connection, null), ConnectResult.Connected(connection, null, isReused: true), ConnectResult.Connected(connection, null, isReused: true));

    private static TransferContext Context(string url, Stream output, string? customCommand = null, string user = "user") =>
        new()
        {
            Url = CurlUrl.Parse(url),
            Output = output,
            Credentials = new NetworkCredential(user, "secret"),
            Mail = customCommand is null ? null : new MailRequestOptions { CustomCommand = customCommand },
        };
}
