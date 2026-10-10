using System.Security.Cryptography;
using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="Pop3Responder"/> to the POP3 side of upstream's <c>tests/ftpserver.pl</c> at
/// <c>curl-8_21_0</c>: the banner, each command's reply text, <c>REPLY</c> overrides, the
/// multi-line listings, the unrecognized answer and the protocol log.
/// </summary>
[TestClass]
public sealed class Pop3ResponderTests
{
    private const string AllCapabilities = "CAPA TOP UIDL APOP USER\nAUTH PLAIN LOGIN\n";

    [TestMethod]
    public void Greeting_NoWelcomeReply_IsTheCurlBannerAndOkLine() =>
        Assert.AreEqual(
            "        _   _ ____  _\r\n" +
            "    ___| | | |  _ \\| |\r\n" +
            "   / __| | | | |_) | |\r\n" +
            "  | (__| |_| |  _ {| |___\r\n" +
            "   \\___|\\___/|_| \\_\\_____|\r\n" +
            "+OK curl POP3 server ready to serve \r\n",
            Encoding.Latin1.GetString(Create(string.Empty).Greeting.Span));

    [TestMethod]
    public void Greeting_WelcomeReply_ReplacesTheBanner() =>
        Assert.AreEqual("-ERR go away\r\n", Encoding.Latin1.GetString(Create("REPLY welcome -ERR go away\n").Greeting.Span));

    [TestMethod]
    public void Construct_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new Pop3Responder(null!, new Dictionary<string, byte[]>()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Pop3Responder(LineProtocolServerCommands.Read([]), null!));
    }

    [TestMethod]
    [DataRow("", "CAPA", "-ERR Unrecognized command\r\n")]
    [DataRow("CAPA APOP\n", "CAPA", "-ERR Unrecognized command\r\n")]
    [DataRow(AllCapabilities, "capa", "+OK List of capabilities follows\r\nTOP\r\n\r\nUIDL\r\n\r\nUSER\r\n\r\nSASL PLAIN LOGIN\r\n\r\nIMPLEMENTATION POP3 pingpong test server\r\n.\r\n")]
    [DataRow("AUTH PLAIN\n", "CAPA", "+OK List of capabilities follows\r\nSASL PLAIN\r\n\r\nIMPLEMENTATION POP3 pingpong test server\r\n.\r\n")]
    [DataRow("", "AUTH", "-ERR Unrecognized command\r\n")]
    [DataRow("AUTH PLAIN LOGIN\n", "AUTH", "+OK List of supported mechanisms follows\r\nPLAIN\r\nLOGIN\r\n.\r\n")]
    [DataRow("", "USER", "-ERR Protocol error\r\n")]
    [DataRow("", "USER 0", "-ERR Protocol error\r\n")]
    [DataRow("", "USER user", "+OK\r\n")]
    [DataRow("", "PASS", "+OK Login successful\r\n")]
    [DataRow("", "APOP user x", "-ERR Unrecognized command\r\n")]
    [DataRow("CAPA APOP\n", "APOP", "-ERR Protocol error\r\n")]
    [DataRow("CAPA APOP\n", "APOP user", "-ERR Protocol error\r\n")]
    [DataRow("CAPA APOP\n", "APOP user wrong", "-ERR Login failure\r\n")]
    [DataRow("", "STAT", "+OK 3 4294967800\r\n")]
    [DataRow("", "STAT 1", "-ERR Protocol error\r\n")]
    [DataRow("", "NOOP", "+OK\r\n")]
    [DataRow("", "NOOP 1", "-ERR Protocol error\r\n")]
    [DataRow("", "UIDL", "-ERR Unrecognized command\r\n")]
    [DataRow("CAPA UIDL\n", "UIDL", "+OK Listing starts\r\n1 1\r\n2 2\r\n3 4\r\n.\r\n")]
    [DataRow("", "TOP 1 0", "-ERR Unrecognized command\r\n")]
    [DataRow("CAPA TOP\n", "TOP", "-ERR Protocol error\r\n")]
    [DataRow("CAPA TOP\n", "TOP 1", "-ERR Protocol error\r\n")]
    [DataRow("CAPA TOP\n", "TOP 1 0", "+OK Mail transfer starts\r\nbody\r\n.line\r\n.\r\n")]
    [DataRow("", "RSET", "+OK\r\n")]
    [DataRow("", "RSET 1", "-ERR Protocol error\r\n")]
    [DataRow("", "QUIT", "+OK curl POP3 server signing off\r\n")]
    [DataRow("", "DELE", "-ERR Protocol error\r\n")]
    [DataRow("", "DELE 1", "+OK\r\n")]
    [DataRow("", "XYZ", "-ERR XYZ is not dealt with!\r\n")]
    [DataRow("", "*", "-ERR * is not dealt with!\r\n")]
    [DataRow("", "dXNlcg==", "-ERR dXNlcg== is not dealt with!\r\n")]
    [DataRow("", "", "-ERR  is not dealt with!\r\n")]
    [DataRow("REPLY AUTH + \nREPLY dXNlcg== +OK Login successful\n", "AUTH PLAIN", "+ \r\n")]
    [DataRow("REPLY AUTH + \nREPLY dXNlcg== +OK Login successful\n", "dXNlcg==", "+OK Login successful\r\n")]
    public void Answer_Command_RepliesAsFtpserverPl(string serverCommands, string commandLine, string expected)
    {
        LineProtocolReply reply = Create(serverCommands).Answer(commandLine);

        Assert.AreEqual(expected, Encoding.Latin1.GetString(reply.Bytes.Span));
        Assert.IsFalse(reply.ClosesConnection);
    }

    [TestMethod]
    [DataRow("RETR 1", "+OK Mail transfer starts\r\nbody\r\n.line\r\n.\r\n")]
    [DataRow("RETR", "+OK Mail transfer starts\r\nbody\r\n.line\r\n.\r\n")]
    [DataRow("RETR 10002", "+OK Mail transfer starts\r\nsecond\r\n.\r\n")]
    [DataRow("LIST", "+OK Listing starts\r\nbody\r\n.line\r\n.\r\n")]
    [DataRow("LIST 10002", "+OK Listing starts\r\nbody\r\n.line\r\n.\r\n")]
    public void Answer_RetrieveOrList_SendsTheReplyDataUnstuffedThenADot(string commandLine, string expected) =>
        Assert.AreEqual(expected, Encoding.Latin1.GetString(Create(string.Empty).Answer(commandLine).Bytes.Span));

    [TestMethod]
    public void Answer_ListWithNoDataPart_SendsAnEmptyListing() =>
        Assert.AreEqual(
            "+OK Listing starts\r\n.\r\n",
            Encoding.Latin1.GetString(new Pop3Responder(LineProtocolServerCommands.Read([]), new Dictionary<string, byte[]>()).Answer("LIST").Bytes.Span));

    [TestMethod]
    public void Answer_RetrieveVerifiedServer_SendsTheProof() =>
        Assert.AreEqual(
            $"+OK Mail transfer starts\r\nWE ROOLZ: {Environment.ProcessId}\r\n.\r\n",
            Encoding.Latin1.GetString(Create(string.Empty).Answer("RETR verifiedserver").Bytes.Span));

    [TestMethod]
    public void Answer_ApopWithTheDigestOfTimestampAndPassword_LogsIn()
    {
        string digest = Convert.ToHexStringLower(MD5.HashData(Encoding.Latin1.GetBytes("<1972.987654321@curl>secret")));

        Assert.AreEqual("+OK Login successful\r\n", Encoding.Latin1.GetString(Create("CAPA APOP\n").Answer("APOP user " + digest).Bytes.Span));
    }

    [TestMethod]
    [DataRow("1-1")]
    [DataRow("a=b")]
    [DataRow("abc===")]
    public void Answer_LineThatIsNoCommand_RefusesAndCloses(string commandLine)
    {
        LineProtocolReply reply = Create(string.Empty).Answer(commandLine);

        Assert.AreEqual("-ERR Unrecognized command\r\n", Encoding.Latin1.GetString(reply.Bytes.Span));
        Assert.IsTrue(reply.ClosesConnection);
    }

    [TestMethod]
    public void Answer_Delete_KeepsTheMessageUntilResetOrQuit()
    {
        Pop3Responder responder = Create(string.Empty);

        responder.Answer("DELE 1");
        responder.Answer("DELE 2");
        CollectionAssert.AreEqual(new[] { "1", "2" }, responder.DeletedMessages.ToArray());
        responder.Answer("RSET 1");
        Assert.HasCount(2, responder.DeletedMessages);
        responder.Answer("RSET");
        Assert.IsEmpty(responder.DeletedMessages);
        responder.Answer("DELE 3");
        responder.Answer("QUIT");
        Assert.IsEmpty(responder.DeletedMessages);
    }

    [TestMethod]
    public void ReceivedCommandLines_KeepsEveryLineWithItsCrlf()
    {
        Pop3Responder responder = Create(string.Empty);

        responder.Answer("CAPA");
        responder.Answer("1-1");

        CollectionAssert.AreEqual(new[] { "CAPA\r\n", "1-1\r\n" }, responder.ReceivedCommandLines.ToArray());
    }

    private static Pop3Responder Create(string serverCommands) =>
        new(
            LineProtocolServerCommands.Read(Encoding.Latin1.GetBytes(serverCommands)),
            new Dictionary<string, byte[]>
            {
                ["data"] = Encoding.Latin1.GetBytes("body\r\n.line\r\n"),
                ["data2"] = Encoding.Latin1.GetBytes("second\r\n"),
            });
}
