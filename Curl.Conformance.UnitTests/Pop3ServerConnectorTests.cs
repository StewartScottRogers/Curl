using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>Pins that <see cref="Pop3ServerConnector"/> serves POP3 on <c>%POP3PORT</c>, records the command lines, and passes every other port on.</summary>
[TestClass]
public sealed class Pop3ServerConnectorTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ConnectAsync_Pop3PortTwice_RecordsEveryConnectionsCommandsAndServesTheReplyData()
    {
        Pop3ServerConnector connector = new(
            ParsedTestCase.From("<reply>\n<servercmd>\nREPLY welcome +OK hi\n</servercmd>\n<data crlf=\"yes\">\nline\n</data>\n</reply>\n"),
            new NoListenPortConnector(new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System)));

        await using IConnection first = (await connector.ConnectAsync(new ConnectTarget("127.0.0.1", Pop3ServerConnector.Pop3Port, false), TestContext.CancellationToken)).Connection!;
        string greeting = await ReadTextAsync(first);
        await first.WriteAsync(Encoding.Latin1.GetBytes("RETR 1\r\n"), TestContext.CancellationToken);
        string message = await ReadTextAsync(first);
        await using IConnection second = (await connector.ConnectAsync(new ConnectTarget("127.0.0.1", Pop3ServerConnector.Pop3Port, false), TestContext.CancellationToken)).Connection!;
        await ReadTextAsync(second);
        await second.WriteAsync(Encoding.Latin1.GetBytes("NOOP\r\n"), TestContext.CancellationToken);
        await ReadTextAsync(second);

        Assert.AreEqual("+OK hi\r\n", greeting);
        StringAssert.Contains(message, "line\r\n.\r\n");
        Assert.AreEqual("RETR 1\r\nNOOP\r\n", Encoding.Latin1.GetString(connector.ProtocolLog.Span));
    }

    [TestMethod]
    public async Task ConnectAsync_OtherPort_ReachesTheWrappedServerAndRecordsNothing()
    {
        Pop3ServerConnector connector = new(
            ParsedTestCase.From(string.Empty),
            new NoListenPortConnector(new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System)));

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", NoListenPortConnector.NoListenPort, false), TestContext.CancellationToken);

        Assert.IsTrue(result.IsConnectionRefused);
        Assert.AreEqual(0, connector.ProtocolLog.Length);
    }

    private async Task<string> ReadTextAsync(IConnection connection)
    {
        byte[] buffer = new byte[4096];
        int read = await connection.ReadAsync(buffer, TestContext.CancellationToken);
        return Encoding.Latin1.GetString(buffer, 0, read);
    }
}
