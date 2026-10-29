using System.Text;

namespace Curl.Extension.WebSocketRepl.Handshake;

[TestClass]
public sealed class WebSocketOpeningHandshakeTests
{
    private static readonly FixedRandomSource RfcNonce = new(Encoding.ASCII.GetBytes("the sample nonce"));

    private static readonly byte[] RfcResponse = Encoding.ASCII.GetBytes(
        "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
        + "Sec-WebSocket-Accept: s3pPLMBiTxaQ9kYGzzhZRbK+xOo=\r\n\r\n");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task PerformAsync_RfcResponse_SendsTheRequestAndSucceeds()
    {
        ScriptedConnection connection = new((1, RfcResponse));

        WebSocketHandshakeResult result = await new WebSocketOpeningHandshake(RfcNonce)
            .PerformAsync(connection, "server.example.com", "/chat", TestContext.CancellationToken);

        Assert.IsNull(result.Failure);
        Assert.IsTrue(result.AlreadyReceived.IsEmpty);
        Assert.AreEqual(
            "GET /chat HTTP/1.1\r\nHost: server.example.com\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
            + "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\nSec-WebSocket-Version: 13\r\n\r\n",
            Encoding.ASCII.GetString(connection.Writes.Single()));
    }

    [TestMethod]
    public async Task PerformAsync_ResponseSplitWithFramesAfterIt_KeepsTheFrames()
    {
        ScriptedConnection connection = new((1, RfcResponse[..10]), (1, [.. RfcResponse[10..], 0x81, 0x02, (byte)'h', (byte)'i']));

        WebSocketHandshakeResult result = await new WebSocketOpeningHandshake(RfcNonce)
            .PerformAsync(connection, "server.example.com", "/chat", TestContext.CancellationToken);

        Assert.IsNull(result.Failure);
        CollectionAssert.AreEqual(new byte[] { 0x81, 0x02, (byte)'h', (byte)'i' }, result.AlreadyReceived.ToArray());
    }

    [TestMethod]
    public async Task PerformAsync_ResponseRefused_FailsWithNoBytesKept()
    {
        ScriptedConnection connection = new((1, Encoding.ASCII.GetBytes("HTTP/1.1 404 Not Found\r\n\r\nbody")));

        WebSocketHandshakeResult result = await new WebSocketOpeningHandshake(RfcNonce)
            .PerformAsync(connection, "server.example.com", "/chat", TestContext.CancellationToken);

        Assert.AreEqual("the server answered \"HTTP/1.1 404 Not Found\", not 101 Switching Protocols", result.Failure);
        Assert.IsTrue(result.AlreadyReceived.IsEmpty);
    }

    [TestMethod]
    public async Task PerformAsync_ConnectionClosesInTheHead_Fails()
    {
        ScriptedConnection connection = new((1, RfcResponse[..20]));

        WebSocketHandshakeResult result = await new WebSocketOpeningHandshake(RfcNonce)
            .PerformAsync(connection, "server.example.com", "/chat", TestContext.CancellationToken);

        Assert.AreEqual("the connection closed before the response head ended", result.Failure);
    }

    [TestMethod]
    public async Task PerformAsync_HeadLongerThanTheLimit_Fails()
    {
        byte[] longHead = new byte[WebSocketOpeningHandshake.LongestResponseHead / 2];
        longHead.AsSpan().Fill((byte)'a');
        ScriptedConnection connection = new((1, longHead), (1, longHead), (1, RfcResponse));

        WebSocketHandshakeResult result = await new WebSocketOpeningHandshake(RfcNonce)
            .PerformAsync(connection, "server.example.com", "/chat", TestContext.CancellationToken);

        Assert.AreEqual("the response head is longer than 65536 bytes", result.Failure);
    }

    [TestMethod]
    public async Task PerformAsync_NullConnection_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            await new WebSocketOpeningHandshake(RfcNonce).PerformAsync(null!, "h", "/", TestContext.CancellationToken));
    }
}
