using Curl.Api.Options.Protocols;

namespace Curl.Api;

/// <summary>
/// How <see cref="CurlClient.ConnectWebSocketAsync(string, CurlWebSocketOptions, CancellationToken)"/>
/// sets up the connection: libcurl's <c>CURLOPT_WS_OPTIONS</c> bits. The defaults are libcurl's.
/// </summary>
public sealed record CurlWebSocketOptions
{
    /// <summary>
    /// Whether each ping received is answered with a pong carrying its payload; <see langword="false"/>
    /// sets <c>CURLWS_NOAUTOPONG</c>. Default <see langword="true"/>.
    /// </summary>
    public bool AutoPong { get; init; } = true;

    /// <summary>
    /// <c>CURLWS_RAW_MODE</c>: <see cref="CurlWebSocket.SendRawAsync"/> writes bytes unframed and no
    /// ping is answered. Default <see langword="false"/>.
    /// </summary>
    public bool RawMode { get; init; }

    /// <summary>The <c>CURLOPT_WS_OPTIONS</c> bits these options stand for.</summary>
    internal WebSocketOptionBits Bits =>
        (AutoPong ? WebSocketOptionBits.None : WebSocketOptionBits.NoAutoPong)
        | (RawMode ? WebSocketOptionBits.RawMode : WebSocketOptionBits.None);
}
