namespace Curl.Api.Options.Protocols;

/// <summary>The WebSocket options of one easy handle, each defaulting to libcurl 8.22.0's default.</summary>
public sealed record EasyWebSocketOptions
{
    /// <summary><c>CURLOPT_WS_OPTIONS</c>: the WebSocket behaviour bits. Default <see cref="WebSocketOptionBits.None"/>.</summary>
    public WebSocketOptionBits Options { get; init; } = WebSocketOptionBits.None;
}
