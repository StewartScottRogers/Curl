using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

// Attacks the sws emulation through its public surface with requests delivered a byte at a time,
// cut short, oversized, pipelined and malformed, on cases with no reply, after Abandon, and on
// many connections at once, by Documentation/Wiki/Adversarial-Testing.md (BL-1494).
public sealed partial class SwsHttpServerConnectorTests
{
    [TestMethod]
    public async Task Get_WrittenOneBytePerWrite_IsAnsweredOnlyAfterTheLastByte()
    {
        Diagnostics.Arrange("request", Get);
        SwsHttpServerConnector server = new(Case(Reply("data", "first\n")));
        IConnection connection = await ConnectAsync(server);

        for (int index = 0; index < Get.Length - 1; index++)
        {
            await WriteAsync(connection, Get[index..(index + 1)]);
            Assert.AreEqual(string.Empty, await ReadAllAsync(connection), $"reply after {index + 1} bytes");
        }

        await WriteAsync(connection, Get[^1..]);

        Assert.AreEqual("first\n", Observe("reply", "first\n", await ReadAllAsync(connection)));
        Assert.AreEqual(Get, Observe("recorded", Get, Text(server.ReceivedBytes)));
    }

    [TestMethod]
    public async Task Request_CutShortThenDisposed_IsRecordedAndNeverAnswered()
    {
        const string CutShort = "GET /1234 HTTP/1.1\r\nHost: 127.0.0.1\r\n";
        Diagnostics.Arrange("request", CutShort);
        SwsHttpServerConnector server = new(Case(Reply("data", "first\n")));
        IConnection connection = await ConnectAsync(server);

        string reply = await ExchangeAsync(connection, CutShort);
        await connection.DisposeAsync();

        Assert.AreEqual(string.Empty, Observe("reply", string.Empty, reply));
        Assert.AreEqual(CutShort, Observe("recorded", CutShort, Text(server.ReceivedBytes)));
    }

    [TestMethod]
    public async Task HeaderLineOfHalfAMebibyteWithNoEnd_IsRecordedAndNeverAnswered()
    {
        const int Length = 512 * 1024;
        Diagnostics.Arrange("request", $"GET /1234 HTTP/1.1, then one header line of {Length} bytes with no line feed, written 64 KiB at a time");
        SwsHttpServerConnector server = new(Case(Reply("data", "first\n")));
        IConnection connection = await ConnectAsync(server);
        await WriteAsync(connection, "GET /1234 HTTP/1.1\r\nX: ");
        byte[] chunk = Encoding.Latin1.GetBytes(new string('a', 64 * 1024));

        for (int written = 0; written < Length; written += chunk.Length)
        {
            await connection.WriteAsync(chunk, CancellationToken.None);
        }

        Assert.AreEqual(string.Empty, Observe("reply", string.Empty, await ReadAllAsync(connection)));
        Assert.AreEqual("GET /1234 HTTP/1.1\r\nX: ".Length + Length, ObserveValue("recorded length", "GET /1234 HTTP/1.1\r\nX: ".Length + Length, server.ReceivedBytes.Length));
    }

    [TestMethod]
    public async Task TwoRequestsInOneWrite_RecordsBothAndAnswersTheFirstFirst()
    {
        Diagnostics.Arrange("request", Get + Get);
        SwsHttpServerConnector server = new(Case(Reply("data", "first\n")));
        IConnection connection = await ConnectAsync(server);

        string reply = await ExchangeAsync(connection, Get + Get);

        Assert.StartsWith("first\n", Observe("reply", "first\n...", reply));
        Assert.AreEqual(Get + Get, Observe("recorded", Get + Get, Text(server.ReceivedBytes)));
    }

    [TestMethod]
    [DataRow("\0ÿ\r\n\r\n", DisplayName = "NUL and 0xFF request line")]
    [DataRow("\r\n\r\n", DisplayName = "empty request line")]
    [DataRow("GET\r\n\r\n", DisplayName = "method only")]
    [DataRow("GET /1234 HTTP/1.1\n\n", DisplayName = "bare line feeds")]
    [DataRow("GET /1234 HTTP/1.1\r\nContent-Length: -5\r\n\r\n", DisplayName = "negative Content-Length")]
    [DataRow("POST /1234 HTTP/1.1\r\nTransfer-Encoding: chunked\r\n\r\nzz\r\n", DisplayName = "chunk size that is not hexadecimal")]
    [DataRow("POST /1234 HTTP/1.1\r\nTransfer-Encoding: chunked\r\n\r\nffffffffffffffffffff\r\n", DisplayName = "chunk size past long")]
    public async Task MalformedRequest_IsRecordedWithoutThrowing(string request)
    {
        Diagnostics.Arrange("request", request);
        SwsHttpServerConnector server = new(Case(Reply("data", "first\n")));
        IConnection connection = await ConnectAsync(server);

        string reply = await ExchangeAsync(connection, request);

        Diagnostics.Act("reply", reply);
        Assert.AreEqual(request, Observe("recorded", request, Text(server.ReceivedBytes)));
    }

    [TestMethod]
    public async Task CaseWithNoReplySection_AnswersWithoutThrowing()
    {
        SwsHttpServerConnector server = new(ParsedTestCase.From("<verify>\n<protocol>\nx\n</protocol>\n</verify>\n"));
        IConnection connection = await ConnectAsync(server);

        string reply = await ExchangeAsync(connection, Get);

        Diagnostics.Act("reply", reply);
        Assert.AreEqual(Get, Observe("recorded", Get, Text(server.ReceivedBytes)));
        Assert.IsEmpty(server.UnsupportedServerCommands);
    }

    [TestMethod]
    public async Task Abandon_Twice_ThenEveryExchangeThrowsIOException()
    {
        SwsHttpServerConnector server = new(Case(Reply("data", "first\n")));
        IConnection connection = await ConnectAsync(server);

        server.Abandon();
        server.Abandon();

        await Assert.ThrowsExactlyAsync<IOException>(async () => await connection.WriteAsync(new byte[1], CancellationToken.None));
        await Assert.ThrowsExactlyAsync<IOException>(async () => await connection.ReadAsync(new byte[1], CancellationToken.None));
        await Assert.ThrowsExactlyAsync<IOException>(async () => await server.ConnectAsync(Target, CancellationToken.None));
        Diagnostics.Assert("exchanges after abandon", "IOException each", "IOException each");
    }

    [TestMethod]
    public async Task Dispose_Twice_RecordsOneDisconnect()
    {
        SwsHttpServerConnector server = new(Case(Reply("servercmd", "connection-monitor\n"), Reply("data", "first\n")));
        IConnection connection = await ConnectAsync(server);
        await ExchangeAsync(connection, Get);

        await connection.DisposeAsync();
        await connection.DisposeAsync();

        string recorded = Text(server.ReceivedBytes);
        int disconnects = recorded.Split("[DISCONNECT]").Length - 1;
        Assert.AreEqual(1, ObserveValue("disconnects recorded", 1, disconnects));
    }

    [TestMethod]
    public async Task ManyConnectionsWithInterleavedHalfRequests_EachIsAnsweredAndEveryByteIsRecorded()
    {
        // One thread on purpose: writes on many threads at once lose recorded bytes (BL-1647 tests them on many threads).
        const int Connections = 16;
        int half = Get.Length / 2;
        SwsHttpServerConnector server = new(Case(Reply("data", "first\n")));
        List<IConnection> connections = [];
        for (int index = 0; index < Connections; index++)
        {
            connections.Add(await ConnectAsync(server));
            await WriteAsync(connections[^1], Get[..half]);
        }

        List<string> replies = [];
        foreach (IConnection connection in connections)
        {
            replies.Add(await ExchangeAsync(connection, Get[half..]));
        }

        Assert.IsTrue(replies.All(reply => reply == "first\n"), Observe("replies", "first\\n each", string.Join("|", replies)));
        string expectedRecording = string.Concat(Enumerable.Repeat(Get[..half], Connections)) + string.Concat(Enumerable.Repeat(Get[half..], Connections));
        Assert.AreEqual(expectedRecording, Observe("recorded", expectedRecording, Text(server.ReceivedBytes)));
    }

    [TestMethod]
    public async Task ThirtyTwoConnectionsWrittenOneBytePerWriteOnThirtyTwoThreads_RecordEveryByte()
    {
        const int Connections = 32;
        const int Rounds = 100;
        int expectedLength = Connections * Get.Length;
        Diagnostics.Arrange("request", $"{Get} on {Connections} connections, one byte per write, each on its own thread, {Rounds} rounds");

        for (int round = 0; round < Rounds; round++)
        {
            SwsHttpServerConnector server = new(Case(Reply("data", "first\n")));
            await Task.WhenAll(Enumerable.Range(0, Connections).Select(_ => Task.Run(async () =>
            {
                IConnection connection = await ConnectAsync(server);
                for (int index = 0; index < Get.Length; index++)
                {
                    await WriteAsync(connection, Get[index..(index + 1)]);
                }
            })));

            Assert.AreEqual(expectedLength, server.ReceivedBytes.Length, $"recorded length in round {round + 1}");
        }

        Diagnostics.Assert("recorded length each round", expectedLength, expectedLength);
    }
}
