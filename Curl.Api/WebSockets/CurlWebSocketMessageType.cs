namespace Curl.Api;

/// <summary>The kind of a whole WebSocket message a <see cref="CurlWebSocket"/> received.</summary>
public enum CurlWebSocketMessageType
{
    /// <summary>A text message (<c>CURLWS_TEXT</c>): UTF-8 bytes.</summary>
    Text,

    /// <summary>A binary message (<c>CURLWS_BINARY</c>).</summary>
    Binary,

    /// <summary>A close frame (<c>CURLWS_CLOSE</c>): the peer is closing the connection.</summary>
    Close,

    /// <summary>A ping frame (<c>CURLWS_PING</c>); answered with a pong unless <see cref="CurlWebSocketOptions.AutoPong"/> is off.</summary>
    Ping,

    /// <summary>A pong frame (<c>CURLWS_PONG</c>).</summary>
    Pong,
}
