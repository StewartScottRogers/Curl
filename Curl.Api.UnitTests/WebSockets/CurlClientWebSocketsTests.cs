using Curl.Api.Codes;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Errors;
using Curl.Api.Options;
using Curl.Api.Options.Network;
using Curl.Api.Options.Protocols;

namespace Curl.Api.WebSockets;

[TestClass]
public sealed class CurlClientWebSocketsTests
{
    [TestMethod]
    public async Task ConnectWebSocketAsync_DefaultOptions_PerformsConnectOnlyWebSocketWithClientDefaults()
    {
        FakeWebSocketEngine engine = new();
        await using CurlClient client = new(engine, new CurlClientOptions { UserAgent = "agent/1" }, ownsEngine: true);

        await using CurlWebSocket socket = await client.ConnectWebSocketAsync("ws://example.test/chat");

        EasyOptions performed = engine.Transfer.PerformedOptions!;
        Assert.AreEqual("ws://example.test/chat", performed.Network.Url);
        Assert.AreEqual(ConnectOnly.WebSocket, performed.Network.ConnectOnly);
        Assert.AreEqual(WebSocketOptionBits.None, performed.Protocols.WebSocket.Options);
        Assert.AreEqual("agent/1", performed.Http.UserAgent);
    }

    [TestMethod]
    public async Task ConnectWebSocketAsync_NoAutoPongAndRawMode_SetsBothBits()
    {
        FakeWebSocketEngine engine = new();
        await using CurlClient client = new(engine);

        await using CurlWebSocket socket = await client.ConnectWebSocketAsync("wss://example.test/", new CurlWebSocketOptions { AutoPong = false, RawMode = true });

        Assert.AreEqual(WebSocketOptionBits.NoAutoPong | WebSocketOptionBits.RawMode, engine.Transfer.PerformedOptions!.Protocols.WebSocket.Options);
    }

    [TestMethod]
    public async Task ConnectWebSocketAsync_PerformFails_ThrowsCurlExceptionWithErrorTextAndDisposesHandle()
    {
        FakeWebSocketEngine engine = new();
        engine.Transfer.PerformResult = CurlCode.CouldntConnect;
        engine.Transfer.ErrorText = "Failed to connect to example.test port 80";
        await using CurlClient client = new(engine);

        CurlException failure = await Assert.ThrowsExactlyAsync<CurlException>(() => client.ConnectWebSocketAsync("ws://example.test/"));

        Assert.AreEqual(CurlCode.CouldntConnect, failure.Code);
        Assert.AreEqual("Failed to connect to example.test port 80", failure.Message);
        Assert.AreEqual(1, engine.Transfer.DisposeCount);
    }

    [TestMethod]
    public async Task ConnectWebSocketAsync_PerformFailsWithoutText_ThrowsCurlExceptionWithStrerrorText()
    {
        FakeWebSocketEngine engine = new();
        engine.Transfer.PerformResult = CurlCode.GotNothing;
        await using CurlClient client = new(engine);

        CurlException failure = await Assert.ThrowsExactlyAsync<CurlException>(() => client.ConnectWebSocketAsync("ws://example.test/"));

        Assert.AreEqual(CurlCodeText.Of(CurlCode.GotNothing), failure.Message);
    }

    [TestMethod]
    public async Task ConnectWebSocketAsync_PerformCancelled_RethrowsAndDisposesHandle()
    {
        FakeWebSocketEngine engine = new();
        engine.Transfer.PerformException = new OperationCanceledException();
        await using CurlClient client = new(engine);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => client.ConnectWebSocketAsync("ws://example.test/"));

        Assert.AreEqual(1, engine.Transfer.DisposeCount);
    }

    [TestMethod]
    public async Task ConnectWebSocketAsync_NullArguments_Throw()
    {
        await using CurlClient client = new(new FakeWebSocketEngine());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => client.ConnectWebSocketAsync(null!));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => client.ConnectWebSocketAsync("ws://x/", null!));
    }

    [TestMethod]
    public async Task ConnectWebSocketAsync_DisposedClient_ThrowsObjectDisposedException()
    {
        CurlClient client = new(new FakeWebSocketEngine());
        await client.DisposeAsync();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => client.ConnectWebSocketAsync("ws://x/"));
    }

    [TestMethod]
    public async Task ConnectWebSocketAsync_ReceiveAnsweredAgain_WaitsOnTheSystemClockAndAsksAgain()
    {
        FakeWebSocketEngine engine = new();
        await using CurlClient client = new(engine);
        await using CurlWebSocket socket = await client.ConnectWebSocketAsync("ws://x/");
        engine.Transfer.FakeWebSocket.QueueResult(CurlCode.Again).Queue([1], CurlWebSocketFlags.Binary);

        CurlWebSocketFramePiece piece = await socket.ReceiveFrameAsync(new byte[4]);

        Assert.AreEqual(1, piece.BytesReceived);
        Assert.AreEqual(2, engine.Transfer.FakeWebSocket.ReceiveCount);
    }
}
