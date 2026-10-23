using System.Text;
using Curl.Conformance.Differential.Protocols.Core;
using Curl.Conformance.Differential.Protocols.Mail;
using Curl.Conformance.Differential.UnitTests.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Protocols.Mail;

[TestClass]
public sealed class Pop3ResponderTests
{
    private const string Banner = "+OK mock pop3\r\n";
    private const string FirstMessage = "From: a@b\r\nSubject: one\r\n\r\nbody one\r\n";

    [TestMethod]
    [DataRow("CAPA", "+OK\r\nTOP\r\nUIDL\r\nUSER\r\nSASL PLAIN\r\n.\r\n")]
    [DataRow("USER u", "+OK\r\n")]
    [DataRow("PASS p", "+OK logged in\r\n")]
    [DataRow("STAT", "+OK 2 74\r\n")]
    [DataRow("LIST", "+OK\r\n1 37\r\n2 37\r\n.\r\n")]
    [DataRow("LIST 2", "+OK 2 37\r\n")]
    [DataRow("LIST +1", "+OK +1 37\r\n")]
    [DataRow("LIST 0", "+OK 0 37\r\n")]
    [DataRow("UIDL", "+OK\r\n1 uid1\r\n2 uid2\r\n.\r\n")]
    [DataRow("RETR 1", "+OK\r\n" + FirstMessage + ".\r\n")]
    [DataRow("RETR 3", "-ERR no such message\r\n")]
    [DataRow("RETR 0", "+OK\r\nFrom: c@d\r\nSubject: two\r\n\r\nbody two\r\n.\r\n")]
    [DataRow("TOP 1 0", "+OK\r\nFrom: a@b\r\nSubject: one\r\n.\r\n")]
    [DataRow("DELE 1", "+OK deleted\r\n")]
    [DataRow("NOOP", "+OK\r\n")]
    [DataRow("noop", "+OK\r\n")]
    [DataRow("BOGUS", "-ERR unknown\r\n")]
    [DataRow("QUIT", "+OK bye\r\n")]
    public async Task RespondAsync_Command_AnswersTheReplyAndLogsTheLine(string line, string reply)
    {
        var (output, log) = await RunAsync(line + "\r\nNOOP\r\n");

        var ends = line == "QUIT";
        Assert.AreEqual(Banner + reply + (ends ? string.Empty : "+OK\r\n"), output);
        Assert.AreEqual(ends ? line : line + "\nNOOP", log);
    }

    [TestMethod]
    [DataRow("LIST x")]
    [DataRow("LIST 3")]
    [DataRow("LIST -3")]
    [DataRow("LIST ")]
    [DataRow("RETR")]
    [DataRow("RETR x")]
    [DataRow("RETR -3")]
    [DataRow("TOP")]
    [DataRow("TOP x")]
    [DataRow("TOP 3")]
    public async Task RespondAsync_NumberThePythonHandlerCrashesOn_EndsTheConnection(string line)
    {
        var (output, _) = await RunAsync(line + "\r\nNOOP\r\n");

        Assert.AreEqual(Banner, output);
    }

    private static async Task<(string Output, string Log)> RunAsync(string input)
    {
        var log = new ProtocolWireLog();
        using var connection = new DuplexMemoryStream(Encoding.Latin1.GetBytes(input));

        await new Pop3Responder(log).RespondAsync(connection, CancellationToken.None);

        return (Encoding.Latin1.GetString(connection.Written), Encoding.UTF8.GetString(log.Take()));
    }
}
