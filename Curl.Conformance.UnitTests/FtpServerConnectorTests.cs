using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>Pins that <see cref="FtpServerConnector"/> serves the FTP control channel on <c>%FTPPORT</c> and passes every other port on.</summary>
[TestClass]
public sealed class FtpServerConnectorTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ConnectAsync_FtpPort_AnswersFromTheCasesReplyLinesAndRecordsTheCommands()
    {
        FtpServerConnector connector = new(
            ParsedTestCase.From("<reply>\n<servercmd>\nREPLY welcome 220 hi\nREPLY USER 530 no\n</servercmd>\n</reply>\n"),
            new NoListenPortConnector(new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System)));

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", FtpServerConnector.FtpPort, false), TestContext.CancellationToken);
        await using IConnection connection = result.Connection!;
        string greeting = await ReadTextAsync(connection);
        await connection.WriteAsync(Encoding.Latin1.GetBytes("USER a\r\n"), TestContext.CancellationToken);
        string reply = await ReadTextAsync(connection);

        Assert.AreEqual("220 hi\r\n", greeting);
        Assert.AreEqual("530 no\r\n", reply);
        Assert.AreEqual("USER a\r\n", Encoding.Latin1.GetString(connector.ReceivedBytes.Span));
    }

    [TestMethod]
    public async Task ConnectAsync_OtherPort_ReachesTheWrappedServer()
    {
        FtpServerConnector connector = new(
            ParsedTestCase.From(string.Empty),
            new NoListenPortConnector(new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System)));

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", NoListenPortConnector.NoListenPort, false), TestContext.CancellationToken);

        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    public async Task ConnectAsync_PassivePortWithNoDataConnectionOffered_IsRefused()
    {
        FtpServerConnector connector = new(
            ParsedTestCase.From(string.Empty),
            new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System));

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", FtpServerConnector.PassivePort, false), TestContext.CancellationToken);

        Assert.IsTrue(result.IsConnectionRefused);
    }

    private async Task<string> ReadTextAsync(IConnection connection)
    {
        byte[] buffer = new byte[256];
        int read = await connection.ReadAsync(buffer, TestContext.CancellationToken);
        return Encoding.Latin1.GetString(buffer, 0, read);
    }
}
