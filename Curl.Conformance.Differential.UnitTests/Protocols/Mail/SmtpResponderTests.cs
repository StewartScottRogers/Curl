using System.Text;
using Curl.Conformance.Differential.Protocols.Core;
using Curl.Conformance.Differential.Protocols.Mail;
using Curl.Conformance.Differential.UnitTests.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Protocols.Mail;

[TestClass]
public sealed class SmtpResponderTests
{
    private const string Banner = "220 mock smtp\r\n";

    [TestMethod]
    [DataRow("EHLO x", "250-mock\r\n250-SIZE 100000\r\n250-8BITMIME\r\n250 AUTH PLAIN LOGIN\r\n")]
    [DataRow("ehlo x", "250-mock\r\n250-SIZE 100000\r\n250-8BITMIME\r\n250 AUTH PLAIN LOGIN\r\n")]
    [DataRow("HELO x", "250 mock\r\n")]
    [DataRow("MAIL FROM:<a@b>", "250 ok\r\n")]
    [DataRow("RCPT TO:<a@b>", "250 ok\r\n")]
    [DataRow("RCPT TO:<bad@b>", "550 no such user\r\n")]
    [DataRow("VRFY u", "250 user ok <u@x>\r\n")]
    [DataRow("EXPN l", "250-a@x\r\n250 b@x\r\n")]
    [DataRow("RSET", "250 ok\r\n")]
    [DataRow("NOOP", "250 ok\r\n")]
    [DataRow("QUIT", "221 bye\r\n")]
    [DataRow("BOGUS", "500 unknown\r\n")]
    [DataRow("", "500 unknown\r\n")]
    [DataRow("AUTH PLAIN dXNlcgBwYXNz", "235 ok\r\n")]
    public async Task RespondAsync_Command_AnswersTheReplyAndLogsTheLine(string line, string reply)
    {
        var (output, log) = await RunAsync(line + "\r\n");

        Assert.AreEqual(Banner + reply, output);
        Assert.AreEqual(line, log);
    }

    [TestMethod]
    public async Task RespondAsync_AuthPlainWithoutInitialResponse_ContinuesOnce()
    {
        var (output, log) = await RunAsync("AUTH PLAIN\r\nc2VjcmV0\r\n");

        Assert.AreEqual(Banner + "334 \r\n235 ok\r\n", output);
        Assert.AreEqual("AUTH PLAIN\nc2VjcmV0", log);
    }

    [TestMethod]
    public async Task RespondAsync_AuthLogin_ContinuesForUsernameAndPassword()
    {
        var (output, log) = await RunAsync("AUTH LOGIN\r\ndXNlcg==\r\ncGFzcw==\r\n");

        Assert.AreEqual(Banner + "334 VXNlcm5hbWU6\r\n334 UGFzc3dvcmQ6\r\n235 ok\r\n", output);
        Assert.AreEqual("AUTH LOGIN\ndXNlcg==\ncGFzcw==", log);
    }

    [TestMethod]
    [DataRow("AUTH PLAIN\r\n", "334 \r\n")]
    [DataRow("AUTH LOGIN\r\n", "334 VXNlcm5hbWU6\r\n")]
    [DataRow("AUTH LOGIN\r\nu\r\n", "334 VXNlcm5hbWU6\r\n334 UGFzc3dvcmQ6\r\n")]
    public async Task RespondAsync_ClientHangsUpDuringAuth_EndsWithoutSuccess(string input, string prompts)
    {
        var (output, _) = await RunAsync(input);

        Assert.AreEqual(Banner + prompts, output);
    }

    [TestMethod]
    public async Task RespondAsync_Data_LogsTheBodyAndStopsLoggingCommands()
    {
        var (output, log) = await RunAsync("DATA\r\nSubject: hi\r\nQUIT\r\n.\r\nNOOP\r\n");

        Assert.AreEqual(Banner + "354 go ahead\r\n250 queued\r\n250 ok\r\n", output);
        Assert.AreEqual("DATA\n[data b'Subject: hi\\nQUIT\\n']\nNOOP", log);
    }

    private static async Task<(string Output, string Log)> RunAsync(string input)
    {
        var log = new ProtocolWireLog();
        using var connection = new DuplexMemoryStream(Encoding.Latin1.GetBytes(input));

        await new SmtpResponder(log).RespondAsync(connection, CancellationToken.None);

        return (Encoding.Latin1.GetString(connection.Written), Encoding.UTF8.GetString(log.Take()));
    }
}
