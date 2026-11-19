using Curl.Api.Codes;
using Curl.Api.EngineContracts.WebSocket;

namespace Curl.Api.WebSockets;

[TestClass]
public sealed class IEasyWebSocketWaitForDataTests
{
    [TestMethod]
    public async Task WaitForDataAsync_AnEngineThatCannotWait_CompletesAtOnce()
    {
        IEasyWebSocket webSocket = new NonWaitingWebSocket();

        await webSocket.WaitForDataAsync(CancellationToken.None);
    }

    /// <summary>An <see cref="IEasyWebSocket"/> that implements nothing but what the interface requires.</summary>
    private sealed class NonWaitingWebSocket : IEasyWebSocket
    {
        public CurlWebSocketFrame? CurrentFrame => null;

        public ValueTask<CurlWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new CurlWebSocketReceiveResult(CurlCode.Again, 0, default));

        public ValueTask<CurlWebSocketSendResult> SendAsync(ReadOnlyMemory<byte> buffer, long fragmentSize, CurlWebSocketFlags flags, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new CurlWebSocketSendResult(CurlCode.Ok, buffer.Length));

        public ValueTask<CurlCode> StartFrameAsync(CurlWebSocketFlags flags, long frameLength, CancellationToken cancellationToken) =>
            ValueTask.FromResult(CurlCode.Ok);
    }
}
