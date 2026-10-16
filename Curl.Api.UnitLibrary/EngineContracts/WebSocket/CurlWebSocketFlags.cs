namespace Curl.Api.EngineContracts.WebSocket;

/// <summary>
/// The frame bits of libcurl's WebSocket API (<c>CURLWS_*</c> in <c>struct curl_ws_frame</c>'s
/// <c>flags</c> and the <c>flags</c> of <c>curl_ws_send</c> and <c>curl_ws_start_frame</c>), with
/// upstream curl 8.22.0's numbers from <c>include/curl/websockets.h</c>.
/// </summary>
[Flags]
public enum CurlWebSocketFlags
{
    /// <summary>No bit set.</summary>
    None = 0,

    /// <summary><c>CURLWS_TEXT</c> (<c>1 &lt;&lt; 0</c>): a text frame.</summary>
    Text = 1 << 0,

    /// <summary><c>CURLWS_BINARY</c> (<c>1 &lt;&lt; 1</c>): a binary frame.</summary>
    Binary = 1 << 1,

    /// <summary><c>CURLWS_CONT</c> (<c>1 &lt;&lt; 2</c>): the frame is a fragment and more of the message follows.</summary>
    Continuation = 1 << 2,

    /// <summary><c>CURLWS_CLOSE</c> (<c>1 &lt;&lt; 3</c>): a close frame.</summary>
    Close = 1 << 3,

    /// <summary><c>CURLWS_PING</c> (<c>1 &lt;&lt; 4</c>): a ping frame.</summary>
    Ping = 1 << 4,

    /// <summary><c>CURLWS_OFFSET</c> (<c>1 &lt;&lt; 5</c>): <c>curl_ws_send</c> sends part of a frame whose total size was given before.</summary>
    Offset = 1 << 5,

    /// <summary><c>CURLWS_PONG</c> (<c>1 &lt;&lt; 6</c>): a pong frame.</summary>
    Pong = 1 << 6,
}
