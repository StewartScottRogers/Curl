using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="ImapResponder"/> to the IMAP side of upstream's <c>tests/ftpserver.pl</c> at
/// <c>curl-8_21_0</c>: the banner, each command's tagged reply, <c>REPLY</c> overrides, the
/// <c>APPEND</c> literal, the malformed-line answer and the protocol log.
/// </summary>
[TestClass]
public sealed class ImapResponderTests
{
    private const string SelectReply =
        "* 172 EXISTS\r\n* 1 RECENT\r\n* OK [UNSEEN 12] Message 12 is first unseen\r\n" +
        "* OK [UIDVALIDITY 3857529045] UIDs valid\r\n* OK [UIDNEXT 4392] Predicted next UID\r\n" +
        "* FLAGS (\\Answered \\Flagged \\Deleted \\Seen \\Draft)\r\n" +
        "* OK [PERMANENTFLAGS (\\Deleted \\Seen \\*)] Limited\r\n";

    [TestMethod]
    public void Greeting_NoWelcomeReply_IsTheCurlBanner() =>
        Assert.AreEqual(
            "        _   _ ____  _\r\n" +
            "    ___| | | |  _ \\| |\r\n" +
            "   / __| | | | |_) | |\r\n" +
            "  | (__| |_| |  _ {| |___\r\n" +
            "   \\___|\\___/|_| \\_\\_____|\r\n" +
            "* OK curl IMAP server ready to serve\r\n",
            Encoding.Latin1.GetString(Create(string.Empty).Greeting.Span));

    [TestMethod]
    public void Greeting_WelcomeReply_ReplacesTheBanner() =>
        Assert.AreEqual("* BYE go away\r\n", Encoding.Latin1.GetString(Create("REPLY welcome * BYE go away\n").Greeting.Span));

    [TestMethod]
    public void Construct_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new ImapResponder(null!, new Dictionary<string, byte[]>()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ImapResponder(LineProtocolServerCommands.Read([]), null!));
    }

    [TestMethod]
    [DataRow("", "A1 CAPABILITY", "A1 BAD Command\r\n")]
    [DataRow("CAPA IDLE\nAUTH PLAIN LOGIN\n", "A1 CAPABILITY", "* CAPABILITY IMAP4 IDLE AUTH=PLAIN AUTH=LOGIN pingpong test server\r\nA1 OK CAPABILITY completed\r\n")]
    [DataRow("AUTH PLAIN\n", "A1 capability", "* CAPABILITY IMAP4 AUTH=PLAIN pingpong test server\r\nA1 OK CAPABILITY completed\r\n")]
    [DataRow("", "A1 LOGIN user secret", "A1 OK LOGIN completed\r\n")]
    [DataRow("", "A1 LOGIN", "A1 BAD Command Argument\r\n")]
    [DataRow("", "A1 LOGIN \"\" secret", "A1 BAD Command Argument\r\n")]
    [DataRow("", "A1 SELECT", "A1 BAD Command Argument\r\n")]
    [DataRow("", "A1 SELECT \"800\"", SelectReply + "A1 OK [READ-WRITE] SELECT completed\r\n")]
    [DataRow("", "A1 EXAMINE 800", "mail\r\nA1 OK [READ-ONLY] EXAMINE completed\r\n")]
    [DataRow("", "A1 EXAMINE", "A1 BAD Command Argument\r\n")]
    [DataRow("", "A1 LIST \"\" *", "A1 BAD Command Argument\r\n")]
    [DataRow("", "A1 LIST 800 *", "mail\r\nA1 OK LIST Completed\r\n")]
    [DataRow("", "A1 LSUB 800 *", "mail\r\nA1 OK LSUB Completed\r\n")]
    [DataRow("", "A1 LSUB", "A1 BAD Command Argument\r\n")]
    [DataRow("", "A1 STATUS 800 (UIDNEXT)", "mail\r\nA1 OK STATUS completed\r\n")]
    [DataRow("", "A1 CREATE box", "A1 OK CREATE completed\r\n")]
    [DataRow("", "A1 CREATE", "A1 BAD Command Argument\r\n")]
    [DataRow("", "A1 DELETE box", "A1 OK DELETE completed\r\n")]
    [DataRow("", "A1 DELETE \"\"", "A1 BAD Command Argument\r\n")]
    [DataRow("", "A1 RENAME a b", "A1 OK RENAME completed\r\n")]
    [DataRow("", "A1 RENAME a", "A1 BAD Command Argument\r\n")]
    [DataRow("", "A1 COPY 1 box", "A1 OK COPY completed\r\n")]
    [DataRow("", "A1 COPY 1", "A1 BAD Command Argument\r\n")]
    [DataRow("", "A1 IDLE", "+ entering idle mode\r\n")]
    [DataRow("", "A1 NOOP", "* 22 EXPUNGE\r\n* 23 EXISTS\r\n* 3 RECENT\r\n* 14 FETCH (FLAGS (\\Seen \\Deleted))\r\nA1 OK NOOP completed\r\n")]
    [DataRow("", "A1 NOOP 0", "* 22 EXPUNGE\r\n* 23 EXISTS\r\n* 3 RECENT\r\n* 14 FETCH (FLAGS (\\Seen \\Deleted))\r\nA1 OK NOOP completed\r\n")]
    [DataRow("", "A1 NOOP x", "A1 BAD Command Argument\r\n")]
    [DataRow("", "A1 LOGOUT", "* BYE curl IMAP server signing off\r\nA1 OK LOGOUT completed\r\n")]
    [DataRow("", "A1 AUTHENTICATE PLAIN", "A1 BAD AUTHENTICATE is not dealt with!\r\n")]
    [DataRow("REPLY AUTHENTICATE +\n", "A1 AUTHENTICATE PLAIN", "+\r\n")]
    [DataRow("REPLY LOGIN A1 NO Login denied\n", "A1 LOGIN user secret", "A1 NO Login denied\r\n")]
    [DataRow("", "A1 CHECK", "A1 BAD Command received in Invalid state\r\n")]
    [DataRow("", "A1 CLOSE", "A1 BAD Command received in Invalid state\r\n")]
    [DataRow("", "A1 EXPUNGE", "A1 BAD Command received in Invalid state\r\n")]
    [DataRow("", "A1 FETCH 1 BODY[]", "A1 BAD Command received in Invalid state\r\n")]
    [DataRow("", "A1 SEARCH ALL", "A1 BAD Command received in Invalid state\r\n")]
    [DataRow("", "A1 STORE 1 +Flags \\Deleted", "A1 BAD Command received in Invalid state\r\n")]
    [DataRow("", "A1 UID FETCH 1 BODY[]", "A1 BAD Command received in Invalid state\r\n")]
    public void Answer_Command_RepliesAsFtpserverPl(string serverCommands, string commandLine, string expected) =>
        Assert.AreEqual(expected, Answer(Create(serverCommands), commandLine));

    [TestMethod]
    [DataRow("A2 FETCH 1 BODY[TEXT]", "* 1 FETCH (BODY[TEXT] {6}\r\nmail\r\n)\r\nA2 OK FETCH completed\r\n")]
    [DataRow("A2 FETCH", "*  FETCH ( {6}\r\nmail\r\n)\r\nA2 OK FETCH completed\r\n")]
    [DataRow("A2 CHECK", "A2 OK CHECK completed\r\n")]
    [DataRow("A2 CLOSE", "A2 BAD Command Argument\r\n")]
    [DataRow("A2 EXPUNGE", "* 172 EXISTS\r\nA2 OK EXPUNGE completed\r\n")]
    [DataRow("A2 SEARCH", "A2 BAD Command Argument\r\n")]
    [DataRow("A2 SEARCH ALL", "mail\r\nA2 OK SEARCH completed\r\n")]
    [DataRow("A2 STORE 1 +Flags \\Seen", "* 1 FETCH (FLAGS (\\Seen \\Seen))\r\nA2 OK STORE completed\r\n")]
    [DataRow("A2 STORE", "A2 BAD Command Argument\r\n")]
    [DataRow("A2 STORE 1 -Flags \\Seen", "A2 BAD Command Argument\r\n")]
    [DataRow("A2 STORE 1 +Flags", "A2 BAD Command Argument\r\n")]
    [DataRow("A2 STORE 1 +Flags ", "A2 BAD Command Argument\r\n")]
    [DataRow("A2 UID FETCH 1 BODY[]", "* FETCH FETCH (1 BODY[] {6}\r\nmail\r\n)\r\nA2 OK FETCH completed\r\n")]
    [DataRow("A2 UID SEARCH", "mail\r\nA2 OK SEARCH completed\r\n")]
    [DataRow("A2 UID COPY", "mail\r\nA2 OK COPY completed\r\n")]
    [DataRow("A2 UID STORE", "mail\r\nA2 OK STORE completed\r\n")]
    [DataRow("A2 UID SEARCH ALL", "A2 BAD Command Argument\r\n")]
    [DataRow("A2 UID", "A2 BAD Command Argument\r\n")]
    public void Answer_AfterSelect_RepliesAsFtpserverPl(string commandLine, string expected)
    {
        ImapResponder responder = Create(string.Empty);
        Answer(responder, "A1 SELECT 800");
        Assert.AreEqual(expected, Answer(responder, commandLine));
    }

    [TestMethod]
    public void Answer_FetchWithNoReplyData_SendsAnEmptyLiteralSizeAndThePostFetchText()
    {
        ImapResponder responder = new(LineProtocolServerCommands.Read(Encoding.Latin1.GetBytes("POSTFETCH extra\n")), new Dictionary<string, byte[]>());
        Answer(responder, "A1 SELECT 1");
        Assert.AreEqual("* 1 FETCH (BODY[] {}\r\nextra)\r\nA1 OK FETCH completed\r\n", Answer(responder, "A1 FETCH 1 BODY[]"));
    }

    [TestMethod]
    public void Answer_FetchOfNumberedMailbox_SendsTheNumberedPart()
    {
        ImapResponder responder = new(LineProtocolServerCommands.Read([]), new Dictionary<string, byte[]> { ["data"] = [], ["data3"] = Encoding.Latin1.GetBytes("three\r\n") });
        Answer(responder, "A1 SELECT 8000003");
        Assert.AreEqual("* 1 FETCH (BODY[] {7}\r\nthree\r\n)\r\nA1 OK FETCH completed\r\n", Answer(responder, "A1 FETCH 1 BODY[]"));
    }

    [TestMethod]
    public void Answer_VerifiedServer_ProvesTheTestServer()
    {
        ImapResponder responder = Create(string.Empty);
        string proof = $"WE ROOLZ: {Environment.ProcessId}";
        Assert.AreEqual($"* LIST () \"/\" \"{proof}\"\r\nA1 OK LIST Completed\r\n", Answer(responder, "A1 LIST verifiedserver *"));
        Answer(responder, "A2 SELECT verifiedserver");
        Assert.AreEqual($"* 1 FETCH (BODY[] {{{proof.Length + 2}}}\r\n{proof}\r\n)\r\nA3 OK FETCH completed\r\n", Answer(responder, "A3 FETCH 1 BODY[]"));
    }

    [TestMethod]
    public void Answer_StoreDeleted_IsExpungedAndLetsCloseComplete()
    {
        ImapResponder responder = Create(string.Empty);
        Answer(responder, "A1 SELECT 800");
        Answer(responder, "A2 STORE 7 +Flags \\Deleted");
        Answer(responder, "A3 STORE \"9\" +Flags \\Deleted");
        Assert.AreEqual("* 7 EXPUNGE\r\n* 9 EXPUNGE\r\nA4 OK EXPUNGE completed\r\n", Answer(responder, "A4 EXPUNGE"));
        Answer(responder, "A5 STORE 7 +Flags \\Deleted");
        Assert.AreEqual("A6 OK CLOSE completed\r\n", Answer(responder, "A6 CLOSE"));
        Assert.AreEqual("A7 BAD Command Argument\r\n", Answer(responder, "A7 CLOSE"));
    }

    [TestMethod]
    [DataRow("*")]
    [DataRow("dXNlcgBzZWNyZXQ=")]
    [DataRow("ab+cd")]
    [DataRow("ab/cd")]
    [DataRow("")]
    public void Answer_AuthenticationResponse_IsACommandUnderTheLastTag(string line)
    {
        ImapResponder responder = Create(string.Empty);
        Answer(responder, "A1 NOOP");
        Assert.AreEqual($"A1 BAD {line} is not dealt with!\r\n", Answer(responder, line));
    }

    [TestMethod]
    public void Answer_AuthenticationResponseReply_SendsItsText() =>
        Assert.AreEqual("A1 OK AUTHENTICATE completed\r\n", Answer(Create("REPLY dXNlcgBzZWNyZXQ= A1 OK AUTHENTICATE completed\n"), "dXNlcgBzZWNyZXQ="));

    [TestMethod]
    [DataRow("a.b")]
    [DataRow("A1  NOOP")]
    [DataRow(" NOOP")]
    [DataRow("abc===")]
    public void Answer_MalformedLine_IsRefusedAndClosesTheConnection(string line)
    {
        LineProtocolReply reply = Create(string.Empty).Answer(line);
        Assert.AreEqual(line + " BAD Command\r\n", Encoding.Latin1.GetString(reply.Bytes.Span));
        Assert.IsTrue(reply.ClosesConnection);
    }

    [TestMethod]
    public void Answer_Append_KeepsTheLiteralAndCompletesAfterItsCrlf()
    {
        ImapResponder responder = Create(string.Empty);
        Assert.AreEqual("+ Ready for literal data\r\n", Answer(responder, "A1 APPEND \"800\" (\\Seen) {12}"));
        Assert.AreEqual(string.Empty, Answer(responder, "Hi there"));
        Assert.AreEqual("A1 OK APPEND completed\r\n", Answer(responder, "ok"));
        Assert.AreEqual("A2 OK CREATE completed\r\n", Answer(responder, "A2 CREATE box"));

        Assert.AreEqual("Hi there\r\nok", Encoding.Latin1.GetString(responder.UploadedMessage.Span));
        CollectionAssert.AreEqual(new[] { "A1 APPEND \"800\" (\\Seen) {12}\r\n", "A2 CREATE box\r\n" }, responder.ReceivedCommandLines.ToArray());
    }

    [TestMethod]
    public void Answer_AppendLiteralEndingInCrlf_WaitsForTheCommandsOwnCrlf()
    {
        ImapResponder responder = Create(string.Empty);
        Answer(responder, "A1 APPEND 800 {4}");
        Assert.AreEqual(string.Empty, Answer(responder, "ab"));
        Assert.AreEqual(string.Empty, Answer(responder, "xx"));
        Assert.AreEqual(string.Empty, Answer(responder, "more"));
        Assert.AreEqual("ab\r\n", Encoding.Latin1.GetString(responder.UploadedMessage.Span));
    }

    [TestMethod]
    public void Answer_AppendOfNoBytes_CompletesOnTheNextCrlf()
    {
        ImapResponder responder = Create(string.Empty);
        Answer(responder, "A1 APPEND 800 {0}");
        Assert.AreEqual("A1 OK APPEND completed\r\n", Answer(responder, string.Empty));
        Assert.AreEqual(0, responder.UploadedMessage.Length);
    }

    [TestMethod]
    [DataRow("A1 APPEND")]
    [DataRow("A1 APPEND 800")]
    [DataRow("A1 APPEND {5}")]
    [DataRow("A1 APPEND 800 {5")]
    [DataRow("A1 APPEND 800 5")]
    [DataRow("A1 APPEND 800 {}")]
    [DataRow("A1 APPEND 800 {x}")]
    [DataRow("A1 APPEND 800 {a{5}")]
    [DataRow("A1 APPEND \"\" {5}")]
    public void Answer_AppendWithoutMailboxAndLiteral_IsRefused(string commandLine)
    {
        ImapResponder responder = Create(string.Empty);
        Assert.AreEqual("A1 BAD Command Argument\r\n", Answer(responder, commandLine));
        Assert.AreEqual("A2 OK CREATE completed\r\n", Answer(responder, "A2 CREATE box"));
    }

    private static ImapResponder Create(string serverCommands) =>
        new(LineProtocolServerCommands.Read(Encoding.Latin1.GetBytes(serverCommands)), new Dictionary<string, byte[]> { ["data"] = Encoding.Latin1.GetBytes("mail\r\n") });

    private static string Answer(ImapResponder responder, string commandLine) =>
        Encoding.Latin1.GetString(responder.Answer(commandLine).Bytes.Span);
}
