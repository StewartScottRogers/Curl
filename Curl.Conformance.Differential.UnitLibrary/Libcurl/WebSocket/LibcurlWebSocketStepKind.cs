namespace Curl.Conformance.Differential.Libcurl.WebSocket;

/// <summary>Which <c>curl_ws_*</c> call a <see cref="LibcurlWebSocketStep"/> makes.</summary>
public enum LibcurlWebSocketStepKind
{
    /// <summary><c>curl_ws_send</c>.</summary>
    Send,

    /// <summary><c>curl_ws_recv</c>, called again while it answers <c>CURLE_AGAIN</c>.</summary>
    Receive,

    /// <summary><c>curl_ws_meta</c>, outside a write callback.</summary>
    Meta,
}
