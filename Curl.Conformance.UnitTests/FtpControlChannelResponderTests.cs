using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="FtpControlChannelResponder"/> to upstream's <c>tests/ftpserver.pl</c> at
/// <c>curl-8_21_0</c>: the banner, the display texts, <c>REPLY</c> overrides, <c>PWD</c> and
/// <c>CWD</c>, the not-dealt-with and unrecognized answers, and the protocol log.
/// </summary>
[TestClass]
public sealed class FtpControlChannelResponderTests
{
    [TestMethod]
    public void Greeting_NoWelcomeReply_IsTheCurlBanner()
    {
        Assert.AreEqual(
            "220-        _   _ ____  _\r\n" +
            "220-    ___| | | |  _ \\| |\r\n" +
            "220-   / __| | | | |_) | |\r\n" +
            "220-  | (__| |_| |  _ {| |___\r\n" +
            "220    \\___|\\___/|_| \\_\\_____|\r\n",
            Encoding.Latin1.GetString(Create(string.Empty).Greeting.Span));
    }

    [TestMethod]
    public void Greeting_WelcomeReply_ReplacesTheBanner() =>
        Assert.AreEqual("230 welcome without password\r\n", Encoding.Latin1.GetString(Create("REPLY welcome 230 welcome without password\n").Greeting.Span));

    [TestMethod]
    public void Construct_NullServerCommands_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new FtpControlChannelResponder(null!));

    [TestMethod]
    [DataRow("USER anonymous", "331 We are happy you popped in!\r\n")]
    [DataRow("PASS ftp@example.com", "230 Welcome you silly person\r\n")]
    [DataRow("TYPE I", "200 I modify TYPE as you wanted\r\n")]
    [DataRow("SYST", "215 UNIX Type: L8\r\n")]
    [DataRow("QUIT", "221 bye bye baby\r\n")]
    [DataRow("CWD path", "250 CWD command successful.\r\n")]
    [DataRow("MKD dir", "257 Created your requested directory\r\n")]
    [DataRow("REST 10", "350 Yeah yeah we set it there for you\r\n")]
    [DataRow("DELE file", "200 OK OK OK whatever you say\r\n")]
    [DataRow("RNFR a", "350 Received your order. Please provide more\r\n")]
    [DataRow("RNTO b", "250 Ok, thanks. File renaming completed.\r\n")]
    [DataRow("NOOP", "200 Yes, I'm very good at doing nothing.\r\n")]
    [DataRow("PBSZ 0", "500 PBSZ not implemented\r\n")]
    [DataRow("PROT P", "500 PROT not implemented\r\n")]
    [DataRow("PORT 127,0,0,1,4,1", "200 You said PORT - I say FINE\r\n")]
    [DataRow("LIST", "150 here comes a directory\r\n")]
    [DataRow("NLST", "150 here comes a directory\r\n")]
    [DataRow("PWD", "257 \"/\" is current directory\r\n")]
    [DataRow("pwd", "257 \"/\" is current directory\r\n")]
    [DataRow("FEAT", "500 FEAT is not dealt with!\r\n")]
    [DataRow("user me", "500 user is not dealt with!\r\n")]
    [DataRow("cwd x", "")]
    public void Answer_Command_GivesFtpserverDefaultReply(string commandLine, string expected)
    {
        LineProtocolReply reply = Create(string.Empty).Answer(commandLine);

        Assert.AreEqual(expected, Encoding.Latin1.GetString(reply.Bytes.Span));
        Assert.IsFalse(reply.ClosesConnection);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("AB")]
    [DataRow("ABCDE")]
    [DataRow("PWD1")]
    [DataRow("1PWD")]
    public void Answer_UnrecognizedLine_Answers500AndCloses(string commandLine)
    {
        LineProtocolReply reply = Create(string.Empty).Answer(commandLine);

        Assert.AreEqual("500 Unrecognized command\r\n", Encoding.Latin1.GetString(reply.Bytes.Span));
        Assert.IsTrue(reply.ClosesConnection);
    }

    [TestMethod]
    public void Answer_ReplyLine_OverridesDefaultAndSkipsHandler()
    {
        FtpControlChannelResponder responder = Create("REPLY PASS 530 Login incorrect\nREPLY CWD 550 no such dir\n");

        Assert.AreEqual("530 Login incorrect\r\n", Encoding.Latin1.GetString(responder.Answer("PASS secret").Bytes.Span));
        Assert.AreEqual("550 no such dir\r\n", Encoding.Latin1.GetString(responder.Answer("CWD a").Bytes.Span));
        Assert.AreEqual("257 \"/\" is current directory\r\n", Encoding.Latin1.GetString(responder.Answer("PWD").Bytes.Span));
    }

    [TestMethod]
    [DataRow(new[] { "CWD a/b" }, "/a/b")]
    [DataRow(new[] { "CWD a/b/" }, "/a/b")]
    [DataRow(new[] { "CWD a", "CWD b" }, "/a/b")]
    [DataRow(new[] { "CWD a/b", "CWD .." }, "/a")]
    [DataRow(new[] { "CWD .." }, "/")]
    [DataRow(new[] { "CWD a-b", "CWD .." }, "/a-")]
    [DataRow(new[] { "CWD a/-", "CWD .." }, "/a/-")]
    [DataRow(new[] { "CWD a", "CWD /" }, "/")]
    [DataRow(new[] { "CWD a", "CWD /b" }, "/b")]
    [DataRow(new[] { "CWD a", "CWD //" }, "/a")]
    [DataRow(new[] { "CWD a", "CWD" }, "/a")]
    [DataRow(new[] { "CWD test-1234" }, "/")]
    public void Answer_PwdAfterCwd_GivesDirectoryAsFtpserverMovesIt(string[] commandLines, string expected)
    {
        FtpControlChannelResponder responder = Create(string.Empty);
        foreach (string commandLine in commandLines)
        {
            responder.Answer(commandLine);
        }

        Assert.AreEqual($"257 \"{expected}\" is current directory\r\n", Encoding.Latin1.GetString(responder.Answer("PWD").Bytes.Span));
    }

    [TestMethod]
    public void ReceivedCommandLines_RecordsEachLineWithCrlfInOrder()
    {
        FtpControlChannelResponder responder = Create(string.Empty);
        responder.Answer("USER anonymous");
        responder.Answer("PASS ftp@example.com");
        responder.Answer("BAD!");

        CollectionAssert.AreEqual(
            new[] { "USER anonymous\r\n", "PASS ftp@example.com\r\n", "BAD!\r\n" },
            responder.ReceivedCommandLines.ToArray());
    }

    private static FtpControlChannelResponder Create(string serverCommands) =>
        new(LineProtocolServerCommands.Read(Encoding.Latin1.GetBytes(serverCommands)));
}
