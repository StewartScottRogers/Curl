using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>Pins that <see cref="ImapServerConnector"/> serves IMAP on <c>%IMAPPORT</c>, records the command lines and the APPEND literal, and passes every other port on.</summary>
[TestClass]
public sealed class ImapServerConnectorTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ConnectAsync_ImapPortTwice_RecordsEveryConnectionsCommandsAndTheLastAppendLiteral()
    {
        ImapServerConnector connector = new(
            ParsedTestCase.From("<reply>\n<servercmd>\nREPLY welcome * OK hi\n</servercmd>\n</reply>\n"),
            new NoListenPortConnector(new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System)));

        await using IConnection first = (await connector.ConnectAsync(new ConnectTarget("127.0.0.1", ImapServerConnector.ImapPort, false), TestContext.CancellationToken)).Connection!;
        string greeting = await ReadTextAsync(first);
        await first.WriteAsync(Encoding.Latin1.GetBytes("A1 APPEND INBOX {4}\r\n"), TestContext.CancellationToken);
        await ReadTextAsync(first);
        await first.WriteAsync(Encoding.Latin1.GetBytes("body\r\n"), TestContext.CancellationToken);
        await ReadTextAsync(first);
        await using IConnection second = (await connector.ConnectAsync(new ConnectTarget("127.0.0.1", ImapServerConnector.ImapPort, false), TestContext.CancellationToken)).Connection!;
        await ReadTextAsync(second);
        await second.WriteAsync(Encoding.Latin1.GetBytes("A2 NOOP\r\n"), TestContext.CancellationToken);
        await ReadTextAsync(second);

        Assert.AreEqual("* OK hi\r\n", greeting);
        Assert.AreEqual("A1 APPEND INBOX {4}\r\nA2 NOOP\r\n", Encoding.Latin1.GetString(connector.ProtocolLog.Span));
        Assert.AreEqual("body", Encoding.Latin1.GetString(connector.UploadedMessage.Span));
    }

    [TestMethod]
    public async Task ConnectAsync_OtherPort_ReachesTheWrappedServerAndRecordsNothing()
    {
        ImapServerConnector connector = new(
            ParsedTestCase.From(string.Empty),
            new NoListenPortConnector(new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System)));

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", NoListenPortConnector.NoListenPort, false), TestContext.CancellationToken);

        Assert.IsTrue(result.IsConnectionRefused);
        Assert.AreEqual(0, connector.ProtocolLog.Length);
        Assert.AreEqual(0, connector.UploadedMessage.Length);
    }

    private async Task<string> ReadTextAsync(IConnection connection)
    {
        byte[] buffer = new byte[4096];
        int read = await connection.ReadAsync(buffer, TestContext.CancellationToken);
        return Encoding.Latin1.GetString(buffer, 0, read);
    }
}
