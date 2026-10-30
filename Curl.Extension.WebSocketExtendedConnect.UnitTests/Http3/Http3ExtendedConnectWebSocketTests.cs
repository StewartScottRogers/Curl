using System.Text;
using Curl.Extension.WebSocketExtendedConnect.Http3;
using Curl.Http3;
using Curl.Protocol.Abstractions;
using Curl.WebSocket;

namespace Curl.Extension.WebSocketExtendedConnect.UnitTests.Http3;

[TestClass]
public sealed class Http3ExtendedConnectWebSocketTests
{
    [TestMethod]
    public async Task SendFrameAsync_TextFrame_WritesItMaskedInOneDataFrame()
    {
        InMemoryMultiplexedConnection connection = new();
        await using Http3ExtendedConnectWebSocket webSocket = await Http3ServerBytes.OpenAsync(connection);

        await webSocket.SendFrameAsync(WsOpcode.Text, isFinal: true, "hi"u8.ToArray(), CancellationToken.None);

        // RFC 9220 section 3: WebSocket frames travel as DATA frame payload; the client masks (RFC 6455 section 5.3).
        byte[] expected = Http3ServerBytes.Data(WsFrameEncoder.Encode(WsOpcode.Text, true, "hi"u8, Http3ServerBytes.FixedRandomSource));
        CollectionAssert.AreEqual(expected, connection.RequestStream.Written.ToArray());
        Assert.IsFalse(connection.RequestStream.IsWriteEnded);
    }

    [TestMethod]
    public async Task ReceiveFramesAsync_DataFrameCarryingATextFrame_GivesItsPayload()
    {
        InMemoryMultiplexedConnection connection = new();
        await using Http3ExtendedConnectWebSocket webSocket = await Http3ServerBytes.OpenAsync(connection);
        connection.RequestStream.Receive(Http3ServerBytes.Data(0x81, 0x02, (byte)'h', (byte)'i'));

        IReadOnlyList<WsFrameRun>? runs = await webSocket.ReceiveFramesAsync(CancellationToken.None);

        Assert.IsNotNull(runs);
        Assert.HasCount(1, runs);
        Assert.AreEqual(WsOpcode.Text, runs[0].Opcode);
        Assert.AreEqual("hi", Encoding.ASCII.GetString(runs[0].Payload.Span));
    }

    [TestMethod]
    public async Task ReceiveFramesAsync_TrailersThenFin_GivesNullForTheOrderlyClose()
    {
        InMemoryMultiplexedConnection connection = new();
        await using Http3ExtendedConnectWebSocket webSocket = await Http3ServerBytes.OpenAsync(connection);
        connection.RequestStream.Receive(Http3ServerBytes.Headers(new Curl.Http2.HeaderField("x-trailer", "1")));
        connection.RequestStream.EndReceiving();

        IReadOnlyList<WsFrameRun>? runs = await webSocket.ReceiveFramesAsync(CancellationToken.None);

        // RFC 9220 section 3: the stream's FIN stands for the TCP FIN of the underlying connection.
        Assert.IsNull(runs);
    }

    [TestMethod]
    public async Task ReceiveFramesAsync_StreamReset_FailsNamingTheErrorCode()
    {
        InMemoryMultiplexedConnection connection = new();
        await using Http3ExtendedConnectWebSocket webSocket = await Http3ServerBytes.OpenAsync(connection);
        connection.RequestStream.FailReceiving(new MultiplexedStreamResetException((long)Http3ErrorCode.RequestCancelled, "reset"));

        Http3ExtendedConnectException failure = await Assert.ThrowsExactlyAsync<Http3ExtendedConnectException>(async () => await webSocket.ReceiveFramesAsync(CancellationToken.None));

        // RFC 9220 section 3: a stream reset stands for a TCP RST, an abortive close.
        StringAssert.Contains(failure.Message, "reset the WebSocket's stream with error code 0x10c");
        Assert.IsInstanceOfType<MultiplexedStreamResetException>(failure.InnerException);
    }

    [TestMethod]
    public async Task ReceiveFramesAsync_FrameWithReservedBitsSet_FailsAsMalformed()
    {
        InMemoryMultiplexedConnection connection = new();
        await using Http3ExtendedConnectWebSocket webSocket = await Http3ServerBytes.OpenAsync(connection);
        connection.RequestStream.Receive(Http3ServerBytes.Data(0xF1, 0x00));

        Http3ExtendedConnectException failure = await Assert.ThrowsExactlyAsync<Http3ExtendedConnectException>(async () => await webSocket.ReceiveFramesAsync(CancellationToken.None));

        StringAssert.StartsWith(failure.Message, "The server sent a malformed WebSocket frame");
    }

    [TestMethod]
    public async Task CloseAsync_Open_EndsTheStreamWithFin()
    {
        InMemoryMultiplexedConnection connection = new();
        await using Http3ExtendedConnectWebSocket webSocket = await Http3ServerBytes.OpenAsync(connection);

        await webSocket.CloseAsync(CancellationToken.None);

        // RFC 9220 section 3: the orderly close is the stream's FIN.
        Assert.IsTrue(connection.RequestStream.IsWriteEnded);
    }

    [TestMethod]
    public async Task Abort_Open_ResetsTheStreamWithRequestCancelled()
    {
        InMemoryMultiplexedConnection connection = new();
        await using Http3ExtendedConnectWebSocket webSocket = await Http3ServerBytes.OpenAsync(connection);

        webSocket.Abort();

        // RFC 9220 section 3: H3_REQUEST_CANCELLED takes the place of HTTP/2's CANCEL.
        Assert.AreEqual((long)Http3ErrorCode.RequestCancelled, connection.RequestStream.AbortedWith);
    }

    [TestMethod]
    public async Task DisposeAsync_Open_DisposesTheConnection()
    {
        InMemoryMultiplexedConnection connection = new();
        Http3ExtendedConnectWebSocket webSocket = await Http3ServerBytes.OpenAsync(connection);

        await webSocket.DisposeAsync();

        Assert.IsTrue(connection.IsDisposed);
    }
}
