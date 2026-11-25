using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>Pins that <see cref="SmtpServerConnector"/> serves SMTP on <c>%SMTPPORT</c>, records the command lines and the message, and passes every other port on.</summary>
[TestClass]
public sealed class SmtpServerConnectorTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ConnectAsync_SmtpPortTwice_RecordsEveryConnectionsCommandsAndTheLastMessage()
    {
        SmtpServerConnector connector = new(
            ParsedTestCase.From("<reply>\n<servercmd>\nREPLY welcome 220 hi\n</servercmd>\n</reply>\n"),
            new NoListenPortConnector(new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System)));

        await using IConnection first = (await connector.ConnectAsync(new ConnectTarget("127.0.0.1", SmtpServerConnector.SmtpPort, false), TestContext.CancellationToken)).Connection!;
        string greeting = await ReadTextAsync(first);
        await first.WriteAsync(Encoding.Latin1.GetBytes("DATA\r\n"), TestContext.CancellationToken);
        await ReadTextAsync(first);
        await first.WriteAsync(Encoding.Latin1.GetBytes("body\r\n.\r\n"), TestContext.CancellationToken);
        await ReadTextAsync(first);
        await using IConnection second = (await connector.ConnectAsync(new ConnectTarget("127.0.0.1", SmtpServerConnector.SmtpPort, false), TestContext.CancellationToken)).Connection!;
        await ReadTextAsync(second);
        await second.WriteAsync(Encoding.Latin1.GetBytes("NOOP\r\n"), TestContext.CancellationToken);
        await ReadTextAsync(second);

        Assert.AreEqual("220 hi\r\n", greeting);
        Assert.AreEqual("DATA\r\nNOOP\r\n", Encoding.Latin1.GetString(connector.ProtocolLog.Span));
        Assert.AreEqual("body\r\n.\r\n", Encoding.Latin1.GetString(connector.UploadedMessage.Span));
    }

    [TestMethod]
    public async Task ConnectAsync_VrfyWithCrlfReplyData_SendsTheReplyLinesWithCrlf()
    {
        SmtpServerConnector connector = new(
            ParsedTestCase.From("<reply>\n<data crlf=\"yes\">\n553-Ambiguous; Possibilities are:\n553 <smith@example.com>\n</data>\n</reply>\n"),
            new NoListenPortConnector(new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System)));

        await using IConnection connection = (await connector.ConnectAsync(new ConnectTarget("127.0.0.1", SmtpServerConnector.SmtpPort, false), TestContext.CancellationToken)).Connection!;
        await ReadTextAsync(connection);
        await connection.WriteAsync(Encoding.Latin1.GetBytes("VRFY smith\r\n"), TestContext.CancellationToken);
        string reply = await ReadTextAsync(connection);

        Assert.AreEqual("553-Ambiguous; Possibilities are:\r\n553 <smith@example.com>\r\n", reply);
    }

    [TestMethod]
    public async Task ConnectAsync_OtherPort_ReachesTheWrappedServerAndRecordsNothing()
    {
        SmtpServerConnector connector = new(
            ParsedTestCase.From(string.Empty),
            new NoListenPortConnector(new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System)));

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", NoListenPortConnector.NoListenPort, false), TestContext.CancellationToken);

        Assert.IsTrue(result.IsConnectionRefused);
        Assert.AreEqual(0, connector.ProtocolLog.Length);
        Assert.AreEqual(0, connector.UploadedMessage.Length);
    }

    [TestMethod]
    public async Task ConnectAsync_SlowDownServer_GivesEachReplyOneBytePerReadAfterAWaitOnTheClock()
    {
        ManualTimeProvider clock = new();
        SmtpServerConnector connector = new(
            ParsedTestCase.From("<reply>\n<servercmd>\nSLOWDOWN\nREPLY welcome 220 hi\nREPLY NOOP 250 no\n</servercmd>\n</reply>\n"),
            new NoListenPortConnector(new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System)),
            clock);
        await using IConnection connection = (await connector.ConnectAsync(new ConnectTarget("127.0.0.1", SmtpServerConnector.SmtpPort, false), TestContext.CancellationToken)).Connection!;

        string greeting = await ReadByteByByteAsync(connection, clock, "220 hi\r\n".Length);
        await connection.WriteAsync(Encoding.Latin1.GetBytes("NOOP\r\n"), TestContext.CancellationToken);
        string reply = await ReadByteByByteAsync(connection, clock, "250 no\r\n".Length);

        Assert.AreEqual("220 hi\r\n", greeting);
        Assert.AreEqual("250 no\r\n", reply);
        Assert.AreEqual(16 * LineProtocolServerConnector.SlowReplyByteDelay, clock.Now);
    }

    // Each read is pending until the clock moves on by the byte delay, then holds exactly one byte.
    private async Task<string> ReadByteByByteAsync(IConnection connection, ManualTimeProvider clock, int count)
    {
        StringBuilder text = new();
        byte[] buffer = new byte[256];
        for (int index = 0; index < count; index++)
        {
            Task<int> read = connection.ReadAsync(buffer, TestContext.CancellationToken).AsTask();
            Assert.IsFalse(read.IsCompleted);
            clock.Advance(LineProtocolServerConnector.SlowReplyByteDelay);
            Assert.AreEqual(1, await read);
            text.Append((char)buffer[0]);
        }

        return text.ToString();
    }

    private async Task<string> ReadTextAsync(IConnection connection)
    {
        byte[] buffer = new byte[4096];
        int read = await connection.ReadAsync(buffer, TestContext.CancellationToken);
        return Encoding.Latin1.GetString(buffer, 0, read);
    }
}
