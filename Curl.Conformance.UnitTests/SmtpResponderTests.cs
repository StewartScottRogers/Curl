using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="SmtpResponder"/> to the SMTP side of upstream's <c>tests/ftpserver.pl</c> at
/// <c>curl-8_21_0</c>: the banner, each command's reply text, <c>REPLY</c> overrides, the
/// <c>DATA</c> message, the unrecognized answer and the protocol log.
/// </summary>
[TestClass]
public sealed class SmtpResponderTests
{
    [TestMethod]
    public void Greeting_NoWelcomeReply_IsTheCurlBanner() =>
        Assert.AreEqual(
            "220-        _   _ ____  _\r\n" +
            "220-    ___| | | |  _ \\| |\r\n" +
            "220-   / __| | | | |_) | |\r\n" +
            "220-  | (__| |_| |  _ {| |___\r\n" +
            "220    \\___|\\___/|_| \\_\\_____|\r\n",
            Encoding.Latin1.GetString(Create(string.Empty).Greeting.Span));

    [TestMethod]
    public void Greeting_WelcomeReply_ReplacesTheBanner() =>
        Assert.AreEqual("554 go away\r\n", Encoding.Latin1.GetString(Create("REPLY welcome 554 go away\n").Greeting.Span));

    [TestMethod]
    public void Construct_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new SmtpResponder(null!, new Dictionary<string, byte[]>()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SmtpResponder(LineProtocolServerCommands.Read([]), null!));
    }

    [TestMethod]
    [DataRow("", "EHLO 900", "250 ESMTP pingpong test server Hello 900\r\n")]
    [DataRow("", "EHLO", "250 ESMTP pingpong test server Hello [127.0.0.1]\r\n")]
    [DataRow("CAPA \"SIZE 32\" SMTPUTF8\nAUTH PLAIN LOGIN\n", "EHLO 913", "250-ESMTP pingpong test server Hello 913\r\n250-SIZE 32\r\n250-SMTPUTF8\r\n250 AUTH PLAIN LOGIN\r\n")]
    [DataRow("AUTH PLAIN\n", "HELO 0", "250 SMTP pingpong test server Hello [127.0.0.1]\r\n")]
    [DataRow("", "MAIL", "501 Unrecognized parameter\r\n")]
    [DataRow("", "MAIL SIZE=5", "501 Invalid address\r\n")]
    [DataRow("", "MAIL FROM:<sender@example.com> SIZE=x", "250 Sender OK\r\n")]
    [DataRow("CAPA \"SIZE 32\"\n", "MAIL FROM:<sender@example.com> SIZE=33", "552 Message size too large\r\n")]
    [DataRow("CAPA \"SIZE 32\"\n", "MAIL FROM:<sender@example.com> SIZE=32", "250 Sender OK\r\n")]
    [DataRow("CAPA \"SIZE x\"\n", "MAIL FROM:<s> SIZE=99", "250 Sender OK\r\n")]
    [DataRow("", "RCPT", "501 Unrecognized parameter\r\n")]
    [DataRow("", "RCPT FROM:<a@b.cc>", "501 Unrecognized parameter\r\n")]
    [DataRow("", "RCPT TO:<recipient@example.com>", "250 Recipient OK\r\n")]
    [DataRow("", "RCPT TO:recipient@example.com", "501 Invalid address\r\n")]
    [DataRow("", "RCPT TO:<>", "501 Invalid address\r\n")]
    [DataRow("", "RCPT TO:<a@b.cc", "501 Invalid address\r\n")]
    [DataRow("", "RCPT TO:<recipient>", "501 Invalid address\r\n")]
    [DataRow("", "RCPT TO:<@example.com>", "501 Invalid address\r\n")]
    [DataRow("", "RCPT TO:<a@example>", "501 Invalid address\r\n")]
    [DataRow("", "RCPT TO:<a@example.c>", "501 Invalid address\r\n")]
    [DataRow("", "RCPT TO:<a@example.c0m>", "501 Invalid address\r\n")]
    [DataRow("", "RCPT TO:<a@.com>", "501 Invalid address\r\n")]
    [DataRow("", "RCPT TO:<a@ex_ample.com>", "501 Invalid address\r\n")]
    [DataRow("", "RCPT TO:<ré@example.com>", "501 Invalid address\r\n")]
    [DataRow("CAPA SMTPUTF8\n", "RCPT TO:<ré@exämple.com>", "250 Recipient OK\r\n")]
    [DataRow("CAPA SMTPUTF8\n", "RCPT TO:<rĀ@example.com>", "501 Invalid address\r\n")]
    [DataRow("", "DATA x", "501 Unrecognized parameter\r\n")]
    [DataRow("", "NOOP", "250 OK\r\n")]
    [DataRow("", "NOOP 0", "250 OK\r\n")]
    [DataRow("", "NOOP x", "501 Unrecognized parameter\r\n")]
    [DataRow("", "RSET", "250 Resetting\r\n")]
    [DataRow("", "RSET x", "501 Unrecognized parameter\r\n")]
    [DataRow("", "HELP", "214-This server supports the following commands:\r\n214 HELO EHLO RCPT DATA RSET MAIL VRFY EXPN QUIT HELP\r\n")]
    [DataRow("AUTH PLAIN\n", "HELP", "214-This server supports the following commands:\r\n214 HELO EHLO RCPT DATA RSET MAIL VRFY EXPN QUIT HELP AUTH\r\n")]
    [DataRow("", "VRFY", "501 Unrecognized parameter\r\n")]
    [DataRow("", "VRFY recipient", "250 <recipient@example.com>\r\n")]
    [DataRow("", "VRFY recipient@example.net extra", "250 <recipient@example.net>\r\n")]
    [DataRow("", "VRFY bad<name", "501 Invalid address\r\n")]
    [DataRow("", "EXPN", "501 Unrecognized parameter\r\n")]
    [DataRow("", "EXPN list", "")]
    [DataRow("", "QUIT", "221 curl  server signing off\r\n")]
    [DataRow("", "AUTH PLAIN", "500 AUTH is not dealt with!\r\n")]
    [DataRow("", "*", "500 * is not dealt with!\r\n")]
    [DataRow("", "AHVzZXIAc2VjcmV0", "500 AHVzZXIAc2VjcmV0 is not dealt with!\r\n")]
    [DataRow("AUTH PLAIN\nREPLY AUTH 334 PLAIN supported\nREPLY AHVzZXIAc2VjcmV0 235 Authenticated\n", "AUTH PLAIN", "334 PLAIN supported\r\n")]
    [DataRow("REPLY AHVzZXIAc2VjcmV0 235 Authenticated\n", "AHVzZXIAc2VjcmV0", "235 Authenticated\r\n")]
    [DataRow("REPLY MAIL 550 no\n", "MAIL FROM:<a@b.cc>", "550 no\r\n")]
    public void Answer_CommandOnFreshConnection_IsFtpserverPlsReply(string serverCommands, string commandLine, string expected)
    {
        LineProtocolReply reply = Create(serverCommands).Answer(commandLine);

        Assert.AreEqual(expected, Encoding.Latin1.GetString(reply.Bytes.Span));
        Assert.IsFalse(reply.ClosesConnection);
    }

    [TestMethod]
    [DataRow("HELO 1", "QUIT", "221 curl SMTP server signing off\r\n")]
    [DataRow("EHLO 1", "QUIT", "221 curl ESMTP server signing off\r\n")]
    [DataRow("EHLO verifiedserver", "HELP", "")]
    [DataRow("EHLO user", "DATA", "501 Invalid arguments\r\n")]
    [DataRow("EHLO 1", "VRFY recipient", "250 <recipient@example.com>\r\n")]
    public void Answer_AfterHello_DependsOnTheClientName(string hello, string commandLine, string expected)
    {
        SmtpResponder responder = Create(string.Empty);
        responder.Answer(hello);

        string answer = Encoding.Latin1.GetString(responder.Answer(commandLine).Bytes.Span);

        Assert.AreEqual(expected.Length == 0 ? $"214 WE ROOLZ: {Environment.ProcessId}\r\n" : expected, answer);
    }

    [TestMethod]
    [DataRow("EHLO 1100", "data", "250 <data@example.com>\r\n")]
    [DataRow("EHLO 10012", "data", "250 <data@example.com>\r\n")]
    [DataRow("EHLO a10012", "data12", "250 <data12@example.com>\r\n")]
    [DataRow("EHLO 10012", "data13", "250 <data@example.com>\r\n")]
    public void Answer_VrfyWithReplyData_SendsTheClientsDataPart(string hello, string partName, string partBody)
    {
        Dictionary<string, byte[]> parts = new() { ["data"] = Encoding.Latin1.GetBytes("250 <data@example.com>\r\n"), [partName] = Encoding.Latin1.GetBytes(partBody) };
        SmtpResponder responder = new(LineProtocolServerCommands.Read([]), parts);
        responder.Answer(hello);

        Assert.AreEqual(partBody, Encoding.Latin1.GetString(responder.Answer("VRFY anyone").Bytes.Span));
    }

    [TestMethod]
    public void Answer_VrfyWithEmptyNumberedPart_FallsBackToData()
    {
        Dictionary<string, byte[]> parts = new() { ["data"] = Encoding.Latin1.GetBytes("553-Ambiguous\r\n553 <a@b.cc>\r\n"), ["data12"] = [] };
        SmtpResponder responder = new(LineProtocolServerCommands.Read([]), parts);
        responder.Answer("EHLO 10012");

        Assert.AreEqual("553-Ambiguous\r\n553 <a@b.cc>\r\n", Encoding.Latin1.GetString(responder.Answer("EXPN list").Bytes.Span));
    }

    [TestMethod]
    public void Answer_DataThroughTheDotLine_StoresTheMessageAsSent()
    {
        SmtpResponder responder = Create(string.Empty);
        responder.Answer("EHLO 900");
        responder.Answer("MAIL FROM:<sender@example.com>");
        responder.Answer("RCPT TO:<recipient@example.com>");

        Assert.AreEqual("354 Show me the mail\r\n", Encoding.Latin1.GetString(responder.Answer("DATA").Bytes.Span));
        Assert.AreEqual(0, responder.Answer("From: different").Bytes.Length);
        Assert.AreEqual(0, responder.Answer("..stuffed").Bytes.Length);
        Assert.AreEqual("250 OK, data received!\r\n", Encoding.Latin1.GetString(responder.Answer(".").Bytes.Span));
        Assert.AreEqual("221 curl ESMTP server signing off\r\n", Encoding.Latin1.GetString(responder.Answer("QUIT").Bytes.Span));

        Assert.AreEqual("From: different\r\n..stuffed\r\n.\r\n", Encoding.Latin1.GetString(responder.UploadedMessage.Span));
        CollectionAssert.AreEqual(
            new[] { "EHLO 900\r\n", "MAIL FROM:<sender@example.com>\r\n", "RCPT TO:<recipient@example.com>\r\n", "DATA\r\n", "QUIT\r\n" },
            responder.ReceivedCommandLines.ToArray());
    }

    [TestMethod]
    public void Answer_DataReplyLine_OverridesAndReceivesNoMessage()
    {
        SmtpResponder responder = Create("REPLY DATA 550 refused\n");

        Assert.AreEqual("550 refused\r\n", Encoding.Latin1.GetString(responder.Answer("DATA").Bytes.Span));
        Assert.AreEqual("221 curl  server signing off\r\n", Encoding.Latin1.GetString(responder.Answer("QUIT").Bytes.Span));
    }

    [TestMethod]
    [DataRow("EHLO_900")]
    [DataRow("a.b")]
    [DataRow("abc===")]
    [DataRow("MAILS FROM:<a>")]
    public void Answer_UnrecognizedLine_Answers500AndCloses(string commandLine)
    {
        LineProtocolReply reply = Create(string.Empty).Answer(commandLine);

        Assert.AreEqual("500 Unrecognized command\r\n", Encoding.Latin1.GetString(reply.Bytes.Span));
        Assert.IsTrue(reply.ClosesConnection);
    }

    [TestMethod]
    public void Answer_Base64LineOver512Characters_IsUnrecognized() =>
        Assert.IsTrue(Create(string.Empty).Answer(new string('A', 513)).ClosesConnection);

    [TestMethod]
    public void Answer_Base64LineOf512CharactersAndPadding_IsACommand() =>
        Assert.IsFalse(Create(string.Empty).Answer(new string('A', 512) + "==").ClosesConnection);

    private static SmtpResponder Create(string serverCommands) =>
        new(LineProtocolServerCommands.Read(Encoding.Latin1.GetBytes(serverCommands)), new Dictionary<string, byte[]>());
}
