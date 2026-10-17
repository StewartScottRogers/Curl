using Curl.Api.EngineContracts.WebSocket;

namespace Curl.Api;

/// <summary>What one <see cref="CurlWebSocket.ReceiveFrameAsync"/> call received: a piece of a frame.</summary>
/// <param name="BytesReceived">How many bytes were written into the buffer.</param>
/// <param name="Frame">The frame they belong to: its bits, offset and bytes still to come.</param>
public readonly record struct CurlWebSocketFramePiece(int BytesReceived, CurlWebSocketFrame Frame);
