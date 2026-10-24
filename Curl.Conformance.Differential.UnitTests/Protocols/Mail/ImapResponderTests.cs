using System.Text;
using Curl.Conformance.Differential.Protocols.Core;
using Curl.Conformance.Differential.Protocols.Mail;
using Curl.Conformance.Differential.UnitTests.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Protocols.Mail;

[TestClass]
public sealed class ImapResponderTests
{
    private const string Banner = "* OK mock imap\r\n";
    private const string Selected = "* 2 EXISTS\r\n* 0 RECENT\r\n* OK [UIDVALIDITY 7] ok\r\na1 OK [READ-WRITE] selected\r\n";

    [TestMethod]
    [DataRow("a1 CAPABILITY", "* CAPABILITY IMAP4rev1 AUTH=PLAIN UIDPLUS\r\na1 OK done\r\n")]
    [DataRow("a1 capability", "* CAPABILITY IMAP4rev1 AUTH=PLAIN UIDPLUS\r\na1 OK done\r\n")]
    [DataRow("a1 LOGIN u p", "a1 OK logged in\r\n")]
    [DataRow("a1 LIST \"\" *", "* LIST (\\HasNoChildren) \"/\" \"INBOX\"\r\n* LIST (\\HasNoChildren) \"/\" \"Sent\"\r\na1 OK done\r\n")]
    [DataRow("a1 SELECT INBOX", Selected)]
    [DataRow("a1 EXAMINE INBOX", Selected)]
    [DataRow("a1 UID FETCH 1 BODY[]", "a1 OK uid done\r\n")]
    [DataRow("a1 SEARCH ALL", "* SEARCH 1 2\r\na1 OK done\r\n")]
    [DataRow("a1 STORE 1 +FLAGS (\\Seen)", "a1 OK stored\r\n")]
    [DataRow("a1 EXPUNGE", "a1 OK expunged\r\n")]
    [DataRow("a1 STATUS INBOX (MESSAGES)", "* STATUS INBOX (MESSAGES 2)\r\na1 OK done\r\n")]
    [DataRow("a1 NOOP", "a1 OK noop\r\n")]
    [DataRow("a1 BOGUS", "a1 BAD unknown\r\n")]
    [DataRow("a1", "a1 BAD unknown\r\n")]
    [DataRow("a1 LOGOUT", "* BYE\r\na1 OK bye\r\n")]
    public async Task RespondAsync_Command_AnswersTheReplyAndLogsTheLine(string line, string reply)
    {
        var (output, log) = await RunAsync(line + "\r\n");

        Assert.AreEqual(Banner + reply, output);
        Assert.AreEqual(line, log);
    }

    [TestMethod]
    public async Task RespondAsync_Authenticate_ContinuesOnceAndLogsTheAnswer()
    {
        var (output, log) = await RunAsync("a1 AUTHENTICATE PLAIN\r\nc2VjcmV0\r\n");

        Assert.AreEqual(Banner + "+ \r\na1 OK auth\r\n", output);
        Assert.AreEqual("a1 AUTHENTICATE PLAIN\nc2VjcmV0", log);
    }

    [TestMethod]
    public async Task RespondAsync_ClientHangsUpDuringAuthenticate_EndsWithoutSuccess()
    {
        var (output, _) = await RunAsync("a1 AUTHENTICATE PLAIN\r\n");

        Assert.AreEqual(Banner + "+ \r\n", output);
    }

    [TestMethod]
    [DataRow("a1 FETCH 1 BODY[]", 1, "From: a@b\r\nSubject: one\r\n\r\nbody one\r\n")]
    [DataRow("a1 FETCH 2   (FLAGS)", 2, "From: c@d\r\nSubject: two\r\n\r\nbody two\r\n")]
    [DataRow("a1 FETCH 0 BODY[]", 0, "From: c@d\r\nSubject: two\r\n\r\nbody two\r\n")]
    public async Task RespondAsync_Fetch_SendsTheMessageAsALiteral(string line, int number, string message)
    {
        var (output, _) = await RunAsync(line + "\r\n");

        Assert.AreEqual($"{Banner}* {number} FETCH (BODY[] {{{message.Length}}}\r\n{message})\r\na1 OK done\r\n", output);
    }

    [TestMethod]
    [DataRow("a1 FETCH")]
    [DataRow("a1 FETCH ")]
    [DataRow("a1 FETCH x BODY[]")]
    [DataRow("a1 FETCH 3 BODY[]")]
    [DataRow("a1 APPEND INBOX")]
    [DataRow("a1 APPEND INBOX {x}")]
    public async Task RespondAsync_CommandThePythonHandlerCrashesOn_EndsTheConnection(string line)
    {
        var (output, _) = await RunAsync(line + "\r\na2 NOOP\r\n");

        Assert.AreEqual(Banner, output);
    }

    [TestMethod]
    [DataRow("a1 APPEND INBOX {5}\r\nhello\r\n", "[append b'hello']")]
    [DataRow("a1 APPEND INBOX {5+}\r\nhello\r\n", "[append b'hello']")]
    [DataRow("a1 APPEND INBOX {3}\r\na\r\nb\r\n", "[append b'a\\r\\n']")]
    public async Task RespondAsync_Append_ReadsTheLiteralAndLogsIt(string input, string appendLog)
    {
        var (output, log) = await RunAsync(input + "a2 NOOP\r\n");

        Assert.AreEqual(Banner + "+ go\r\na1 OK appended\r\na2 OK noop\r\n", output);
        Assert.AreEqual(input[..input.IndexOf("\r\n", StringComparison.Ordinal)] + "\n" + appendLog + "\na2 NOOP", log);
    }

    private static async Task<(string Output, string Log)> RunAsync(string input)
    {
        var log = new ProtocolWireLog();
        using var connection = new DuplexMemoryStream(Encoding.Latin1.GetBytes(input));

        await new ImapResponder(log).RespondAsync(connection, CancellationToken.None);

        return (Encoding.Latin1.GetString(connection.Written), Encoding.UTF8.GetString(log.Take()));
    }
}
