using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>Pins that <see cref="SwsKeptAliveConnection"/> waits for the next request where the sws emulation's read would end the stream, as sws blocks reading it (test1586, BL-2241).</summary>
[TestClass]
public sealed class SwsKeptAliveConnectionTests
{
    private const string KeepAliveCase = "<reply>\n<data>\nHTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nhi\n</data>\n</reply>\n";

    private const string ClosingCase = "<reply>\n<data>\nHTTP/1.1 200 OK\r\nContent-Length: 3\r\nConnection: close\r\n\r\nhi\n</data>\n<servercmd>\nswsclose\n</servercmd>\n</reply>\n";

    private const string Request = "GET http://example.com/1 HTTP/1.1\r\nHost: example.com\r\n\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ReadAsync_NoReplyWaitingOnAKeptAliveConnection_WaitsForTheNextRequestAndReadsItsReply()
    {
        await using SwsKeptAliveConnection connection = await ConnectAsync(KeepAliveCase);
        await connection.WriteAsync(Encoding.Latin1.GetBytes(Request), TestContext.CancellationToken);
        string first = await ReadReplyAsync(connection);

        ValueTask<int> waiting = connection.ReadAsync(new byte[256], TestContext.CancellationToken);
        bool waitedForTheRequest = !waiting.IsCompleted;
        await connection.WriteAsync(Encoding.Latin1.GetBytes(Request), TestContext.CancellationToken);
        int read = await waiting;

        Assert.StartsWith("HTTP/1.1 200 OK", first);
        Assert.IsTrue(waitedForTheRequest);
        Assert.IsGreaterThan(0, read);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ReadAsync_SwsClosedTheConnection_ReturnsTheEndOfTheStream()
    {
        await using SwsKeptAliveConnection connection = await ConnectAsync(ClosingCase);
        await connection.WriteAsync(Encoding.Latin1.GetBytes(Request), TestContext.CancellationToken);
        _ = await ReadReplyAsync(connection);

        int read = await connection.ReadAsync(new byte[256], TestContext.CancellationToken);

        Assert.AreEqual(0, read);
    }

    [TestMethod]
    public async Task Properties_AreTheSwsConnectionsAndNotSecure()
    {
        SwsHttpServerConnector sws = new(ParsedTestCase.From(KeepAliveCase), TimeProvider.System);
        ConnectResult result = await sws.ConnectAsync(new ConnectTarget("127.0.0.1", SwsHttpServerConnector.ProxyPort, false), TestContext.CancellationToken);

        await using SwsKeptAliveConnection connection = new((SwsHttpServerConnection)result.Connection!);
        await connection.FlushAsync(TestContext.CancellationToken);

        Assert.IsFalse(connection.IsSecure);
        Assert.AreEqual(result.Connection!.RemoteEndPoint, connection.RemoteEndPoint);
        Assert.AreEqual(result.Connection.LocalEndPoint, connection.LocalEndPoint);
    }

    private async Task<SwsKeptAliveConnection> ConnectAsync(string testCase)
    {
        SwsHttpServerConnector sws = new(ParsedTestCase.From(testCase), TimeProvider.System);
        ConnectResult result = await sws.ConnectAsync(new ConnectTarget("127.0.0.1", SwsHttpServerConnector.ProxyPort, false), TestContext.CancellationToken);
        return new SwsKeptAliveConnection((SwsHttpServerConnection)result.Connection!);
    }

    // Reads until the reply's three body bytes have arrived.
    private async Task<string> ReadReplyAsync(SwsKeptAliveConnection connection)
    {
        StringBuilder reply = new();
        byte[] buffer = new byte[256];
        while (!reply.ToString().EndsWith("\r\n\r\nhi\n", StringComparison.Ordinal))
        {
            int read = await connection.ReadAsync(buffer, TestContext.CancellationToken);
            reply.Append(Encoding.Latin1.GetString(buffer, 0, read));
        }

        return reply.ToString();
    }
}
