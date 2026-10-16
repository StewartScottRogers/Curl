using Curl.Api.Codes;

namespace Curl.Api.EngineContracts.WebSocket;

/// <summary>
/// libcurl's WebSocket API on one easy handle (<c>include/curl/websockets.h</c>), usable after a
/// <c>ws://</c> or <c>wss://</c> transfer set up with <c>CURLOPT_CONNECT_ONLY</c> 2, or from inside
/// its write callback.
/// </summary>
public interface IEasyWebSocket
{
    /// <summary>
    /// <c>curl_ws_meta</c>: the frame being delivered to the write callback, or
    /// <see langword="null"/> outside the callback, as libcurl returns <c>NULL</c>.
    /// </summary>
    CurlWebSocketFrame? CurrentFrame { get; }

    /// <summary><c>curl_ws_recv</c>: receive the next piece of WebSocket data.</summary>
    /// <param name="buffer">Where the received bytes are written.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    /// <returns>The result, the byte count and the frame they belong to.</returns>
    ValueTask<CurlWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken);

    /// <summary><c>curl_ws_send</c>: send data as a WebSocket frame, or part of one.</summary>
    /// <param name="buffer">The bytes to send.</param>
    /// <param name="fragmentSize"><c>fragsize</c>: the whole frame's size when <see cref="CurlWebSocketFlags.Offset"/> is set and this is its first part; otherwise 0.</param>
    /// <param name="flags">The frame's <c>CURLWS_*</c> bits.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    /// <returns>The result and the byte count sent.</returns>
    ValueTask<CurlWebSocketSendResult> SendAsync(ReadOnlyMemory<byte> buffer, long fragmentSize, CurlWebSocketFlags flags, CancellationToken cancellationToken);

    /// <summary>
    /// <c>curl_ws_start_frame</c>: from inside a read callback, start a frame whose payload the
    /// callback then supplies.
    /// </summary>
    /// <param name="flags">The frame's <c>CURLWS_*</c> bits.</param>
    /// <param name="frameLength"><c>frame_len</c>: the frame's payload size.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    /// <returns>The call's result.</returns>
    ValueTask<CurlCode> StartFrameAsync(CurlWebSocketFlags flags, long frameLength, CancellationToken cancellationToken);
}
