using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="LineProtocolServerConnector"/>, the shared core of the ftpserver.pl stand-ins,
/// through a test-only echo protocol: greeting, CRLF line splitting across writes, replies,
/// recording for <c>&lt;verify&gt;&lt;protocol&gt;</c> and close on request.
/// </summary>
[TestClass]
public sealed class LineProtocolServerConnectorTests
{
    private static readonly ConnectTarget Target = new("127.0.0.1", 8992, false);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Connect_GreetingIsReadFirst()
    {
        await using IConnection connection = await ConnectAsync(new LineProtocolServerConnector(() => new EchoResponder()));

        Assert.AreEqual("+OK echo ready\r\n", await ReadTextAsync(connection, 64));
    }

    [TestMethod]
    public async Task Connect_GivesLoopbackEndPointsAndCountsLocalPorts()
    {
        LineProtocolServerConnector server = new(() => new EchoResponder());
        await using IConnection first = await ConnectAsync(server);
        await using IConnection second = await ConnectAsync(server);

        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 8992), first.RemoteEndPoint);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 49152), first.LocalEndPoint);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 49153), second.LocalEndPoint);
        Assert.IsFalse(first.IsSecure);
    }

    [TestMethod]
    public void Construct_NullFactory_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new LineProtocolServerConnector(null!));

    [TestMethod]
    public async Task Write_LineSplitAcrossWrites_IsAnsweredOnceComplete()
    {
        await using IConnection connection = await ConnectAsync(new LineProtocolServerConnector(() => new EchoResponder()));
        await ReadTextAsync(connection, 64);

        await WriteTextAsync(connection, "E");
        await WriteTextAsync(connection, "CHO a");
        await WriteTextAsync(connection, "b\r");
        await WriteTextAsync(connection, "\nECHO c\r\nECHO d\r\n");
        await connection.FlushAsync(TestContext.CancellationToken);

        Assert.AreEqual("ab\r\nc\r\nd\r\n", await ReadTextAsync(connection, 64));
    }

    [TestMethod]
    public async Task Write_LoneLineFeed_StaysPartOfTheLine()
    {
        await using IConnection connection = await ConnectAsync(new LineProtocolServerConnector(() => new EchoResponder()));
        await ReadTextAsync(connection, 64);

        await WriteTextAsync(connection, "ECHO x\ny\r\n");

        Assert.AreEqual("x\ny\r\n", await ReadTextAsync(connection, 64));
    }

    [TestMethod]
    public async Task Read_SmallBuffer_ReadsTheReplyInPieces()
    {
        await using IConnection connection = await ConnectAsync(new LineProtocolServerConnector(() => new EchoResponder()));

        Assert.AreEqual("+OK e", await ReadTextAsync(connection, 5));
        Assert.AreEqual("cho ready\r\n", await ReadTextAsync(connection, 64));
    }

    [TestMethod]
    public async Task Read_NothingWaiting_WaitsForTheNextWrite()
    {
        await using IConnection connection = await ConnectAsync(new LineProtocolServerConnector(() => new EchoResponder()));
        await ReadTextAsync(connection, 64);

        Task<string> read = ReadTextAsync(connection, 64);
        Assert.IsFalse(read.IsCompleted);
        await WriteTextAsync(connection, "ECHO late\r\n");

        Assert.AreEqual("late\r\n", await read);
    }

    [TestMethod]
    public async Task Read_NothingWaiting_IsCancelled()
    {
        await using IConnection connection = await ConnectAsync(new LineProtocolServerConnector(() => new EchoResponder()));
        await ReadTextAsync(connection, 64);
        using CancellationTokenSource cancellation = new();

        Task<int> read = connection.ReadAsync(new byte[8], cancellation.Token).AsTask();
        await cancellation.CancelAsync();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => read);
    }

    [TestMethod]
    public async Task Quit_ClosesAndDropsLaterBytesUnrecorded()
    {
        LineProtocolServerConnector server = new(() => new EchoResponder());
        await using IConnection connection = await ConnectAsync(server);
        await ReadTextAsync(connection, 64);

        await WriteTextAsync(connection, "ECHO a\r\nQUIT\r\nECHO b\r\n");
        await WriteTextAsync(connection, "ECHO c\r\n");

        Assert.AreEqual("a\r\n+OK bye\r\n", await ReadTextAsync(connection, 64));
        Assert.AreEqual(0, await connection.ReadAsync(new byte[8], TestContext.CancellationToken));
        Assert.AreEqual("ECHO a\r\nQUIT\r\n", Encoding.Latin1.GetString(server.ReceivedBytes.Span));
    }

    [TestMethod]
    public async Task ReceivedBytes_RecordsEveryConnectionInWriteOrder()
    {
        LineProtocolServerConnector server = new(() => new EchoResponder());
        await using IConnection first = await ConnectAsync(server);
        await using IConnection second = await ConnectAsync(server);

        await WriteTextAsync(first, "ECHO 1\r\nPART");
        await WriteTextAsync(second, "ECHO 2\r\n");

        Assert.AreEqual("ECHO 1\r\nPARTECHO 2\r\n", Encoding.Latin1.GetString(server.ReceivedBytes.Span));
    }

    [TestMethod]
    public void EmulatedServers_AreFtpSmtpAndImap_TheStandInsTheRunnerWiresIn() =>
        CollectionAssert.AreEqual(new[] { "ftp", "smtp", "imap" }, LineProtocolServerConnector.EmulatedServers.ToArray());

    [TestMethod]
    public void ServerCommands_ReplyLines_AreFoundByCommandNameIgnoringCase()
    {
        LineProtocolServerCommands commands = LineProtocolServerCommands.Read(
            "REPLY PASV 500 no such command\r\nREPLY pasv 502 later wins\nDELAY LIST 2\nREPLY EPSV\n"u8);

        Assert.IsTrue(commands.TryFindReply("Pasv", out byte[] reply));
        Assert.AreEqual("502 later wins\r\n", Encoding.Latin1.GetString(reply));
        Assert.IsFalse(commands.TryFindReply("EPSV", out _));
        Assert.IsFalse(commands.TryFindReply("LIST", out _));
    }

    [TestMethod]
    public void ServerCommands_Empty_FindsNothing() =>
        Assert.IsFalse(LineProtocolServerCommands.Read([]).TryFindReply("USER", out _));

    private async Task<IConnection> ConnectAsync(LineProtocolServerConnector server)
    {
        ConnectResult result = await server.ConnectAsync(Target, TestContext.CancellationToken);
        return result.Connection!;
    }

    private Task WriteTextAsync(IConnection connection, string text) =>
        connection.WriteAsync(Encoding.Latin1.GetBytes(text), TestContext.CancellationToken).AsTask();

    private async Task<string> ReadTextAsync(IConnection connection, int bufferSize)
    {
        byte[] buffer = new byte[bufferSize];
        int count = await connection.ReadAsync(buffer, TestContext.CancellationToken);
        return Encoding.Latin1.GetString(buffer, 0, count);
    }

    // A test-only protocol: "ECHO <text>" answers the text, "QUIT" answers and closes, anything else answers nothing.
    private sealed class EchoResponder : ILineProtocolResponder
    {
        public ReadOnlyMemory<byte> Greeting { get; } = "+OK echo ready\r\n"u8.ToArray();

        public LineProtocolReply Answer(string commandLine) => commandLine switch
        {
            "QUIT" => new LineProtocolReply("+OK bye\r\n"u8.ToArray(), true),
            _ when commandLine.StartsWith("ECHO ", StringComparison.Ordinal) => new LineProtocolReply(Encoding.Latin1.GetBytes(commandLine[5..] + "\r\n"), false),
            _ => new LineProtocolReply(ReadOnlyMemory<byte>.Empty, false),
        };
    }
}
