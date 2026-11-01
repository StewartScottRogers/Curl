using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Conformance;

/// <summary>
/// Pins how <see cref="SwsHttpServerConnection"/> ends a connection (AF-0420, BL-4449): a reply that
/// closes ends the connection only once its bytes are read and its close time has come, stops serving
/// requests on it and records the disconnect once; a reply that does not close leaves the connection
/// open for the next request. Every read is bounded in real time, so a broken close fails rather than hangs.
/// </summary>
[TestClass]
public sealed class SwsHttpServerConnectionTests
{
    private const string Get = "GET /1234 HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n";

    private const string Disconnect = "[DISCONNECT]\n";

    private static readonly TimeSpan ReadLimit = TimeSpan.FromSeconds(10);

    private static readonly ConnectTarget Target = new("127.0.0.1", 8990, false);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task Send_ClosingReply_EndsTheConnectionAfterTheReplyAndRecordsTheDisconnectOnce()
    {
        Diagnostics.Arrange("reply", "reply swsclose, postcmd wait 1, connection-monitor");
        ManualTimeProvider clock = new();
        SwsHttpServerConnector server = new(Case(Reply("data", "reply swsclose\n"), Reply("postcmd", "wait 1\n"), Reply("servercmd", "connection-monitor\n")), clock);
        IConnection connection = await ConnectAsync(server);
        await WriteAsync(connection, Get);

        string reply = await ReadOnceAsync(connection);
        Task<string> close = ReadOnceAsync(connection);
        clock.Advance(TimeSpan.FromSeconds(1) - TimeSpan.FromTicks(1));
        bool closedBeforeItsTime = close.IsCompleted;
        clock.Advance(TimeSpan.FromTicks(1));
        string atTheClose = await close.WaitAsync(ReadLimit);
        await WriteAsync(connection, Get);
        string afterTheClose = await ReadOnceAsync(connection).WaitAsync(ReadLimit);
        string recordedAtTheClose = Text(server.ReceivedBytes);
        await connection.DisposeAsync();

        Diagnostics.Act("reply", reply);
        Assert.AreEqual("reply swsclose\n", reply, "the reply's bytes come before the close");
        Assert.IsFalse(closedBeforeItsTime, "the close waits for the reply's <postcmd> wait: it is the empty send queued after the reply, not a read that finds nothing");
        Assert.AreEqual(string.Empty, atTheClose, "the read at the close returns 0, ending the connection");
        Assert.AreEqual(string.Empty, afterTheClose, "a request written after the close goes unanswered");
        Assert.AreEqual(Get + Disconnect, recordedAtTheClose, "the close records the disconnect, and the request written after it is not recorded");
        Assert.AreEqual(Get + Disconnect, Text(server.ReceivedBytes), "disposing the closed connection records no second disconnect");
    }

    [TestMethod]
    public async Task Send_ReplyThatDoesNotClose_KeepsTheConnectionOpenForTheNextRequest()
    {
        Diagnostics.Arrange("reply", "first, connection-monitor");
        SwsHttpServerConnector server = new(Case(Reply("data", "first\n"), Reply("servercmd", "connection-monitor\n")));
        IConnection connection = await ConnectAsync(server);

        await WriteAsync(connection, Get);
        string firstReply = await ReadOnceAsync(connection).WaitAsync(ReadLimit);
        await WriteAsync(connection, Get);
        string secondReply = await ReadOnceAsync(connection).WaitAsync(ReadLimit);
        string recordedWhileOpen = Text(server.ReceivedBytes);
        await connection.DisposeAsync();

        Diagnostics.Act("replies", firstReply + secondReply);
        Assert.AreEqual("first\n", firstReply);
        Assert.AreEqual("first\n", secondReply, "the second request on the open connection is answered");
        Assert.AreEqual(Get + Get, recordedWhileOpen, "both requests are recorded and no disconnect is, while the connection is open");
        Assert.AreEqual(Get + Get + Disconnect, Text(server.ReceivedBytes), "the client's close records the disconnect");
    }

    private static UpstreamTestCase Case(params string[] replyParts)
    {
        string file = "<testcase>\n<reply>\n" + string.Concat(replyParts) + "</reply>\n</testcase>\n";
        return UpstreamTestCaseParser.Parse(Encoding.Latin1.GetBytes(file)).TestCase!;
    }

    private static string Reply(string openingTag, string content) => $"<{openingTag}>\n{content}</{openingTag}>\n";

    private static async Task<IConnection> ConnectAsync(SwsHttpServerConnector server) =>
        (await server.ConnectAsync(Target, CancellationToken.None)).Connection!;

    private static ValueTask WriteAsync(IConnection connection, string text) =>
        connection.WriteAsync(Encoding.Latin1.GetBytes(text), CancellationToken.None);

    private static async Task<string> ReadOnceAsync(IConnection connection)
    {
        byte[] buffer = new byte[1024];
        int count = await connection.ReadAsync(buffer, CancellationToken.None);
        return Encoding.Latin1.GetString(buffer, 0, count);
    }

    private static string Text(ReadOnlyMemory<byte> bytes) => Encoding.Latin1.GetString(bytes.Span);
}
