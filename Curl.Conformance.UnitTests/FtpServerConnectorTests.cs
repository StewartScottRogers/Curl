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
    public async Task ConnectAsync_Ftp6PortOnIpv6Loopback_ReachesTheControlChannelWithIpv6EndPoints()
    {
        FtpServerConnector connector = new(
            ParsedTestCase.From("<reply>\n<servercmd>\nREPLY welcome 220 six\n</servercmd>\n</reply>\n"),
            new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System));

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("[::1]", FtpServerConnector.Ftp6Port, false), TestContext.CancellationToken);
        await using IConnection connection = result.Connection!;
        string greeting = await ReadTextAsync(connection);

        Assert.AreEqual("220 six\r\n", greeting);
        Assert.AreEqual($"[::1]:{FtpServerConnector.Ftp6Port}", connection.RemoteEndPoint!.ToString());
        Assert.StartsWith("[::1]:", connection.LocalEndPoint!.ToString()!);
    }

    [TestMethod]
    public async Task ConnectAsync_EpsvOnFtp6Port_OffersThePassivePortAndItsDataConnection()
    {
        FtpServerConnector connector = new(
            ParsedTestCase.From(string.Empty),
            new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System));
        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("::1", FtpServerConnector.Ftp6Port, false), TestContext.CancellationToken);
        await using IConnection connection = result.Connection!;
        await ReadTextAsync(connection);

        await connection.WriteAsync(Encoding.Latin1.GetBytes("EPSV\r\n"), TestContext.CancellationToken);
        string reply = await ReadTextAsync(connection);
        ConnectResult data = await connector.ConnectAsync(new ConnectTarget("::1", FtpServerConnector.PassivePort, false), TestContext.CancellationToken);

        Assert.AreEqual($"229 Entering Passive Mode (|||{FtpServerConnector.PassivePort}|)\r\n", reply);
        Assert.IsNotNull(data.Connection);
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

    [TestMethod]
    public async Task ConnectAsync_SlowDownServer_GivesEachReplyOneBytePerReadAfterAWaitOnTheClock()
    {
        ManualTimeProvider clock = new();
        FtpServerConnector connector = new(
            ParsedTestCase.From("<reply>\n<servercmd>\nSLOWDOWN\nREPLY welcome 220 hi\nREPLY USER 530 no\n</servercmd>\n</reply>\n"),
            new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System),
            clock);
        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", FtpServerConnector.FtpPort, false), TestContext.CancellationToken);
        await using IConnection connection = result.Connection!;

        string greeting = await ReadByteByByteAsync(connection, clock, "220 hi\r\n".Length);
        await connection.WriteAsync(Encoding.Latin1.GetBytes("USER a\r\n"), TestContext.CancellationToken);
        await connection.FlushAsync(TestContext.CancellationToken);
        string reply = await ReadByteByByteAsync(connection, clock, "530 no\r\n".Length);

        Assert.AreEqual("220 hi\r\n", greeting);
        Assert.AreEqual("530 no\r\n", reply);
        Assert.AreEqual(16 * FtpServerConnector.SlowReplyByteDelay, clock.Now);
        Assert.AreEqual("USER a\r\n", Encoding.Latin1.GetString(connector.ReceivedBytes.Span));
        Assert.IsFalse(connection.IsSecure);
        Assert.AreEqual($"127.0.0.1:{FtpServerConnector.FtpPort}", connection.RemoteEndPoint!.ToString());
        Assert.StartsWith("127.0.0.1:", connection.LocalEndPoint!.ToString()!);
    }

    [TestMethod]
    public async Task ConnectAsync_SlowDownServerReadCancelledDuringItsWait_KeepsTheByteForTheNextRead()
    {
        ManualTimeProvider clock = new();
        FtpServerConnector connector = new(
            ParsedTestCase.From("<reply>\n<servercmd>\nSLOWDOWN\nREPLY welcome 220 hi\n</servercmd>\n</reply>\n"),
            new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System),
            clock);
        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", FtpServerConnector.FtpPort, false), TestContext.CancellationToken);
        await using IConnection connection = result.Connection!;
        using CancellationTokenSource cancelled = new();

        Task<int> read = connection.ReadAsync(new byte[256], cancelled.Token).AsTask();
        await cancelled.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => read);
        Assert.AreEqual("2", await ReadByteByByteAsync(connection, clock, 1));
    }

    [TestMethod]
    public async Task ConnectAsync_SlowDownDataOnly_GivesTheWholeReplyAtOnce()
    {
        FtpServerConnector connector = new(
            ParsedTestCase.From("<reply>\n<servercmd>\nSLOWDOWNDATA\nREPLY welcome 220 hi\n</servercmd>\n</reply>\n"),
            new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System),
            new ManualTimeProvider());

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", FtpServerConnector.FtpPort, false), TestContext.CancellationToken);
        await using IConnection connection = result.Connection!;

        Assert.AreEqual("220 hi\r\n", await ReadTextAsync(connection));
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
            clock.Advance(FtpServerConnector.SlowReplyByteDelay);
            Assert.AreEqual(1, await read);
            text.Append((char)buffer[0]);
        }

        return text.ToString();
    }

    private async Task<string> ReadTextAsync(IConnection connection)
    {
        byte[] buffer = new byte[256];
        int read = await connection.ReadAsync(buffer, TestContext.CancellationToken);
        return Encoding.Latin1.GetString(buffer, 0, read);
    }
}
