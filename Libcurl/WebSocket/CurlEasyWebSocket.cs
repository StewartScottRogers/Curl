using Curl.Api.Codes;
using Curl.Api.EngineContracts.WebSocket;
using Libcurl.Easy;

namespace Libcurl.WebSocket;

/// <summary>
/// libcurl's WebSocket API on a <see cref="CurlEasy"/> (<c>include/curl/websockets.h</c>, ADR-0585):
/// <see cref="WebSocketRecv"/> is <c>curl_ws_recv</c>, <see cref="WebSocketSend"/> is <c>curl_ws_send</c>,
/// <see cref="WebSocketMeta"/> is <c>curl_ws_meta</c> and <see cref="WebSocketStartFrame"/> is
/// <c>curl_ws_start_frame</c>, each over the engine's <see cref="IEasyWebSocket"/>. A <see langword="null"/>
/// or cleaned-up handle fails libcurl's <c>GOOD_EASY_HANDLE</c> check: the calls answer
/// <see cref="CurlCode.BadFunctionArgument"/> and <see cref="WebSocketMeta"/> <see langword="null"/>, without
/// calling the engine. Each blocking call blocks on its <c>Async</c> form, as <see cref="CurlEasy.Perform"/> does.
/// </summary>
public static class CurlEasyWebSocket
{
    /// <summary>
    /// <c>curl_ws_recv</c>: receives the next piece of WebSocket data into <paramref name="buffer"/>, blocking
    /// on <see cref="WebSocketRecvAsync"/>.
    /// </summary>
    /// <param name="easy">The handle, after a <c>CURLOPT_CONNECT_ONLY</c> 2 perform.</param>
    /// <param name="buffer">Where the received bytes are written.</param>
    /// <param name="received"><c>*recv</c>: how many bytes were written; 0 unless the answer is <see cref="CurlCode.Ok"/>.</param>
    /// <param name="meta"><c>*metap</c>: the frame the bytes belong to; <see langword="null"/> unless the answer is <see cref="CurlCode.Ok"/>.</param>
    /// <returns>The engine's answer (<see cref="CurlCode.Again"/> when no data is ready), or <see cref="CurlCode.BadFunctionArgument"/> for a <see langword="null"/> or cleaned-up handle.</returns>
    public static CurlCode WebSocketRecv(this CurlEasy? easy, Memory<byte> buffer, out int received, out CurlWebSocketFrame? meta)
    {
        var result = easy.WebSocketRecvAsync(buffer).AsTask().GetAwaiter().GetResult();
        var ok = result.Result == CurlCode.Ok;
        received = ok ? result.BytesReceived : 0;
        meta = ok ? result.Frame : null;
        return result.Result;
    }

    /// <summary><c>curl_ws_recv</c>: receives the next piece of WebSocket data into <paramref name="buffer"/>.</summary>
    /// <param name="easy">The handle, after a <c>CURLOPT_CONNECT_ONLY</c> 2 perform.</param>
    /// <param name="buffer">Where the received bytes are written.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    /// <returns>The engine's result, or <see cref="CurlCode.BadFunctionArgument"/> with no bytes for a <see langword="null"/> or cleaned-up handle.</returns>
    public static ValueTask<CurlWebSocketReceiveResult> WebSocketRecvAsync(this CurlEasy? easy, Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var webSocket = LiveWebSocket(easy);
        return webSocket is null
            ? ValueTask.FromResult(new CurlWebSocketReceiveResult(CurlCode.BadFunctionArgument, 0, default))
            : webSocket.ReceiveAsync(buffer, cancellationToken);
    }

    /// <summary>
    /// <c>curl_ws_send</c>: sends <paramref name="buffer"/> as a WebSocket frame, or part of one, blocking on
    /// <see cref="WebSocketSendAsync"/>.
    /// </summary>
    /// <param name="easy">The handle, after a <c>CURLOPT_CONNECT_ONLY</c> 2 perform.</param>
    /// <param name="buffer">The bytes to send.</param>
    /// <param name="sent"><c>*sent</c>: how many bytes of <paramref name="buffer"/> were sent.</param>
    /// <param name="fragmentSize"><c>fragsize</c>: the whole frame's size when <see cref="CurlWebSocketFlags.Offset"/> is set and this is its first part; otherwise 0.</param>
    /// <param name="flags">The frame's <c>CURLWS_*</c> bits.</param>
    /// <returns>The engine's answer (<see cref="CurlCode.Again"/> when nothing could be sent yet), or <see cref="CurlCode.BadFunctionArgument"/> for a <see langword="null"/> or cleaned-up handle.</returns>
    public static CurlCode WebSocketSend(this CurlEasy? easy, ReadOnlyMemory<byte> buffer, out int sent, long fragmentSize, CurlWebSocketFlags flags)
    {
        var result = easy.WebSocketSendAsync(buffer, fragmentSize, flags).AsTask().GetAwaiter().GetResult();
        sent = result.BytesSent;
        return result.Result;
    }

    /// <summary><c>curl_ws_send</c>: sends <paramref name="buffer"/> as a WebSocket frame, or part of one.</summary>
    /// <param name="easy">The handle, after a <c>CURLOPT_CONNECT_ONLY</c> 2 perform.</param>
    /// <param name="buffer">The bytes to send.</param>
    /// <param name="fragmentSize"><c>fragsize</c>: the whole frame's size when <see cref="CurlWebSocketFlags.Offset"/> is set and this is its first part; otherwise 0.</param>
    /// <param name="flags">The frame's <c>CURLWS_*</c> bits.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    /// <returns>The engine's result, or <see cref="CurlCode.BadFunctionArgument"/> with nothing sent for a <see langword="null"/> or cleaned-up handle.</returns>
    public static ValueTask<CurlWebSocketSendResult> WebSocketSendAsync(this CurlEasy? easy, ReadOnlyMemory<byte> buffer, long fragmentSize, CurlWebSocketFlags flags, CancellationToken cancellationToken = default)
    {
        var webSocket = LiveWebSocket(easy);
        return webSocket is null
            ? ValueTask.FromResult(new CurlWebSocketSendResult(CurlCode.BadFunctionArgument, 0))
            : webSocket.SendAsync(buffer, fragmentSize, flags, cancellationToken);
    }

    /// <summary>
    /// <c>curl_ws_meta</c>: the frame being delivered to the write callback, or <see langword="null"/> outside
    /// the callback and for a <see langword="null"/> or cleaned-up handle, as libcurl returns <c>NULL</c>.
    /// </summary>
    /// <param name="easy">The handle whose write callback is running.</param>
    /// <returns>The engine's current frame, or <see langword="null"/>.</returns>
    public static CurlWebSocketFrame? WebSocketMeta(this CurlEasy? easy) => LiveWebSocket(easy)?.CurrentFrame;

    /// <summary>
    /// <c>curl_ws_start_frame</c>: from inside a read callback, starts a frame whose payload the callback then
    /// supplies, blocking on <see cref="WebSocketStartFrameAsync"/>.
    /// </summary>
    /// <param name="easy">The handle whose read callback is running.</param>
    /// <param name="flags">The frame's <c>CURLWS_*</c> bits.</param>
    /// <param name="frameLength"><c>frame_len</c>: the frame's payload size.</param>
    /// <returns>The engine's answer, or <see cref="CurlCode.BadFunctionArgument"/> for a <see langword="null"/> or cleaned-up handle.</returns>
    public static CurlCode WebSocketStartFrame(this CurlEasy? easy, CurlWebSocketFlags flags, long frameLength) =>
        easy.WebSocketStartFrameAsync(flags, frameLength).AsTask().GetAwaiter().GetResult();

    /// <summary><c>curl_ws_start_frame</c>: from inside a read callback, starts a frame whose payload the callback then supplies.</summary>
    /// <param name="easy">The handle whose read callback is running.</param>
    /// <param name="flags">The frame's <c>CURLWS_*</c> bits.</param>
    /// <param name="frameLength"><c>frame_len</c>: the frame's payload size.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    /// <returns>The engine's answer, or <see cref="CurlCode.BadFunctionArgument"/> for a <see langword="null"/> or cleaned-up handle.</returns>
    public static ValueTask<CurlCode> WebSocketStartFrameAsync(this CurlEasy? easy, CurlWebSocketFlags flags, long frameLength, CancellationToken cancellationToken = default)
    {
        var webSocket = LiveWebSocket(easy);
        return webSocket is null
            ? ValueTask.FromResult(CurlCode.BadFunctionArgument)
            : webSocket.StartFrameAsync(flags, frameLength, cancellationToken);
    }

    private static IEasyWebSocket? LiveWebSocket(CurlEasy? easy) =>
        easy is null || easy.IsCleanedUp ? null : easy.Transfer.WebSocket;
}
